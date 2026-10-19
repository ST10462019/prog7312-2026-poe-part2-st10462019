using SmartX.Part2.Models;
using SmartX.Part2.Services;

namespace SmartX.Part2;

public sealed record TelemetryRequest(string? Mac, double? Value, bool? State, DateTime? TimestampUtc);
public sealed record RegisterRequest(string? Mac, string? Name, string? Location, string? Category,
    string? Unit, double? WarnMin, double? WarnMax, double? CritMin, double? CritMax);
public sealed record CommandRequest(List<string>? Macs, string? Action);
public sealed record SearchLogRequest(string? Text);

public static class ApiEndpoints
{
    public static void Map(WebApplication app)
    {
        var api = app.MapGroup("/api");

        api.MapPost("/telemetry", (TelemetryRequest r, TelemetryProcessor p, TimeProvider time) =>
        {
            if (!TryConvert(r, time, out var packet, out var error))
                return Results.BadRequest(new { error });

            return p.Enqueue(packet) switch
            {
                EnqueueResult.UnknownDevice => Results.NotFound(new { error = "Device is not registered." }),
                EnqueueResult.Critical => Results.Json(new { queued = "priority" }, statusCode: 202),
                _ => Results.Json(new { queued = "standard" }, statusCode: 202)
            };
        });

        api.MapPost("/telemetry/batch", (List<TelemetryRequest> batch, TelemetryProcessor p, TimeProvider time) =>
        {
            int standard = 0, critical = 0, rejected = 0;
            foreach (var r in batch)
            {
                if (!TryConvert(r, time, out var packet, out _)) { rejected++; continue; }
                switch (p.Enqueue(packet))
                {
                    case EnqueueResult.Standard: standard++; break;
                    case EnqueueResult.Critical: critical++; break;
                    default: rejected++; break;
                }
            }
            return Results.Json(new { standard, critical, rejected }, statusCode: 202);
        });

        api.MapGet("/devices", (string? category, string? status, string? q, DeviceRegistry reg) =>
        {
            DeviceCategory? cat = Enum.TryParse<DeviceCategory>(category, true, out var c) ? c : null;
            DeviceStatus? st = Enum.TryParse<DeviceStatus>(status, true, out var s) ? s : null;
            return Results.Ok(reg.Query(cat, st, q));
        });

        api.MapPost("/devices", (RegisterRequest r, DeviceRegistry reg) =>
        {
            var mac = MacAddress.Normalize(r.Mac);
            if (mac is null) return Results.BadRequest(new { error = "MAC must look like AA:BB:CC:DD:EE:FF." });
            if (string.IsNullOrWhiteSpace(r.Name) || string.IsNullOrWhiteSpace(r.Location))
                return Results.BadRequest(new { error = "Name and location are required." });
            if (!Enum.TryParse<DeviceCategory>(r.Category, true, out var cat))
                return Results.BadRequest(new { error = "Category must be Environmental, PowerConsumption or Actuator." });

            var d = SeedData.DefaultsFor(cat);
            var device = new Device
            {
                Mac = mac, Name = r.Name.Trim(), Location = r.Location.Trim(), Category = cat,
                Unit = r.Unit ?? d.Unit,
                WarnMin = r.WarnMin ?? d.WarnMin, WarnMax = r.WarnMax ?? d.WarnMax,
                CritMin = r.CritMin ?? d.CritMin, CritMax = r.CritMax ?? d.CritMax
            };
            return reg.TryRegister(device)
                ? Results.Created($"/api/devices/{mac}", device)
                : Results.Conflict(new { error = "A device with that MAC is already registered." });
        });

        api.MapGet("/devices/{mac}/history", (string mac, int? take, DeviceRegistry reg) =>
        {
            var key = MacAddress.Normalize(mac);
            if (key is null || reg.Get(key) is null) return Results.NotFound(new { error = "Unknown device." });
            var n = Math.Clamp(take ?? 120, 1, DeviceRegistry.MaxHistoryPerDevice);
            return Results.Ok(reg.History(key, n).Select(kv => new { t = kv.Key, v = kv.Value }));
        });

        api.MapPost("/commands", (CommandRequest r, CommandService cmd) =>
        {
            var res = cmd.Execute(r.Macs ?? new List<string>(), r.Action);
            return res.Ok ? Results.Ok(new { message = res.Message }) : Results.BadRequest(new { error = res.Message });
        });

        api.MapPost("/commands/undo", (CommandService cmd) =>
        {
            var res = cmd.Undo();
            return res.Ok ? Results.Ok(new { message = res.Message }) : Results.BadRequest(new { error = res.Message });
        });

        api.MapGet("/commands", (CommandService cmd) =>
            Results.Ok(new { undoDepth = cmd.UndoDepth, recent = cmd.Recent(10) }));

        api.MapGet("/stats", (TelemetryProcessor p, DeviceRegistry reg, AlertTracker alerts, CommandService cmd) =>
        {
            var s = p.Stats();
            var all = reg.Snapshot();
            return Results.Ok(new
            {
                standardQueueDepth = s.StandardDepth,
                priorityQueueDepth = s.PriorityDepth,
                processedStandard = s.ProcessedStandard,
                processedCritical = s.ProcessedCritical,
                rejected = s.Rejected,
                duplicateAlertsSuppressed = alerts.DuplicatesSuppressed,
                activeAlerts = alerts.Count,
                undoDepth = cmd.UndoDepth,
                devices = all.Count,
                online = all.Count(d => d.Status == DeviceStatus.Online),
                alerting = all.Count(d => d.Status == DeviceStatus.Alert),
                offline = all.Count(d => d.Status == DeviceStatus.Offline)
            });
        });

        api.MapGet("/alerts", (AlertTracker alerts) => Results.Ok(alerts.Snapshot()));

        api.MapGet("/insights", (InsightService insights) => Results.Ok(insights.Build()));

        api.MapPost("/search-log", (SearchLogRequest r, PredictionEngine prediction) =>
        {
            var text = r.Text?.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(text) || text.Length is < 2 or > 60)
                return Results.BadRequest(new { error = "Search text must be 2-60 characters." });
            prediction.Observe(Tokens.Search(text));
            return Results.NoContent();
        });
    }

    private static bool TryConvert(TelemetryRequest r, TimeProvider time, out TelemetryPacket packet, out string error)
    {
        packet = default;
        error = "";
        if (string.IsNullOrWhiteSpace(r.Mac)) { error = "mac is required."; return false; }

        double value;
        if (r.Value is { } v) value = v;
        else if (r.State is { } b) value = b ? 1 : 0;
        else { error = "Provide either value or state."; return false; }

        if (!double.IsFinite(value)) { error = "value must be a finite number."; return false; }

        packet = new TelemetryPacket(r.Mac, value, r.TimestampUtc?.ToUniversalTime() ?? time.GetUtcNow().UtcDateTime);
        return true;
    }
}
