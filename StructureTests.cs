using SmartX.Part2.Models;
using SmartX.Part2.Services;
using Xunit;

namespace SmartX.Part2.Tests;

public class StructureTests
{
    
    [Fact]
    public void Duplicate_alerts_are_suppressed_by_the_hash_set()
    {
        var alerts = new AlertTracker();
        Assert.True(alerts.Raise("A", AlertTracker.Disconnected));
        Assert.False(alerts.Raise("A", AlertTracker.Disconnected));
        Assert.False(alerts.Raise("A", AlertTracker.Disconnected));
        Assert.True(alerts.Raise("A", AlertTracker.OutOfRange));            
        Assert.Equal(2, alerts.Count);
        Assert.Equal(2, alerts.DuplicatesSuppressed);

        Assert.True(alerts.Clear("A", AlertTracker.Disconnected));
        Assert.True(alerts.Raise("A", AlertTracker.Disconnected));          
    }

    [Fact]
    public void Silent_device_is_flagged_disconnected_exactly_once()
    {
        var k = new Kit();
        k.Processor.Enqueue(k.Packet(Kit.M1, 50));
        k.Processor.Drain();

        k.Time.Advance(TimeSpan.FromSeconds(3));
        Assert.Equal(0, k.Processor.SweepOffline(TimeSpan.FromSeconds(6)));   

        k.Time.Advance(TimeSpan.FromSeconds(10));
        Assert.Equal(1, k.Processor.SweepOffline(TimeSpan.FromSeconds(6)));
        Assert.Equal(DeviceStatus.Offline, k.Registry.Get(Kit.M1)!.Status);

        Assert.Equal(0, k.Processor.SweepOffline(TimeSpan.FromSeconds(6)));   
        Assert.Equal(1, k.Alerts.DuplicatesSuppressed);
    }

    [Fact]
    public void Reconnecting_device_clears_the_disconnect_alert()
    {
        var k = new Kit();
        k.Processor.Enqueue(k.Packet(Kit.M1, 50));
        k.Processor.Drain();
        k.Time.Advance(TimeSpan.FromSeconds(20));
        k.Processor.SweepOffline(TimeSpan.FromSeconds(6));

        k.Processor.Enqueue(k.Packet(Kit.M1, 52, 30));
        k.Processor.Drain();

        Assert.False(k.Alerts.IsActive(Kit.M1, AlertTracker.Disconnected));
        Assert.Equal(DeviceStatus.Online, k.Registry.Get(Kit.M1)!.Status);
    }

    [Fact]
    public void Devices_that_never_reported_are_not_flagged_by_the_sweep()
    {
        var k = new Kit();
        k.Time.Advance(TimeSpan.FromHours(1));
        Assert.Equal(0, k.Processor.SweepOffline(TimeSpan.FromSeconds(6)));
    }

    [Fact]
    public void Registry_lookup_is_case_insensitive_and_rejects_duplicate_macs()
    {
        var k = new Kit();
        Assert.NotNull(k.Registry.Get(Kit.M1.ToLowerInvariant()));
        Assert.False(k.Registry.TryRegister(new Device { Mac = Kit.M1, Name = "dup", Location = "x" }));
        Assert.Null(k.Registry.Get("FF:FF:FF:FF:FF:FF"));
    }

    [Fact]
    public void Query_filters_by_category_status_and_text()
    {
        var k = new Kit();
        Assert.Equal(2, k.Registry.Query(DeviceCategory.Actuator, null, null).Count);
        Assert.Single(k.Registry.Query(null, null, "pump a"));
        Assert.Equal(3, k.Registry.Query(null, null, "zone 1").Count);       
        Assert.Single(k.Registry.Query(null, null, "mois b"));                 

        k.Processor.Enqueue(k.Packet(Kit.M2, 5));
        var alerting = k.Registry.Query(null, DeviceStatus.Alert, null);
        Assert.Equal(Kit.M2, Assert.Single(alerting).Mac);
    }

    [Fact]
    public void History_is_ordered_by_timestamp_even_when_packets_arrive_out_of_order()
    {
        var k = new Kit();
        var t = k.Time.GetUtcNow().UtcDateTime;
        k.Registry.AppendHistory(Kit.M1, t.AddSeconds(5), 3);
        k.Registry.AppendHistory(Kit.M1, t.AddSeconds(1), 1);
        k.Registry.AppendHistory(Kit.M1, t.AddSeconds(3), 2);

        var keys = k.Registry.HistoryKeys(Kit.M1);
        Assert.Equal(keys.OrderBy(x => x), keys);
        Assert.Equal(new[] { 1.0, 2.0, 3.0 }, k.Registry.History(Kit.M1, 10).Select(p => p.Value));
    }

    [Fact]
    public void History_take_returns_only_the_latest_readings()
    {
        var k = new Kit();
        var t = k.Time.GetUtcNow().UtcDateTime;
        for (var i = 0; i < 10; i++) k.Registry.AppendHistory(Kit.M1, t.AddSeconds(i), i);
        Assert.Equal(new[] { 7.0, 8.0, 9.0 }, k.Registry.History(Kit.M1, 3).Select(p => p.Value));
    }

