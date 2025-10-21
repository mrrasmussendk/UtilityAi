using System;
using UtilityAi.Evaluators;
using Xunit;

namespace Tests;

public class LogisticCurveTests
{
    [Fact]
    public void LogisticCurve_Increasing_Decreasing_ByKSign()
    {
        var inc = new LogisticCurve(domain: new UtilityAi.Evaluators.Range(0, 1), output: new UtilityAi.Evaluators.Range(0, 1), k: 10.0, x0: 0.5);
        var dec = new LogisticCurve(domain: new UtilityAi.Evaluators.Range(0, 1), output: new UtilityAi.Evaluators.Range(0, 1), k: -10.0, x0: 0.5);

        // Increasing: value at 0 < value at 0.5 < value at 1
        var y0 = inc.Evaluate(0);
        var yMid = inc.Evaluate(0.5);
        var y1 = inc.Evaluate(1);
        Assert.True(y0 < yMid && yMid < y1);

        // Decreasing: reverse ordering
        var y0d = dec.Evaluate(0);
        var yMidD = dec.Evaluate(0.5);
        var y1d = dec.Evaluate(1);
        Assert.True(y0d > yMidD && yMidD > y1d);

        // Clamp to output and domain
        Assert.InRange(inc.Evaluate(-10), 0, 1);
        Assert.InRange(inc.Evaluate(10), 0, 1);
    }

    [Fact]
    public void LogisticCurve_FitFromAnchors_MatchesAnchors()
    {
        var domain = new UtilityAi.Evaluators.Range(0, 1);
        var output = new UtilityAi.Evaluators.Range(0, 1);
        var a = (x: 0.25, y: 0.2);
        var b = (x: 0.75, y: 0.8);

        var curve = LogisticCurve.FitFromAnchors(a, b, domain, output);

        Assert.InRange(curve.Evaluate(a.x), a.y - 1e-9, a.y + 1e-9);
        Assert.InRange(curve.Evaluate(b.x), b.y - 1e-9, b.y + 1e-9);

        // Also ensure curve maps into [0,1]
        foreach (var x in new[] {0.0, 0.1, 0.25, 0.5, 0.75, 0.9, 1.0})
        {
            var y = curve.Evaluate(x);
            Assert.True(y >= 0 - 1e-12 && y <= 1 + 1e-12);
        }
    }

    [Fact]
    public void LogisticCurve_FitFromAnchors_ThrowsForInvalidAnchors()
    {
        var domain = new UtilityAi.Evaluators.Range(0, 1);
        var output = new UtilityAi.Evaluators.Range(0, 1);
        // y equal to bounds is invalid per implementation
        Assert.Throws<ArgumentException>(() => LogisticCurve.FitFromAnchors((0.2, 0.0), (0.8, 0.5), domain, output));
        Assert.Throws<ArgumentException>(() => LogisticCurve.FitFromAnchors((0.2, 1.0), (0.8, 0.5), domain, output));
        // same x is invalid
        Assert.Throws<ArgumentException>(() => LogisticCurve.FitFromAnchors((0.5, 0.2), (0.5, 0.8), domain, output));
    }
}