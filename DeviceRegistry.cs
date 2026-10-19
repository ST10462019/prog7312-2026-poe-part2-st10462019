using System.Text.RegularExpressions;
using SmartX.Part2.Models;

namespace SmartX.Part2.Services;

public sealed class DeviceRegistry
{
    public const int MaxHistoryPerDevice = 300;

    private readonly Dictionary<string, Device> _devices = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, SortedDictionary<DateTime, double>> _history = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _gate = new();

    public bool TryRegister(Device device)
    {
        lock (_gate)
        {
            if (!_devices.TryAdd(device.Mac, device)) return false;
            _history[device.Mac] = new SortedDictionary<DateTime, double>();
            return true;
        }
    }

    public Device? Get(string mac)
    {
        lock (_gate) return _devices.TryGetValue(mac, out var d) ? d.Clone() : null;
    }

    public bool Update(string mac, Action<Device> change)
    {
        lock (_gate)
        {
            if (!_devices.TryGetValue(mac, out var d)) return false;
            change(d);
            return true;
        }
    }

    public List<Device> Snapshot()
    {
        lock (_gate) return _devices.Values.Select(d => d.Clone()).OrderBy(d => d.Name).ToList();
    }

    public List<Device> Query(DeviceCategory? category, DeviceStatus? status, string? text)
    {
        var words = (text ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        lock (_gate)
        {
            return _devices.Values
                .Where(d => category is null || d.Category == category)
                .Where(d => status is null || d.Status == status)
                .Where(d => Matches(d, words))
                .Select(d => d.Clone())
                .OrderBy(d => d.Name)
                .ToList();
        }
    }

    private static bool Matches(Device d, string[] words)
    {
        if (words.Length == 0) return true;
        var tokens = Regex.Split($"{d.Name} {d.Location}".ToLowerInvariant(), @"[^\p{L}\p{N}]+")
            .Where(t => t.Length > 0).ToArray();
        return words.All(w =>
            tokens.Any(t => t.StartsWith(w, StringComparison.OrdinalIgnoreCase)) ||
            (w.Length >= 4 && d.Mac.Contains(w, StringComparison.OrdinalIgnoreCase)));
    }

    public void AppendHistory(string mac, DateTime timestampUtc, double value)
    {
        lock (_gate)
        {
            if (!_history.TryGetValue(mac, out var log)) return;
            log[timestampUtc] = value;                    
            while (log.Count > MaxHistoryPerDevice)
                log.Remove(log.Keys.First());             
        }
    }

    public List<KeyValuePair<DateTime, double>> History(string mac, int take)
    {
        lock (_gate)
        {
            if (!_history.TryGetValue(mac, out var log)) return new();
            return log.Skip(Math.Max(0, log.Count - take)).ToList();
        }
    }

    public IReadOnlyList<DateTime> HistoryKeys(string mac)
    {
        lock (_gate) return _history.TryGetValue(mac, out var log) ? log.Keys.ToList() : new List<DateTime>();
    }
}
