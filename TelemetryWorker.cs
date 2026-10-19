namespace SmartX.Part2.Services;

public sealed class TelemetryWorker : BackgroundService
{
    private readonly TelemetryProcessor _processor;
    private readonly ILogger<TelemetryWorker> _log;

    public TelemetryWorker(TelemetryProcessor processor, ILogger<TelemetryWorker> log)
    {
        _processor = processor;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(100));
        var tick = 0;
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                _processor.Drain();
                if (++tick % 10 == 0) _processor.SweepOffline(TimeSpan.FromSeconds(6));
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Telemetry worker iteration failed");
            }
        }
    }
}
