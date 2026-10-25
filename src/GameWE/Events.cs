// gameWE - CounterStrikeSharp plugin
// Copyright (C) 2026 gameWE contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using GameWE.Core;

namespace GameWE;

public sealed partial class GameWEPlugin
{
    // player models of CS2 (characters/models/ctm_sas/...): the role names of
    // the SourceMod plugin's CS:GO list, logged as `changed role to "sas"`
    private static readonly string[] RoleNames =
    {
        "leet", "phoenix", "balkan", "st6", "gign", "gsg9", "anarchist", "pirate",
        "professional", "separatist", "fbi", "idf", "sas", "swat", "zombie",
    };

    private void RegisterEvents()
    {
        RegisterEventHandler<EventPlayerConnectFull>((e, _) =>
        {
            if (IsHuman(e.Userid))
            {
                PlayerJoined(e.Userid!);
            }
            return HookResult.Continue;
        });
        RegisterEventHandler<EventPlayerDisconnect>((e, _) =>
        {
            if (e.Userid is { IsValid: true } player)
            {
                PlayerLeft(player);
            }
            return HookResult.Continue;
        }, HookMode.Pre);

        RegisterEventHandler<EventWeaponFire>((e, _) =>
        {
            if (e.Userid is { IsValid: true } player)
            {
                State(player)?.Weapons.Shot(e.Weapon);
            }
            return HookResult.Continue;
        });
        RegisterEventHandler<EventPlayerHurt>(OnPlayerHurt);
        RegisterEventHandler<EventPlayerDeath>(OnPlayerDeath, HookMode.Pre);
        RegisterEventHandler<EventPlayerSpawn>(OnPlayerSpawn);

        RegisterEventHandler<EventRoundStart>((_, _) =>
        {
            foreach (var state in _players)
            {
                if (state != null)
                {
                    state.SpectatorShown = 0f;
                }
            }
            _spectatorTexts.Clear();
            RunAutoCommands(AutoEvent.Start);
            return HookResult.Continue;
        });
        RegisterEventHandler<EventRoundEnd>((_, _) =>
        {
            foreach (var player in Utilities.GetPlayers())
            {
                if (IsHuman(player) && player.PawnIsAlive && DamageMode(player) == 1)
                {
                    ShowDamagePanel(player);
                }
                FlushWeaponStats(player);
            }
            RunAutoCommands(AutoEvent.End);
            return HookResult.Continue;
        });
        RegisterEventHandler<EventRoundAnnounceMatchStart>((_, _) =>
        {
            foreach (var state in _players)
            {
                state?.Weapons.Reset();
            }
            _damage.ResetAll();
            return HookResult.Continue;
        });
        RegisterEventHandler<EventRoundMvp>((e, _) =>
        {
            if (e.Userid is { IsValid: true } player)
            {
                LogEvent(player, "triggered", "mvp");
            }
            return HookResult.Continue;
        });

        // the spectator info of these players is out of date
        RegisterEventHandler<EventBombDropped>((e, _) => SpectatorChanged(e.Userid));
        RegisterEventHandler<EventBombPickup>((e, _) => SpectatorChanged(e.Userid));
        RegisterEventHandler<EventBombPlanted>((e, _) => SpectatorChanged(e.Userid));
        RegisterEventHandler<EventBombDefused>((e, _) => SpectatorChanged(e.Userid));
        RegisterEventHandler<EventHostageKilled>((e, _) => SpectatorChanged(e.Userid));
        RegisterEventHandler<EventHostageRescued>((e, _) => SpectatorChanged(e.Userid));
    }

    private HookResult OnPlayerHurt(EventPlayerHurt e, GameEventInfo info)
    {
        var attacker = e.Attacker;
        var victim = e.Userid;
        if (attacker is not { IsValid: true } || victim is not { IsValid: true } || attacker.Slot == victim.Slot)
        {
            return HookResult.Continue;
        }
        State(attacker)?.Weapons.Hit(e.Weapon, e.DmgHealth);
        if (Config.DamageDisplayAllow)
        {
            _damage.Hurt(attacker.Slot, victim.Slot, e.DmgHealth);
        }
        return HookResult.Continue;
    }

    private HookResult OnPlayerDeath(EventPlayerDeath e, GameEventInfo info)
    {
        var victim = e.Userid;
        var attacker = e.Attacker;
        if (victim is not { IsValid: true })
        {
            return HookResult.Continue;
        }
        if (attacker is { IsValid: true } && attacker.Slot != victim.Slot)
        {
            bool teamkill = attacker.TeamNum == victim.TeamNum;
            string? weapon = PlayerWeaponStats.NormalizeWeapon(e.Weapon);
            State(attacker)?.Weapons.Kill(weapon, e.Headshot, teamkill);
            State(victim)?.Weapons.Death(weapon);

            string attackerRef = PlayerRef(attacker);
            string victimRef = PlayerRef(victim);
            if (e.Headshot && !teamkill)
            {
                // the collector rewards one headshot action per kill
                Log(LogFormat.Event(attackerRef, "triggered", "headshot"));
            }
            if (e.Dominated != 0)
            {
                Log(LogFormat.EventAgainst(attackerRef, "triggered", "domination", victimRef));
            }
            else if (e.Revenge != 0)
            {
                Log(LogFormat.EventAgainst(attackerRef, "triggered", "revenge", victimRef));
            }
            // kill assists are logged by CS2 itself ("assisted killing")

            if (Config.DamageDisplayAllow)
            {
                _damage.Kill(attacker.Slot, victim.Slot, (int)attacker.PawnHealth, weapon ?? string.Empty);
            }
            SpectatorChanged(attacker);
        }

        FlushWeaponStats(victim);
        SpectatorChanged(victim);
        if (IsHuman(victim))
        {
            ShowDamageOnDeath(victim);
            RunAutoCommands(AutoEvent.Kill, victim);
        }
        return HookResult.Continue;
    }

    private HookResult OnPlayerSpawn(EventPlayerSpawn e, GameEventInfo info)
    {
        if (e.Userid is not { IsValid: true } player || State(player) is not { } state)
        {
            return HookResult.Continue;
        }
        state.Weapons.Reset();
        _damage.Reset(player.Slot);
        state.SpectatorTarget = -1;
        if (player.TeamNum is 2 or 3)
        {
            // the model is set after the spawn event
            Server.NextFrame(() => LogRole(player));
        }
        return HookResult.Continue;
    }

    private void LogRole(CCSPlayerController player)
    {
        if (!player.IsValid || State(player) is not { } state)
        {
            return;
        }
        string model;
        try
        {
            model = player.PlayerPawn.Value?.CBodyComponent?.SceneNode?.GetSkeletonInstance()?.ModelState.ModelName ?? string.Empty;
        }
        catch (Exception ex) when (ex is InvalidOperationException or NullReferenceException)
        {
            return;
        }
        string role = string.Empty;
        foreach (string name in RoleNames)
        {
            if (model.Contains(name, StringComparison.OrdinalIgnoreCase))
            {
                role = name;
            }
        }
        if (role.Length > 0 && role != state.Role)
        {
            state.Role = role;
            Log($"\"{PlayerRef(player)}\" changed role to \"{role}\"");
        }
    }
}
