// gameWE - CounterStrikeSharp plugin
// Copyright (C) 2026 gameWE contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Net;
using System.Text;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using GameWE.Core;

namespace GameWE;

/// <summary>
/// The damage display on death and at round end, and the stats of the
/// spectated player (spectator info). Every player chooses them in the
/// settings menu; the config only sets the default.
/// </summary>
public sealed partial class GameWEPlugin
{
    private const float SpectatorInterval = 0.5f;
    private const int RawMessageClose = 1000;
    private const int RawMessageSpectator = 1001;

    // the collector's spectator text per spectated player slot
    private readonly Dictionary<int, string> _spectatorTexts = new();
    // open queries: query id -> (spectated slot, game time asked)
    private readonly Dictionary<int, (int Slot, float Asked)> _spectatorQueries = new();
    // spectated slot -> game time of the last request
    private readonly Dictionary<int, float> _spectatorAsked = new();
    private int _queryId;

    // ------------------------------------------------------------- damage

    /// <summary>
    /// The player's damage display (0 off, 1 panel, 2 chat): the player's own
    /// choice, otherwise the server default DamageDisplay.
    /// </summary>
    private int DamageMode(CCSPlayerController player)
    {
        if (!Config.DamageDisplayAllow)
        {
            return 0;
        }
        int mode = State(player)?.Settings.DamageDisplay ?? Core.PlayerSettings.ServerDefault;
        if (mode == Core.PlayerSettings.ServerDefault)
        {
            mode = Config.DamageDisplay;
        }
        return mode is >= 0 and <= 2 ? mode : 0;
    }

    private void ShowDamageOnDeath(CCSPlayerController victim)
    {
        switch (DamageMode(victim))
        {
            case 2:
                ShowDamageChat(victim);
                break;
            case 1:
                ShowDamagePanel(victim);
                break;
            default:
                // not displayed: forget the killer, so it does not show up after a later death
                _damage.ForgetKiller(victim.Slot);
                break;
        }
    }

    private static string NameOf(int slot) => PlayerBySlot(slot)?.PlayerName ?? "?";

    /// <summary>The summary of the round (attackers, victims, killer) as a center panel.</summary>
    private void ShowDamagePanel(CCSPlayerController player)
    {
        if (!Config.DamageDisplayAllow || !IsHuman(player))
        {
            return;
        }
        var summary = _damage.Build(player.Slot);
        if (summary.IsEmpty)
        {
            return;
        }
        string dmg = T(player, "DamagePanel_Dmg");
        string Hits(int hits) => $"{hits} {T(player, hits == 1 ? "DamagePanel_Hit" : "DamagePanel_Hits")}";
        var html = new StringBuilder();
        void Section(string caption, List<DamageTracker.Line> lines, Func<DamageTracker.Line, string> format)
        {
            if (lines.Count == 0)
            {
                return;
            }
            if (html.Length > 0)
            {
                html.Append("<br>");
            }
            html.Append("<b>").Append(WebUtility.HtmlEncode(T(player, caption))).Append("</b>");
            foreach (var line in lines)
            {
                html.Append("<br>").Append(WebUtility.HtmlEncode(format(line)));
            }
        }
        Section("DamagePanel_Attackers", summary.Attackers, l => $"{NameOf(l.Slot)} - {l.Damage} {dmg}, {Hits(l.Hits)}");
        Section("DamagePanel_Killed", summary.Killed, l => $"{NameOf(l.Slot)} - {l.Damage} {dmg}, {Hits(l.Hits)}");
        Section("DamagePanel_Wounded", summary.Wounded, l => $"{NameOf(l.Slot)} - {l.Damage} {dmg}, {Hits(l.Hits)}");
        Section("DamagePanel_Killer", summary.Killers,
            l => $"{NameOf(l.Slot)} - {l.HealthLeft} {T(player, "DamagePanel_Hp")}, {l.Damage} {dmg}, {l.Weapon}");
        player.PrintToCenterHtml(html.ToString(), 10);
    }

    /// <summary>The killer, the damage and the killer's health left, in chat.</summary>
    private void ShowDamageChat(CCSPlayerController player)
    {
        if (!Config.DamageDisplayAllow || !IsHuman(player))
        {
            return;
        }
        var summary = _damage.Build(player.Slot);
        if (summary.Killers.Count == 0)
        {
            return;
        }
        var killer = summary.Killers[^1];
        string text = T(player, "DamageChat_Killedyou", NameOf(killer.Slot), killer.Damage, killer.HealthLeft);
        string line = _messagePrefix.Length > 0 ? $"{_messagePrefix} {text}" : text;
        player.PrintToChat(ChatText.ChatStart(ChatText.StripControl(line)));
    }

