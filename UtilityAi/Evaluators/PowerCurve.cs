namespace UtilityAi.Evaluators;

/// <summary>
/// Power-law shaping in Output range. For gamma &gt; 1 the curve is concave (slow start, strong end);
/// for 0 &lt; gamma &lt; 1 the curve is convex (fast start, gentle end). Use negative gamma to invert via 1 - t^|gamma|.
/// </summary>
public sealed class PowerCurve : ICurve
{
    public Range Domain { get; }
    public Range Output { get; }
    public double Gamma { get; }  // shape parameter

    public bool Decreasing { get; } // true => decreasing mapping

    /// <param name="gamma">|gamma| &gt; 0; values &gt;1 concave, (0,1) convex.</param>
    public PowerCurve(Range domain, Range? output = null, double gamma = 2.0, bool decreasing = false)
    {
        if (Math.Abs(domain.Size) < 1e-12) throw new ArgumentException("Domain length must be > 0.", nameof(domain));
        if (Math.Abs(gamma) < 1e-12) throw new ArgumentException("Gamma must be non-zero.", nameof(gamma));

        Domain = domain;
        Output = output ?? new Range(0, 1);
        Gamma = gamma;
        Decreasing = decreasing;
    }

    public double Evaluate(double x)
    {
        double t = MathX.Clamp01(Domain.Normalize(Domain.Clamp(x)));
        double y01 = Decreasing
            ? 1.0 - Math.Pow(t, Math.Abs(Gamma))
            : Math.Pow(t, Math.Abs(Gamma));
        return Output.Denormalize(MathX.Clamp01(y01));
    }
}
/// <summary>
/// Convenience helpers for sampling/exporting curves (useful in tests, plots, and docs).
/// </summary>
public static class CurveSampling
{
    public static (double[] xs, double[] ys) Sample(ICurve f, int samples)
    {
        if (samples < 2) throw new ArgumentException("Need at least 2 samples.", nameof(samples));
        var xs = new double[samples];
        var ys = new double[samples];
        for (int i = 0; i < samples; i++)
        {
            double x = f.Domain.Min + f.Domain.Size * i / (samples - 1);
            xs[i] = x;
            ys[i] = f.Evaluate(x);
        }
        return (xs, ys);
    }
}