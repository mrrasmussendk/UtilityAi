using UtilityAi.Evaluators;
using Xunit;

namespace Tests;

public class CurvesTests
{
    [Fact]
    public void Curves_Logistic_DefaultParameters()
    {
        var f = Curves.Logistic();
        // At x=0.5 (midpoint), should be close to 0.5
        Assert.InRange(f(0.5), 0.4, 0.6);
        // Below midpoint should be < 0.5
        Assert.True(f(0.0) < 0.5);
        // Above midpoint should be > 0.5
        Assert.True(f(1.0) > 0.5);
    }

    [Fact]
    public void Curves_Logistic_CustomParameters()
    {
        var f = Curves.Logistic(k: 5, m: 0.3);
        // At x=0.3 (midpoint), should be close to 0.5
        Assert.InRange(f(0.3), 0.4, 0.6);
        // Below midpoint should be < 0.5
        Assert.True(f(0.0) < 0.5);
        // Above midpoint should be > 0.5
        Assert.True(f(0.6) > 0.5);
    }

    [Fact]
    public void Curves_Identity_ReturnsInput()
    {
        var f = Curves.Identity();
        Assert.Equal(0.0, f(0.0));
        Assert.Equal(0.5, f(0.5));
        Assert.Equal(1.0, f(1.0));
        Assert.Equal(42.0, f(42.0));
    }

    [Fact]
    public void Curves_OneMinus_ReturnsComplement()
    {
        var f = Curves.OneMinus();
        Assert.Equal(1.0, f(0.0));
        Assert.Equal(0.5, f(0.5));
        Assert.Equal(0.0, f(1.0));
        Assert.Equal(-41.0, f(42.0));
    }
}
