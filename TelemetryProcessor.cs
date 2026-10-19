using SmartX.Part2.Models;

namespace SmartX.Part2.Services;

public sealed class TelemetryProcessor
{
    private readonly DeviceRegistry _registry;
    private readonly AlertTracker _alerts;
    private readonly PredictionEngine _prediction;
    private readonly TimeProvider _time;

    private readonly Queue<TelemetryPacket> _standard = new();
    private readonly PriorityQueue<TelemetryPacket, double> _critical = new();
    private readonly object _gate = new();

    private long _processedStandard, _processedCritical, _rejected;

    public TelemetryProcessor(DeviceRegistry registry, AlertTracker alerts, PredictionEngine prediction, TimeProvider time)
    {
        _registry = registry;
        _alerts = alerts;
        _prediction = prediction;
        _time = time;
    }

    public EnqueueResult Enqueue(TelemetryPacket packet, bool processCriticalNow = true)
    {
        var mac = MacAddress.Normalize(packet.Mac);
        var device = mac is null ? null : _registry.Get(mac);     
        if (mac is null || device is null)
        {
            Interlocked.Increment(ref _rejected);
            return EnqueueResult.UnknownDevice;
        }

        packet = packet with { Mac = mac };
        var (severity, _, deviation) = AlertRules.Classify(device, packet.Value);

        lock (_gate)
        {
            if (severity == Severity.Critical)
            {
               
                _critical.Enqueue(packet, -deviation);
                if (processCriticalNow) DrainCriticalLocked();
                return EnqueueResult.Critical;
            }

            _standard.Enqueue(packet);
            return EnqueueResult.Standard;
        }
    }

    public int DrainCritical()
    {
        lock (_gate) return DrainCriticalLocked();
    }

    public int Drain(int maxStandard = 500)
    {
        lock (_gate)
        {
            var processed = DrainCriticalLocked();
            var std = 0;
            while (std < maxStandard && _standard.TryDequeue(out var packet))
            {
                Apply(packet);
                _processedStandard++;
                std++;
                if (_critical.Count > 0) processed += DrainCriticalLocked();   
            }
            return processed + std;
        }
    }

    private int DrainCriticalLocked()
    {
        var n = 0;
        while (_critical.TryDequeue(out var packet, out _))
        {
            Apply(packet);
            _processedCritical++;
            n++;
        }
        return n;
    }

    private void Apply(TelemetryPacket p)
    {
        var device = _registry.Get(p.Mac);
        if (device is null) return;

        var now = _time.GetUtcNow().UtcDateTime;
        var (severity, reason, _) = AlertRules.Classify(device, p.Value);

        _registry.AppendHistory(p.Mac, p.TimestampUtc, p.Value);
        _alerts.Clear(p.Mac, AlertTracker.Disconnected);          

        if (severity == Severity.Normal)
        {
            _alerts.Clear(p.Mac, AlertTracker.OutOfRange);
            _registry.Update(p.Mac, d =>
            {
                d.LastValue = p.Value;
                d.LastSeenUtc = now;
                d.Status = DeviceStatus.Online;
                d.AlertReason = null;
            });
            return;
        }

        var isNew = _alerts.Raise(p.Mac, AlertTracker.OutOfRange);   
        _registry.Update(p.Mac, d =>
        {
            d.LastValue = p.Value;
            d.LastSeenUtc = now;
            d.Status = DeviceStatus.Alert;
            d.AlertReason = reason;
        });
        if (isNew) _prediction.Observe(Tokens.Alert(p.Mac));
    }

    public int SweepOffline(TimeSpan timeout)
    {
        lock (_gate)
        {
            var now = _time.GetUtcNow().UtcDateTime;
            var raised = 0;
            foreach (var d in _registry.Snapshot())
            {
                if (d.LastSeenUtc == default || now - d.LastSeenUtc <= timeout) continue;
                if (_alerts.IsActive(d.Mac, AlertTracker.Disconnected)) continue;   

                if (_alerts.Raise(d.Mac, AlertTracker.Disconnected))
                {
                    var secs = (int)(now - d.LastSeenUtc).TotalSeconds;
                    _registry.Update(d.Mac, x =>
                    {
                        x.Status = DeviceStatus.Offline;
                        x.AlertReason = $"No telemetry for {secs}s";
                    });
                    _prediction.Observe(Tokens.Alert(d.Mac));
                    raised++;
                }
            }
            return raised;
        }
    }

    public ProcessorStats Stats()
    {
        lock (_gate)
            return new ProcessorStats(_standard.Count, _critical.Count, _processedStandard, _processedCritical,
                Interlocked.Read(ref _rejected));
    }
}
