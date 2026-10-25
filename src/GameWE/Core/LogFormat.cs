// gameWE - CounterStrikeSharp plugin
// Copyright (C) 2026 gameWE contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using System.Text;

namespace GameWE.Core;

/// <summary>
/// The log lines the plugin sends to the collector, in the format CS2 itself
/// writes: <c>MM/DD/YYYY - HH:MM:SS.mmm - </c> followed by the event, players
/// as <c>"Name&lt;userid&gt;&lt;[U:1:123]&gt;&lt;CT&gt;"</c>.
/// </summary>
public static class LogFormat
{
    // CS2 log lines may be up to 8 KiB at the collector; ours stay far below
    public const int MaxLineLength = 4096;

    public static string Timestamp(DateTime time) =>
        time.ToString("MM'/'dd'/'yyyy' - 'HH':'mm':'ss'.'fff' - '", CultureInfo.InvariantCulture);

    /// <summary>
    /// Text from players or the network inside a quoted log field: a <c>"</c>
    /// would end the field and could fake an event, line breaks would start a
    /// new line, so <c>"</c> becomes <c>'</c> and control characters are removed.
    /// </summary>
    public static string Sanitize(string? text, int maxLength = 128)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }
        var builder = new StringBuilder(Math.Min(text.Length, maxLength));
        foreach (char c in text)
        {
            if (builder.Length >= maxLength)
            {
                break;
            }
            if (c == '"')
            {
                builder.Append('\'');
            }
            else if (!char.IsControl(c))
            {
                builder.Append(c);
            }
        }
        return builder.ToString();
    }

    /// <summary>The team name CS2 writes into a player reference.</summary>
    public static string TeamName(int teamNum) => teamNum switch
    {
        1 => "Spectator",
        2 => "TERRORIST",
        3 => "CT",
        _ => "Unassigned",
    };

    /// <summary>
    /// The unique id of a player reference: <c>[U:1:accountid]</c>, <c>BOT</c>
    /// for bots and <c>STEAM_ID_PENDING</c> while the Steam id is not known.
    /// </summary>
    public static string UniqueId(ulong steamId64, bool isBot)
    {
        if (isBot)
        {
            return "BOT";
        }
        uint accountId = (uint)(steamId64 & 0xFFFFFFFF);
        // individual account in the public universe (76561197960265728 + id)
        if ((steamId64 >> 32) != 0x01100001 || accountId == 0)
        {
            return "STEAM_ID_PENDING";
        }
        return $"[U:1:{accountId.ToString(CultureInfo.InvariantCulture)}]";
    }

    public static string PlayerRef(string name, int userId, string uniqueId, int teamNum) =>
        $"{Sanitize(name)}<{userId.ToString(CultureInfo.InvariantCulture)}><{uniqueId}><{TeamName(teamNum)}>";

    public static string Event(string player, string verb, string what) =>
        $"\"{player}\" {verb} \"{Sanitize(what, 192)}\"";

    public static string EventAgainst(string player, string verb, string what, string victim) =>
        $"\"{player}\" {verb} \"{Sanitize(what, 192)}\" against \"{victim}\"";

    public static string Setting(string player, string verb, string name, string value) =>
        $"\"{player}\" {verb} \"{Sanitize(name, 64)}\" (value \"{Sanitize(value, 64)}\")";

    public static string ServerSetting(string verb, string name, string value) =>
        $"\"Server\" {verb} \"{Sanitize(name, 64)}\" (value \"{Sanitize(value, 64)}\")";

    public static string Team(string team, string action, string objective)
    {
        string line = $"Team \"{Sanitize(team, 32)}\" triggered \"{Sanitize(action, 192)}\"";
        return objective.Length > 0 ? $"{line} (object \"{Sanitize(objective, 192)}\")" : line;
    }

    public static string World(string action) => $"World triggered \"{Sanitize(action, 192)}\"";

    public static string WeaponStats(string player, string weapon, WeaponCounters w) =>
        string.Create(CultureInfo.InvariantCulture,
            $"\"{player}\" triggered \"weaponstats\" (weapon \"{weapon}\") (shots \"{w.Shots}\") (hits \"{w.Hits}\") (kills \"{w.Kills}\") (headshots \"{w.Headshots}\") (tks \"{w.Teamkills}\") (damage \"{w.Damage}\") (deaths \"{w.Deaths}\")");
}
