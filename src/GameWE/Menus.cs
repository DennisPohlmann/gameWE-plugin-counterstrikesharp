// gameWE - CounterStrikeSharp plugin
// Copyright (C) 2026 gameWE contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Menu;

namespace GameWE;

/// <summary>
/// The gameWE menu (!gamewe), the chat events menu and the settings menu
/// (!gamewe_settings). The menu entries send the matching chat command for
/// the player, e.g. /rank.
/// </summary>
public sealed partial class GameWEPlugin
{
    private static readonly (string Phrase, string Command)[] MainMenu =
    {
        ("IngameMenu_Menu1", "/rank"),
        ("IngameMenu_Menu2", "/next"),
        ("IngameMenu_Menu3", "/top10"),
        ("IngameMenu_Menu7", "/clans"),
        ("IngameMenu_Menu8", "/status"),
        ("IngameMenu_Menu9", "/statsme"),
        ("IngameMenu_Menu5", ""), // chat events menu
        ("IngameMenu_Menu10", "/weapons"),
        ("IngameMenu_Menu11", "/accuracy"),
        ("IngameMenu_Menu12", "/targets"),
        ("IngameMenu_Menu13", "/kills"),
        ("IngameMenu_Menu14", "/cheaters"),
        ("IngameMenu_Menu15", "/help"),
        ("IngameMenu_Menu17", ""), // settings menu
    };

    private IMenu NewMenu(string title) =>
        Config.MenuType == "chat" ? new ChatMenu(title) : new CenterHtmlMenu(title, this);

    private void OpenMainMenu(CCSPlayerController player)
    {
        var menu = NewMenu(T(player, "IngameMenu_Caption"));
        foreach (var (phrase, command) in MainMenu)
        {
            string entry = command;
            menu.AddMenuOption(T(player, phrase), (p, _) =>
            {
                if (!IsHuman(p))
                {
                    return;
                }
                if (entry.Length > 0)
                {
                    MakePlayerCommand(p, entry);
                }
                else if (phrase == "IngameMenu_Menu5")
                {
                    OpenEventsMenu(p);
                }
                else
                {
                    DisplaySettingsMenu(p);
                }
            });
        }
        menu.Open(player);
    }

    private void OpenEventsMenu(CCSPlayerController player)
    {
        var menu = NewMenu(T(player, "ConsoleMenu_Caption"));
        void Add(string phrase, bool chat, string value) =>
            menu.AddMenuOption(T(player, phrase), (p, _) =>
            {
                if (IsHuman(p) && State(p) is { } state)
                {
                    SetEventSetting(p, state, chat, value);
                }
            });
        Add("ConsoleMenu_Menu1", false, "1");
        Add("ConsoleMenu_Menu2", false, "0");
        Add("ConsoleMenu_Menu3", true, "1");
        Add("ConsoleMenu_Menu4", true, "0");
        menu.Open(player);
    }

    /// <summary>
    /// Shows the settings menu. Without the ranking state yet, the plugin asks
    /// the collector for it (`requested "hideranking"`, at most every 5 seconds,
    /// so opening the menu cannot flood the collector); the menu is shown again
    /// when gamewe_hidestate arrives.
    /// </summary>
    private void DisplaySettingsMenu(CCSPlayerController player)
    {
        if (State(player) is not { } state)
        {
            return;
        }
        float now = Server.CurrentTime;
        float asked = state.HideRankingAsked;
        if (state.HideRanking == HideRankingUnknown && (asked <= 0f || now - asked >= 5f || now < asked))
        {
            state.HideRankingAsked = now;
            state.HideRankingWait = true;
            LogSetting(player, "requested", "hideranking", "0");
        }
        OpenSettingsMenu(player);
    }

    private string OnOff(CCSPlayerController player, bool on) => T(player, on ? "SettingsValue_On" : "SettingsValue_Off");

    private string DamageModeName(CCSPlayerController player, int mode) =>
        T(player, mode switch { 1 => "SettingsValue_Menu", 2 => "SettingsValue_Chat", _ => "SettingsValue_Off" });

