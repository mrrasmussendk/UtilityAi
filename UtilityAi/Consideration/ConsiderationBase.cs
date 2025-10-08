using System;
using UtilityAi.Utils;

namespace UtilityAi.Consideration;

/// <summary>
/// Base class for considerations that map blackboard inputs to a normalized utility in [0,1]
/// using a parameterized response function (no graph assets needed).
/// </summary>
public abstract class ConsiderationBase : IConsideration
{
    /// <summary>Invert the shaped response (s -> 1 - s).</summary>
    public bool Invert { get; init; }

    /// <summary>Optional weight multiplier applied after shaping. Clamped to [0,10].</summary>
    public double Weight { get; init; } = 1.0;

    public double Consider(IBlackboard bb)
    {
        var v = Math.Clamp(ComputeRaw(bb), 0.0, 1.0);       // expected input range
        var s = Math.Clamp(Shape(v), 0.0, 1.0);             // shape/curve within [0,1]
        if (Invert) s = 1.0 - s;                            // optional inversion
        s *= Math.Clamp(Weight, 0.0, 10.0);                 // weighting, modest bounds
        return Math.Clamp(s, 0.0, 1.0);                     // ensure within [0,1]
    }

    /// <summary>
    /// Extract and optionally normalize the raw input from the blackboard. Prefer returning a value in [0,1].
    /// </summary>
    protected abstract double ComputeRaw(IBlackboard bb);

    /// <summary>
    /// Response curve over v in [0,1]. Defaults to identity.
    /// </summary>
    protected virtual double Shape(double v) => v;
}