    [Fact]
    public void Undo_restores_the_previous_actuator_state()
    {
        var k = new Kit();
        Assert.True(k.Commands.Execute(new[] { Kit.Pump }, "on").Ok);
        Assert.True(k.Registry.Get(Kit.Pump)!.ActuatorOn);

        Assert.True(k.Commands.Undo().Ok);
        Assert.False(k.Registry.Get(Kit.Pump)!.ActuatorOn);
        Assert.Equal(0, k.Commands.UndoDepth);
    }

    [Fact]
    public void Undo_is_last_in_first_out()
    {
        var k = new Kit();
        k.Commands.Execute(new[] { Kit.Pump }, "on");
        k.Commands.Execute(new[] { Kit.Pump2 }, "on");

        k.Commands.Undo();

        Assert.False(k.Registry.Get(Kit.Pump2)!.ActuatorOn);   
        Assert.True(k.Registry.Get(Kit.Pump)!.ActuatorOn);
        Assert.Equal(1, k.Commands.UndoDepth);
    }

    [Fact]
    public void Bulk_shutdown_can_be_undone_in_one_step()
    {
        var k = new Kit();
        k.Commands.Execute(new[] { Kit.Pump }, "on");                       
        k.Commands.Execute(new[] { Kit.Pump, Kit.Pump2 }, "off");           
        Assert.False(k.Registry.Get(Kit.Pump)!.ActuatorOn);

        k.Commands.Undo();

        Assert.True(k.Registry.Get(Kit.Pump)!.ActuatorOn);                  
        Assert.False(k.Registry.Get(Kit.Pump2)!.ActuatorOn);                
    }

    [Fact]
    public void Toggle_flips_each_device_independently()
    {
        var k = new Kit();
        k.Commands.Execute(new[] { Kit.Pump }, "on");
        k.Commands.Execute(new[] { Kit.Pump, Kit.Pump2 }, "toggle");
        Assert.False(k.Registry.Get(Kit.Pump)!.ActuatorOn);
        Assert.True(k.Registry.Get(Kit.Pump2)!.ActuatorOn);
    }

    [Fact]
    public void Commands_are_validated()
    {
        var k = new Kit();
        Assert.False(k.Commands.Execute(new[] { Kit.M1 }, "on").Ok);                     
        Assert.False(k.Commands.Execute(new[] { "AA:AA:AA:AA:AA:AA" }, "on").Ok);        
        Assert.False(k.Commands.Execute(new[] { Kit.Pump }, "explode").Ok);              
        Assert.False(k.Commands.Execute(Array.Empty<string>(), "on").Ok);                 
        Assert.False(k.Commands.Undo().Ok);                                              
        Assert.Equal(0, k.Commands.UndoDepth);
    }

    [Fact]
    public void A_failed_bulk_command_changes_nothing()
    {
        var k = new Kit();
        var r = k.Commands.Execute(new[] { Kit.Pump, Kit.M1 }, "on");
        Assert.False(r.Ok);
        Assert.False(k.Registry.Get(Kit.Pump)!.ActuatorOn);
    }

    [Fact]
    public void Undone_commands_are_flagged_in_the_recent_log()
    {
        var k = new Kit();
        k.Commands.Execute(new[] { Kit.Pump }, "on");
        k.Commands.Undo();
        Assert.True(Assert.Single(k.Commands.Recent()).Undone);
    }

    [Theory]
    [InlineData("aa:bb:cc:dd:ee:ff", "AA:BB:CC:DD:EE:FF")]
    [InlineData("AA-BB-CC-DD-EE-FF", "AA:BB:CC:DD:EE:FF")]
    [InlineData("  AA:BB:CC:DD:EE:FF ", "AA:BB:CC:DD:EE:FF")]
    [InlineData("AA:BB:CC:DD:EE", null)]
    [InlineData("GG:BB:CC:DD:EE:FF", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void Mac_normalisation(string? input, string? expected) =>
        Assert.Equal(expected, MacAddress.Normalize(input));

    [Theory]
    [InlineData(50, Severity.Normal)]
    [InlineData(25, Severity.Warning)]
    [InlineData(85, Severity.Warning)]
    [InlineData(19, Severity.Critical)]
    [InlineData(95, Severity.Critical)]
    public void Alert_rules_classify_readings(double value, Severity expected)
    {
        var k = new Kit();
        Assert.Equal(expected, AlertRules.Classify(k.Registry.Get(Kit.M1)!, value).Severity);
    }

    [Fact]
    public void Actuators_never_raise_threshold_alerts()
    {
        var k = new Kit();
        Assert.Equal(Severity.Normal, AlertRules.Classify(k.Registry.Get(Kit.Pump)!, 9999).Severity);
    }
}
