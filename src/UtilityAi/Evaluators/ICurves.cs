namespace UtilityAi.Evaluators;

/// <summary>
/// Continuous mapping from an input domain (typically real-world units) to a normalized score, usually in [0,1].
/// Implementations MUST be side-effect free and thread-safe.
/// </summary>
public interface ICurve
{
    /// <summary>Inclusive input range where the curve is defined (used for clamping / editor UX).</summary>
    Range Domain { get; }

    /// <summary>Inclusive output range. For Utility AI this is usually [0,1].</summary>
    Range Output { get; }

    /// <summary>Evaluate the curve at x. Implementations should clamp to Domain and Output as needed.</summary>
    double Evaluate(double x);
}

/// <summary>Simple numeric range with helpers for (de)normalization.</summary>
public readonly record struct Range(double Min, double Max)
{
    public double Clamp(double v) => Math.Min(Max, Math.Max(Min, v));
    public double Size => Max - Min;

    public double Normalize(double v)
    {
        if (Math.Abs(Size) < 1e-12) throw new ArgumentException("Range has zero length.");
        return (v - Min) / Size;
    }

    public double Denormalize(double t) => Min + Size * t;
}

internal static class MathX
{
    public static double Clamp01(double v) => Math.Min(1.0, Math.Max(0.0, v));
    public static double Lerp(double a, double b, double t) => a + (b - a) * t;
    public static double InverseLerp(double a, double b, double v)
    {
        if (Math.Abs(b - a) < 1e-12) return 0.0;
        return (v - a) / (b - a);
    }
    public static int LowerBound(double[] xs, double x)
    {
        // first index i such that xs[i] >= x (binary search)
        int lo = 0, hi = xs.Length - 1;
        while (lo < hi)
        {
            int mid = (lo + hi) >> 1;
            if (xs[mid] < x) lo = mid + 1; else hi = mid;
        }
        return lo;
    }
}