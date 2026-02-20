using UtilityAi.Facts;
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
    public async Task SenseAsync_SkipsIfAlreadyAnalyzedSameContext()
    {
        // Arrange
        var bus = new EventBus();
        bus.Publish(new UserMessage("Test"));

        var llmResponse = @"{
            ""intent"": ""test.action"",
            ""entities"": {},
            ""confidence"": 1.0
        }";

        var mockLlm = new MockLlmClient(llmResponse);
        var sensor = LlmIntentSensor.ForMessageType<UserMessage>(mockLlm, msg => msg.Text);
        var rt = new Runtime(bus, 1);

        // Act - First analysis
        await sensor.SenseAsync(rt, CancellationToken.None);
        Assert.Equal(1, mockLlm.CallCount);

        // Act - Second call with same context
        await sensor.SenseAsync(rt, CancellationToken.None);

        // Assert - Should not call LLM again (same context hash)
        Assert.Equal(1, mockLlm.CallCount);
    }

    [Fact]
    public async Task SenseAsync_ReanalyzesAfterActionsExecute()
    {
        // Arrange
        var bus = new EventBus();
        var userMsg = new UserMessage("Research Denmark");
        bus.Publish(userMsg);

        var llmResponse = @"{
            ""intent"": ""research"",
            ""entities"": {},
            ""confidence"": 1.0
        }";

        var mockLlm = new MockLlmClient(llmResponse);
        
        // Sensor configured to reanalyze after actions
        var sensor = LlmIntentSensor.ForMessageType<UserMessage>(
            mockLlm,
            msg => msg.Text,
            includeCapabilities: false,
            reanalyzeAfterActions: true
        );
        
        var rt = new Runtime(bus, 1);

        // Act - First analysis (no actions executed yet)
        await sensor.SenseAsync(rt, CancellationToken.None);
        Assert.Equal(1, mockLlm.CallCount);

        // Simulate action execution by publishing ExecutionHistory
        var executedAction = new ExecutedAction(
            ProposalId: "research.web",
            Description: "Search the web",
            TickNumber: 1,
            Timestamp: DateTimeOffset.UtcNow
        );
        bus.Publish(new ExecutionHistory(new[] { executedAction }));

        // Act - Second analysis (after action executed)
        await sensor.SenseAsync(rt, CancellationToken.None);

        // Assert - Should call LLM again (new action executed)
        Assert.Equal(2, mockLlm.CallCount);
    }

    [Fact]
    public async Task SenseAsync_DoesNotReanalyzeWithoutFlag()
    {
        // Arrange
        var bus = new EventBus();
        var userMsg = new UserMessage("Research Denmark");
        bus.Publish(userMsg);

        var llmResponse = @"{
            ""intent"": ""research"",
            ""entities"": {},
            ""confidence"": 1.0
        }";

        var mockLlm = new MockLlmClient(llmResponse);
        
        // Sensor NOT configured to reanalyze (default behavior)
        var sensor = LlmIntentSensor.ForMessageType<UserMessage>(
            mockLlm,
            msg => msg.Text,
            includeCapabilities: false,
            reanalyzeAfterActions: false  // Explicit false
        );
        
        var rt = new Runtime(bus, 1);

        // Act - First analysis
        await sensor.SenseAsync(rt, CancellationToken.None);
        Assert.Equal(1, mockLlm.CallCount);

        // Simulate action execution
        var executedAction = new ExecutedAction(
            ProposalId: "research.web",
            Description: "Search the web",
            TickNumber: 1,
            Timestamp: DateTimeOffset.UtcNow
        );
        bus.Publish(new ExecutionHistory(new[] { executedAction }));

        // Act - Second call (after action executed)
        await sensor.SenseAsync(rt, CancellationToken.None);

        // Assert - Should NOT call LLM again (reanalyzeAfterActions=false)
        Assert.Equal(1, mockLlm.CallCount);
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
