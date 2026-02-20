using System.Reflection;
using UtilityAi.Facts;
using UtilityAi.Sensor.BuiltIn;
using UtilityAi.Utils;

namespace Tests.Sensor;

public class ResourceSensorTests
{
    [Fact]
    public async Task SenseAsync_ZeroElapsedTime_DoesNotProduceInvalidCpuValue()
    {
        var sensor = new ResourceSensor();
        var bus = new EventBus();
        var runtime = new Runtime(bus, 0);

        typeof(ResourceSensor)
            .GetField("_lastCheck", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(sensor, DateTimeOffset.UtcNow);

        await sensor.SenseAsync(runtime, CancellationToken.None);

        var usage = bus.GetOrDefault<ResourceUsage>();
        Assert.NotNull(usage);
        Assert.False(double.IsNaN(usage.CpuPercent));
        Assert.False(double.IsInfinity(usage.CpuPercent));
    }
}
