using System.Collections.Generic;

namespace MetaCue;

public enum SwitchAction { None, Begin, Select, Commit, Cancel }
public readonly record struct SwitchKey(bool Consume, SwitchAction Action = SwitchAction.None, int Slot = -1);

// Runs in the hook: no UI, filesystem, COM or window activation here.
public sealed class ExtendedGesture
{
    private readonly HashSet<int> down = new(), captured = new();
    private bool active, selected, blocked, owned;
    private bool Win => down.Contains(0x5B) || down.Contains(0x5C);
    private bool Shift => down.Contains(0x10) || down.Contains(0xA0) || down.Contains(0xA1);
    public bool Chord => Win && Shift;
    private static bool Modifier(int k) => k is 0x5B or 0x5C or 0x10 or 0xA0 or 0xA1;
    public SwitchKey Update(int key, bool pressed, bool enabled = true)
    {
        bool fresh = pressed ? down.Add(key) : down.Remove(key);
        bool consume = !pressed && captured.Remove(key);
        if (pressed && captured.Contains(key)) return new(true); // repeat: do not advance
        if (!enabled)
        {
            bool cancel = active;
            active = selected = owned = false;
            blocked = Win || Shift;
            return new(consume, cancel ? SwitchAction.Cancel : SwitchAction.None);
        }
        if (!pressed)
        {
            if (!Win && !Shift)
            {
                var action = active ? (selected ? SwitchAction.Commit : SwitchAction.Cancel) : SwitchAction.None;
                active = selected = blocked = owned = false;
                return new(consume, action);
            }
            return new(consume);
        }
        if (!fresh) return new(consume);
        if (owned && (!active || !Chord) && key is >= 0x30 and <= 0x39)
        { captured.Add(key); return new(true); }
        if (active && Chord && key is >= 0x30 and <= 0x39)
        {
            captured.Add(key);
            selected = true;
            return new(true, SwitchAction.Select, key == 0x30 ? 9 : key - 0x31);
        }
        if (active && !Modifier(key))
        {
            active = selected = false;
            blocked = true;
            if (key is 0x11 or 0x12 or 0xA2 or 0xA3 or 0xA4 or 0xA5) owned = false;
            if (key == 0x1B) { captured.Add(key); consume = true; }
            return new(consume, SwitchAction.Cancel);
        }
        if (!Modifier(key)) { blocked = true; return new(consume); }
        if (!active && !blocked && Chord && down.Count == 2)
        {
            active = owned = true;
            return new(false, SwitchAction.Begin);
        }
        return new(consume);
    }
    public void Reset()
    {
        // Keep captured key-ups paired even if disabled/session-cancelled mid-press.
        down.Clear(); active = selected = blocked = owned = false;
    }
}

public sealed class CycleSelection
{
    public int Slot { get; private set; } = -1;
    public int Presses { get; private set; }
    public void Press(int slot) { Presses = slot == Slot ? Presses + 1 : 1; Slot = slot; }
    public int Index(int count) => count > 0 ? (Presses - 1) % count : -1;
    public void Reset() { Slot = -1; Presses = 0; }
}
