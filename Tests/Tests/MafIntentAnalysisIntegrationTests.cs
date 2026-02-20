using System.ClientModel;
using System.Text.Json;
using Azure.AI.Projects.OpenAI;
using OpenAI.Chat;
using UtilityAi.Helpers.OpenAiStructuredOutputHelper;
using UtilityAi.Helpers.OpenAiStructuredOutputHelper.SchemaGenerator;
using UtilityAi.Helpers.OpenAiStructuredOutputHelper.Strategy;
using UtilityAi.Maf;
using UtilityAi.Orchestration;
using UtilityAi.Sensor.LLM;
using UtilityAi.Utils;
using Xunit;

namespace Tests;

/// <summary>
/// Integration tests for IntentAnalysis with mock ChatClient that returns OpenAI-formatted JSON
/// </summary>
public class MafIntentAnalysisIntegrationTests
{
    [Fact]
    public void CompleteAndDeserialize_WithMockOpenAiResponse_DeserializesCorrectly()
    {
        // Arrange - Simulate what OpenAI returns with structured output
        var openAiJsonResponse = """
        {
            "output": [
                {
                    "intent": "ticket.create",
                    "entities": {
                        "issueType": "bug",
                        "severity": "high"
                    },
                    "confidence": 0.95,
                    "parameters": {
                        "urgency": 0.9,
                        "customerTier": "enterprise"
                    }
                }
            ]
        }
        """;

        var mockClient = new MockChatClient(openAiJsonResponse);

        var builder = AiRequestBuilder.Create()
            .WithModel("gpt-4")
            .AddUser("Create a ticket for high severity bug")
            .WithJsonSchemaFrom<IntentAnalysis>("intent");

        // Act
        var result = builder.CompleteAndDeserialize<IntentAnalysis>(mockClient);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("ticket.create", result.Intent);
        Assert.Equal(0.95, result.Confidence);
        Assert.Equal(2, result.Entities.Count);
        Assert.Equal(2, result.Parameters!.Count);

        // Verify GetEntity works
        Assert.Equal("bug", result.GetEntity<string>("issueType"));
        Assert.Equal("high", result.GetEntity<string>("severity"));

        // Verify GetParameter works
        Assert.Equal(0.9, result.GetParameter<double>("urgency"));
        Assert.Equal("enterprise", result.GetParameter<string>("customerTier"));
    }

    [Fact]
    public void CompleteAndDeserialize_WithNestedObjects_DeserializesCorrectly()
    {
        // Arrange - OpenAI response with nested objects
        var openAiJsonResponse = """
        {
            "output": [
                {
                    "intent": "order.create",
                    "entities": {
                        "product": {
                            "id": 123,
                            "name": "Widget",
                            "price": 29.99
                        },
                        "customer": {
                            "email": "user@example.com",
                            "tier": "premium"
                        }
                    },
                    "confidence": 0.98,
                    "parameters": {
                        "quantity": 5,
                        "shippingConfig": {
                            "method": "express",
                            "trackingEnabled": true
                        }
                    }
                }
            ]
        }
        """;

        var mockClient = new MockChatClient(openAiJsonResponse);

        var builder = AiRequestBuilder.Create()
            .WithModel("gpt-4")
            .AddUser("Create an order")
            .WithJsonSchemaFrom<IntentAnalysis>("intent");

        // Act
        var result = builder.CompleteAndDeserialize<IntentAnalysis>(mockClient);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("order.create", result.Intent);

        // Verify nested entity
        var product = result.GetEntity<JsonElement>("product");
        Assert.Equal(123, product.GetProperty("id").GetInt32());
        Assert.Equal("Widget", product.GetProperty("name").GetString());
        Assert.Equal(29.99, product.GetProperty("price").GetDouble());

        // Verify nested parameter
        var config = result.GetParameter<JsonElement>("shippingConfig");
        Assert.Equal("express", config.GetProperty("method").GetString());
        Assert.True(config.GetProperty("trackingEnabled").GetBoolean());
    }

    [Fact]
    public void CompleteAndDeserialize_WithArrays_DeserializesCorrectly()
    {
        // Arrange - OpenAI response with arrays
        var openAiJsonResponse = """
        {
            "output": [
                {
                    "intent": "data.process",
                    "entities": {
                        "records": [
                            {"id": 1, "value": "first"},
                            {"id": 2, "value": "second"},
                            {"id": 3, "value": "third"}
                        ]
                    },
                    "confidence": 0.92,
                    "parameters": {
                        "tags": ["urgent", "important", "review"]
                    }
                }
            ]
        }
        """;

        var mockClient = new MockChatClient(openAiJsonResponse);

        var builder = AiRequestBuilder.Create()
            .WithModel("gpt-4")
            .AddUser("Process data")
            .WithJsonSchemaFrom<IntentAnalysis>("intent");

        // Act
        var result = builder.CompleteAndDeserialize<IntentAnalysis>(mockClient);

        // Assert
        Assert.NotNull(result);

        // Verify array entity
        var records = result.GetEntity<JsonElement>("records");
        Assert.Equal(JsonValueKind.Array, records.ValueKind);
        Assert.Equal(3, records.GetArrayLength());
        Assert.Equal(1, records[0].GetProperty("id").GetInt32());

        // Verify array parameter
        var tags = result.GetParameter<JsonElement>("tags");
        Assert.Equal(JsonValueKind.Array, tags.ValueKind);
        Assert.Equal(3, tags.GetArrayLength());
        Assert.Equal("urgent", tags[0].GetString());
    }

