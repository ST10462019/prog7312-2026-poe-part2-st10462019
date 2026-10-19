namespace SmartX.Part2.Services;

public sealed record ActiveAlert(string Mac, string Kind);

public sealed class AlertTracker
{
    public const string OutOfRange = "OUT_OF_RANGE";
    public const string Disconnected = "DISCONNECTED";

    private readonly HashSet<(string Mac, string Kind)> _active = new();
    private readonly object _gate = new();
    private long _suppressed;

    public bool Raise(string mac, string kind)
    {
        lock (_gate)
        {
            if (_active.Add((mac, kind))) return true;
            _suppressed++;
            return false;
        }
    }

    public bool Clear(string mac, string kind)
    {
        lock (_gate) return _active.Remove((mac, kind));
    }

    public bool IsActive(string mac, string kind)
    {
        lock (_gate) return _active.Contains((mac, kind));
    }

    public IReadOnlyList<ActiveAlert> Snapshot()
    {
        lock (_gate) return _active.Select(a => new ActiveAlert(a.Mac, a.Kind)).ToList();
    }

    public int Count { get { lock (_gate) return _active.Count; } }
    public long DuplicatesSuppressed { get { lock (_gate) return _suppressed; } }
}
