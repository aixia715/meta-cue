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

foreach (int win in new[] { 0x5B, 0x5C })
foreach (int shift in new[] { 0xA0, 0xA1 })
foreach (bool shiftFirst in new[] { false, true })
{
    var g = new ExtendedGesture();
    g.Update(shiftFirst ? shift : win, true);
    Check(g.Update(shiftFirst ? win : shift, true).Action == SwitchAction.Begin, "Either modifier order begins");
    var key = g.Update(0x31, true);
    Check(key.Consume && key.Action == SwitchAction.Select && key.Slot == 0, "1 selects taskbar slot 11");
    Check(g.Update(0x31, true) == new SwitchKey(true), "Auto repeat swallowed without cycling");
    Check(g.Update(0x31, false).Consume, "Captured key-up swallowed");
    Check(g.Update(0x31, true).Action == SwitchAction.Select, "Second discrete press cycles");
    g.Update(0x31, false);
    Check(g.Update(0x30, true).Slot == 9, "0 selects taskbar slot 20");
    g.Update(0x30, false);
    Check(g.Update(shift, false).Action == SwitchAction.None, "First modifier release does not commit");
    Check(g.Update(win, false).Action == SwitchAction.Commit, "Last modifier release commits once");
    Check(g.Update(win, false).Action == SwitchAction.None, "No double commit");
}
{
    var g = new ExtendedGesture();
    g.Update(0x5B, true); g.Update(0xA0, true); g.Update(0x31, true);
    Check(g.Update(0x1B, true) == new SwitchKey(true, SwitchAction.Cancel), "Esc cancels and is consumed");
    Check(g.Update(0x1B, false).Consume && g.Update(0x31, false).Consume, "Key releases stay paired after cancel");
    var cancelled = g.Update(0x32, true);
    Check(cancelled.Consume && cancelled.Action != SwitchAction.Select, "Cancelled owned chord cannot leak native new-instance shortcut");
    g.Reset(); g.Update(0x11, true); g.Update(0x5B, true);
    Check(g.Update(0xA0, true).Action == SwitchAction.None && !g.Update(0x31, true).Consume, "Ctrl chord is untouched");
    g.Reset(); g.Update(0x5B, true);
    Check(!g.Update(0x31, true).Consume, "Win+number stays native");
    g.Reset(); g.Update(0x5B, true); g.Update(0xA0, true); g.Update(0x31, true);
    g.Update(0xA0, false, false);
    Check(g.Update(0x31, false, false).Consume, "Disable retains captured key-up");
    g.Reset(); g.Update(0x5B, true, false); g.Update(0xA0, true, false);
    Check(!g.Update(0x31, true, false).Consume, "Disabled feature passes native shortcut");
    g.Reset(); g.Update(0x5B, true); g.Update(0xA0, true); g.Update(0x11, true);
    Check(!g.Update(0x31, true).Consume, "Adding Ctrl leaves native admin shortcut available");
}
{
    var cycle = new CycleSelection();
    for (int i = 0; i < 7; i++) { cycle.Press(0); Check(cycle.Index(3) == i % 3, "Stable wraparound"); }
    cycle.Press(1); Check(cycle.Index(3) == 0, "Changing digit resets candidate");
    Check(cycle.Index(0) == -1 && cycle.Index(1) == 0, "Empty/single group");
}
Console.WriteLine("Extended shortcut tests passed: modifier order, mapping, repeat filtering, cycling, commit, cancel, disable and native pass-through.");
