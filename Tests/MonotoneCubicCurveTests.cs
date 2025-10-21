using System;
using System.Linq;
using UtilityAi.Evaluators;
using Xunit;

namespace Tests;

public class MonotoneCubicCurveTests
{
    [Fact]
    public void MonotoneCubic_PreservesEndpoints_AndMonotonicity_Increasing()
    {
        var curve = new MonotoneCubicCurve(new[]
        {
            (t: 0.0, v: 0.0),
            (t: 0.5, v: 0.5),
            (t: 1.0, v: 1.0)
        }, domain: new UtilityAi.Evaluators.Range(0,1), output: new UtilityAi.Evaluators.Range(0,1));

        // Endpoints
        Assert.InRange(curve.Evaluate(-1), 0 - 1e-12, 0 + 1e-12);
        Assert.InRange(curve.Evaluate(2), 1 - 1e-12, 1 + 1e-12);

        // Monotone increasing across domain
        var xs = Enumerable.Range(0, 21).Select(i => i / 20.0).ToArray();
        double prev = double.NegativeInfinity;
        foreach (var x in xs)
        {
            var y = curve.Evaluate(x);
            Assert.True(y >= prev - 1e-9);
            prev = y;
        }
    }

    [Fact]
    public void MonotoneCubic_PreservesMonotonicity_Decreasing()
    {
        var curve = new MonotoneCubicCurve(new[]
        {
            (t: 0.0, v: 1.0),
            (t: 1.0, v: 0.5),
            (t: 2.0, v: 0.0)
        }, output: new UtilityAi.Evaluators.Range(0,1));

        var xs = Enumerable.Range(0, 21).Select(i => i / 10.0).ToArray();
        double prev = double.PositiveInfinity;
        foreach (var x in xs)
        {
            var y = curve.Evaluate(x);
            Assert.True(y <= prev + 1e-9);
            prev = y;
        }
    }

    [Fact]
    public void MonotoneCubic_NoOvershoot_BetweenKeys()
    {
        // A set of increasing keys with varying slopes
        var keys = new[]
        {
            (t: 0.0, v: 0.0),
            (t: 1.0, v: 0.5),
            (t: 2.0, v: 0.75),
            (t: 3.0, v: 1.0)
        };
        var curve = new MonotoneCubicCurve(keys, output: new UtilityAi.Evaluators.Range(0,1));

        // For each segment, sample midpoints and ensure value stays within the min/max of segment endpoints
        for (int i = 0; i < keys.Length - 1; i++)
        {
            double xa = keys[i].t;
            double xb = keys[i+1].t;
            double ya = keys[i].v;
            double yb = keys[i+1].v;
            double ymin = Math.Min(ya, yb);
            double ymax = Math.Max(ya, yb);

            for (int s = 1; s < 10; s++)
            {
                double x = xa + (xb - xa) * s / 10.0;
                double y = curve.Evaluate(x);
                Assert.InRange(y, ymin - 1e-9, ymax + 1e-9);
            }
        }
    }
}
