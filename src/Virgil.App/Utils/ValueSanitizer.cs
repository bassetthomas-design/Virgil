using System;

namespace Virgil.App.Utils;

public static class ValueSanitizer
{
    public static double Sanitize01(double value, double fallback)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return Math.Clamp(fallback, 0d, 1d);
        }

        return Math.Clamp(value, 0d, 1d);
    }
}
