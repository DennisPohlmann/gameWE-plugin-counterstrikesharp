// gameWE - CounterStrikeSharp plugin
// Copyright (C) 2026 gameWE contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using GameWE.Core;

namespace GameWE;

/// <summary>
/// The chat commands. The collector answers the stats commands; the plugin
/// only reads and forwards them and never executes text from chat.
/// </summary>
public sealed partial class GameWEPlugin
{
    private static readonly string[] AutoEventPhrases = { "Auto_Start", "Auto_End", "Auto_Kill" };

    private void RegisterChat()
    {
        AddCommandListener("say", OnSay, HookMode.Pre);
        AddCommandListener("say_team", OnSay, HookMode.Pre);
    }

    private HookResult OnSay(CCSPlayerController? player, CommandInfo info)
    {
        if (!IsHuman(player) || State(player) is not { } state)
        {
            return HookResult.Continue;
        }
        string text = info.ArgString.Trim();
        if (text.StartsWith('"'))
        {
            text = text.EndsWith('"') && text.Length >= 2 ? text.Substring(1, text.Length - 2) : text.Substring(1);
        }
        if (text.Length == 0)
        {
            return HookResult.Continue;
        }
        if (HandlePlayerSetting(player!, state, text))
        {
            // kept by the plugin; "/..." is silent, "!..." stays in chat
            return text[0] == '/' ? HookResult.Handled : HookResult.Continue;
        }
        if (BlockedCommands.IsSettingsCommand(text))
        {
            // css_gamewe_settings opens the menu (CounterStrikeSharp's chat trigger);
            // not logged, so the collector does not answer in chat as well
            return HookResult.Continue;
        }
        if (BlockedCommands.IsMenuCommand(text))
        {
            OpenMainMenu(player!);
        }
        // "/rank" is silent: the plugin logs it itself. "!rank" and "rank"
        // stay in chat and the engine logs them.
        if (text[0] == '/' && _blocked.IsBlocked(text))
        {
            if (TriggerAllowed(state))
            {
                LogEvent(player!, "say", text);
            }
            return HookResult.Handled;
        }
        return HookResult.Continue;
    }

    /// <summary>
    /// The player sends a gameWE command (menu entries, auto commands), as if
    /// typed with "/" in front: logged as a say line for the collector.
    /// </summary>
    private bool MakePlayerCommand(CCSPlayerController player, string command)
    {
        if (State(player) is { } state && TriggerAllowed(state))
        {
            LogEvent(player, "say", command);
            return true;
        }
        return false;
    }

