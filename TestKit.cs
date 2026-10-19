using SmartX.Part2.Models;
using SmartX.Part2.Services;

namespace SmartX.Part2.Tests;

public sealed class FakeTime : TimeProvider
{
    private DateTimeOffset _now = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => _now;
    public void Advance(TimeSpan by) => _now += by;
}

public sealed class Kit
{
    public const string M1 = "AA:BB:CC:00:00:01";
    public const string M2 = "AA:BB:CC:00:00:02";
    public const string Meter = "AA:BB:CC:00:00:06";
    public const string Pump = "AA:BB:CC:00:00:09";
    public const string Pump2 = "AA:BB:CC:00:00:0A";

    public FakeTime Time { get; } = new();
    public DeviceRegistry Registry { get; } = new();
    public AlertTracker Alerts { get; } = new();
    public PredictionEngine Prediction { get; } = new();
    public TelemetryProcessor Processor { get; }
    public CommandService Commands { get; }
    public InsightService Insights { get; }

    public Kit()
    {
        Processor = new TelemetryProcessor(Registry, Alerts, Prediction, Time);
        Commands = new CommandService(Registry, Prediction, Time);
        Insights = new InsightService(Registry, Alerts, Prediction);

        Registry.TryRegister(Moisture(M1, "Moisture A"));
        Registry.TryRegister(Moisture(M2, "Moisture B"));
        Registry.TryRegister(new Device { Mac = Meter, Name = "Meter 1", Location = "Facility A", Category = DeviceCategory.PowerConsumption, Unit = "W", WarnMin = 0, WarnMax = 2500, CritMax = 3200 });
        Registry.TryRegister(new Device { Mac = Pump, Name = "Pump A", Location = "Zone 1", Category = DeviceCategory.Actuator });
        Registry.TryRegister(new Device { Mac = Pump2, Name = "Pump B", Location = "Zone 2", Category = DeviceCategory.Actuator });
    }

    private static Device Moisture(string mac, string name) => new()
    {
        Mac = mac,
        Name = name,
        Location = "Zone 1",
        Category = DeviceCategory.Environmental,
        Unit = "%",
        WarnMin = 30,
        WarnMax = 80,
        CritMin = 20,
        CritMax = 90
    };

    public TelemetryPacket Packet(string mac, double value, int secondsOffset = 0) =>
        new(mac, value, Time.GetUtcNow().UtcDateTime.AddSeconds(secondsOffset));
}
