using UtilityAi.Sensor.LLM;
using UtilityAi.Utils;

namespace Tests.Sensor.LLM;

public sealed class LlmIntentSensorTests
{
    private sealed record UserMessage(string Text);

    [Fact]
    public async Task SenseAsync_AnalyzesMessageAndPublishesIntentAnalysis()
    {
        // Arrange
        var bus = new EventBus();
        bus.Publish(new UserMessage("Show me sales data"));

        var llmResponse = @"{
            ""intent"": ""query.sales"",
            ""entities"": {
                ""dataType"": ""sales""
            },
            ""confidence"": 0.9
        }";

        var mockLlm = new MockLlmClient(llmResponse);
        var sensor = LlmIntentSensor.ForMessageType<UserMessage>(
            mockLlm,
            msg => msg.Text
        );

        var rt = new Runtime(bus,  1);

        // Act
        await sensor.SenseAsync(rt, CancellationToken.None);

        // Assert
        Assert.Equal(1, mockLlm.CallCount);

        var analysis = bus.GetOrDefault<IntentAnalysis>();
        Assert.NotNull(analysis);
        Assert.Equal("query.sales", analysis.Intent);
        Assert.Equal(0.9, analysis.Confidence);
        Assert.True(analysis.Entities.ContainsKey("dataType"));
    }

    [Fact]
    public async Task SenseAsync_HandlesMarkdownCodeBlocks()
    {
        // Arrange
        var bus = new EventBus();
        bus.Publish(new UserMessage("Test"));

        var llmResponse = @"```json
{
  ""intent"": ""test.action"",
  ""entities"": {},
  ""confidence"": 1.0
}
```";

        var mockLlm = new MockLlmClient(llmResponse);
        var sensor = LlmIntentSensor.ForMessageType<UserMessage>(mockLlm, msg => msg.Text);
        var rt = new Runtime(bus,  1);

        // Act
        await sensor.SenseAsync(rt, CancellationToken.None);

        // Assert
        var analysis = bus.GetOrDefault<IntentAnalysis>();
        Assert.NotNull(analysis);
        Assert.Equal("test.action", analysis.Intent);
    }

    [Fact]
    public async Task SenseAsync_SkipsIfAlreadyAnalyzed()
    {
        // Arrange
        var bus = new EventBus();
        bus.Publish(new UserMessage("Test"));
        bus.Publish(new IntentAnalysis("existing", new Dictionary<string, object>(), 1.0));

        var mockLlm = new MockLlmClient("{}");
        var sensor = LlmIntentSensor.ForMessageType<UserMessage>(mockLlm, msg => msg.Text);
        var rt = new Runtime(bus,  1);

        // Act
        await sensor.SenseAsync(rt, CancellationToken.None);

        // Assert - Should not call LLM
        Assert.Equal(0, mockLlm.CallCount);
    }

    [Fact]
    public async Task SenseAsync_SkipsIfNoMessage()
    {
        // Arrange
        var bus = new EventBus();
        // No UserMessage published

        var mockLlm = new MockLlmClient("{}");
        var sensor = LlmIntentSensor.ForMessageType<UserMessage>(mockLlm, msg => msg.Text);
        var rt = new Runtime(bus,  1);

        // Act
        await sensor.SenseAsync(rt, CancellationToken.None);

        // Assert - Should not call LLM
        Assert.Equal(0, mockLlm.CallCount);
    }

    [Fact]
    public async Task SenseAsync_HandlesMalformedResponse()
    {
        // Arrange
        var bus = new EventBus();
        bus.Publish(new UserMessage("Test"));

        var mockLlm = new MockLlmClient("This is not JSON");
        var sensor = LlmIntentSensor.ForMessageType<UserMessage>(mockLlm, msg => msg.Text);
        var rt = new Runtime(bus,  1);

        // Act
        await sensor.SenseAsync(rt, CancellationToken.None);

        // Assert - Should publish fallback analysis
        var analysis = bus.GetOrDefault<IntentAnalysis>();
        Assert.NotNull(analysis);
        Assert.Equal("parse_error", analysis.Intent);
        Assert.Equal(0.0, analysis.Confidence);
    }

    [Fact]
    public async Task SenseAsync_SkipsEmptyMessage()
    {
        // Arrange
        var bus = new EventBus();
        bus.Publish(new UserMessage(""));

        var mockLlm = new MockLlmClient("{}");
        var sensor = LlmIntentSensor.ForMessageType<UserMessage>(mockLlm, msg => msg.Text);
        var rt = new Runtime(bus,  1);

        // Act
        await sensor.SenseAsync(rt, CancellationToken.None);

        // Assert - Should not call LLM
        Assert.Equal(0, mockLlm.CallCount);
    }
}
