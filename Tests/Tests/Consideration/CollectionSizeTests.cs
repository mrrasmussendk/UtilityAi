using UtilityAi.Consideration.General;
using UtilityAi.Evaluators;

namespace Tests.Consideration;

public class CollectionSizeTests
{
    private sealed record QueueFact(IReadOnlyList<int> Items);

    [Fact]
    public void CollectionSize_NegativeDomainMin_ThrowsArgumentOutOfRangeException()
    {
        var curve = new PowerCurve(new UtilityAi.Evaluators.Range(0, 1), gamma: 1.0);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CollectionSize<QueueFact>(f => f.Items.Count, curve, (-1, 10)));
    }

    [Fact]
    public void CollectionSize_InvalidDomainOrder_ThrowsArgumentException()
    {
        var curve = new PowerCurve(new UtilityAi.Evaluators.Range(0, 1), gamma: 1.0);
        Assert.Throws<ArgumentException>(() =>
            new CollectionSize<QueueFact>(f => f.Items.Count, curve, (5, 5)));
    }
}
