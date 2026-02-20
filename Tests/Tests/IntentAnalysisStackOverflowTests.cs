using System.Text.Json;
using UtilityAi.Orchestration;
using UtilityAi.Sensor.LLM;
using UtilityAi.Utils;
using Xunit;

namespace Tests;

/// <summary>
/// Tests to ensure JsonElement handling doesn't cause stack overflow in schema generation
/// </summary>
public class IntentAnalysisStackOverflowTests
{
    [Fact]
    public void GetEntity_WithBusAndJsonElement_DoesNotStackOverflow()
    {
        // Arrange
        var json = """
        {
            "intent": "message.send",
            "entities": {
                "msg.response": "Hello, World!"
            },
            "confidence": 0.95
        }
        """;

        var intent = JsonSerializer.Deserialize<IntentAnalysis>(json)!;
        var bus = new EventBus();
        bus.Publish(intent);
        var rt = new Runtime(bus, 0);

        // Act
        var value = rt.Bus.GetOrDefault<IntentAnalysis>()!.GetEntity("msg.response", "");

        // Assert
        Assert.Equal("Hello, World!", value);
    }

    [Fact]
    public void GetEntity_NestedObject_DoesNotStackOverflow()
    {
        // Arrange
        var json = """
        {
            "intent": "order.create",
            "entities": {
                "product": {
                    "id": 123,
                    "name": "Widget",
                    "price": 29.99
                },
                "msg.response": "Order created successfully"
            },
            "confidence": 0.95
        }
        """;

        var intent = JsonSerializer.Deserialize<IntentAnalysis>(json)!;
        var bus = new EventBus();
        bus.Publish(intent);
        var rt = new Runtime(bus, 0);

        // Act
        var response = rt.Bus.GetOrDefault<IntentAnalysis>()!.GetEntity("msg.response", "");
        var product = rt.Bus.GetOrDefault<IntentAnalysis>()!.GetEntity<JsonElement>("product");

        // Assert
        Assert.Equal("Order created successfully", response);
        Assert.Equal(123, product.GetProperty("id").GetInt32());
        Assert.Equal("Widget", product.GetProperty("name").GetString());
    }

    [Fact]
    public void GetParameter_WithBusAndJsonElement_DoesNotStackOverflow()
    {
        // Arrange
        var json = """
        {
            "intent": "ticket.create",
            "entities": {},
            "confidence": 0.9,
            "parameters": {
                "urgency": 0.85,
                "customerTier": "premium"
            }
        }
        """;

        var intent = JsonSerializer.Deserialize<IntentAnalysis>(json)!;
        var bus = new EventBus();
        bus.Publish(intent);
        var rt = new Runtime(bus, 0);

        // Act
        var urgency = rt.Bus.GetOrDefault<IntentAnalysis>()!.GetParameter<double>("urgency");
        var tier = rt.Bus.GetOrDefault<IntentAnalysis>()!.GetParameter<string>("customerTier");

        // Assert
        Assert.Equal(0.85, urgency);
        Assert.Equal("premium", tier);
    }

    [Fact]
    public void IntentAnalysis_WithComplexNestedStructures_DoesNotStackOverflow()
    {
        // Arrange - Complex nested structure with arrays and objects
        var json = """
        {
            "intent": "data.process",
            "entities": {
                "dataset": {
                    "records": [
                        {"id": 1, "value": "first"},
                        {"id": 2, "value": "second"}
                    ],
                    "metadata": {
                        "count": 2,
                        "source": "api"
                    }
                }
            },
            "confidence": 0.95,
            "parameters": {
                "batchSize": 100,
                "config": {
                    "timeout": 30,
                    "retry": true
                }
            }
        }
        """;

        var intent = JsonSerializer.Deserialize<IntentAnalysis>(json)!;
        var bus = new EventBus();
        bus.Publish(intent);
        var rt = new Runtime(bus, 0);

        // Act
        var dataset = rt.Bus.GetOrDefault<IntentAnalysis>()!.GetEntity<JsonElement>("dataset");
        var batchSize = rt.Bus.GetOrDefault<IntentAnalysis>()!.GetParameter<int>("batchSize");
        var config = rt.Bus.GetOrDefault<IntentAnalysis>()!.GetParameter<JsonElement>("config");

        // Assert
        Assert.Equal(2, dataset.GetProperty("metadata").GetProperty("count").GetInt32());
        Assert.Equal(100, batchSize);
        Assert.Equal(30, config.GetProperty("timeout").GetInt32());
    }

    [Fact]
    public void SchemaGeneration_ForIntentAnalysis_DoesNotStackOverflow()
    {
        // This test ensures the schema generator doesn't recurse infinitely
        // when processing Dictionary<string, JsonElement>

        // Arrange & Act - If this causes stack overflow, the test will fail
        var schema = UtilityAi.Helpers.OpenAiStructuredOutputHelper.SchemaGenerator.JsonSchemaGenerator
            .BuildOutputArraySchemaFrom<IntentAnalysis>();

        // Assert
        Assert.NotNull(schema);
        Assert.Equal("object", schema["type"]?.ToString());

        var properties = schema["properties"] as System.Text.Json.Nodes.JsonObject;
        Assert.NotNull(properties);

        var output = properties!["output"] as System.Text.Json.Nodes.JsonObject;
        Assert.NotNull(output);
        Assert.Equal("array", output!["type"]?.ToString());
    }
}
