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
{
    var g = new Gesture();
    g.Update(win, true); // Shell/menu transition loses this release.
    Check(g.WinDown(win, Array.Empty<int>()), "First fresh hold recovers missed Win release");
    g.Update(0x31, true); // A taskbar shortcut also loses its digit release.
    Check(g.WinDown(win, Array.Empty<int>()), "First fresh hold clears stale combination");
    Check(!g.Update(0x11, false) && g.Eligible, "Unrelated shell key-up does not restart hold delay");
    Check(!g.WinDown(win, new[] { win }), "Physical repeat must not restart delay");
    g.Update(0x31, true); g.Update(0x31, false);
    Check(!g.WinDown(win, new[] { win }) && !g.Eligible, "Repeat must not revive cancelled combination");
    Check(!g.WinDown(win, new[] { 0xA0 }) && !g.Eligible, "Real held Shift remains blocked");
    g.Update(0xA0, false);
    Check(!g.Eligible, "Releasing modifier cannot rearm current hold");
    Check(g.WinDown(win, Array.Empty<int>()), "Next physical press is eligible immediately");
}
{
    var queries = new HintQueryGate();
    Check(queries.TryBegin(0, out var old), "First query starts");
    Check(queries.Overdue(700) && queries.IsCurrent(old), "Slow query hides stale hints without cancelling current hold");
    queries.Invalidate(); // User releases Win and starts a new hold while UIA is busy.
    Check(!queries.IsCurrent(old), "Previous hold result rejected");
    Check(!queries.TryBegin(800, out _), "Do not accumulate UIA worker queries");
    queries.Complete(old);
    Check(queries.TryBegin(900, out var next), "New hold can query as soon as previous query returns");
    queries.Complete(old);
    Check(queries.IsCurrent(next) && queries.Busy, "Obsolete completion cannot clear newer query");
    Check(queries.Overdue(1600) && queries.IsCurrent(next), "Timeout does not invalidate new hold");
    queries.Complete(next);
    Check(queries.TryBegin(1700, out var retry), "Transient query failure can retry during same hold");
    queries.Invalidate(); Check(!queries.IsCurrent(retry), "Release prevents delayed display");
}
Console.WriteLine("Recovery tests passed: lost releases, real modifiers/repeat, slow query, old-query isolation and retry within the same hold.");
