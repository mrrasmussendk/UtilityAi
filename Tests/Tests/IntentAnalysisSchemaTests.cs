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

    /// <summary>
    /// Regression test: proves the fix for Dictionary&lt;string, JsonElement&gt; schema generation.
    /// 
    /// BUG: The old schema emitted additionalProperties: false for entities/parameters,
    ///      which instructed OpenAI structured output to return only empty objects {}.
    ///      Any dynamic keys (like "kilometer", "ticketId") were silently dropped by the LLM.
    /// 
    /// FIX: Changed to additionalProperties: true, allowing OpenAI to populate the dictionary
    ///      with arbitrary key-value pairs extracted from the user's message.
    /// </summary>
    [Fact]
    public void Regression_EntitiesSchema_MustAllowAdditionalProperties_ForDynamicKeys()
    {
        // Arrange - Build the broken schema (what the old code produced)
        var brokenSchema = new System.Text.Json.Nodes.JsonObject
        {
            ["type"] = "object",
            ["properties"] = new System.Text.Json.Nodes.JsonObject(),
            ["required"] = new System.Text.Json.Nodes.JsonArray(),
            ["additionalProperties"] = false  // OLD BUG: blocks all dynamic keys
        };

        // Arrange - Build the fixed schema (what the new code produces)
        var fixedSchema = new System.Text.Json.Nodes.JsonObject
        {
            ["type"] = "object",
            ["additionalProperties"] = true  // FIX: allows dynamic keys
        };

        // Document the difference
        _output.WriteLine("=== OLD (broken) entities schema ===");
        _output.WriteLine(JsonSerializer.Serialize(brokenSchema, new JsonSerializerOptions { WriteIndented = true }));
        _output.WriteLine("\n=== NEW (fixed) entities schema ===");
        _output.WriteLine(JsonSerializer.Serialize(fixedSchema, new JsonSerializerOptions { WriteIndented = true }));

        // Act - Generate the actual schema from IntentAnalysis
        var schema = JsonSchemaGenerator.BuildOutputArraySchemaFrom<IntentAnalysis>();
        var schemaJson = JsonSerializer.Serialize(schema, new JsonSerializerOptions { WriteIndented = true });

        _output.WriteLine("\n=== Actual generated schema ===");
        _output.WriteLine(schemaJson);

        var root = JsonDocument.Parse(schemaJson).RootElement;
        var entitiesSchema = root.GetProperty("properties")
            .GetProperty("output")
            .GetProperty("items")
            .GetProperty("properties")
            .GetProperty("entities");

        var parametersSchema = root.GetProperty("properties")
            .GetProperty("output")
            .GetProperty("items")
            .GetProperty("properties")
            .GetProperty("parameters");

        // Assert - Both entities and parameters must allow additional properties
        Assert.Equal("object", entitiesSchema.GetProperty("type").GetString());
        Assert.True(entitiesSchema.GetProperty("additionalProperties").GetBoolean(),
            "entities schema must have additionalProperties: true — without this, " +
            "OpenAI structured output returns empty {} and all dynamic keys are lost");

        Assert.Equal("object", parametersSchema.GetProperty("type").GetString());
        Assert.True(parametersSchema.GetProperty("additionalProperties").GetBoolean(),
            "parameters schema must have additionalProperties: true — without this, " +
            "OpenAI structured output returns empty {} and all dynamic keys are lost");

        // Assert - The schema must NOT have empty "properties"/{} and "required"/[] fields
        // Those combined with additionalProperties:false is what caused the bug
        Assert.False(entitiesSchema.TryGetProperty("properties", out _),
            "entities schema should not have an empty 'properties' field — it's a dynamic dictionary");
        Assert.False(entitiesSchema.TryGetProperty("required", out _),
            "entities schema should not have an empty 'required' field — it's a dynamic dictionary");
    }

    /// <summary>
    /// End-to-end regression test: proves that dynamic entity keys like "kilometer"
    /// survive the full pipeline: schema generation → OpenAI response → deserialization → GetEntity.
    ///
    /// This was the originally reported bug — msg.kilometer didn't work because
    /// the schema told OpenAI not to include any dynamic properties.
    /// </summary>
    [Fact]
    public void Regression_DynamicEntityKeys_SurviveFullPipeline()
    {
        // Step 1: Verify the schema permits dynamic keys
        var schema = JsonSchemaGenerator.BuildOutputArraySchemaFrom<IntentAnalysis>();
        var schemaJson = JsonSerializer.Serialize(schema, new JsonSerializerOptions { WriteIndented = true });
        _output.WriteLine("Step 1 — Generated schema sent to OpenAI:");
        _output.WriteLine(schemaJson);

        var entitiesSchema = JsonDocument.Parse(schemaJson).RootElement
            .GetProperty("properties").GetProperty("output")
            .GetProperty("items").GetProperty("properties")
            .GetProperty("entities");

        Assert.True(entitiesSchema.GetProperty("additionalProperties").GetBoolean(),
            "Schema must allow additional properties for OpenAI to populate entities");

        // Step 2: Simulate what OpenAI would return with the fixed schema
        // (with the old schema, OpenAI would have returned "entities": {} here)
        var openAiResponse = """
        {
            "output": [
                {
                    "intent": "convert_distance",
                    "entities": {
                        "kilometer": 100,
                        "targetUnit": "miles",
                        "sourceCountry": "Denmark"
                    },
                    "confidence": 0.95,
                    "parameters": {
                        "precision": 2
                    }
                }
            ]
        }
        """;
        _output.WriteLine("\nStep 2 — Simulated OpenAI response:");
        _output.WriteLine(openAiResponse);

        // Step 3: Deserialize (same code path as CompleteAndDeserialize in production)
        using var doc = JsonDocument.Parse(openAiResponse);
        var outputArray = doc.RootElement.GetProperty("output");
        var firstItem = outputArray[0];
        var result = JsonSerializer.Deserialize<IntentAnalysis>(firstItem);

        Assert.NotNull(result);
        _output.WriteLine($"\nStep 3 — Deserialized IntentAnalysis:");
        _output.WriteLine($"  Intent: {result!.Intent}");
        _output.WriteLine($"  Confidence: {result.Confidence}");
        _output.WriteLine($"  Entities count: {result.Entities?.Count}");

        // Step 4: Verify all dynamic entity keys are accessible via GetEntity<T>
        // This is the exact scenario from the bug report — "msg.kilometer doesn't work"
        Assert.Equal("convert_distance", result.Intent);
        Assert.Equal(0.95, result.Confidence);

        var kilometer = result.GetEntity<int>("kilometer");
        Assert.Equal(100, kilometer);
        _output.WriteLine($"  GetEntity<int>(\"kilometer\"): {kilometer} ✓");

        var targetUnit = result.GetEntity<string>("targetUnit");
        Assert.Equal("miles", targetUnit);
        _output.WriteLine($"  GetEntity<string>(\"targetUnit\"): {targetUnit} ✓");

        var sourceCountry = result.GetEntity<string>("sourceCountry");
        Assert.Equal("Denmark", sourceCountry);
        _output.WriteLine($"  GetEntity<string>(\"sourceCountry\"): {sourceCountry} ✓");

        var precision = result.GetParameter<int>("precision");
        Assert.Equal(2, precision);
        _output.WriteLine($"  GetParameter<int>(\"precision\"): {precision} ✓");

        _output.WriteLine("\n✅ All dynamic entity keys accessible — fix verified");
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