    // ---------------------------------------------------------- spectator

    private bool SpectatorInfoEnabled(CCSPlayerController player)
    {
        if (!Config.DisplaySpectatorinfoAllow)
        {
            return false;
        }
        int setting = State(player)?.Settings.SpectatorInfo ?? Core.PlayerSettings.ServerDefault;
        if (setting == Core.PlayerSettings.ServerDefault)
        {
            setting = Config.DisplaySpectatorinfo;
        }
        return setting == 1;
    }

    /// <summary>The player's stats changed (kill, death, bomb): ask the collector again.</summary>
    private HookResult SpectatorChanged(CCSPlayerController? player)
    {
        if (player is { IsValid: true })
        {
            _spectatorTexts.Remove(player.Slot);
            _spectatorAsked.Remove(player.Slot);
        }
        return HookResult.Continue;
    }

    // the slot of the player the observer watches in first or third person, -1 for none
    private static int ObservedSlot(CCSPlayerController observer)
    {
        var services = observer.Pawn.Value?.ObserverServices;
        if (services == null)
        {
            return -1;
        }
        var mode = (ObserverMode_t)services.ObserverMode;
        if (mode != ObserverMode_t.OBS_MODE_IN_EYE && mode != ObserverMode_t.OBS_MODE_CHASE)
        {
            return -1;
        }
        var target = services.ObserverTarget.Value;
        if (target == null || !target.IsValid)
        {
            return -1;
        }
        foreach (var player in Utilities.GetPlayers())
        {
            if (player.PlayerPawn.Value is { IsValid: true } pawn && pawn.Index == target.Index)
            {
                return player.Slot;
            }
        }
        return -1;
    }

    private void SpectatorTick()
    {
        if (!Config.DisplaySpectatorinfoAllow)
        {
            return;
        }
        float now = Server.CurrentTime;
        foreach (var (id, query) in _spectatorQueries.ToList())
        {
            if (now - query.Asked > 30f || now < query.Asked)
            {
                _spectatorQueries.Remove(id);
            }
        }
        foreach (var observer in Utilities.GetPlayers())
        {
            if (!IsHuman(observer) || observer.PawnIsAlive || State(observer) is not { } state || !SpectatorInfoEnabled(observer))
            {
                continue;
            }
            int target;
            try
            {
                target = ObservedSlot(observer);
            }
            catch (InvalidOperationException)
            {
                continue;
            }
            if (target < 0 || PlayerBySlot(target) is not { } watched)
            {
                state.SpectatorTarget = -1;
                continue;
            }
            if (!_spectatorTexts.ContainsKey(target))
            {
                AskSpectatorInfo(watched, now);
            }
            if (target != state.SpectatorTarget)
            {
                state.SpectatorShown = 0f;
                state.SpectatorTarget = target;
            }
            if (_spectatorTexts.TryGetValue(target, out string? text) && text.Length > 0 &&
                (now - state.SpectatorShown > 5f || now < state.SpectatorShown))
            {
                observer.PrintToCenterHtml(text, 5);
                state.SpectatorShown = now;
            }
        }
    }

    // `"Name<...>" requested "spectatorinfo" (value "17")`, at most every 5 seconds per player
    private void AskSpectatorInfo(CCSPlayerController watched, float now)
    {
        if (_spectatorAsked.TryGetValue(watched.Slot, out float asked) && now - asked < 5f && now >= asked)
        {
            return;
        }
        _spectatorAsked[watched.Slot] = now;
        _queryId = _queryId >= 65535 ? 1 : _queryId + 1;
        _spectatorQueries[_queryId] = (watched.Slot, now);
        LogSetting(watched, "requested", "spectatorinfo", _queryId.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    // gamewe_raw_message 1001 <query_id> <callers> <userid> "<text>"
    private void SpectatorAnswer(int queryId, string targetUserId, string text)
    {
        if (!_spectatorQueries.Remove(queryId, out var query))
        {
            return;
        }
        var target = PlayerByUserId(CommandText.Int(targetUserId, -1));
        if (target == null || target.Slot != query.Slot)
        {
            return;
        }
        _spectatorTexts[target.Slot] = ChatText.Html(text);
    }
}
