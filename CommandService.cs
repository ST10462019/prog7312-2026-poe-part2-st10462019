using SmartX.Part2.Models;

namespace SmartX.Part2.Services;

public sealed class CommandService
{
    private const int MaxLog = 200;

    private readonly DeviceRegistry _registry;
    private readonly PredictionEngine _prediction;
    private readonly TimeProvider _time;

    private readonly Stack<CommandRecord> _undo = new();
    private readonly List<CommandRecord> _log = new();
    private readonly HashSet<Guid> _undone = new();
    private readonly object _gate = new();

    public CommandService(DeviceRegistry registry, PredictionEngine prediction, TimeProvider time)
    {
        _registry = registry;
        _prediction = prediction;
        _time = time;
    }

    public int UndoDepth { get { lock (_gate) return _undo.Count; } }

    public CommandResult Execute(IReadOnlyList<string> macs, string? action)
    {
        action = action?.Trim().ToLowerInvariant();
        if (action is not ("on" or "off" or "toggle"))
            return new(false, "Action must be on, off or toggle.", null);
        if (macs.Count == 0)
            return new(false, "Select at least one device.", null);

        lock (_gate)
        {
            var devices = new List<Device>();
            foreach (var raw in macs)
            {
                var mac = MacAddress.Normalize(raw);
                var d = mac is null ? null : _registry.Get(mac);
                if (d is null) return new(false, $"Unknown device '{raw}'.", null);
                if (d.Category != DeviceCategory.Actuator)
                    return new(false, $"{d.Name} is a sensor and cannot be commanded.", null);
                devices.Add(d);
            }

            var previous = new Dictionary<string, bool>();
            var applied = new Dictionary<string, bool>();
            foreach (var d in devices)
            {
                var target = action == "toggle" ? !d.ActuatorOn : action == "on";
                previous[d.Mac] = d.ActuatorOn;
                applied[d.Mac] = target;
                _registry.Update(d.Mac, x => x.ActuatorOn = target);
            }

            var record = new CommandRecord(Guid.NewGuid(), _time.GetUtcNow().UtcDateTime, action,
                devices.Select(d => d.Mac).ToList(), previous, applied);
            _undo.Push(record);
            _log.Add(record);
            if (_log.Count > MaxLog) _log.RemoveAt(0);

            if (devices.Count == 1)
                _prediction.Observe(Tokens.Command(devices[0].Mac, applied[devices[0].Mac] ? "on" : "off"));

            return new(true, $"{action.ToUpperInvariant()} sent to {devices.Count} device(s).", record);
        }
    }

    public CommandResult Undo()
    {
        lock (_gate)
        {
            if (!_undo.TryPop(out var record))
                return new(false, "Nothing to undo.", null);

            foreach (var (mac, wasOn) in record.Previous)
                _registry.Update(mac, d => d.ActuatorOn = wasOn);

            _undone.Add(record.Id);
            return new(true, $"Reverted '{record.Action}' on {record.Targets.Count} device(s).", record);
        }
    }

    public IReadOnlyList<CommandView> Recent(int take = 10)
    {
        lock (_gate)
            return _log.AsEnumerable().Reverse().Take(take)
                .Select(r => new CommandView(r.Id, r.TimestampUtc, r.Action, r.Targets, _undone.Contains(r.Id)))
                .ToList();
    }
}
