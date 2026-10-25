// gameWE - CounterStrikeSharp plugin
// Copyright (C) 2026 gameWE contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace GameWE.Core;

public enum AutoEvent
{
    Start = 0,
    End = 1,
    Kill = 2,
}

/// <summary>
/// The commands a player can have sent automatically (gamewe_auto): the stats
/// answers of the collector, never free text.
/// </summary>
public static class AutoCommands
{
    public static readonly string[] All =
    {
        "rank", "skill", "points", "kpd", "kdratio", "session", "sdata", "next",
        "top5", "top10", "top20", "statsme",
    };

    /// <summary>The command as listed (lower case), null if it cannot be an auto command.</summary>
    public static string? Find(string? command) =>
        string.IsNullOrEmpty(command) ? null : All.FirstOrDefault(c => string.Equals(c, command, StringComparison.OrdinalIgnoreCase));

    public static AutoEvent? Event(string type) => type.ToLowerInvariant() switch
    {
        "start" => AutoEvent.Start,
        "end" => AutoEvent.End,
        "kill" => AutoEvent.Kill,
        _ => null,
    };
}

/// <summary>
/// The languages of gamewe_lang: the code a player types, the culture of the
/// plugin's translations and the code the collector gets
/// (<c>setup "gamewe_lang"</c>).
/// </summary>
public static class Languages
{
    private static readonly (string Code, string Culture)[] Codes =
    {
        ("en", "en"), ("de", "de"), ("ru", "ru"), ("es", "es"), ("fr", "fr"),
        ("pt", "pt"), ("nl", "nl"), ("dk", "da"), ("da", "da"), ("pl", "pl"),
    };

    /// <summary>The culture of a gamewe_lang code, null for an unknown code.</summary>
    public static string? Culture(string? code)
    {
        if (string.IsNullOrEmpty(code))
        {
            return null;
        }
        foreach (var (c, culture) in Codes)
        {
            if (string.Equals(c, code, StringComparison.OrdinalIgnoreCase))
            {
                return culture;
            }
        }
        return null;
    }

    public static bool IsCulture(string? culture) => Codes.Any(c => c.Culture == culture);
}

/// <summary>
/// The chat commands the collector answers. With "/" in front the plugin
/// hides them and logs them itself; with "!" or nothing in front they stay
/// in chat and the engine logs them.
/// </summary>
public sealed class BlockedCommands
{
    private static readonly string[] Defaults =
    {
        "rank", "skill", "points", "place", "session", "sdata", "session_data", "kpd", "kdratio", "kdeath",
        "next", "load", "status", "servers", "top20", "top10", "top5", "maps", "map_stats", "clans",
        "cheaters", "statsme", "weapons", "weapon", "action", "actions", "accuracy",
        "targets", "target", "kills", "kill", "player_kills", "cmd", "cmds", "commands", "help",
        "gstats", "global_stats", "gamewe", "gamewe_menu", "gamewe_display 0", "gamewe_display 1",
        "gamewe_atb 0", "gamewe_atb 1", "gamewe_hideranking", "gamewe_hideranking 0", "gamewe_hideranking 1",
        "hideranking", "hideranking 0", "hideranking 1", "hideranking on", "hideranking off",
        "gamewe_hideranking on", "gamewe_hideranking off", "gamewe_reset", "gamewe_reset 0", "gamewe_reset 1",
        "gamewe_chat 0", "gamewe_chat 1",
        "gameme", "gameme_menu", "gameme_display 0", "gameme_display 1",
        "gameme_atb 0", "gameme_atb 1", "gameme_hideranking", "gameme_hideranking 0", "gameme_hideranking 1",
        "gameme_hideranking on", "gameme_hideranking off", "gameme_reset", "gameme_reset 0", "gameme_reset 1",
        "gameme_chat 0", "gameme_chat 1",
    };

    private readonly HashSet<string> _commands = new(StringComparer.Ordinal);

    public BlockedCommands()
    {
        foreach (string command in Defaults)
        {
            Add(command);
        }
    }

    public int Count => _commands.Count;

    private void Add(string command)
    {
        if (command.Length is > 0 and <= 64)
        {
            _commands.Add(command);
            _commands.Add("/" + command);
            _commands.Add("!" + command);
        }
    }

    /// <summary>
    /// BlockCommandsValues of the config: more commands separated by spaces,
    /// or "clear" for none at all.
    /// </summary>
    public void Apply(string values)
    {
        if (values.Trim() == "clear")
        {
            _commands.Clear();
            return;
        }
        foreach (string command in values.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(32))
        {
            Add(command);
        }
    }

    public bool IsBlocked(string command) => _commands.Contains(command);

    /// <summary>The command without "/" or "!" in front.</summary>
    public static string Bare(string command) =>
        command.Length > 0 && (command[0] == '/' || command[0] == '!') ? command.Substring(1) : command;

    public static bool IsMenuCommand(string command) =>
        Bare(command) is "gamewe" or "gamewe_menu" or "gameme" or "gameme_menu";

    public static bool IsSettingsCommand(string command) =>
        command.Length > 1 && (command[0] == '/' || command[0] == '!') && Bare(command) == "gamewe_settings";
}
