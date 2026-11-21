using System;
using MetaCue;

static void Check(bool value, string name) { if (!value) throw new Exception(name); }
foreach (int win in new[] { 0x5B, 0x5C })
{
    var g = new Gesture();
    Check(g.Update(win, true) && g.Eligible, "Win arms");
    Check(!g.Update(win, true) && g.Eligible, "Repeat does not rearm");
    g.Update(win, false); Check(!g.Eligible, "Release hides");
    g.Update(win, true); g.Update(0x31, true); Check(!g.Eligible, "Combination cancels");
    g.Update(0x31, false); Check(!g.Eligible, "No rearm until Win released");
    g.Update(win, false); Check(g.Update(win, true), "Next gesture works");
    g.Reset(); g.Update(0x10, true); g.Update(win, true); Check(!g.Eligible, "Held Shift blocks");
    g.Update(0x10, false); Check(!g.Eligible, "Shift release stays blocked");
    g.Reset(); g.Update(win, true); g.Update(win == 0x5B ? 0x5C : 0x5B, true); Check(!g.Eligible, "Both Win keys block");
    g.Reset(); Check(!g.Eligible, "Session reset");
}
Console.WriteLine("Gesture tests passed (left/right Win, repeat, release, combination, pre-held modifier, dual Win, reset).");
