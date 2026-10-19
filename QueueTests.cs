using SmartX.Part2.Models;
using SmartX.Part2.Services;
using Xunit;

namespace SmartX.Part2.Tests;

public class QueueTests
{
    [Fact]
    public void Standard_packets_are_processed_in_arrival_order()
    {
        var k = new Kit();
        k.Processor.Enqueue(k.Packet(Kit.M1, 50, 0));
        k.Processor.Enqueue(k.Packet(Kit.M1, 51, 1));
        k.Processor.Enqueue(k.Packet(Kit.M1, 52, 2));

        Assert.Equal(3, k.Processor.Stats().StandardDepth);
        k.Processor.Drain();

        Assert.Equal(new[] { 50.0, 51.0, 52.0 }, k.Registry.History(Kit.M1, 10).Select(p => p.Value));
        Assert.Equal(52, k.Registry.Get(Kit.M1)!.LastValue);         
        Assert.Equal(0, k.Processor.Stats().StandardDepth);
    }

    [Fact]
    public void Critical_packet_bypasses_the_standard_queue()
    {
        var k = new Kit();
        k.Processor.Enqueue(k.Packet(Kit.M1, 50));
        k.Processor.Enqueue(k.Packet(Kit.M1, 51, 1));
        k.Processor.Enqueue(k.Packet(Kit.M1, 52, 2));

        var result = k.Processor.Enqueue(k.Packet(Kit.M2, 10));      

        Assert.Equal(EnqueueResult.Critical, result);
        Assert.Equal(DeviceStatus.Alert, k.Registry.Get(Kit.M2)!.Status);   
        Assert.Equal(3, k.Processor.Stats().StandardDepth);                 
        Assert.Equal(default(DateTime), k.Registry.Get(Kit.M1)!.LastSeenUtc);   
        Assert.Equal(1, k.Processor.Stats().ProcessedCritical);
    }

    [Fact]
    public void Priority_queue_serves_the_most_severe_deviation_first()
    {
        var k = new Kit();
        
        k.Processor.Enqueue(k.Packet(Kit.M1, 15), processCriticalNow: false);
        k.Processor.Enqueue(k.Packet(Kit.M2, 5), processCriticalNow: false);
        Assert.Equal(2, k.Processor.Stats().PriorityDepth);

        k.Processor.DrainCritical();

        var next = k.Prediction.Followers(Tokens.Alert(Kit.M2));
        Assert.Contains(next, f => f.Token == Tokens.Alert(Kit.M1));
        Assert.Equal(0, k.Processor.Stats().PriorityDepth);
    }

    [Fact]
    public void Drain_always_serves_critical_before_standard()
    {
        var k = new Kit();
        k.Processor.Enqueue(k.Packet(Kit.M1, 50));
        k.Processor.Enqueue(k.Packet(Kit.Meter, 4000), processCriticalNow: false);

        var processed = k.Processor.Drain();

        Assert.Equal(2, processed);
        Assert.Equal(1, k.Processor.Stats().ProcessedCritical);
        Assert.Equal(1, k.Processor.Stats().ProcessedStandard);
    }

    [Fact]
    public void Warning_readings_use_the_standard_queue_but_still_raise_an_alert()
    {
        var k = new Kit();
        var result = k.Processor.Enqueue(k.Packet(Kit.M1, 25));      
        Assert.Equal(EnqueueResult.Standard, result);

        k.Processor.Drain();
        Assert.Equal(DeviceStatus.Alert, k.Registry.Get(Kit.M1)!.Status);
    }

    [Fact]
    public void Unknown_or_malformed_devices_are_rejected()
    {
        var k = new Kit();
        Assert.Equal(EnqueueResult.UnknownDevice, k.Processor.Enqueue(k.Packet("AA:AA:AA:AA:AA:AA", 1)));
        Assert.Equal(EnqueueResult.UnknownDevice, k.Processor.Enqueue(k.Packet("not-a-mac", 1)));
        Assert.Equal(2, k.Processor.Stats().Rejected);
    }

    [Fact]
    public void Lower_case_and_dash_separated_macs_are_accepted()
    {
        var k = new Kit();
        Assert.Equal(EnqueueResult.Standard, k.Processor.Enqueue(k.Packet("aa-bb-cc-00-00-01", 50)));
    }

    [Fact]
    public void Recovery_clears_the_alert_and_returns_device_to_online()
    {
        var k = new Kit();
        k.Processor.Enqueue(k.Packet(Kit.M1, 10));
        Assert.Equal(1, k.Alerts.Count);

        k.Processor.Enqueue(k.Packet(Kit.M1, 55, 1));
        k.Processor.Drain();

        Assert.Equal(0, k.Alerts.Count);
        Assert.Equal(DeviceStatus.Online, k.Registry.Get(Kit.M1)!.Status);
        Assert.Null(k.Registry.Get(Kit.M1)!.AlertReason);
    }

    [Fact]
    public void Load_test_ten_thousand_packets_are_all_processed()
    {
        var k = new Kit();
        for (var i = 0; i < 10_000; i++)
            k.Processor.Enqueue(k.Packet(Kit.M1, 40 + i % 10, i));

        var total = 0;
        while (k.Processor.Stats().StandardDepth > 0) total += k.Processor.Drain(500);

        Assert.Equal(10_000, total);
        Assert.Equal(10_000, k.Processor.Stats().ProcessedStandard);
        Assert.Equal(DeviceRegistry.MaxHistoryPerDevice, k.Registry.HistoryKeys(Kit.M1).Count);   
    }
}