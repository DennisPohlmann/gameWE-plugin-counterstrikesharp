// gameWE - CounterStrikeSharp plugin
// Copyright (C) 2026 gameWE contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using GameWE.Core;

namespace GameWE;

/// <summary>
/// The commands the collector sends over RCON (gamewe_psay, ...). They are
/// server commands only: a player who types one in the console gets nothing.
/// Players are addressed by their userid.
/// </summary>
public sealed partial class GameWEPlugin
{
    private void RegisterServerCommands()
    {
        var commands = new (string Name, string Help, Action<CommandInfo> Run)[]
        {
            ("raw_message", "<type> <query_id> <userid> <data...> - collector data (spectator info)", RawMessage),
            ("psay", "<userid[,userid...]> [colored] <message> - send a private chat message", Psay),
            ("csay", "<message> - display a center message to all players", Csay),
            ("msay", "<time> <userid> [handler] <message> - display a panel to a player", Msay),
            ("hidestate", "<userid> <hideranking> - ranking state of a player for the settings menu", HideState),
            ("tsay", "<time> <userid> <message> - a message to a player (chat on CS2)", Tsay),
            ("hint", "<userid[,userid...]> <message> - display a hint message", Hint),
            ("khint", "<userid[,userid...]> <message> - display a key hint message", KeyHint),
            ("browse", "<userid[,userid...]> <url> - show a website address (CS2 has no in-game browser)", Browse),
            ("swap", "<userid> - move a player to the opposite team", Swap),
            ("redirect", "<time> <userid[,userid...]> <address> <reason> - ask a player to join another server", Redirect),
            ("player_action", "<userid> <action> - log a player action", PlayerAction),
            ("team_action", "<team> <action> [objective] - log a team action", TeamAction),
            ("world_action", "<action> - log a world action", WorldAction),
            ("console_event", "<userid[,userid...]> <message> - console line for players with gamewe_display 1", info => PlayerEvent(info, chat: false)),
            ("chat_event", "<userid[,userid...]> <message> - chat line for players with gamewe_chat 1", info => PlayerEvent(info, chat: true)),
        };
        foreach (var (name, help, run) in commands)
        {
            // gameME's names too, so collectors and scripts written for gameME keep working
            foreach (string prefix in new[] { "gamewe_", "gameme_" })
            {
                AddCommand(prefix + name, "gameWE: " + help, (player, info) =>
                {
                    if (player == null)
                    {
                        run(info);
                    }
                });
            }
        }
    }

    private static int ArgCount(CommandInfo info) => info.ArgCount - 1;

    private static string Arg(CommandInfo info, int index) =>
        index < info.ArgCount ? CommandText.StripQuotes(info.GetArg(index)) : string.Empty;

    private static IEnumerable<CCSPlayerController> Humans(string userIds)
    {
        foreach (int userId in CommandText.UserIds(userIds))
        {
            var player = PlayerByUserId(userId);
            if (IsHuman(player))
            {
                yield return player!;
            }
        }
    }

    private static void Usage(CommandInfo info, string usage) => info.ReplyToCommand("Usage: " + info.GetArg(0) + " " + usage);

    private void Psay(CommandInfo info)
    {
        if (ArgCount(info) < 2)
        {
            Usage(info, "<userid[,userid...]> [colored] <message>");
            return;
        }
        string colored = Arg(info, 2);
        bool hasColorArg = colored is "0" or "1" or "2" or "3";
        string message = CommandText.Message(info.ArgString, hasColorArg ? 2 : 1);
        message = ChatText.ApplyPrefix(message, _messagePrefix);
        if (hasColorArg && colored != "0")
        {
            // 2 and 3 (team colors) need a team member as sender; CS2 shows them like 1
            message = ChatText.ColorEntities(message);
        }
        message = ChatText.ChatStart(ChatText.StripControl(message));
        if (message.Trim().Length == 0)
        {
            return;
        }
        foreach (var player in Humans(Arg(info, 1)))
        {
            player.PrintToChat(message);
        }
    }

