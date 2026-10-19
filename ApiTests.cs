using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SmartX.Part2.Services;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace SmartX.Part2.Tests;

public class ApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    public ApiTests(WebApplicationFactory<Program> factory) => _factory = factory;

    private static async Task<JsonElement> Json(HttpResponseMessage r) => await r.Content.ReadFromJsonAsync<JsonElement>();

    [Fact]
    public async Task Landing_page_is_served()
    {
        var c = _factory.CreateClient();
        var html = await c.GetStringAsync("/");
        Assert.Contains("Real-Time Command Stream", html);
        Assert.Contains("Network Topology", html);
    }

    [Fact]
    public async Task Telemetry_for_a_registered_device_is_accepted()
    {
        var c = _factory.CreateClient();
        var r = await c.PostAsJsonAsync("/api/telemetry", new { mac = SeedData.MoistureA, value = 55.5 });
        Assert.Equal(HttpStatusCode.Accepted, r.StatusCode);
        Assert.Equal("standard", (await Json(r)).GetProperty("queued").GetString());
    }

    [Fact]
    public async Task Boolean_state_payloads_are_accepted_for_actuators()
    {
        var c = _factory.CreateClient();
        var r = await c.PostAsJsonAsync("/api/telemetry", new { mac = SeedData.PumpA, state = true });
        Assert.Equal(HttpStatusCode.Accepted, r.StatusCode);
    }

    [Fact]
    public async Task Critical_telemetry_goes_to_the_priority_path_and_raises_an_alert_immediately()
    {
        var c = _factory.CreateClient();
        var r = await c.PostAsJsonAsync("/api/telemetry", new { mac = SeedData.Meter1, value = 4100 });
        Assert.Equal(HttpStatusCode.Accepted, r.StatusCode);
        Assert.Equal("priority", (await Json(r)).GetProperty("queued").GetString());

        var alerting = await c.GetFromJsonAsync<JsonElement>("/api/devices?status=Alert");
        Assert.Contains(alerting.EnumerateArray(), d => d.GetProperty("mac").GetString() == SeedData.Meter1);
    }

    [Fact]
    public async Task Bad_telemetry_is_rejected_with_the_right_status_codes()
    {
        var c = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await c.PostAsJsonAsync("/api/telemetry", new { mac = "AA:AA:AA:AA:AA:AA", value = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync("/api/telemetry", new { mac = SeedData.MoistureA })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync("/api/telemetry", new { value = 1 })).StatusCode);
    }

    [Fact]
    public async Task Batch_endpoint_reports_how_each_packet_was_routed()
    {
        var c = _factory.CreateClient();
        var batch = new object[]
        {
            new { mac = SeedData.MoistureA, value = 50 },
            new { mac = SeedData.MoistureA, value = 51 },
            new { mac = SeedData.MoistureB, value = 5 },
            new { mac = "AA:AA:AA:AA:AA:AA", value = 1 },
        };
        var r = await c.PostAsJsonAsync("/api/telemetry/batch", batch);
        var body = await Json(r);
        Assert.Equal(2, body.GetProperty("standard").GetInt32());
        Assert.Equal(1, body.GetProperty("critical").GetInt32());
        Assert.Equal(1, body.GetProperty("rejected").GetInt32());
    }

    [Fact]
    public async Task Category_filter_returns_only_that_category()
    {
        var c = _factory.CreateClient();
        var list = await c.GetFromJsonAsync<JsonElement>("/api/devices?category=Actuator");
        Assert.Equal(4, list.GetArrayLength());
        Assert.All(list.EnumerateArray(), d => Assert.Equal("Actuator", d.GetProperty("category").GetString()));
    }

    [Fact]
    public async Task Text_search_finds_devices_by_name()
    {
        var c = _factory.CreateClient();
        var list = await c.GetFromJsonAsync<JsonElement>("/api/devices?q=pump%20a");
        Assert.Equal("Pump A", Assert.Single(list.EnumerateArray()).GetProperty("name").GetString());
    }

    [Fact]
    public async Task Override_command_then_undo_round_trip()
    {
        var c = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await c.PostAsJsonAsync("/api/commands", new { macs = new[] { SeedData.PumpA }, action = "on" })).StatusCode);

        var devices = await c.GetFromJsonAsync<JsonElement>("/api/devices?q=pump%20a");
        Assert.True(devices[0].GetProperty("actuatorOn").GetBoolean());

        Assert.Equal(HttpStatusCode.OK, (await c.PostAsync("/api/commands/undo", null)).StatusCode);
        devices = await c.GetFromJsonAsync<JsonElement>("/api/devices?q=pump%20a");
        Assert.False(devices[0].GetProperty("actuatorOn").GetBoolean());
    }

    [Fact]
    public async Task Commanding_a_sensor_is_a_bad_request()
    {
        var c = _factory.CreateClient();
        var r = await c.PostAsJsonAsync("/api/commands", new { macs = new[] { SeedData.MoistureA }, action = "on" });
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }

    [Fact]
    public async Task Registering_a_device_validates_and_prevents_duplicates()
    {
        var c = _factory.CreateClient();
        var body = new { mac = "11:22:33:44:55:66", name = "Test Node", location = "Lab", category = "Environmental" };
        Assert.Equal(HttpStatusCode.Created, (await c.PostAsJsonAsync("/api/devices", body)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsJsonAsync("/api/devices", body)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync("/api/devices", new { mac = "nope", name = "x", location = "y", category = "Environmental" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync("/api/devices", new { mac = "11:22:33:44:55:77", name = "x", location = "y", category = "Banana" })).StatusCode);
    }

    [Fact]
    public async Task History_endpoint_returns_readings_in_timestamp_order()
    {
        var c = _factory.CreateClient();
        var now = DateTime.UtcNow;
        await c.PostAsJsonAsync("/api/telemetry", new { mac = SeedData.MoistureA, value = 61, timestampUtc = now.AddSeconds(-1) });
        await c.PostAsJsonAsync("/api/telemetry", new { mac = SeedData.MoistureA, value = 60, timestampUtc = now.AddSeconds(-5) });
        _factory.Services.GetRequiredService<TelemetryProcessor>().Drain();

        var hist = await c.GetFromJsonAsync<JsonElement>($"/api/devices/{SeedData.MoistureA}/history?take=50");
        var times = hist.EnumerateArray().Select(p => p.GetProperty("t").GetDateTime()).ToList();
        Assert.Equal(times.OrderBy(t => t), times);
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync("/api/devices/AA:AA:AA:AA:AA:AA/history")).StatusCode);
    }

    [Fact]
    public async Task Seeded_pattern_produces_a_suggested_action_when_moisture_b_alarms()
    {
        var c = _factory.CreateClient();
        await c.PostAsJsonAsync("/api/commands", new { macs = new[] { SeedData.PumpA }, action = "off" });   
        await c.PostAsJsonAsync("/api/telemetry", new { mac = SeedData.MoistureB, value = 8 });

        var insights = await c.GetFromJsonAsync<JsonElement>("/api/insights");
        Assert.Contains(insights.EnumerateArray(), i =>
            i.GetProperty("kind").GetString() == "Suggested action" &&
            i.GetProperty("mac").GetString() == SeedData.PumpA &&
            i.GetProperty("action").GetString() == "on");
    }

    [Fact]
    public async Task Search_log_validates_input()
    {
        var c = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.NoContent, (await c.PostAsJsonAsync("/api/search-log", new { text = "pump a" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync("/api/search-log", new { text = "x" })).StatusCode);
    }

    [Fact]
    public async Task Stats_endpoint_exposes_queue_and_alert_counters()
    {
        var c = _factory.CreateClient();
        var s = await c.GetFromJsonAsync<JsonElement>("/api/stats");
        foreach (var key in new[] { "standardQueueDepth", "priorityQueueDepth", "duplicateAlertsSuppressed", "undoDepth", "devices" })
            Assert.True(s.TryGetProperty(key, out _), key);
    }
}
