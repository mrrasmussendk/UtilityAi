using System;
using UtilityAi.Evaluators;
using Xunit;

namespace Tests;

public class PowerCurveTests
{
    [Fact]
    public void PowerCurve_Increasing_BasicMapping()
    {
        var f = new PowerCurve(domain: new UtilityAi.Evaluators.Range(0, 10), output: new UtilityAi.Evaluators.Range(0, 1), gamma: 2.0, decreasing: false);
        Assert.InRange(f.Evaluate(0), 0 - 1e-12, 0 + 1e-12);
        Assert.InRange(f.Evaluate(10), 1 - 1e-12, 1 + 1e-12);
        var mid = f.Evaluate(5);
        Assert.InRange(mid, 0.25 - 1e-12, 0.25 + 1e-12); // (0.5)^2
    }

    [Fact]
    public void PowerCurve_Decreasing_MirrorsIncreasing()
    {
        var f = new PowerCurve(domain: new UtilityAi.Evaluators.Range(0, 10), output: new UtilityAi.Evaluators.Range(0, 1), gamma: 2.0, decreasing: true);
        Assert.InRange(f.Evaluate(0), 1 - 1e-12, 1 + 1e-12);
        Assert.InRange(f.Evaluate(10), 0 - 1e-12, 0 + 1e-12);
        var mid = f.Evaluate(5);
        Assert.InRange(mid, 0.75 - 1e-12, 0.75 + 1e-12); // 1 - (0.5)^2
    }

    [Fact]
    public void PowerCurve_ClampsToDomainAndOutput()
    {
        var f = new PowerCurve(domain: new UtilityAi.Evaluators.Range(0, 1), output: new UtilityAi.Evaluators.Range(0, 1), gamma: 0.5, decreasing: false);
        // Below domain -> clamp to 0
        Assert.InRange(f.Evaluate(-10), 0 - 1e-12, 0 + 1e-12);
        // Above domain -> clamp to 1
        Assert.InRange(f.Evaluate(10), 1 - 1e-12, 1 + 1e-12);
    }

    [Fact]
    public void PowerCurve_NegativeGamma_TreatedSameAsPositiveMagnitude()
    {
        var fPos = new PowerCurve(new UtilityAi.Evaluators.Range(0, 1), gamma: 3.0);
        var fNeg = new PowerCurve(new UtilityAi.Evaluators.Range(0, 1), gamma: -3.0);
        foreach (var x in new[] { 0.0, 0.1, 0.25, 0.5, 0.9, 1.0 })
        {
            Assert.InRange(fNeg.Evaluate(x), fPos.Evaluate(x) - 1e-12, fPos.Evaluate(x) + 1e-12);
        }
    }
}