    private void OpenSettingsMenu(CCSPlayerController player)
    {
        if (State(player) is not { } state)
        {
            return;
        }
        var settings = state.Settings;
        var menu = NewMenu(T(player, "SettingsMenu_Caption"));

        // the ranking entry: answered by the collector, which knows the state
        string ranking = state.HideRanking switch
        {
            0 => T(player, "SettingsMenu_Hide"),
            3 => T(player, "SettingsMenu_Show"),
            HideRankingUnknown => T(player, "SettingsMenu_Ranking"),
            _ => T(player, "SettingsMenu_RankingLocked"),
        };
        // hidden by an admin or banned: only an admin can change it
        bool rankingLocked = state.HideRanking is 1 or 2;
        menu.AddMenuOption(ranking, (p, _) =>
        {
            if (State(p) is not { } s)
            {
                return;
            }
            string command = s.HideRanking switch
            {
                0 => "/gamewe_hideranking 1",
                3 => "/gamewe_hideranking 0",
                _ => "/gamewe_hideranking",
            };
            // the collector answers with the new state: show the menu with it
            s.HideRankingWait = MakePlayerCommand(p, command);
        }, rankingLocked);

        menu.AddMenuOption(T(player, "SettingsMenu_Display", OnOff(player, settings.ConsoleEvents)), (p, _) =>
        {
            if (State(p) is { } s)
            {
                SetEventSetting(p, s, chat: false, s.Settings.ConsoleEvents ? "0" : "1");
                Server.NextFrame(() => OpenSettingsMenu(p));
            }
        });
        menu.AddMenuOption(T(player, "SettingsMenu_Chat", OnOff(player, settings.GlobalChat)), (p, _) =>
        {
            if (State(p) is { } s)
            {
                SetEventSetting(p, s, chat: true, s.Settings.GlobalChat ? "0" : "1");
                Server.NextFrame(() => OpenSettingsMenu(p));
            }
        });

        string damage;
        if (!Config.DamageDisplayAllow)
        {
            damage = T(player, "SettingsValue_Disabled");
        }
        else if (settings.DamageDisplay == Core.PlayerSettings.ServerDefault)
        {
            damage = T(player, "SettingsValue_Default", DamageModeName(player, DamageMode(player)));
        }
        else
        {
            damage = DamageModeName(player, settings.DamageDisplay);
        }
        menu.AddMenuOption(T(player, "SettingsMenu_Damage", damage), (p, _) =>
        {
            if (State(p) is not { } s || !Config.DamageDisplayAllow)
            {
                return;
            }
            // server default -> off -> panel -> chat -> server default
            int setting = s.Settings.DamageDisplay;
            s.Settings.DamageDisplay = setting >= 2 ? Core.PlayerSettings.ServerDefault : setting + 1;
            SaveSettings(p, s);
            Server.NextFrame(() => OpenSettingsMenu(p));
        }, !Config.DamageDisplayAllow);

        string spectator;
        if (!Config.DisplaySpectatorinfoAllow)
        {
            spectator = T(player, "SettingsValue_Disabled");
        }
        else
        {
            string value = OnOff(player, SpectatorInfoEnabled(player));
            spectator = settings.SpectatorInfo == Core.PlayerSettings.ServerDefault ? T(player, "SettingsValue_Default", value) : value;
        }
        menu.AddMenuOption(T(player, "SettingsMenu_Spectator", spectator), (p, _) =>
        {
            if (State(p) is not { } s || !Config.DisplaySpectatorinfoAllow)
            {
                return;
            }
            // server default -> off -> on -> server default
            int setting = s.Settings.SpectatorInfo;
            s.Settings.SpectatorInfo = setting >= 1 ? Core.PlayerSettings.ServerDefault : setting + 1;
            SaveSettings(p, s);
            Server.NextFrame(() => OpenSettingsMenu(p));
        }, !Config.DisplaySpectatorinfoAllow);

        menu.Open(player);
    }
}
