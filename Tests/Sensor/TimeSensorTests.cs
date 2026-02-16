using UtilityAi.Facts;
using UtilityAi.Sensor.BuiltIn;
using UtilityAi.Utils;
using Xunit;

namespace Tests.Sensor;

public class TimeSensorTests
{
    [Fact]
    public async Task TimeSensor_PublishesTimeFacts()
    {
        var bus = new EventBus();
        var sensor = new TimeSensor();
        var rt = new Runtime(bus, new UserIntent(new IntentGoal("test")), 0);

        await sensor.SenseAsync(rt, CancellationToken.None);

        var currentTime = bus.GetOrDefault<CurrentTime>();
        var tickNumber = bus.GetOrDefault<TickNumber>();
        var elapsedTime = bus.GetOrDefault<ElapsedTime>();

        Assert.NotNull(currentTime);
        Assert.NotNull(tickNumber);
        Assert.NotNull(elapsedTime);
        Assert.Equal(0, tickNumber.Value);
    }

    [Fact]
    public async Task TimeSensor_TracksElapsedTime()
    {
        var bus = new EventBus();
        var sensor = new TimeSensor();

        // First tick
        var rt1 = new Runtime(bus, new UserIntent(new IntentGoal("test")), 0);
        await sensor.SenseAsync(rt1, CancellationToken.None);

        await Task.Delay(100);

        // Second tick
        var rt2 = new Runtime(bus, new UserIntent(new IntentGoal("test")), 1);
        await sensor.SenseAsync(rt2, CancellationToken.None);

        var elapsedTime = bus.GetOrDefault<ElapsedTime>();
        Assert.NotNull(elapsedTime);
        Assert.True(elapsedTime.Value.TotalMilliseconds >= 100);
    }
}
