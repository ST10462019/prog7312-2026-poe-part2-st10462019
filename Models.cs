using System.Text.RegularExpressions;

namespace SmartX.Part2.Models;

public enum DeviceCategory { Environmental, PowerConsumption, Actuator }
public enum DeviceStatus { Online, Alert, Offline }
public enum Severity { Normal, Warning, Critical }
public enum EnqueueResult { Standard, Critical, UnknownDevice }

public sealed class Device
{
    public string Mac { get; init; } = "";
    public string Name { get; set; } = "";
    public string Location { get; set; } = "";
    public DeviceCategory Category { get; set; }
    public string Unit { get; set; } = "";

    public double? WarnMin { get; set; }
    public double? WarnMax { get; set; }
    public double? CritMin { get; set; }
    public double? CritMax { get; set; }

    public double LastValue { get; set; }
    public bool ActuatorOn { get; set; }
    public DateTime LastSeenUtc { get; set; }
    public DeviceStatus Status { get; set; } = DeviceStatus.Offline;
    public string? AlertReason { get; set; }

    public Device Clone() => (Device)MemberwiseClone();
}

public readonly record struct TelemetryPacket(string Mac, double Value, DateTime TimestampUtc);

public sealed record CommandRecord(
    Guid Id,
    DateTime TimestampUtc,
    string Action,
    IReadOnlyList<string> Targets,
    IReadOnlyDictionary<string, bool> Previous,
    IReadOnlyDictionary<string, bool> Applied);

public sealed record CommandView(Guid Id, DateTime TimestampUtc, string Action, IReadOnlyList<string> Targets, bool Undone);

public sealed record CommandResult(bool Ok, string Message, CommandRecord? Record);

public sealed record Insight(string Kind, string Title, string Reason, double Confidence, string? Mac, string? Action);

public sealed record ProcessorStats(int StandardDepth, int PriorityDepth, long ProcessedStandard, long ProcessedCritical, long Rejected);

public static class MacAddress
{
    private static readonly Regex Rx = new(@"^[0-9A-F]{2}([:-][0-9A-F]{2}){5}$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static string? Normalize(string? mac)
    {
        if (string.IsNullOrWhiteSpace(mac)) return null;
        mac = mac.Trim();
        return Rx.IsMatch(mac) ? mac.Replace('-', ':').ToUpperInvariant() : null;
    }
}

public static class Tokens
{
    public static string Alert(string mac) => $"alert|{mac}";
    public static string Command(string mac, string action) => $"cmd|{mac}|{action}";
    public static string Search(string text) => $"search|{text}";
}
