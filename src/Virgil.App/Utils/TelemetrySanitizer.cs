using System;
using System.Globalization;

namespace Virgil.App.Utils;

public static class TelemetrySanitizer
{
    public static bool IsValid(double value)
        => !(double.IsNaN(value) || double.IsInfinity(value));

    public static double OrFallback(double value, double fallback)
        => IsValid(value) ? value : fallback;

    public static double Clamp01(double value, double fallbackIfInvalid)
    {
        var safeValue = IsValid(value) ? value : fallbackIfInvalid;
        return Math.Clamp(safeValue, 0d, 1d);
    }

    public static string FormatOptional(double value, string unit)
    {
        if (!IsValid(value))
        {
            return "N/A";
        }

        var suffix = unit ?? string.Empty;
        return string.Format(CultureInfo.CurrentCulture, "{0:F0}{1}", value, suffix);
    }
}
