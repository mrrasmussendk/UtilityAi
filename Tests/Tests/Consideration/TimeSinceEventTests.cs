using UtilityAi.Consideration.General;
using UtilityAi.Evaluators;

namespace Tests.Consideration;

public class TimeSinceEventTests
{
    [Fact]
    public void TimeSinceEvent_InvalidDomainOrder_ThrowsArgumentException()
    {
        var curve = new PowerCurve(new UtilityAi.Evaluators.Range(0, 1), gamma: 1.0);
        Assert.Throws<ArgumentException>(() =>
            new TimeSinceEvent<string>(curve, (10, 10)));
    }
}
