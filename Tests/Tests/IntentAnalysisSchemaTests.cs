using System.Text.Json;
using UtilityAi.Helpers.OpenAiStructuredOutputHelper;
using UtilityAi.Helpers.OpenAiStructuredOutputHelper.SchemaGenerator;
using UtilityAi.Sensor.LLM;
using Xunit;
using Xunit.Abstractions;

namespace Tests;

/// <summary>
/// Tests to validate IntentAnalysis schema generation and actual OpenAI response deserialization
/// </summary>
public class IntentAnalysisSchemaTests
{
    private readonly ITestOutputHelper _output;

    public IntentAnalysisSchemaTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void IntentAnalysis_GeneratesValidSchema()
    {
        // Act
        var schema = JsonSchemaGenerator.BuildOutputArraySchemaFrom<IntentAnalysis>();
        var schemaJson = JsonSerializer.Serialize(schema, new JsonSerializerOptions { WriteIndented = true });

        _output.WriteLine("Generated Schema:");
        _output.WriteLine(schemaJson);

        // Assert
        Assert.NotNull(schema);

        var root = JsonDocument.Parse(schemaJson).RootElement;
        var items = root.GetProperty("properties")
            .GetProperty("output")
            .GetProperty("items");

        var properties = items.GetProperty("properties");

        // Check entities property
        Assert.True(properties.TryGetProperty("entities", out var entitiesProperty));
        _output.WriteLine($"\nEntities property: {entitiesProperty}");

        // Check parameters property
        Assert.True(properties.TryGetProperty("parameters", out var parametersProperty));
        _output.WriteLine($"\nParameters property: {parametersProperty}");
    }

    [Fact]
    public void IntentAnalysis_DeserializesFromOpenAiFormat()
    {
        // Arrange - Exact format OpenAI would return
        var openAiResponse = """
        {
            "output": [
                {
                    "intent": "ticket.create",
                    "entities": {
                        "issueType": "bug"
                    },
                    "confidence": 0.95,
                    "parameters": {
                        "urgency": 0.8
                    }
                }
            ]
        }
        """;

        _output.WriteLine("OpenAI Response:");
        _output.WriteLine(openAiResponse);

        // Act - Deserialize the output array item
        using var doc = JsonDocument.Parse(openAiResponse);
        var outputArray = doc.RootElement.GetProperty("output");
        var firstItem = outputArray[0];

        _output.WriteLine("\nFirst item JSON:");
        _output.WriteLine(firstItem.GetRawText());

        IntentAnalysis? result = null;
        Exception? exception = null;

        try
        {
            result = JsonSerializer.Deserialize<IntentAnalysis>(firstItem);
        }
        catch (Exception ex)
        {
            exception = ex;
            _output.WriteLine($"\nDeserialization failed: {ex.Message}");
            _output.WriteLine($"Stack: {ex.StackTrace}");
        }

        // Assert
        if (exception != null)
        {
            Assert.Fail($"Deserialization failed: {exception.Message}");
        }

        Assert.NotNull(result);
        Assert.Equal("ticket.create", result!.Intent);
        Assert.Equal(0.95, result.Confidence);
    }

    [Fact]
    public void IntentAnalysis_InspectEntitiesType()
    {
        // Arrange
        var json = """
        {
            "intent": "test",
            "entities": {
                "key1": "value1"
            },
            "confidence": 0.9
        }
        """;

        _output.WriteLine("Input JSON:");
        _output.WriteLine(json);

        // Act
        using var doc = JsonDocument.Parse(json);
        var entitiesElement = doc.RootElement.GetProperty("entities");

        _output.WriteLine($"\nEntities ValueKind: {entitiesElement.ValueKind}");
        _output.WriteLine($"Entities Raw: {entitiesElement.GetRawText()}");

        // Try to deserialize
        Exception? exception = null;
        IntentAnalysis? result = null;

        try
        {
            result = JsonSerializer.Deserialize<IntentAnalysis>(json);
        }
        catch (Exception ex)
        {
            exception = ex;
            _output.WriteLine($"\nError: {ex.Message}");
        }

        if (exception != null)
        {
            Assert.Fail($"Failed to deserialize: {exception.Message}");
        }

        Assert.NotNull(result);
    }

    [Fact]
    public void IntentAnalysis_WithJsonSerializerOptions_Deserializes()
    {
        // Test with explicit options
        var json = """
        {
            "intent": "test",
            "entities": {
                "key1": "value1"
            },
            "confidence": 0.9,
            "parameters": {
                "param1": 123
            }
        }
        """;

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        var result = JsonSerializer.Deserialize<IntentAnalysis>(json, options);

        Assert.NotNull(result);
        Assert.Equal("test", result!.Intent);
        Assert.Single(result.Entities);
        Assert.Single(result.Parameters!);
    }
}
