// gameWE - CounterStrikeSharp plugin
// Copyright (C) 2026 gameWE contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GameWE.Core;

/// <summary>
/// The display settings a player chooses in the gameWE settings menu and
/// with gamewe_display, gamewe_chat, gamewe_auto and gamewe_lang. -1 means
/// the server default.
/// </summary>
public sealed class PlayerSettings
{
    public const int ServerDefault = -1;

    [JsonPropertyName("damage_display")] public int DamageDisplay { get; set; } = ServerDefault; // 0 off, 1 menu, 2 chat
    [JsonPropertyName("spectatorinfo")] public int SpectatorInfo { get; set; } = ServerDefault; // 0 off, 1 on
    [JsonPropertyName("console_events")] public bool ConsoleEvents { get; set; } = true; // gamewe_display
    [JsonPropertyName("global_chat")] public bool GlobalChat { get; set; } = true; // gamewe_chat
    [JsonPropertyName("auto_start")] public string AutoStart { get; set; } = string.Empty;
    [JsonPropertyName("auto_end")] public string AutoEnd { get; set; } = string.Empty;
    [JsonPropertyName("auto_kill")] public string AutoKill { get; set; } = string.Empty;
    [JsonPropertyName("lang")] public string Lang { get; set; } = string.Empty; // culture code, "" = the game's

    [JsonIgnore]
    public bool IsDefault =>
        DamageDisplay == ServerDefault && SpectatorInfo == ServerDefault && ConsoleEvents && GlobalChat &&
        AutoStart.Length == 0 && AutoEnd.Length == 0 && AutoKill.Length == 0 && Lang.Length == 0;

    public string GetAuto(AutoEvent autoEvent) => autoEvent switch
    {
        AutoEvent.Start => AutoStart,
        AutoEvent.End => AutoEnd,
        _ => AutoKill,
    };

    public void SetAuto(AutoEvent autoEvent, string command)
    {
        switch (autoEvent)
        {
            case AutoEvent.Start: AutoStart = command; break;
            case AutoEvent.End: AutoEnd = command; break;
            default: AutoKill = command; break;
        }
    }

    /// <summary>Takes only valid values, so a hand-edited file cannot break anything.</summary>
    public PlayerSettings Validated()
    {
        return new PlayerSettings
        {
            DamageDisplay = DamageDisplay is >= ServerDefault and <= 2 ? DamageDisplay : ServerDefault,
            SpectatorInfo = SpectatorInfo is >= ServerDefault and <= 1 ? SpectatorInfo : ServerDefault,
            ConsoleEvents = ConsoleEvents,
            GlobalChat = GlobalChat,
            AutoStart = AutoCommands.Find(AutoStart) ?? string.Empty,
            AutoEnd = AutoCommands.Find(AutoEnd) ?? string.Empty,
            AutoKill = AutoCommands.Find(AutoKill) ?? string.Empty,
            Lang = Languages.IsCulture(Lang) ? Lang : string.Empty,
        };
    }
}

/// <summary>
/// Saves the players' settings in one JSON file, keyed by SteamID64. Only
/// players with a setting other than the default are kept; the file holds
/// nothing but these values (no names, no addresses).
/// </summary>
public sealed class PlayerSettingsStore
{
    // the most players kept, so the file cannot grow without bound
    public const int MaxPlayers = 100_000;

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _path;
    private Dictionary<string, PlayerSettings> _players = new(StringComparer.Ordinal);

    public PlayerSettingsStore(string path)
    {
        _path = path;
    }

    public int Count => _players.Count;

    public void Load()
    {
        _players = new Dictionary<string, PlayerSettings>(StringComparer.Ordinal);
        if (!File.Exists(_path))
        {
            return;
        }
        var data = JsonSerializer.Deserialize<Dictionary<string, PlayerSettings>>(File.ReadAllText(_path), JsonOptions);
        if (data == null)
        {
            return;
        }
        foreach (var (key, settings) in data)
        {
            if (_players.Count >= MaxPlayers)
            {
                break;
            }
            if (ulong.TryParse(key, NumberStyles.None, CultureInfo.InvariantCulture, out _) && settings != null)
            {
                var valid = settings.Validated();
                if (!valid.IsDefault)
                {
                    _players[key] = valid;
                }
            }
        }
    }

    /// <summary>A copy of the player's settings (the defaults for an unknown player).</summary>
    public PlayerSettings Get(ulong steamId)
    {
        if (steamId != 0 && _players.TryGetValue(steamId.ToString(CultureInfo.InvariantCulture), out var settings))
        {
            return settings.Validated();
        }
        return new PlayerSettings();
    }

    /// <summary>Stores the settings; true if the file has to be written.</summary>
    public bool Set(ulong steamId, PlayerSettings settings)
    {
        if (steamId == 0)
        {
            return false;
        }
        string key = steamId.ToString(CultureInfo.InvariantCulture);
        var valid = settings.Validated();
        if (valid.IsDefault)
        {
            return _players.Remove(key);
        }
        if (!_players.ContainsKey(key) && _players.Count >= MaxPlayers)
        {
            return false;
        }
        _players[key] = valid;
        return true;
    }

    public string Path => _path;

    /// <summary>The file content of the current settings (taken on the game thread).</summary>
    public string Snapshot() => JsonSerializer.Serialize(_players, JsonOptions);

    public void Save() => Write(_path, Snapshot());

    // one writer at a time: the background save and the save on unload share the temporary file
    private static readonly object WriteLock = new();

    /// <summary>Writes the file: first a temporary file, then moved over the old one.</summary>
    public static void Write(string path, string content)
    {
        lock (WriteLock)
        {
            WriteFile(path, content);
        }
    }

    private static void WriteFile(string path, string content)
    {
        string? directory = System.IO.Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
        string temp = path + ".tmp";
        File.WriteAllText(temp, content);
        File.Move(temp, path, overwrite: true);
    }
}
