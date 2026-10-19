using SmartX.Part2.Models;

namespace SmartX.Part2.Services;

public static class AlertRules
{
    public static (Severity Severity, string? Reason, double Deviation) Classify(Device d, double v)
    {
        if (d.Category == DeviceCategory.Actuator) return (Severity.Normal, null, 0);

        if (d.CritMin is { } cmin && v < cmin)
            return (Severity.Critical, $"{v:0.##}{d.Unit} is below the critical minimum of {cmin:0.##}{d.Unit}", Dev(cmin, v));
        if (d.CritMax is { } cmax && v > cmax)
            return (Severity.Critical, $"{v:0.##}{d.Unit} is above the critical maximum of {cmax:0.##}{d.Unit}", Dev(cmax, v));
        if (d.WarnMin is { } wmin && v < wmin)
            return (Severity.Warning, $"{v:0.##}{d.Unit} is below the safe minimum of {wmin:0.##}{d.Unit}", Dev(wmin, v));
        if (d.WarnMax is { } wmax && v > wmax)
            return (Severity.Warning, $"{v:0.##}{d.Unit} is above the safe maximum of {wmax:0.##}{d.Unit}", Dev(wmax, v));

        return (Severity.Normal, null, 0);
    }

    private static double Dev(double limit, double v) => Math.Abs(v - limit) / Math.Max(1, Math.Abs(limit));
}
