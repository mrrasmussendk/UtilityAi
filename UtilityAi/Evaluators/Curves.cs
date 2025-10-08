using System;

namespace UtilityAi.Utils;

/// <summary>
/// Common response curve helpers operating on values in [0,1].
/// </summary>
public static class Curves
{
    private static double Clamp01(double v) => v < 0.0 ? 0.0 : (v > 1.0 ? 1.0 : v);

    /// <summary>
    /// Cubic smoothstep over [0,1].
    /// </summary>
    public static double Smoothstep(double v)
    {
        v = Clamp01(v);
        return v * v * (3.0 - 2.0 * v);
    }

    /// <summary>
    /// Quintic smootherstep over [0,1].
    /// </summary>
    public static double Smootherstep(double v)
    {
        v = Clamp01(v);
        return v * v * v * (v * (v * 6.0 - 15.0) + 10.0);
    }

    /// <summary>
    /// Emphasize high values: y = v^p.
    /// </summary>
    public static double PowUp(double v, double p)
    {
        v = Clamp01(v);
        return Math.Pow(v, p);
    }

    /// <summary>
    /// Emphasize low values: y = 1 - (1 - v)^p.
    /// </summary>
    public static double PowDown(double v, double p)
    {
        v = Clamp01(v);
        return 1.0 - Math.Pow(1.0 - v, p);
    }

    /// <summary>
    /// Logistic S-curve centered at 0.5 mapped approximately to [0,1].
    /// k controls steepness (typical 4..12).
    /// </summary>
    public static double Logistic01(double v, double k = 8.0)
    {
        v = Clamp01(v);
        // raw logistic centered at 0.5
        var y = 1.0 / (1.0 + Math.Exp(-k * (v - 0.5)));
        // precompute edges at 0 and 1 for normalization
        var y0 = 1.0 / (1.0 + Math.Exp(k * 0.5));
        var y1 = 1.0 / (1.0 + Math.Exp(-k * 0.5));
        return (y - y0) / (y1 - y0);
    }
}
