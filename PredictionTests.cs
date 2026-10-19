using SmartX.Part2.Models;
using SmartX.Part2.Services;
using Xunit;

namespace SmartX.Part2.Tests;

public class PredictionTests
{
    private static void TrainPumpPattern(Kit k, int times)
    {
        for (var i = 0; i < times; i++)
        {
            k.Prediction.Observe(Tokens.Alert(Kit.M2));
            k.Prediction.Observe(Tokens.Command(Kit.Pump, "on"));
        }
        k.Prediction.ResetSequence();
    }

    [Fact]
    public void Transition_probabilities_reflect_observed_history()
    {
        var p = new PredictionEngine();
        for (var i = 0; i < 3; i++) { p.Observe("a"); p.Observe("x"); }
        p.Observe("a"); p.Observe("y");

        var f = p.Followers("a");
        Assert.Equal("x", f[0].Token);
        Assert.Equal(3, f[0].Count);
        Assert.Equal(4, f[0].Total);
        Assert.Equal(0.75, f[0].Probability, 3);
    }

    [Fact]
    public void Followers_of_an_unseen_event_is_empty()
    {
        Assert.Empty(new PredictionEngine().Followers("nothing"));
    }

    [Fact]
    public void Reset_sequence_stops_unrelated_events_being_linked()
    {
        var p = new PredictionEngine();
        p.Observe("a"); p.ResetSequence(); p.Observe("b");
        Assert.Empty(p.Followers("a"));
    }

    [Fact]
    public void Active_alert_produces_a_suggested_command_from_learned_pattern()
    {
        var k = new Kit();
        TrainPumpPattern(k, 4);

        k.Processor.Enqueue(k.Packet(Kit.M2, 10));                      

        var s = Assert.Single(k.Insights.Build(), i => i.Kind == InsightService.SuggestedAction);
        Assert.Equal(Kit.Pump, s.Mac);
        Assert.Equal("on", s.Action);
        Assert.Equal(1.0, s.Confidence, 3);
        Assert.Contains("4 of 4", s.Reason);
    }

    [Fact]
    public void Suggestion_is_hidden_when_the_actuator_is_already_in_the_suggested_state()
    {
        var k = new Kit();
        TrainPumpPattern(k, 4);
        k.Commands.Execute(new[] { Kit.Pump }, "on");
        k.Prediction.ResetSequence();

        k.Processor.Enqueue(k.Packet(Kit.M2, 10));

        Assert.DoesNotContain(k.Insights.Build(), i => i.Kind == InsightService.SuggestedAction && i.Mac == Kit.Pump);
    }

    [Fact]
    public void Weak_patterns_below_threshold_are_not_suggested()
    {
        var k = new Kit();
        k.Prediction.Observe(Tokens.Alert(Kit.M2)); k.Prediction.Observe(Tokens.Command(Kit.Pump, "on"));   
        k.Prediction.ResetSequence();

        k.Processor.Enqueue(k.Packet(Kit.M2, 10));

        Assert.DoesNotContain(k.Insights.Build(), i => i.Kind == InsightService.SuggestedAction);
    }

    [Fact]
    public void Search_history_can_be_suggested_as_a_follow_up()
    {
        var k = new Kit();
        for (var i = 0; i < 3; i++) { k.Prediction.Observe(Tokens.Alert(Kit.M2)); k.Prediction.Observe(Tokens.Search("pump a")); }
        k.Prediction.ResetSequence();

        k.Processor.Enqueue(k.Packet(Kit.M2, 10));

        var s = Assert.Single(k.Insights.Build(), i => i.Kind == InsightService.SuggestedAction);
        Assert.Equal("search:pump a", s.Action);
    }

    [Fact]
    public void Operator_behaviour_is_learned_live_from_executed_commands()
    {
        var k = new Kit();
        for (var i = 0; i < 3; i++)
        {
            k.Processor.Enqueue(k.Packet(Kit.M2, 10, i * 2));           
            k.Commands.Execute(new[] { Kit.Pump }, "on");              
            k.Processor.Enqueue(k.Packet(Kit.M2, 55, i * 2 + 1));     
            k.Processor.Drain();                                       
            k.Commands.Undo();                                          
        }
        
        k.Processor.Enqueue(k.Packet(Kit.M2, 10, 100));

        var s = Assert.Single(k.Insights.Build(), i => i.Kind == InsightService.SuggestedAction);
        Assert.Equal(Kit.Pump, s.Mac);
    }

    [Fact]
    public void Out_of_range_and_silent_devices_get_automated_insights()
    {
        var k = new Kit();
        k.Processor.Enqueue(k.Packet(Kit.M2, 10));
        k.Processor.Enqueue(k.Packet(Kit.M1, 50)); k.Processor.Drain();
        k.Time.Advance(TimeSpan.FromSeconds(30));
        k.Processor.SweepOffline(TimeSpan.FromSeconds(6));

        var insights = k.Insights.Build();
        Assert.Contains(insights, i => i.Kind == InsightService.AutomatedInsight && i.Mac == Kit.M1 && i.Title.Contains("stopped reporting"));
    }

    [Fact]
    public void Steady_climb_towards_the_limit_is_flagged_before_it_breaches()
    {
        var k = new Kit();
        foreach (var (v, i) in new[] { 70.0, 72, 74, 76, 78 }.Select((v, i) => (v, i)))
            k.Processor.Enqueue(k.Packet(Kit.M1, v, i));
        k.Processor.Drain();

        Assert.Contains(k.Insights.Build(), i => i.Mac == Kit.M1 && i.Title.Contains("trending"));
    }

    [Fact]
    public void Healthy_system_has_no_insights()
    {
        var k = new Kit();
        k.Processor.Enqueue(k.Packet(Kit.M1, 50)); k.Processor.Drain();
        Assert.Empty(k.Insights.Build());
    }
}

