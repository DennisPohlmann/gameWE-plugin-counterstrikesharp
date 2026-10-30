// gameWE - CounterStrikeSharp plugin
//
// Logs extended player statistics for the gameWE collector and shows its
// answers (rank, top10, session ...) in game, on Counter-Strike 2.
//
// Port of the gameWE SourceMod plugin (https://github.com/DennisPohlmann/gameWE-plugin-sourcemod),
// itself a fork of the gameME Plugin 5.0.0 (https://github.com/gamemedev/plugin-sourcemod),
// Copyright (C) 2007-2020 TTS Oetzel & Goerz GmbH, licensed under the
// GNU General Public License version 2 or (at your option) any later version.
// Copyright (C) 2026 gameWE contributors
//
// SPDX-License-Identifier: GPL-3.0-or-later
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <https://www.gnu.org/licenses/>.

using System.Globalization;
using System.Reflection;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Translations;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Cvars.Validators;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Timers;
using GameWE.Core;
using Microsoft.Extensions.Logging;

namespace GameWE;

public sealed partial class GameWEPlugin : BasePlugin, IPluginConfig<GameWEConfig>
{
    public static readonly string PluginVersion =
        (typeof(GameWEPlugin).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0")
        .Split('+')[0];

    // gamewe_plugin_version, as in the SourceMod and AMX Mod X plugins.
    // CounterStrikeSharp registers it as a server command on load: the server
    // console and RCON read it, players get nothing (no FCVAR_REPLICATED) and
    // any other value is rejected. The flags match the SourceMod ConVar; a
    // FakeConVar does not hand them to the engine.
    public static readonly FakeConVar<string> PluginVersionConVar = new(
        "gamewe_plugin_version", "gameWE Plugin", PluginVersion,
        ConVarFlags.FCVAR_NOTIFY | ConVarFlags.FCVAR_DONTRECORD, new ReadOnlyValue(PluginVersion));

    /// <summary>Accepts only the value the ConVar was created with.</summary>
    private sealed class ReadOnlyValue(string fixedValue) : IValidator<string>
    {
        public bool Validate(string value, out string? errorMessage)
        {
            errorMessage = string.Equals(value, fixedValue, StringComparison.Ordinal) ? null : "read only";
            return errorMessage == null;
        }
    }

    public override string ModuleName => "gameWE";
    public override string ModuleVersion => PluginVersion;
    public override string ModuleAuthor => "gameWE contributors, based on gameME by TTS Oetzel & Goerz GmbH";
    public override string ModuleDescription => "Extended statistics logging and in-game messages for the gameWE collector";

    // the collector's ranking state of a player (gamewe_hidestate)
    private const int HideRankingUnknown = -1;

    /// <summary>What the plugin keeps per player slot.</summary>
    private sealed class PlayerState
    {
        public ulong SteamId;
        public PlayerSettings Settings = new();
        public readonly PlayerWeaponStats Weapons = new();
        public float LastTrigger;                    // game time of the last gameWE command (chat cooldown)
        public readonly float[] LastAuto = new float[3]; // per AutoEvent, so double round events send once
        public int HideRanking = HideRankingUnknown;
        public bool HideRankingWait;                 // show the settings menu again when the state arrives
        public float HideRankingAsked;
        public string Role = string.Empty;
        public int SpectatorTarget = -1;             // slot of the spectated player
        public float SpectatorShown;
    }

    private readonly PlayerState?[] _players = new PlayerState?[DamageTracker.MaxSlots];
    private readonly DamageTracker _damage = new();
    private readonly BlockedCommands _blocked = new();
    private LogSender? _sender;
    private PlayerSettingsStore? _store;
    private bool _storeDirty;
    private string _messagePrefix = string.Empty;
    private string _capturedLogUrl = string.Empty;

    public GameWEConfig Config { get; set; } = new();

    public void OnConfigParsed(GameWEConfig config)
    {
        config.Validate();
        Config = config;
        _messagePrefix = ChatText.ColorEntities(config.MessagePrefix);
        _blocked.Apply(config.BlockCommandsValues);
    }

    public override void Load(bool hotReload)
    {
        _sender = new LogSender(message => Logger.LogWarning("gameWE: {Message}", message));
        UpdateLogUrl();

        _store = new PlayerSettingsStore(Path.GetFullPath(Path.Combine(ModuleDirectory, "..", "..", "data", "GameWE", "players.json")));
        try
        {
            _store.Load();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            Logger.LogError("gameWE: player settings could not be read ({Error}), starting without them", ex.Message);
        }

        LogStart();
        RegisterServerCommands();
        RegisterChat();
        RegisterEvents();
        RegisterLogAddressWatch();

        AddCommand("css_gamewe_settings", "gameWE: your settings (ranking, console events, global chat, damage display, spectator info)",
            (player, _) =>
            {
                if (IsHuman(player))
                {
                    DisplaySettingsMenu(player!);
                }
            });

        RegisterListener<Listeners.OnMapStart>(_ => OnMapStart());
        AddTimer(5.0f, SaveSettingsIfDirty, TimerFlags.REPEAT);
        AddTimer(SpectatorInterval, SpectatorTick, TimerFlags.REPEAT);

        if (hotReload)
        {
            foreach (var player in Utilities.GetPlayers())
            {
                if (IsHuman(player))
                {
                    PlayerJoined(player);
                }
            }
        }
    }

    public override void Unload(bool hotReload)
    {
        foreach (var player in Utilities.GetPlayers())
        {
            FlushWeaponStats(player);
        }
        if (_store != null && _storeDirty)
        {
            TrySave(_store.Path, _store.Snapshot());
        }
        if (_sender != null)
        {
            // last lines, bounded by the HTTP timeout
            _sender.FlushAsync().Wait(TimeSpan.FromSeconds(3));
            _sender.Dispose();
        }
    }

    private void OnMapStart()
    {
        for (int i = 0; i < _players.Length; i++)
        {
            if (_players[i] is { } state)
            {
                state.Weapons.Reset();
                state.Role = string.Empty;
            }
        }
        _damage.ResetAll();
        _spectatorTexts.Clear();
        _spectatorQueries.Clear();
        LogStart();
    }

    // the collector recognises the plugin (and switches on its answers) by this line
    private void LogStart() => Log($"gameWE Plugin {PluginVersion} (CounterStrikeSharp)");

    // ---------------------------------------------------------------- logging

    private void Log(string line) => _sender?.Add(line, DateTime.Now);

    private static string UniqueId(CCSPlayerController player) => LogFormat.UniqueId(player.SteamID, player.IsBot);

    private static string PlayerRef(CCSPlayerController player) =>
        LogFormat.PlayerRef(player.PlayerName, player.UserId ?? -1, UniqueId(player), player.TeamNum);

    private void LogEvent(CCSPlayerController player, string verb, string what) =>
        Log(LogFormat.Event(PlayerRef(player), verb, what));

    private void LogSetting(CCSPlayerController player, string verb, string name, string value) =>
        Log(LogFormat.Setting(PlayerRef(player), verb, name, value));

    private void FlushWeaponStats(CCSPlayerController? player)
    {
        if (player == null || !player.IsValid || State(player) is not { } state || state.Weapons.IsEmpty)
        {
            return;
        }
        foreach (string line in state.Weapons.Flush(PlayerRef(player)))
        {
            Log(line);
        }
    }

    // ------------------------------------------------------- the log address

    private void UpdateLogUrl()
    {
        if (_sender != null)
        {
            _sender.Url = Config.LogUrl.Length > 0 ? Config.LogUrl : _capturedLogUrl;
        }
    }

    /// <summary>
    /// The collector sets the server's log address with logaddress_add_http
    /// over RCON; the plugin posts to the same address. The address is never
    /// written to the log or the chat: its path holds the collector's token.
    /// </summary>
    private void RegisterLogAddressWatch()
    {
        AddCommandListener("logaddress_add_http", (player, info) =>
        {
            if (player == null && info.ArgCount >= 2)
            {
                string url = CommandText.StripQuotes(info.ArgString.Trim());
                if (CommandText.IsLogUrl(url))
                {
                    _capturedLogUrl = url;
                    UpdateLogUrl();
                }
            }
            return HookResult.Continue;
        });
        AddCommandListener("logaddress_delall_http", (player, _) =>
        {
            if (player == null)
            {
                LogAddressRemoved();
            }
            return HookResult.Continue;
        });
        AddCommandListener("logaddress_del_http", (player, info) =>
        {
            if (player == null && CommandText.StripQuotes(info.ArgString.Trim()) == _capturedLogUrl)
            {
                LogAddressRemoved();
            }
            return HookResult.Continue;
        });
        AddCommandListener("log", (player, info) =>
        {
            string value = info.ArgCount >= 2 ? info.GetArg(1) : string.Empty;
            if (player == null && Config.ProtectLogAddress && (value == "off" || value == "0"))
            {
                Log("gameWE address protection active, logging reenabled!");
                Server.NextFrame(() => Server.ExecuteCommand("log on"));
            }
            return HookResult.Continue;
        });
    }

    private void LogAddressRemoved()
    {
        string url = _capturedLogUrl;
        if (url.Length == 0)
        {
            return;
        }
        if (Config.ProtectLogAddress && CommandText.IsLogUrl(url))
        {
            Log("gameWE address protection active, logaddress readded!");
            Server.NextFrame(() => Server.ExecuteCommand($"logaddress_add_http \"{url}\""));
            return;
        }
        _capturedLogUrl = string.Empty;
        UpdateLogUrl();
    }

    // --------------------------------------------------------------- players

    private static bool IsHuman(CCSPlayerController? player) =>
        player != null && player.IsValid && !player.IsBot && !player.IsHLTV;

    private PlayerState? State(CCSPlayerController? player)
    {
        if (player == null || !player.IsValid || player.Slot < 0 || player.Slot >= _players.Length)
        {
            return null;
        }
        return _players[player.Slot] ??= new PlayerState();
    }

    private static CCSPlayerController? PlayerByUserId(int userId)
    {
        if (userId < 0)
        {
            return null;
        }
        var player = Utilities.GetPlayerFromUserid(userId);
        return player != null && player.IsValid && player.Connected == PlayerConnectedState.Connected ? player : null;
    }

    private static CCSPlayerController? PlayerBySlot(int slot)
    {
        var player = Utilities.GetPlayerFromSlot(slot);
        return player != null && player.IsValid ? player : null;
    }

    /// <summary>A player is in the game: settings, language, the setup lines for the collector.</summary>
    private void PlayerJoined(CCSPlayerController player)
    {
        if (State(player) is not { } state)
        {
            return;
        }
        state.SteamId = player.SteamID;
        state.Settings = _store?.Get(player.SteamID) ?? new PlayerSettings();
        state.HideRanking = HideRankingUnknown;
        state.HideRankingWait = false;
        state.LastTrigger = 0f;
        Array.Clear(state.LastAuto);
        state.Weapons.Reset();
        state.SpectatorTarget = -1;
        _damage.Reset(player.Slot);

        try
        {
            string language = player.GetConVarValue("cl_language");
            if (!string.IsNullOrEmpty(language))
            {
                LogSetting(player, "setup", "cl_language", language);
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            // not every client has it
        }
        ApplyLanguage(player, state);
    }

    private void PlayerLeft(CCSPlayerController player)
    {
        FlushWeaponStats(player);
        if (player.Slot >= 0 && player.Slot < _players.Length)
        {
            _players[player.Slot] = null;
        }
        _damage.Reset(player.Slot);
        _spectatorTexts.Remove(player.Slot);
    }

    /// <summary>
    /// The player's gamewe_lang for the plugin's messages (CounterStrikeSharp's
    /// player language) and for the collector's answers (setup "gamewe_lang").
    /// </summary>
    private void ApplyLanguage(CCSPlayerController player, PlayerState state)
    {
        if (state.Settings.Lang.Length == 0)
        {
            return;
        }
        try
        {
            PlayerLanguageManager.Instance.SetLanguage((SteamID)player.SteamID, CultureInfo.GetCultureInfo(state.Settings.Lang));
        }
        catch (CultureNotFoundException)
        {
            return;
        }
        LogSetting(player, "setup", "gamewe_lang", state.Settings.Lang);
    }

    // -------------------------------------------------------------- settings

    private void SaveSettings(CCSPlayerController player, PlayerState state)
    {
        if (_store != null && _store.Set(player.SteamID, state.Settings))
        {
            _storeDirty = true;
        }
    }

    private void SaveSettingsIfDirty()
    {
        if (_store == null || !_storeDirty)
        {
            return;
        }
        _storeDirty = false;
        string path = _store.Path;
        string content = _store.Snapshot();
        Task.Run(() => TrySave(path, content));
    }

    private void TrySave(string path, string content)
    {
        try
        {
            PlayerSettingsStore.Write(path, content);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Logger.LogError("gameWE: player settings could not be saved: {Error}", ex.Message);
        }
    }

    /// <summary>
    /// true if the player may send the next gameWE command to the collector;
    /// a player who floods is not logged, so the collector does not answer.
    /// </summary>
    private bool TriggerAllowed(PlayerState state)
    {
        float now = Server.CurrentTime;
        float cooldown = Config.ChatCooldown;
        if (cooldown <= 0f || state.LastTrigger <= 0f || now - state.LastTrigger >= cooldown || now < state.LastTrigger)
        {
            state.LastTrigger = now;
            return true;
        }
        return false;
    }

    // ----------------------------------------------------------- translations

    private string T(CCSPlayerController player, string key, params object[] args) =>
        args.Length == 0 ? Localizer.ForPlayer(player, key) : Localizer.ForPlayer(player, key, args);

    private string PrefixedLine(string message)
    {
        string line = ChatText.ApplyPrefix($"{ChatText.ChatTag} {message}", _messagePrefix);
        return ChatText.ChatStart(line);
    }

    /// <summary>A chat line of the plugin to one player, in the player's language, with the prefix.</summary>
    private void Reply(CCSPlayerController player, string key, params object[] args) =>
        player.PrintToChat(PrefixedLine(T(player, key, args)));
}
