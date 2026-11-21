using System.Collections.Generic;

namespace MetaCue;

public sealed class Gesture
{
    private readonly HashSet<int> down = new();
    private bool blocked;
    public bool Eligible => !blocked && down.Count == 1 && (down.Contains(0x5B) || down.Contains(0x5C));
    public bool WinDown(int key, IEnumerable<int> heldBeforeEvent)
    {
        // The low-level hook runs BEFORE the incoming event updates async state.
        // A fresh Win-down is a recovery boundary for missed key-up events. A repeat
        // must retain the current gesture's blocked state.
        var held = new HashSet<int>(heldBeforeEvent);
        if (!held.Contains(key))
        {
            Reset();
            foreach (int other in held) Update(other, true);
        }
        return Update(key, true);
    }
    public bool Update(int key, bool pressed)
    {
        if (pressed)
        {
            if (!down.Add(key)) return false;
            if (down.Count != 1 || (key != 0x5B && key != 0x5C)) blocked = true;
        }
        else
        {
            down.Remove(key);
            if (!down.Contains(0x5B) && !down.Contains(0x5C)) blocked = false;
        }
        // An unrelated or duplicate key-up must not restart the hold timer.
        return pressed && Eligible;
    }
    public void Reset() { down.Clear(); blocked = false; }
}
