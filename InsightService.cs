using SmartX.Part2.Models;

namespace SmartX.Part2.Services;

public sealed class InsightService
{
    public const string SuggestedAction = "Suggested action";
    public const string AutomatedInsight = "Automated insight";

    private readonly DeviceRegistry _registry;
    private readonly AlertTracker _alerts;
    private readonly PredictionEngine _prediction;

    public InsightService(DeviceRegistry registry, AlertTracker alerts, PredictionEngine prediction)
    {
        _registry = registry;
        _alerts = alerts;
        _prediction = prediction;
    }

    public IReadOnlyList<Insight> Build()
    {
        var devices = _registry.Snapshot();
        var byMac = devices.ToDictionary(d => d.Mac, StringComparer.OrdinalIgnoreCase);
        var insights = new List<Insight>();
        var seen = new HashSet<string>();

        foreach (var mac in _alerts.Snapshot().Select(a => a.Mac).Distinct())
        {
            if (!byMac.TryGetValue(mac, out var source)) continue;

            foreach (var f in _prediction.Followers(Tokens.Alert(mac), top: 5, minCount: 2))
            {
                if (f.Probability < 0.25) continue;
                var reason = $"After alerts on {source.Name} you followed up with this {f.Count} of {f.Total} times ({f.Probability:P0}).";
                var parts = f.Token.Split('|', 3);

                if (parts[0] == "cmd" && parts.Length == 3 && byMac.TryGetValue(parts[1], out var target))
                {
                    var wantOn = parts[2] == "on";
                    if (target.ActuatorOn == wantOn) continue;              
                    if (seen.Add(f.Token))
                        insights.Add(new Insight(SuggestedAction, $"Turn {target.Name} {parts[2].ToUpperInvariant()}",
                            reason, f.Probability, target.Mac, parts[2]));
                }
                else if (parts[0] == "search" && parts.Length >= 2)
                {
                    var text = f.Token["search|".Length..];
                    if (seen.Add(f.Token))
                        insights.Add(new Insight(SuggestedAction, $"Look up \"{text}\"", reason, f.Probability, null, $"search:{text}"));
                }
            }
        }

        foreach (var d in devices)
        {
            if (_alerts.IsActive(d.Mac, AlertTracker.Disconnected))
                insights.Add(new Insight(AutomatedInsight, $"{d.Name} stopped reporting", d.AlertReason ?? "No recent telemetry.", 0.90, d.Mac, null));
            else if (d.Status == DeviceStatus.Alert)
                insights.Add(new Insight(AutomatedInsight, $"{d.Name} is out of range", d.AlertReason ?? "Reading outside limits.", 0.95, d.Mac, null));
            else if (TrendingToLimit(d, out var why))
                insights.Add(new Insight(AutomatedInsight, $"{d.Name} is trending towards its limit", why, 0.60, d.Mac, null));
        }

        return insights.OrderByDescending(i => i.Confidence).ThenBy(i => i.Title).Take(8).ToList();
    }

    private bool TrendingToLimit(Device d, out string reason)
    {
        reason = "";
        if (d.Category == DeviceCategory.Actuator) return false;

        var values = _registry.History(d.Mac, 5).Select(kv => kv.Value).ToArray();
        if (values.Length < 5) return false;

        var last = values[^1];
        var span = Math.Max(1, (d.WarnMax ?? last * 2) - (d.WarnMin ?? 0));
        var margin = span * 0.10;

        var rising = Enumerable.Range(1, 4).All(i => values[i] > values[i - 1]);
        var falling = Enumerable.Range(1, 4).All(i => values[i] < values[i - 1]);

        if (rising && d.WarnMax is { } max && last <= max && last >= max - margin)
        {
            reason = $"Last 5 readings are climbing and {last:0.##}{d.Unit} is close to the {max:0.##}{d.Unit} limit.";
            return true;
        }
        if (falling && d.WarnMin is { } min && last >= min && last <= min + margin)
        {
            reason = $"Last 5 readings are falling and {last:0.##}{d.Unit} is close to the {min:0.##}{d.Unit} limit.";
            return true;
        }
        return false;
    }
}
