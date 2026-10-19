namespace SmartX.Part2.Services;

public sealed record Follower(string Token, int Count, int Total)
{
    public double Probability => Total == 0 ? 0 : (double)Count / Total;
}

public sealed class PredictionEngine
{
    private readonly Dictionary<string, Dictionary<string, int>> _transitions = new();
    private readonly object _gate = new();
    private string? _previous;

    public void Observe(string token)
    {
        lock (_gate)
        {
            if (_previous is not null)
            {
                if (!_transitions.TryGetValue(_previous, out var row))
                    _transitions[_previous] = row = new Dictionary<string, int>();
                row[token] = row.GetValueOrDefault(token) + 1;
            }
            _previous = token;
        }
    }

    public void ResetSequence()
    {
        lock (_gate) _previous = null;
    }

    public IReadOnlyList<Follower> Followers(string token, int top = 3, int minCount = 1)
    {
        lock (_gate)
        {
            if (!_transitions.TryGetValue(token, out var row)) return Array.Empty<Follower>();
            var total = row.Values.Sum();
            return row.Where(kv => kv.Value >= minCount)
                .OrderByDescending(kv => kv.Value)
                .Take(top)
                .Select(kv => new Follower(kv.Key, kv.Value, total))
                .ToList();
        }
    }
}
