using UtilityAi.Evaluators;
using Xunit;

namespace Tests;

public class CurveSamplingTests
{
    [Fact]
    public void Sample_MinimumSamples_ReturnsCorrectArrays()
    {
        var curve = new PowerCurve(new UtilityAi.Evaluators.Range(0, 10), gamma: 2.0);
        var (xs, ys) = CurveSampling.Sample(curve, 2);

        Assert.Equal(2, xs.Length);
        Assert.Equal(2, ys.Length);
        Assert.Equal(0, xs[0]);
        Assert.Equal(10, xs[1]);
    }

    [Fact]
    public void Sample_MultipleSamples_ReturnsCorrectArrays()
    {
        var curve = new PowerCurve(new UtilityAi.Evaluators.Range(0, 10), gamma: 2.0);
        var (xs, ys) = CurveSampling.Sample(curve, 5);

        Assert.Equal(5, xs.Length);
        Assert.Equal(5, ys.Length);
        
        // Check that x values are evenly spaced
        Assert.Equal(0, xs[0]);
        Assert.Equal(2.5, xs[1]);
        Assert.Equal(5, xs[2]);
        Assert.Equal(7.5, xs[3]);
        Assert.Equal(10, xs[4]);
        
        // Check that y values match curve evaluation
        for (int i = 0; i < 5; i++)
        {
            Assert.Equal(curve.Evaluate(xs[i]), ys[i], 1e-12);
        }
    }

    [Fact]
    public void Sample_LessThanTwoSamples_ThrowsException()
    {
        var curve = new PowerCurve(new UtilityAi.Evaluators.Range(0, 10), gamma: 2.0);
        Assert.Throws<ArgumentException>(() => CurveSampling.Sample(curve, 1));
        Assert.Throws<ArgumentException>(() => CurveSampling.Sample(curve, 0));
        Assert.Throws<ArgumentException>(() => CurveSampling.Sample(curve, -1));
    }

    [Fact]
    public void Sample_WithLogisticCurve_ProducesValidSamples()
    {
        var curve = new LogisticCurve(new UtilityAi.Evaluators.Range(0, 100));
        var (xs, ys) = CurveSampling.Sample(curve, 10);

        Assert.Equal(10, xs.Length);
        Assert.Equal(10, ys.Length);

        // All y values should be in [0, 1] range
        foreach (var y in ys)
        {
            Assert.InRange(y, 0, 1);
        }
    }
}