    /// <summary>
    /// gamewe_display, gamewe_chat, gamewe_auto and gamewe_lang (also with
    /// gameme_): settings the plugin keeps itself, not sent to the collector.
    /// </summary>
    private bool HandlePlayerSetting(CCSPlayerController player, PlayerState state, string text)
    {
        string[] words = text.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
        string first = BlockedCommands.Bare(words[0]);
        if (!first.StartsWith("gamewe_", StringComparison.OrdinalIgnoreCase) && !first.StartsWith("gameme_", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        string name = first.Substring(7).ToLowerInvariant();
        if (name is not ("display" or "chat" or "auto" or "lang"))
        {
            return false;
        }
        // every change writes the settings file (gamewe_lang a log line too):
        // at most one per ChatCooldown
        if (!TriggerAllowed(state))
        {
            return true;
        }
        string argument = words.Length > 1 ? words[1] : string.Empty;
        string argument2 = words.Length > 2 ? words[2].Trim() : string.Empty;
        switch (name)
        {
            case "display": SetEventSetting(player, state, chat: false, argument); break;
            case "chat": SetEventSetting(player, state, chat: true, argument); break;
            case "auto": AutoCommand(player, state, argument, argument2); break;
            default: PlayerLanguage(player, state, argument); break;
        }
        return true;
    }

    /// <summary>gamewe_display 0|1 (console events) or gamewe_chat 0|1 (global chat).</summary>
    private void SetEventSetting(CCSPlayerController player, PlayerState state, bool chat, string value)
    {
        bool on;
        if (value == "1" || value.Equals("on", StringComparison.OrdinalIgnoreCase))
        {
            on = true;
        }
        else if (value == "0" || value.Equals("off", StringComparison.OrdinalIgnoreCase))
        {
            on = false;
        }
        else
        {
            Reply(player, "Chat_OnOffUsage", chat ? "gamewe_chat" : "gamewe_display");
            return;
        }
        if (chat)
        {
            state.Settings.GlobalChat = on;
            Reply(player, on ? "Chat_ChatOn" : "Chat_ChatOff");
        }
        else
        {
            state.Settings.ConsoleEvents = on;
            Reply(player, on ? "Chat_DisplayOn" : "Chat_DisplayOff");
        }
        SaveSettings(player, state);
    }

    /// <summary>gamewe_auto clear | gamewe_auto start|end|kill [command|clear]</summary>
    private void AutoCommand(CCSPlayerController player, PlayerState state, string type, string command)
    {
        if (type.Equals("clear", StringComparison.OrdinalIgnoreCase))
        {
            foreach (AutoEvent e in Enum.GetValues<AutoEvent>())
            {
                state.Settings.SetAuto(e, string.Empty);
            }
            SaveSettings(player, state);
            Reply(player, "Auto_Cleared");
            return;
        }
        if (AutoCommands.Event(type) is not { } autoEvent)
        {
            string none = T(player, "SettingsValue_Off");
            string Current(AutoEvent e) => state.Settings.GetAuto(e) is { Length: > 0 } c ? c : none;
            Reply(player, "Auto_List", Current(AutoEvent.Start), Current(AutoEvent.End), Current(AutoEvent.Kill));
            Reply(player, "Auto_Usage");
            return;
        }
        string eventName = T(player, AutoEventPhrases[(int)autoEvent]);
        if (command.Length == 0 || command.Equals("clear", StringComparison.OrdinalIgnoreCase))
        {
            state.Settings.SetAuto(autoEvent, string.Empty);
            SaveSettings(player, state);
            Reply(player, "Auto_Removed", eventName);
            return;
        }
        if (AutoCommands.Find(command) is not { } found)
        {
            Reply(player, "Auto_Unknown", LogFormat.Sanitize(command, 32));
            Reply(player, "Auto_Usage");
            return;
        }
        state.Settings.SetAuto(autoEvent, found);
        SaveSettings(player, state);
        Reply(player, "Auto_Set", eventName, found);
    }

    /// <summary>gamewe_lang code: the language of the plugin's and the collector's messages.</summary>
    private void PlayerLanguage(CCSPlayerController player, PlayerState state, string code)
    {
        if (Languages.Culture(code) is not { } culture)
        {
            Reply(player, "Lang_Usage");
            return;
        }
        state.Settings.Lang = culture;
        SaveSettings(player, state);
        ApplyLanguage(player, state);
        Reply(player, "Lang_Set", code.ToLowerInvariant());
    }

    /// <summary>Sends the players' auto command of the event (onlyPlayer: just that player).</summary>
    private void RunAutoCommands(AutoEvent autoEvent, CCSPlayerController? onlyPlayer = null)
    {
        float now = Server.CurrentTime;
        IEnumerable<CCSPlayerController> players = onlyPlayer != null ? new[] { onlyPlayer } : Utilities.GetPlayers();
        foreach (var player in players)
        {
            if (!IsHuman(player) || State(player) is not { } state)
            {
                continue;
            }
            string command = state.Settings.GetAuto(autoEvent);
            float last = state.LastAuto[(int)autoEvent];
            if (command.Length == 0 || (last > 0f && now >= last && now - last < 2f))
            {
                continue;
            }
            state.LastAuto[(int)autoEvent] = now;
            MakePlayerCommand(player, "/" + command);
        }
    }
}
