using System;
using System.Collections.Generic;
using UtilityAi.Consideration;
using UtilityAi.Evaluators;

namespace UtilityAi.Utils;

/// <summary>
/// Helper to aggregate multiple considerations using multiplicative composition, followed by
/// the standard Utility AI "makeup" adjustment to compensate for damping with many terms.
/// </summary>
public static class Scoring
{
    public static double AggregateWithMakeup(IReadOnlyList<IConsideration> cons, IBlackboard bb)
    {
        if (cons == null || cons.Count == 0) return 1.0;

        double product = 1.0;
        for (int i = 0; i < cons.Count; i++)
        {
            var s = cons[i].Consider(bb);
            if (s < 0.0) s = 0.0;
            else if (s > 1.0) s = 1.0;
            product *= s;
        }

        var adjusted = AdjustForConsiderations((float)product, cons.Count);
        return Math.Clamp(adjusted, 0.0f, 1.0f);
    }
    
    private static float AdjustForConsiderations(float score, int considerationCount)
    {
        if (considerationCount <= 1) return score.Clamp01();
        var s = score.Clamp01();
        var n = (float)considerationCount;
        var adjusted = 1f - (float)System.Math.Pow(1f - s, 1f / n);
        return adjusted.Clamp01();
    }
}
