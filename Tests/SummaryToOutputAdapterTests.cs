using System.Threading;
using System.Threading.Tasks;
using Example.OutputModule.DTO;
using Example.SearchAndSummerizeModule.DTO;
using Example.Sensor;
using UtilityAi.Utils;
using Xunit;

namespace Tests;

public class SummaryToOutputAdapterTests
{
    [Fact]
    public async Task Adapter_Publishes_OutputTextMessage_From_Summary_Once()
    {
        var bus = new EventBus();
        var intent = new UserIntent("q");
        var rt = new Runtime(bus, intent, 0);

        // Seed Summary
        bus.Publish(new Summary("hello world"));

        var sensor = new SummaryToOutputAdapter();
        await sensor.SenseAsync(rt, CancellationToken.None);
        await sensor.SenseAsync(rt, CancellationToken.None); // idempotent

        var msg = bus.GetOrDefault<OutputTextMessage>();
        Assert.NotNull(msg);
        Assert.Equal("hello world", msg!.Text);
    }
}
