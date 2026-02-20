using System.Text.Json;
using UtilityAi.Sensor.LLM;
using Xunit;

namespace Tests;

public class IntentAnalysisDeserializationTests
{
    [Fact]
    public void IntentAnalysis_Deserializes_FromCamelCaseJson()
    {
        // Arrange
        var json = """
        {
            "intent": "ticket.create",
            "entities": {
                "ticketType": "bug",
                "priority": "high"
            },
            "confidence": 0.95,
            "parameters": {
                "urgency": 0.85,
                "customerTier": "premium"
            }
        }
        """;

        // Act
        var result = JsonSerializer.Deserialize<IntentAnalysis>(json);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("ticket.create", result.Intent);
        Assert.Equal(0.95, result.Confidence);
        Assert.NotNull(result.Entities);
        Assert.Equal(2, result.Entities.Count);
        Assert.NotNull(result.Parameters);
        Assert.Equal(2, result.Parameters.Count);
    }

    [Fact]
    public void IntentAnalysis_GetParameter_ReturnsTypedValue()
    {
        // Arrange
        var json = """
        {
            "intent": "test",
            "entities": {},
            "confidence": 0.9,
            "parameters": {
                "urgency": 0.85,
                "customerTier": "premium",
                "requiresHuman": true
            }
        }
        """;
        var intent = JsonSerializer.Deserialize<IntentAnalysis>(json)!;

        // Act & Assert
        Assert.Equal(0.85, intent.GetParameter<double>("urgency"));
        Assert.Equal("premium", intent.GetParameter<string>("customerTier"));
        Assert.True(intent.GetParameter<bool>("requiresHuman"));
    }

    [Fact]
    public void IntentAnalysis_GetEntity_ReturnsTypedValue()
    {
        // Arrange
        var json = """
        {
            "intent": "test",
            "entities": {
                "ticketId": 12345,
                "status": "open"
            },
            "confidence": 0.9
        }
        """;
        var intent = JsonSerializer.Deserialize<IntentAnalysis>(json)!;

        // Act & Assert
        Assert.Equal(12345, intent.GetEntity<int>("ticketId"));
        Assert.Equal("open", intent.GetEntity<string>("status"));
    }

    [Fact]
    public void IntentAnalysis_GetParameter_ReturnsDefaultWhenMissing()
    {
        // Arrange
        var json = """
        {
            "intent": "test",
            "entities": {},
            "confidence": 0.9
        }
        """;
        var intent = JsonSerializer.Deserialize<IntentAnalysis>(json)!;

        // Act & Assert
        Assert.Equal(0.5, intent.GetParameter<double>("missing", 0.5));
        Assert.Null(intent.GetParameter<string>("missing"));
    }

    [Fact]
    public void IntentAnalysis_GetParameter_ReturnsDefaultOnWrongType()
    {
        // Arrange
        var json = """
        {
            "intent": "test",
            "entities": {},
            "confidence": 0.9,
            "parameters": {
                "value": "not-a-number"
            }
        }
        """;
        var intent = JsonSerializer.Deserialize<IntentAnalysis>(json)!;

        // Act & Assert
        Assert.Equal(99.0, intent.GetParameter<double>("value", 99.0));
    }

    [Fact]
    public void IntentAnalysis_ParameterAbove_WorksCorrectly()
    {
        // Arrange
        var json = """
        {
            "intent": "test",
            "entities": {},
            "confidence": 0.9,
            "parameters": {
                "urgency": 0.85
            }
        }
        """;
        var intent = JsonSerializer.Deserialize<IntentAnalysis>(json)!;

        // Act & Assert
        Assert.True(intent.ParameterAbove("urgency", 0.8));
        Assert.False(intent.ParameterAbove("urgency", 0.9));
        Assert.False(intent.ParameterAbove("missing", 0.5));
    }

    [Fact]
    public void IntentAnalysis_ParameterBelow_WorksCorrectly()
    {
        // Arrange
        var json = """
        {
            "intent": "test",
            "entities": {},
            "confidence": 0.9,
            "parameters": {
                "urgency": 0.25
            }
        }
        """;
        var intent = JsonSerializer.Deserialize<IntentAnalysis>(json)!;

        // Act & Assert
        Assert.True(intent.ParameterBelow("urgency", 0.3));
        Assert.False(intent.ParameterBelow("urgency", 0.2));
        Assert.False(intent.ParameterBelow("missing", 0.5)); // Missing param defaults to MaxValue, so not below threshold
    }

    [Fact]
    public void IntentAnalysis_WithoutParameters_Deserializes()
    {
        // Arrange
        var json = """
        {
            "intent": "simple.action",
            "entities": {},
            "confidence": 0.8
        }
        """;

        // Act
        var result = JsonSerializer.Deserialize<IntentAnalysis>(json);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("simple.action", result.Intent);
        Assert.Null(result.Parameters);
    }

    [Fact]
    public void IntentAnalysis_ComplexNestedObject_Deserializes()
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
                }
            },
            "confidence": 0.95,
            "parameters": {
                "quantity": 5
            }
        }
        """;

        // Act
        var result = JsonSerializer.Deserialize<IntentAnalysis>(json)!;

        // Assert
        var product = result.GetEntity<JsonElement>("product");
        Assert.Equal(123, product.GetProperty("id").GetInt32());
        Assert.Equal("Widget", product.GetProperty("name").GetString());
        Assert.Equal(29.99, product.GetProperty("price").GetDouble());
    }
}
