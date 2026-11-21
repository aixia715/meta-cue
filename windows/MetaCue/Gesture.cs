using System.Collections.Generic;

namespace MetaCue;

public sealed class Gesture
{
    private readonly HashSet<int> down = new();
    private bool blocked;
    public bool Eligible => !blocked && down.Count == 1 && (down.Contains(0x5B) || down.Contains(0x5C));
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
        return Eligible;
    }
    public void Reset() { down.Clear(); blocked = false; }
}
