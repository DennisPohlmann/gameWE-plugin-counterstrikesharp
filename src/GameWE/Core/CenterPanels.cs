// gameWE - CounterStrikeSharp plugin
// Copyright (C) 2026 gameWE contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace GameWE.Core;

/// <summary>
/// The center HTML panels still on show, one per slot (0-63). CS2 hides a
/// panel right away whatever duration it gets, so the plugin sends it again
/// every server tick until its time (game time in seconds) is up.
/// </summary>
public sealed class CenterPanels
{
    public readonly record struct Resend(int Slot, string Html, int SecondsLeft);

    private readonly Dictionary<int, (string Html, float Until)> _panels = new();
    private readonly List<Resend> _due = new();

    public int Count => _panels.Count;

    /// <summary>A new panel replaces the one the player has.</summary>
    public void Show(int slot, string html, int seconds, float now)
    {
        if (slot >= 0 && slot < DamageTracker.MaxSlots)
        {
            _panels[slot] = (html, now + seconds);
        }
    }

    public void Remove(int slot) => _panels.Remove(slot);

    /// <summary>Also on a map change: the game time starts again there.</summary>
    public void Clear() => _panels.Clear();

    /// <summary>The panels to send again at now; the list is reused by the next call.</summary>
    public IReadOnlyList<Resend> Tick(float now)
    {
        _due.Clear();
        foreach (var (slot, panel) in _panels)
        {
            float left = panel.Until - now;
            if (left <= 0f)
            {
                _panels.Remove(slot); // allowed while enumerating since .NET Core 3.0
                continue;
            }
            _due.Add(new Resend(slot, panel.Html, (int)MathF.Ceiling(left)));
        }
        return _due;
    }
}
