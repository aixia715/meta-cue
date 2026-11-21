namespace MetaCue;

public readonly record struct HintQueryToken(long Session, long Sequence);

// A slow query may outlive a key hold. Keep just one query in flight, reject its
// obsolete result, but never let its age cancel a newer hold.
public sealed class HintQueryGate
{
    private long session, sequence, started;
    private HintQueryToken? pending;
    public bool Busy => pending.HasValue;
    public bool Overdue(long now) => Busy && now - started > 600;
    public void Invalidate() => session++;
    public bool TryBegin(long now, out HintQueryToken token)
    {
        token = default;
        if (Busy) return false;
        token = new HintQueryToken(session, ++sequence);
        pending = token; started = now;
        return true;
    }
    public bool IsCurrent(HintQueryToken token) => token.Session == session && pending == token;
    public void Complete(HintQueryToken token) { if (pending == token) pending = null; }
}
