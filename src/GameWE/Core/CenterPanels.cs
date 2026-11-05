// gameWE - CounterStrikeSharp plugin
// Copyright (C) 2026 gameWE contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace GameWE.Core;

/// <summary>
/// The center HTML panels still on show, one per slot (0-63). CS2 hides a
/// panel after a moment whatever duration it gets, so the plugin sends it
/// again until its time is up. A count, because the game clock restarts on
/// a map change.
/// </summary>
public sealed class CenterPanels
{
    public readonly record struct Resend(int Slot, string Html, int SecondsLeft);

    private readonly Dictionary<int, (string Html, int SecondsLeft)> _panels = new();

    /// <summary>A new panel replaces the one the player has.</summary>
    public void Show(int slot, string html, int seconds)
    {
        if (slot >= 0 && slot < DamageTracker.MaxSlots)
        {
            _panels[slot] = (html, seconds);
        }
    }

    public void Remove(int slot) => _panels.Remove(slot);

    public void Clear() => _panels.Clear();

    /// <summary>One second has passed: the panels to send again.</summary>
    public List<Resend> Tick()
    {
        var resend = new List<Resend>(_panels.Count);
        foreach (var (slot, panel) in _panels.ToArray())
        {
            int left = panel.SecondsLeft - 1;
            if (left <= 0)
            {
                _panels.Remove(slot);
                continue;
            }
            _panels[slot] = (panel.Html, left);
            resend.Add(new Resend(slot, panel.Html, left));
        }
        return resend;
    }
}
