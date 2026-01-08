using System;

namespace Virgil.App.Utils;

public static class ValueSanitizer
{
    public static double Sanitize01(double value, double fallback)
        => TelemetrySanitizer.Clamp01(value, fallback);
}