    [Fact]
    public void CompleteAndDeserialize_WithEventBus_IntegrationTest()
    {
        // Arrange - Full integration test with EventBus
        var openAiJsonResponse = """
        {
            "output": [
                {
                    "intent": "message.send",
                    "entities": {
                        "msg.response": "Hello, World!",
                        "recipient": "user@example.com"
                    },
                    "confidence": 0.96,
                    "parameters": {
                        "priority": 0.8,
                        "sendImmediate": true
                    }
                }
            ]
        }
        """;

        var mockClient = new MockChatClient(openAiJsonResponse);
        var bus = new EventBus();
        var rt = new Runtime(bus, 0);

        var builder = AiRequestBuilder.Create()
            .WithModel("gpt-4")
            .AddUser("Send a hello message")
            .WithJsonSchemaFrom<IntentAnalysis>("intent");

        // Act
        var result = builder.CompleteAndDeserialize<IntentAnalysis>(mockClient);
        bus.Publish(result);

        // Assert - Retrieve from bus and use
        var intentFromBus = rt.Bus.GetOrDefault<IntentAnalysis>();
        Assert.NotNull(intentFromBus);

        var response = intentFromBus!.GetEntity("msg.response", "");
        Assert.Equal("Hello, World!", response);

        Assert.True(intentFromBus.ParameterAbove("priority", 0.7));
        Assert.Equal(true, intentFromBus.GetParameter<bool>("sendImmediate"));
    }

    [Fact]
    public void SchemaGeneration_ForIntentAnalysis_GeneratesValidSchema()
    {
        // Arrange & Act - Generate schema with strict mode
        var schema = UtilityAi.Helpers.OpenAiStructuredOutputHelper.SchemaGenerator.JsonSchemaGenerator
            .BuildOutputArraySchemaFrom<IntentAnalysis>(new SchemaGeneratorOptions
            {
                RequiredStrategy = RequiredStrategy.AllProperties
            });

        // Assert - Verify schema structure
        Assert.NotNull(schema);
        Assert.Equal("object", schema["type"]?.ToString());

        var properties = schema["properties"] as System.Text.Json.Nodes.JsonObject;
        Assert.NotNull(properties);

        var output = properties!["output"] as System.Text.Json.Nodes.JsonObject;
        Assert.NotNull(output);
        Assert.Equal("array", output!["type"]?.ToString());

        var items = output["items"] as System.Text.Json.Nodes.JsonObject;
        Assert.NotNull(items);

        var itemProps = items!["properties"] as System.Text.Json.Nodes.JsonObject;
        Assert.NotNull(itemProps);

        // Verify IntentAnalysis properties exist
        Assert.True(itemProps!.ContainsKey("intent"));
        Assert.True(itemProps.ContainsKey("entities"));
        Assert.True(itemProps.ContainsKey("confidence"));
        Assert.True(itemProps.ContainsKey("parameters"));

        // Verify required array contains all properties
        var required = items["required"] as System.Text.Json.Nodes.JsonArray;
        Assert.NotNull(required);
        Assert.Contains("intent", required!.Select(x => x?.ToString()));
        Assert.Contains("entities", required.Select(x => x?.ToString()));
        Assert.Contains("confidence", required.Select(x => x?.ToString()));
        Assert.Contains("parameters", required.Select(x => x?.ToString()));
    }

    [Fact]
    public void CompleteAndDeserialize_WithMinimalResponse_HandlesGracefully()
    {
        // Arrange - Minimal valid response
        var openAiJsonResponse = """
        {
            "output": [
                {
                    "intent": "unknown",
                    "entities": {},
                    "confidence": 0.5
                }
            ]
        }
        """;

        var mockClient = new MockChatClient(openAiJsonResponse);

        var builder = AiRequestBuilder.Create()
            .WithModel("gpt-4")
            .AddUser("Unclear request")
            .WithJsonSchemaFrom<IntentAnalysis>("intent");

        // Act
        var result = builder.CompleteAndDeserialize<IntentAnalysis>(mockClient);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("unknown", result.Intent);
        Assert.Equal(0.5, result.Confidence);
        Assert.Empty(result.Entities);
        Assert.Null(result.Parameters);
    }
}

/// <summary>
/// Mock ChatClient that returns pre-configured JSON responses
/// </summary>
internal class MockChatClient
{
    private readonly string _responseJson;

    public MockChatClient(string responseJson)
    {
        _responseJson = responseJson;
    }

    public string CompleteChat(IEnumerable<ChatMessage> messages, ChatCompletionOptions? options = null)
    {
        // Return the JSON response directly
        return _responseJson;
    }
}

/// <summary>
/// Extension methods for testing with MockChatClient
/// </summary>
internal static class MockChatClientExtensions
{
    public static T CompleteAndDeserialize<T>(this AiRequestBuilder builder, MockChatClient mockClient, string propertyName = "output")
    {
        var options = builder.ToAzureOpenAiChatOptions(out var messages);
        var jsonResponse = mockClient.CompleteChat(messages, options);

        using JsonDocument structuredJson = JsonDocument.Parse(jsonResponse);
        var outputElement = structuredJson.RootElement.GetProperty(propertyName);

        // Handle both array and direct object cases
        return outputElement.ValueKind == JsonValueKind.Array
            ? JsonSerializer.Deserialize<T>(outputElement[0])!
            : JsonSerializer.Deserialize<T>(outputElement)!;
    }
}
