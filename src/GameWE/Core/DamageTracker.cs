// gameWE - CounterStrikeSharp plugin
// Copyright (C) 2026 gameWE contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace GameWE.Core;

/// <summary>
/// The damage players did to each other since their last spawn, for the
/// damage display on death and at round end. Players are their slot (0-63).
/// </summary>
public sealed class DamageTracker
{
    public const int MaxSlots = 64;

    public sealed class Entry
    {
        public int Hits;
        public int Damage;
        public int Kills;
        public bool Killer;    // this attacker killed the victim last
        public int HealthLeft; // the killer's health at the kill
        public string Weapon = string.Empty;
    }

    public readonly record struct Line(int Slot, int Hits, int Damage, int HealthLeft, string Weapon);

    public sealed class Summary
    {
        public List<Line> Attackers { get; } = new(); // hurt the player, did not kill
        public List<Line> Killers { get; } = new();   // killed the player
        public List<Line> Killed { get; } = new();    // killed by the player
        public List<Line> Wounded { get; } = new();   // hurt by the player, not killed

        public bool IsEmpty => Attackers.Count == 0 && Killers.Count == 0 && Killed.Count == 0 && Wounded.Count == 0;
    }

    // the most lines per section, as in the SourceMod plugin's panel
    public const int MaxLines = 8;

    private readonly Entry?[,] _entries = new Entry?[MaxSlots, MaxSlots];

    private static bool Valid(int slot) => slot is >= 0 and < MaxSlots;

    public void Hurt(int attacker, int victim, int damage)
    {
        if (!Valid(attacker) || !Valid(victim) || attacker == victim)
        {
            return;
        }
        var entry = _entries[attacker, victim] ??= new Entry();
        entry.Hits++;
        entry.Damage += Math.Max(0, damage);
    }

    public void Kill(int attacker, int victim, int healthLeft, string weapon)
    {
        if (!Valid(attacker) || !Valid(victim) || attacker == victim)
        {
            return;
        }
        var entry = _entries[attacker, victim] ??= new Entry();
        entry.Kills++;
        entry.Killer = true;
        entry.HealthLeft = Math.Max(0, healthLeft);
        entry.Weapon = weapon;
    }

    /// <summary>
    /// What the player did and got since the last reset. The killer mark is
    /// taken: a later summary does not show the same killer again.
    /// </summary>
    public Summary Build(int player)
    {
        var summary = new Summary();
        if (!Valid(player))
        {
            return summary;
        }
        for (int other = 0; other < MaxSlots; other++)
        {
            if (other == player)
            {
                continue;
            }
            var done = _entries[player, other];
            if (done is { Hits: > 0 })
            {
                var list = done.Kills > 0 ? summary.Killed : summary.Wounded;
                if (list.Count < MaxLines)
                {
                    list.Add(new Line(other, done.Hits, done.Damage, 0, string.Empty));
                }
            }
            var got = _entries[other, player];
            if (got is { Hits: > 0 })
            {
                if (got.Killer)
                {
                    if (summary.Killers.Count < MaxLines)
                    {
                        summary.Killers.Add(new Line(other, got.Hits, got.Damage, got.HealthLeft, got.Weapon));
                    }
                    got.Killer = false;
                }
                else if (summary.Attackers.Count < MaxLines)
                {
                    summary.Attackers.Add(new Line(other, got.Hits, got.Damage, 0, string.Empty));
                }
            }
        }
        return summary;
    }

    /// <summary>Forgets the killer marks of the player, when no summary is shown.</summary>
    public void ForgetKiller(int victim)
    {
        if (!Valid(victim))
        {
            return;
        }
        for (int other = 0; other < MaxSlots; other++)
        {
            if (_entries[other, victim] is { } entry)
            {
                entry.Killer = false;
            }
        }
    }

    /// <summary>A new life: what the player did and got so far is forgotten.</summary>
    public void Reset(int player)
    {
        if (!Valid(player))
        {
            return;
        }
        for (int other = 0; other < MaxSlots; other++)
        {
            _entries[player, other] = null;
            _entries[other, player] = null;
        }
    }

    public void ResetAll() => Array.Clear(_entries);
}
