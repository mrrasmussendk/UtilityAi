using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Example.Sensor;
using UtilityAi.Utils;
using Xunit;

namespace Tests;

public class IntentSensorTests
{
    [Fact]
    public async Task IntentSensor_Publishes_SignalOutputMode_FromDeliverySlot()
    {
        var bus = new EventBus();
        var intent = new UserIntent(
            Goal: new IntentGoal("test-goal"),
            Slots: new Dictionary<string, object?> { ["delivery"] = "sms" }
        );
        var rt = new Runtime(bus, intent, 0);

        var sensor = new IntentSensor();
        await sensor.SenseAsync(rt, CancellationToken.None);

        var sig = bus.GetOrDefault<SignalOutputMode>();
        Assert.NotNull(sig);
        Assert.Equal("sms", sig!.Mode);
    }
}
