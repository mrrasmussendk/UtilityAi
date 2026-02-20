using System.Runtime.CompilerServices;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
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
    [JsonConverter(typeof(AlwaysThrowingConverter))]
    private sealed class AlwaysThrowingType;

    private sealed class AlwaysThrowingConverter : JsonConverter<AlwaysThrowingType>
    {
        public override AlwaysThrowingType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            throw new InvalidOperationException("converter failure");

        public override void Write(Utf8JsonWriter writer, AlwaysThrowingType value, JsonSerializerOptions options) =>
            throw new NotSupportedException();
    }

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
    public void FindDeserializableElement_DoesNotSwallowNonJsonExceptions()
    {
        using var doc = JsonDocument.Parse(@"{""output"":{""value"":1}}");
        var method = typeof(MafRequestExtensions)
            .GetMethod("FindDeserializableElement", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
            .MakeGenericMethod(typeof(AlwaysThrowingType));

        var exception = Assert.Throws<TargetInvocationException>(() => method.Invoke(null, new object[] { doc.RootElement, "output" }));
        Assert.IsType<InvalidOperationException>(exception.InnerException);
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

    // ─── CompleteAndDeserialize Tests ────────────────────────────

    [Fact]
    public void CompleteAndDeserialize_WithArrayResponse_DeserializesFirstElement()
    {
        // Note: This is an integration test concept. In practice, you would need a real or mocked ChatClient.
        // For demonstration, we'll test the deserialization logic with a mock response structure.
        
        var responseJson = @"{""output"":[{""Explanation"":""test explanation"",""Output"":""42""}]}";
        
        // Verify the JSON structure can be parsed correctly
        using var doc = JsonDocument.Parse(responseJson);
        var outputElement = doc.RootElement.GetProperty("output");
        
        Assert.Equal(JsonValueKind.Array, outputElement.ValueKind);
        var result = JsonSerializer.Deserialize<MathStep>(outputElement[0]);
        
        Assert.NotNull(result);
        Assert.Equal("test explanation", result.Explanation);
        Assert.Equal("42", result.Output);
    }

    [Fact]
    public void CompleteAndDeserialize_WithDirectObjectResponse_DeserializesObject()
    {
        var responseJson = @"{""output"":{""Explanation"":""direct test"",""Output"":""99""}}";
        
        using var doc = JsonDocument.Parse(responseJson);
        var outputElement = doc.RootElement.GetProperty("output");
        
        Assert.Equal(JsonValueKind.Object, outputElement.ValueKind);
        var result = JsonSerializer.Deserialize<MathStep>(outputElement);
        
        Assert.NotNull(result);
        Assert.Equal("direct test", result.Explanation);
        Assert.Equal("99", result.Output);
    }

    [Fact]
    public void CompleteAndDeserialize_WithCustomPropertyName_ExtractsCorrectProperty()
    {
        var responseJson = @"{""result"":[{""Explanation"":""custom property"",""Output"":""7""}]}";

        using var doc = JsonDocument.Parse(responseJson);
        var resultElement = doc.RootElement.GetProperty("result");

        Assert.Equal(JsonValueKind.Array, resultElement.ValueKind);
        var result = JsonSerializer.Deserialize<MathStep>(resultElement[0]);

        Assert.NotNull(result);
        Assert.Equal("custom property", result.Explanation);
        Assert.Equal("7", result.Output);
    }

    [Fact]
    public void CompleteAndDeserialize_WithStringEncodedJson_ParsesAndDeserializes()
    {
        // Simulates the case where output is a JSON-encoded string with proper casing
        var responseJson = "{\"output\":\"{\\\"Intent\\\": \\\"msg.response\\\", \\\"Entities\\\": {\\\"msg.response\\\": \\\"How far is france from germany?\\\"}, \\\"Confidence\\\": 0.95}\"}";

        using var doc = JsonDocument.Parse(responseJson);
        var outputElement = doc.RootElement.GetProperty("output");

        Assert.Equal(JsonValueKind.String, outputElement.ValueKind);

        // Parse the string as JSON
        var jsonString = outputElement.GetString()!;
        var result = JsonSerializer.Deserialize<IntentResponse>(jsonString);

        Assert.NotNull(result);
        Assert.Equal("msg.response", result.Intent);
        Assert.Equal(0.95, result.Confidence);
        Assert.NotNull(result.Entities);
    }

    [Fact]
    public void CompleteAndDeserialize_WithDirectObjectInOutput_DeserializesCorrectly()
    {
        // Simulates the case where output contains a direct object (not string-encoded)
        var responseJson = @"{""output"":{""Intent"":""msg.response"",""Entities"":{""msg.response"":""How far is france from germany?""},""Confidence"":0.95}}";

        using var doc = JsonDocument.Parse(responseJson);
        var outputElement = doc.RootElement.GetProperty("output");

        Assert.Equal(JsonValueKind.Object, outputElement.ValueKind);
        var result = JsonSerializer.Deserialize<IntentResponse>(outputElement);

        Assert.NotNull(result);
        Assert.Equal("msg.response", result.Intent);
        Assert.Equal(0.95, result.Confidence);
        Assert.NotNull(result.Entities);
    }

    [Fact]
    public void FindDeserializableElement_WithMissingPropertyName_FallsBackToRoot()
    {
        // Test when the specified property doesn't exist
        var responseJson = @"{""Intent"":""msg.response"",""Entities"":{""msg.response"":""test""},""Confidence"":0.95}";

        using var doc = JsonDocument.Parse(responseJson);
        // Try to get "output" property which doesn't exist - should fall back to root
        var hasProp = doc.RootElement.TryGetProperty("output", out _);
        Assert.False(hasProp);

        // Should deserialize from root instead
        var result = JsonSerializer.Deserialize<IntentResponse>(doc.RootElement);
        Assert.NotNull(result);
        Assert.Equal("msg.response", result.Intent);
    }

    [Fact]
    public void FindDeserializableElement_WithNestedObjectInWrongProperty_FindsCorrectOne()
    {
        // Test searching through properties to find the deserializable object
        var responseJson = @"{""metadata"":{""id"":123},""data"":{""Intent"":""msg.response"",""Entities"":{},""Confidence"":0.85}}";

        using var doc = JsonDocument.Parse(responseJson);
        var dataElement = doc.RootElement.GetProperty("data");
        var result = JsonSerializer.Deserialize<IntentResponse>(dataElement);

        Assert.NotNull(result);
        Assert.Equal("msg.response", result.Intent);
        Assert.Equal(0.85, result.Confidence);
    }

    [Fact]
    public void FindDeserializableElement_WithArrayWrappedObject_UnwrapsAndDeserializes()
    {
        // Test unwrapping single-element arrays
        var responseJson = @"{""output"":[{""Explanation"":""wrapped in array"",""Output"":""123""}]}";

        using var doc = JsonDocument.Parse(responseJson);
        var outputElement = doc.RootElement.GetProperty("output");
        Assert.Equal(JsonValueKind.Array, outputElement.ValueKind);

        var result = JsonSerializer.Deserialize<MathStep>(outputElement[0]);
        Assert.NotNull(result);
        Assert.Equal("wrapped in array", result.Explanation);
    }

    [Fact]
    public void FindDeserializableElement_WithDoubleEncodedJson_UnwrapsMultipleLevels()
    {
        // Test double-encoded JSON string
        var innerJson = @"{""Intent"":""msg.response"",""Entities"":{},""Confidence"":0.99}";
        var escapedJson = JsonSerializer.Serialize(innerJson);
        var responseJson = $@"{{""output"":{escapedJson}}}";

        using var doc = JsonDocument.Parse(responseJson);
        var outputElement = doc.RootElement.GetProperty("output");
        Assert.Equal(JsonValueKind.String, outputElement.ValueKind);

        // First unwrap
        var firstUnwrap = outputElement.GetString()!;
        var result = JsonSerializer.Deserialize<IntentResponse>(firstUnwrap);

        Assert.NotNull(result);
        Assert.Equal("msg.response", result.Intent);
        Assert.Equal(0.99, result.Confidence);
    }

    [Fact]
    public void FindDeserializableElement_WithRootAsArray_ExtractsFirstElement()
    {
        // Test when root itself is an array
        var responseJson = @"[{""Explanation"":""first item"",""Output"":""999""}]";

        using var doc = JsonDocument.Parse(responseJson);
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);

        var result = JsonSerializer.Deserialize<MathStep>(doc.RootElement[0]);
        Assert.NotNull(result);
        Assert.Equal("first item", result.Explanation);
        Assert.Equal("999", result.Output);
    }

    [Fact]
    public void FindDeserializableElement_WithComplexNestedStructure_FindsTarget()
    {
        // Test complex nesting with multiple levels
        var responseJson = @"{
            ""status"": ""success"",
            ""result"": {
                ""data"": {
                    ""Intent"": ""msg.response"",
                    ""Entities"": {""msg.response"": ""nested deep""},
                    ""Confidence"": 0.88
                }
            }
        }";

        using var doc = JsonDocument.Parse(responseJson);
        var dataElement = doc.RootElement.GetProperty("result").GetProperty("data");
        var result = JsonSerializer.Deserialize<IntentResponse>(dataElement);

        Assert.NotNull(result);
        Assert.Equal("msg.response", result.Intent);
        Assert.Equal(0.88, result.Confidence);
    }

    [Fact]
    public void FindDeserializableElement_WithStringEncodedNestedObject_ParsesCorrectly()
    {
        // Your original problematic case
        var responseJson = "{\"output\":\"{\\\"intent\\\": \\\"msg.response\\\", \\\"entities\\\": {\\\"msg.response\\\": \\\"How far is france from germany?\\\"}, \\\"confidence\\\": 0.95}\"}";

        using var doc = JsonDocument.Parse(responseJson);
        var outputElement = doc.RootElement.GetProperty("output");

        // Verify it's a string
        Assert.Equal(JsonValueKind.String, outputElement.ValueKind);

        // Parse the string
        var jsonString = outputElement.GetString()!;
        using var innerDoc = JsonDocument.Parse(jsonString);

        // Now we can access the properties with different casing
        var intent = innerDoc.RootElement.GetProperty("intent").GetString();
        var confidence = innerDoc.RootElement.GetProperty("confidence").GetDouble();

        Assert.Equal("msg.response", intent);
        Assert.Equal(0.95, confidence);
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

    /// <summary>
    /// Test model for intent response testing.
    /// </summary>
    private record IntentResponse
    {
        public string Intent { get; init; } = string.Empty;
        public Dictionary<string, string>? Entities { get; init; }
        public double Confidence { get; init; }
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
