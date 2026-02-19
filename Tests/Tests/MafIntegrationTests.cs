using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using Azure.AI.Projects.OpenAI;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using UtilityAi.Consideration;
using UtilityAi.Helpers.OpenAiStructuredOutputHelper;
using UtilityAi.Maf;
using UtilityAi.Orchestration;
using UtilityAi.Utils;
using Xunit;

namespace Tests;

/// <summary>
/// Tests for the Microsoft Agent Framework (MAF) integration.
/// </summary>
public class MafIntegrationTests
{
    // ─── MafClient ────────────────────────────────────────────────


    [Fact]
    public void MafClient_GetAgentsClient_ReturnsClient()
    {
        var client = new MafClient("https://example.openai.azure.com");

        var agentsClient = client.GetAgentsClient();

        Assert.NotNull(agentsClient);
    }



    // ─── MafRequestExtensions ────────────────────────────────────

    [Fact]
    public void ToAzureOpenAiChatOptions_WithValidSchema_ReturnsOptionsAndMessages()
    {
        // Arrange
        var builder = AiRequestBuilder.Create()
            .WithModel("gpt-4")
            .AddSystem("You are a helpful assistant")
            .AddUser("Test message")
            .WithJsonSchemaFrom<MathStep>("math_reasoning");

        // Act
        var options = builder.ToAzureOpenAiChatOptions(out var messages);

        // Assert
        Assert.NotNull(options);
        Assert.NotNull(options.ResponseFormat);
        Assert.NotNull(messages);
        Assert.Equal(2, messages.Count);
        Assert.IsType<OpenAI.Chat.SystemChatMessage>(messages[0]);
        Assert.IsType<OpenAI.Chat.UserChatMessage>(messages[1]);
    }

    [Fact]
    public void ToAzureOpenAiChatOptions_WithoutSchema_ThrowsException()
    {
        // Arrange
        var builder = AiRequestBuilder.Create()
            .WithModel("gpt-4")
            .AddUser("Test message");

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => builder.ToAzureOpenAiChatOptions(out _));
    }

    [Fact]
    public void CreateStructuredOptions_FromType_ReturnsValidOptions()
    {
        // Act
        var options = MafRequestExtensions.CreateStructuredOptions<MathStep>("math_reasoning");

        // Assert
        Assert.NotNull(options);
        Assert.NotNull(options.ResponseFormat);
    }

    [Fact]
    public void CreateStructuredOptions_FromJsonObject_ReturnsValidOptions()
    {
        // Arrange
        var schema = new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["explanation"] = new JsonObject { ["type"] = "string" },
                ["output"] = new JsonObject { ["type"] = "string" }
            },
            ["required"] = new JsonArray { "explanation", "output" },
            ["additionalProperties"] = false
        };

        // Act
        var options = MafRequestExtensions.CreateStructuredOptions("test_schema", schema);

        // Assert
        Assert.NotNull(options);
        Assert.NotNull(options.ResponseFormat);
    }

    [Fact]
    public void CreateStructuredOptions_WithStrictMode_ConfiguresCorrectly()
    {
        // Act - strict = true
        var strictOptions = MafRequestExtensions.CreateStructuredOptions<MathStep>("test", strict: true);

        // Act - strict = false
        var nonStrictOptions = MafRequestExtensions.CreateStructuredOptions<MathStep>("test", strict: false);

        // Assert
        Assert.NotNull(strictOptions);
        Assert.NotNull(nonStrictOptions);
        // Note: ResponseFormat doesn't expose strict property directly, but we verify it doesn't throw
    }

    [Fact]
    public void ToAzureOpenAiChatOptions_ExtractsMultipleMessages_Correctly()
    {
        // Arrange
        var builder = AiRequestBuilder.Create()
            .WithModel("gpt-4")
            .AddSystem("You are a math expert")
            .AddUser("What is 2+2?")
            .AddSystem("Additional instruction")
            .AddUser("Follow up question")
            .WithJsonSchemaFrom<MathStep>("math_reasoning");

        // Act
        var options = builder.ToAzureOpenAiChatOptions(out var messages);

        // Assert
        Assert.NotNull(messages);
        Assert.Equal(4, messages.Count);
        Assert.IsType<OpenAI.Chat.SystemChatMessage>(messages[0]);
        Assert.IsType<OpenAI.Chat.UserChatMessage>(messages[1]);
        Assert.IsType<OpenAI.Chat.SystemChatMessage>(messages[2]);
        Assert.IsType<OpenAI.Chat.UserChatMessage>(messages[3]);
    }

    // ─── Test Helpers ────────────────────────────────────────────

    /// <summary>
    /// Test model for structured output testing.
    /// </summary>
    private record MathStep
    {
        public string Explanation { get; init; } = string.Empty;
        public string Output { get; init; } = string.Empty;
    }

    private sealed class StubSession : AgentSession
    {
        public override object? GetService(Type serviceType, object? serviceKey = null) => null;
    }

    /// <summary>
    /// A simple fixed-value consideration for testing.
    /// </summary>
    private sealed class FixedScore : IConsideration
    {
        private readonly double _score;
        public FixedScore(double score) => _score = score;
        public string Name => "fixed";
        public double Evaluate(Runtime rt) => _score;
    }

    /// <summary>
    /// Test result record for EventBus.
    /// </summary>
    private record TestResult(string Text);
}