    private void Csay(CommandInfo info)
    {
        string message = ChatText.StripColors(CommandText.Message(info.ArgString, 0));
        if (message.Length == 0)
        {
            Usage(info, "<message>");
            return;
        }
        foreach (var player in Utilities.GetPlayers())
        {
            if (IsHuman(player))
            {
                player.PrintToCenter(message);
            }
        }
    }

    private void Msay(CommandInfo info)
    {
        if (ArgCount(info) < 3)
        {
            Usage(info, "<time> <userid> [handler] <message>");
            return;
        }
        int time = CommandText.Int(Arg(info, 1), 10);
        time = time <= 0 ? 10 : Math.Min(time, 60);
        string handler = Arg(info, 3);
        // the handler (select a line) is a SourceMod menu; on CS2 the text is a panel
        string message = CommandText.Message(info.ArgString, handler is "0" or "1" ? 3 : 2);
        var player = PlayerByUserId(CommandText.Int(Arg(info, 2), -1));
        if (IsHuman(player) && message.Length > 0)
        {
            player!.PrintToCenterHtml(ChatText.Html(message), time);
        }
    }

    private void Tsay(CommandInfo info)
    {
        if (ArgCount(info) < 3)
        {
            Usage(info, "<time> <userid> <message>");
            return;
        }
        string message = CommandText.Message(info.ArgString, 2);
        var player = PlayerByUserId(CommandText.Int(Arg(info, 2), -1));
        if (IsHuman(player) && message.Length > 0)
        {
            // CS2 has no top-left dialog: the message comes as chat lines
            foreach (string line in ChatText.Lines(message))
            {
                player!.PrintToChat(ChatText.ChatStart(line));
            }
        }
    }

    private void Hint(CommandInfo info)
    {
        if (ArgCount(info) < 2)
        {
            Usage(info, "<userid[,userid...]> <message>");
            return;
        }
        string message = ChatText.StripColors(CommandText.Message(info.ArgString, 1));
        if (message.Length == 0)
        {
            return;
        }
        foreach (var player in Humans(Arg(info, 1)))
        {
            player.PrintToCenter(message);
        }
    }

    private void KeyHint(CommandInfo info)
    {
        if (ArgCount(info) < 2)
        {
            Usage(info, "<userid[,userid...]> <message>");
            return;
        }
        string message = CommandText.Message(info.ArgString, 1);
        if (message.Length == 0)
        {
            return;
        }
        // CS2 shows no key hints: a center panel, as the SourceMod plugin does on CS:GO
        string html = ChatText.Html(message);
        foreach (var player in Humans(Arg(info, 1)))
        {
            player.PrintToCenterHtml(html, 5);
        }
    }

    private void Browse(CommandInfo info)
    {
        if (ArgCount(info) < 2)
        {
            Usage(info, "<userid[,userid...]> <url>");
            return;
        }
        string url = Arg(info, 2);
        if (!CommandText.IsWebUrl(url))
        {
            info.ReplyToCommand("gamewe_browse: only http:// and https:// URLs are allowed");
            return;
        }
        // CS2 shows no web pages in game: the address comes as a chat line
        foreach (var player in Humans(Arg(info, 1)))
        {
            player.PrintToChat(ChatText.ChatStart(url));
        }
    }

    private void Swap(CommandInfo info)
    {
        if (ArgCount(info) < 1)
        {
            Usage(info, "<userid>");
            return;
        }
        var player = PlayerByUserId(CommandText.Int(Arg(info, 1), -1));
        if (player == null)
        {
            return;
        }
        CsTeam team = player.Team switch
        {
            CsTeam.CounterTerrorist => CsTeam.Terrorist,
            CsTeam.Terrorist => CsTeam.CounterTerrorist,
            _ => CsTeam.None,
        };
        if (team == CsTeam.None)
        {
            return;
        }
        if (player.PawnIsAlive)
        {
            player.SwitchTeam(team);
            player.Respawn();
            if (team == CsTeam.CounterTerrorist)
            {
                player.RemoveItemByDesignerName("weapon_c4");
            }
        }
        else
        {
            player.ChangeTeam(team);
        }
    }

