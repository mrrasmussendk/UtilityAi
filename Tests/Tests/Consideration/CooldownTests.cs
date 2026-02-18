using UtilityAi.Consideration.General;
using UtilityAi.Utils;
using Xunit;

namespace Tests.Consideration;

public class CooldownTests
{
    private record TestEvent(string Data);

    [Fact]
    public void Cooldown_NoEvent_ReturnsOne()
    {
        var bus = new EventBus();
        var rt = new Runtime(bus, new UserIntent(new IntentGoal("test")), 0);

        var consideration = new Cooldown<TestEvent>(TimeSpan.FromSeconds(10));
        var result = consideration.Evaluate(rt);

        Assert.Equal(1.0, result);
    }

    [Fact]
    public async Task Cooldown_RecentEvent_ReturnsZero()
    {
        var bus = new EventBus();
        bus.Publish(new TestEvent("data"));
        await Task.Delay(100); // Small delay, well within cooldown

        var rt = new Runtime(bus, new UserIntent(new IntentGoal("test")), 0);

        var consideration = new Cooldown<TestEvent>(TimeSpan.FromSeconds(10));
        var result = consideration.Evaluate(rt);

        Assert.Equal(0.0, result);
    }

    [Fact]
    public async Task Cooldown_ExpiredCooldown_ReturnsOne()
    {
        var bus = new EventBus();
        bus.Publish(new TestEvent("data"));
        await Task.Delay(1100); // Wait for cooldown to expire

        var rt = new Runtime(bus, new UserIntent(new IntentGoal("test")), 0);

        var consideration = new Cooldown<TestEvent>(TimeSpan.FromSeconds(1));
        var result = consideration.Evaluate(rt);

        Assert.Equal(1.0, result);
    }
}
