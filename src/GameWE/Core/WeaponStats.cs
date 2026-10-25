// gameWE - CounterStrikeSharp plugin
// Copyright (C) 2026 gameWE contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace GameWE.Core;

public sealed class WeaponCounters
{
    public int Shots;
    public int Hits;
    public int Kills;
    public int Headshots;
    public int Teamkills;
    public int Damage;
    public int Deaths;
}

/// <summary>
/// The weapon statistics of one player since the last <c>weaponstats</c>
/// lines: shots, hits, kills, headshots, team kills, damage and deaths per
/// weapon. The hit groups are not counted: CS2 logs them itself in its
/// <c>attacked</c> lines.
/// </summary>
public sealed class PlayerWeaponStats
{
    // the most weapons kept per player, so a broken event cannot grow it
    public const int MaxWeapons = 64;

    // thrown weapons: weapon_fire is the throw, not a shot that can hit
    private static readonly HashSet<string> NoShotWeapons = new(StringComparer.Ordinal)
    {
        "hegrenade", "inferno", "decoy", "flashbang", "smokegrenade", "molotov",
        "incgrenade", "tagrenade", "bumpmine", "breachcharge", "snowball",
    };

    private readonly Dictionary<string, WeaponCounters> _weapons = new(StringComparer.Ordinal);

    public bool IsEmpty => _weapons.Count == 0;

    /// <summary>
    /// The weapon name as the collector knows it: lower case, without
    /// <c>weapon_</c>, only <c>a-z 0-9 _</c>, at most 32 characters; null for
    /// anything else (world, invalid names).
    /// </summary>
    public static string? NormalizeWeapon(string? weapon)
    {
        if (string.IsNullOrEmpty(weapon))
        {
            return null;
        }
        string name = weapon.ToLowerInvariant();
        if (name.StartsWith("weapon_", StringComparison.Ordinal))
        {
            name = name.Substring(7);
        }
        if (name.Length == 0 || name.Length > 32 || name == "world")
        {
            return null;
        }
        foreach (char c in name)
        {
            if (!((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_'))
            {
                return null;
            }
        }
        return name;
    }

    private WeaponCounters? Get(string? weapon)
    {
        string? name = NormalizeWeapon(weapon);
        if (name == null)
        {
            return null;
        }
        if (!_weapons.TryGetValue(name, out var counters))
        {
            if (_weapons.Count >= MaxWeapons)
            {
                return null;
            }
            counters = new WeaponCounters();
            _weapons[name] = counters;
        }
        return counters;
    }

    public void Shot(string? weapon)
    {
        string? name = NormalizeWeapon(weapon);
        if (name != null && !NoShotWeapons.Contains(name) && Get(name) is { } w)
        {
            w.Shots++;
        }
    }

    public void Hit(string? weapon, int damage)
    {
        var w = Get(weapon);
        if (w == null)
        {
            return;
        }
        // a hit without a counted shot (e.g. the first shot before the plugin
        // loaded): count one shot, so hits never exceed shots
        if (w.Shots == 0)
        {
            w.Shots++;
        }
        w.Hits++;
        w.Damage += Math.Max(0, damage);
    }

    public void Kill(string? weapon, bool headshot, bool teamkill)
    {
        var w = Get(weapon);
        if (w == null)
        {
            return;
        }
        w.Kills++;
        if (headshot)
        {
            w.Headshots++;
        }
        if (teamkill)
        {
            w.Teamkills++;
        }
    }

    public void Death(string? weapon)
    {
        var w = Get(weapon);
        if (w != null)
        {
            w.Deaths++;
        }
    }

    public void Reset() => _weapons.Clear();

    /// <summary>
    /// The <c>weaponstats</c> lines of every weapon that was fired, then the
    /// counters start again from 0.
    /// </summary>
    public List<string> Flush(string playerRef)
    {
        var lines = new List<string>();
        foreach (var (weapon, counters) in _weapons.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            if (counters.Shots > 0)
            {
                lines.Add(LogFormat.WeaponStats(playerRef, weapon, counters));
            }
        }
        if (lines.Count > 0)
        {
            _weapons.Clear();
        }
        return lines;
    }
}