    private void Redirect(CommandInfo info)
    {
        if (ArgCount(info) < 3)
        {
            Usage(info, "<time> <userid[,userid...]> <address> <reason>");
            return;
        }
        string address = Arg(info, 3);
        if (!CommandText.IsServerAddress(address))
        {
            return;
        }
        string reason = ChatText.StripControl(CommandText.Message(info.ArgString, 3));
        // CS2 has no "connect to" dialog: the reason and the address come as chat lines
        foreach (var player in Humans(Arg(info, 2)))
        {
            if (reason.Length > 0)
            {
                player.PrintToChat(PrefixedLine(reason));
            }
            Reply(player, "Redirect_Connect", address);
        }
    }

    private void PlayerAction(CommandInfo info)
    {
        if (ArgCount(info) < 2)
        {
            Usage(info, "<userid> <action>");
            return;
        }
        var player = PlayerByUserId(CommandText.Int(Arg(info, 1), -1));
        string action = Arg(info, 2);
        if (player != null && action.Length > 0)
        {
            LogEvent(player, "triggered", action);
        }
    }

    private void TeamAction(CommandInfo info)
    {
        if (ArgCount(info) < 2)
        {
            Usage(info, "<team> <action> [objective]");
            return;
        }
        string team = Arg(info, 1);
        string action = Arg(info, 2);
        if (team.Length > 0 && action.Length > 0)
        {
            Log(LogFormat.Team(team, action, ArgCount(info) > 2 ? Arg(info, 3) : string.Empty));
        }
    }

    private void WorldAction(CommandInfo info)
    {
        string action = Arg(info, 1);
        if (action.Length == 0)
        {
            Usage(info, "<action>");
            return;
        }
        Log(LogFormat.World(action));
    }

    /// <summary>
    /// gamewe_hidestate userid hideranking: the collector tells the player's
    /// ranking state, after !hideranking / !settings or when the menu asked.
    /// </summary>
    private void HideState(CommandInfo info)
    {
        if (ArgCount(info) < 2)
        {
            Usage(info, "<userid> <hideranking>");
            return;
        }
        var player = PlayerByUserId(CommandText.Int(Arg(info, 1), -1));
        if (!IsHuman(player) || State(player) is not { } state)
        {
            return;
        }
        int hideState = CommandText.Int(Arg(info, 2), HideRankingUnknown);
        state.HideRanking = hideState is >= 0 and <= 3 ? hideState : HideRankingUnknown;
        if (state.HideRankingWait)
        {
            state.HideRankingWait = false;
            OpenSettingsMenu(player!);
        }
    }

    /// <summary>
    /// gamewe_console_event / gamewe_chat_event: the collector's points of
    /// kills and actions, only for the listed players whose gamewe_display
    /// (console) or gamewe_chat (chat) is on.
    /// </summary>
    private void PlayerEvent(CommandInfo info, bool chat)
    {
        if (ArgCount(info) < 2)
        {
            Usage(info, "<userid[,userid...]> <message>");
            return;
        }
        string message = ChatText.StripControl(CommandText.Message(info.ArgString, 1));
        if (message.Length == 0)
        {
            return;
        }
        string chatLine = ChatText.ChatStart(ChatText.ApplyPrefix(message, _messagePrefix));
        foreach (var player in Humans(Arg(info, 1)))
        {
            if (State(player) is not { } state)
            {
                continue;
            }
            if (chat && state.Settings.GlobalChat)
            {
                player.PrintToChat(chatLine);
            }
            else if (!chat && state.Settings.ConsoleEvents)
            {
                player.PrintToConsole($"{ChatText.ChatTag} {ChatText.StripColors(message)}");
            }
        }
    }

    /// <summary>
    /// gamewe_raw_message: of the data for other plugins, CS2 uses the
    /// spectator info (1001) and closing a query (1000).
    /// </summary>
    private void RawMessage(CommandInfo info)
    {
        if (ArgCount(info) < 1)
        {
            Usage(info, "<type> <query_id> <userid> <data...>");
            return;
        }
        switch (CommandText.Int(Arg(info, 1), 0))
        {
            case RawMessageClose:
                _spectatorQueries.Remove(CommandText.Int(Arg(info, 2), -1));
                break;
            case RawMessageSpectator when ArgCount(info) >= 5:
                SpectatorAnswer(CommandText.Int(Arg(info, 2), -1), Arg(info, 4), Arg(info, 5));
                break;
        }
    }
}
