using SmartX.Part2.Models;

namespace SmartX.Part2.Services;

public static class SeedData
{
    public const string MoistureA = "AA:BB:CC:00:00:01";
    public const string MoistureB = "AA:BB:CC:00:00:02";
    public const string Meter1 = "AA:BB:CC:00:00:06";
    public const string PumpA = "AA:BB:CC:00:00:09";
    public const string RelayR1 = "AA:BB:CC:00:00:0C";

    public static (string Unit, double? WarnMin, double? WarnMax, double? CritMin, double? CritMax) DefaultsFor(DeviceCategory c) => c switch
    {
        DeviceCategory.Environmental => ("%", 30, 80, 20, 90),
        DeviceCategory.PowerConsumption => ("W", 0, 2500, null, 3200),
        _ => ("", null, null, null, null)
    };

    public static void Apply(IServiceProvider sp)
    {
        var registry = sp.GetRequiredService<DeviceRegistry>();
        var prediction = sp.GetRequiredService<PredictionEngine>();

        Device D(string mac, string name, string loc, DeviceCategory cat, string unit,
                 double? wMin, double? wMax, double? cMin, double? cMax) => new()
        {
            Mac = mac, Name = name, Location = loc, Category = cat, Unit = unit,
            WarnMin = wMin, WarnMax = wMax, CritMin = cMin, CritMax = cMax
        };

        var devices = new[]
        {
            D(MoistureA,            "Moisture Sensor A",  "Zone 1 / Bay A",      DeviceCategory.Environmental,    "%",  30, 80, 20, 90),
            D(MoistureB,            "Moisture Sensor B",  "Zone 1 / Bay B",      DeviceCategory.Environmental,    "%",  30, 80, 20, 90),
            D("AA:BB:CC:00:00:03",  "Temperature Node C", "Greenhouse 1",        DeviceCategory.Environmental,    "°C", 15, 32, 10, 38),
            D("AA:BB:CC:00:00:04",  "Humidity Node D",    "Greenhouse 1",        DeviceCategory.Environmental,    "%",  40, 85, 30, 92),
            D("AA:BB:CC:00:00:05",  "pH Probe E",         "Zone 2 / Reservoir",  DeviceCategory.Environmental,    "pH", 5.5, 7, 5, 7.5),
            D(Meter1,               "Smart Meter 1",      "Facility A / Main DB",DeviceCategory.PowerConsumption,  "W",  0, 2500, null, 3200),
            D("AA:BB:CC:00:00:07",  "Smart Meter 2",      "Facility A / Sub DB", DeviceCategory.PowerConsumption,  "W",  0, 2500, null, 3200),
            D("AA:BB:CC:00:00:08",  "Solar Inverter Meter","Facility B / Roof",  DeviceCategory.PowerConsumption,  "W",  0, 4000, null, 5200),
            D(PumpA,                "Pump A",             "Zone 1 / Bay B",      DeviceCategory.Actuator,         "",   null, null, null, null),
            D("AA:BB:CC:00:00:0A",  "Pump B",             "Zone 2 / Reservoir",  DeviceCategory.Actuator,         "",   null, null, null, null),
            D("AA:BB:CC:00:00:0B",  "Valve V1",           "Zone 1 / Manifold",   DeviceCategory.Actuator,         "",   null, null, null, null),
            D(RelayR1,              "Smart Relay R1",     "Facility A / Main DB",DeviceCategory.Actuator,         "",   null, null, null, null),
        };
        foreach (var d in devices) registry.TryRegister(d);

        for (var i = 0; i < 5; i++)
        {
            prediction.Observe(Tokens.Alert(MoistureB));
            prediction.Observe(Tokens.Command(PumpA, "on"));
        }
        for (var i = 0; i < 2; i++)
        {
            prediction.Observe(Tokens.Alert(MoistureB));
            prediction.Observe(Tokens.Search("pump a"));
            prediction.Observe(Tokens.Command(PumpA, "on"));
        }

        for (var i = 0; i < 4; i++)
        {
            prediction.Observe(Tokens.Alert(Meter1));
            prediction.Observe(Tokens.Command(RelayR1, "off"));
        }
        prediction.ResetSequence();
    }
}
