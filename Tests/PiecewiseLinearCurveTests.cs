using System;
using System.Linq;
using UtilityAi.Evaluators;
using Xunit;

namespace Tests;

public class PiecewiseLinearCurveTests
{
    [Fact]
    public void Piecewise_InterpolatesLinearly_AndClampsEndpoints()
    {
        var curve = new PiecewiseLinearCurve(new[]
        {
            (t: 0.0, v: 0.0),
            (t: 1.0, v: 1.0)
        }, domain: new UtilityAi.Evaluators.Range(0,1), output: new UtilityAi.Evaluators.Range(0,1));

        Assert.InRange(curve.Evaluate(0.5), 0.5 - 1e-12, 0.5 + 1e-12);
        Assert.InRange(curve.Evaluate(-1), 0 - 1e-12, 0 + 1e-12);
        Assert.InRange(curve.Evaluate(2), 1 - 1e-12, 1 + 1e-12);
    }

    [Fact]
    public void Piecewise_Throws_OnDuplicateKeyTimes()
    {
        Assert.Throws<ArgumentException>(() => new PiecewiseLinearCurve(new[]
        {
            (t: 0.0, v: 0.0),
            (t: 0.0, v: 1.0)
        }));
    }

    [Fact]
    public void Piecewise_ClampsKeyValuesToOutput()
    {
        var curve = new PiecewiseLinearCurve(new[]
        {
            (t: 0.0, v: 2.0), // will clamp to 1
            (t: 1.0, v: -1.0) // will clamp to 0
        }, output: new UtilityAi.Evaluators.Range(0,1));

        // After clamping, keys become (0,1) and (1,0); linear interpolation gives 0.5 at mid
        Assert.InRange(curve.Evaluate(0.5), 0.5 - 1e-12, 0.5 + 1e-12);
    }

    [Fact]
    public void Piecewise_HandlesBoundaryEdgeCases()
    {
        // Test for Bug #3: Ensure boundary values don't cause index out of bounds
        var curve = new PiecewiseLinearCurve(new[]
        {
            (t: 0.0, v: 0.0),
            (t: 0.5, v: 0.5),
            (t: 1.0, v: 1.0)
        }, domain: new UtilityAi.Evaluators.Range(0, 1), output: new UtilityAi.Evaluators.Range(0, 1));

        // Test exact boundary values
        Assert.InRange(curve.Evaluate(0.0), 0.0 - 1e-12, 0.0 + 1e-12);
        Assert.InRange(curve.Evaluate(1.0), 1.0 - 1e-12, 1.0 + 1e-12);
        
        // Test values just inside boundaries
        Assert.InRange(curve.Evaluate(0.0001), 0.0, 0.1);
        Assert.InRange(curve.Evaluate(0.9999), 0.9, 1.0);
    }
}
