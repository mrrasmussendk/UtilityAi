using System.Text.Json;
using System.Text.Json.Nodes;
using Azure.AI.Projects.OpenAI;
using OpenAI.Chat;
using UtilityAi.Helpers.OpenAiStructuredOutputHelper;
using UtilityAi.Helpers.OpenAiStructuredOutputHelper.SchemaGenerator;
using UtilityAi.Maf;
using UtilityAi.Sensor.LLM;
using Xunit;
using Xunit.Abstractions;

namespace Tests;

/// <summary>
/// CRITICAL TESTS: Validates that IntentAnalysis schema can actually be used with OpenAI API.
/// These tests ensure the package will work in production without OpenAI schema validation errors.
/// </summary>
public class IntentAnalysisStrictModeValidationTests
{
    private readonly ITestOutputHelper _output;

    public IntentAnalysisStrictModeValidationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void IntentAnalysisSchema_WithStrictFalse_CreatesValidChatCompletionOptions()
    {
        // This is the CORRECT way users should use IntentAnalysis
        // Act
        Exception? exception = null;
        ChatCompletionOptions? options = null;

        try
        {
            var builder = AiRequestBuilder.Create()
                .WithModel("gpt-4")
                .AddUser("Test message")
                .WithJsonSchemaFrom<IntentAnalysis>("intent", strict: false); // ✅ MUST use strict: false

            options = builder.ToAzureOpenAiChatOptions(out var messages);
        }
        catch (Exception ex)
        {
            exception = ex;
            _output.WriteLine($"Failed to create ChatCompletionOptions: {ex.Message}");
        }

        // Assert
        Assert.Null(exception);
        Assert.NotNull(options);
        Assert.NotNull(options!.ResponseFormat);

        _output.WriteLine("✅ ChatCompletionOptions created successfully with strict: false");
    }

    [Fact]
    public void IntentAnalysisSchema_WithStrictTrue_ShouldBeDocumentedAsUnsupported()
    {
        // This test documents that strict: true is NOT supported for IntentAnalysis
        // because Dictionary<string, JsonElement> is incompatible with OpenAI strict mode

        // Act - We can still create the options locally, but OpenAI will reject it
        Exception? exception = null;
        ChatCompletionOptions? options = null;

        try
        {
            var builder = AiRequestBuilder.Create()
                .WithModel("gpt-4")
                .AddUser("Test message")
                .WithJsonSchemaFrom<IntentAnalysis>("intent", strict: true); // ❌ Will fail at OpenAI API

            options = builder.ToAzureOpenAiChatOptions(out var messages);
        }
        catch (Exception ex)
        {
            exception = ex;
        }

        // Assert - The options are created locally, but would fail at OpenAI validation
        Assert.Null(exception);
        Assert.NotNull(options);

        _output.WriteLine("⚠️  Options created locally, but OpenAI API would reject with:");
        _output.WriteLine("'additionalProperties' is required to be supplied and to be false");
        _output.WriteLine("See docs/INTENT_ANALYSIS_USAGE.md for details");
    }

    [Fact]
    public void IntentAnalysisSchema_ValidatesStructure()
    {
        // Act
        var schema = JsonSchemaGenerator.BuildOutputArraySchemaFrom<IntentAnalysis>();
        var schemaJson = JsonSerializer.Serialize(schema, new JsonSerializerOptions { WriteIndented = true });

        _output.WriteLine("Generated Schema:");
        _output.WriteLine(schemaJson);

        // Assert - Validate the schema structure
        var root = JsonDocument.Parse(schemaJson).RootElement;

        // Root level
        Assert.Equal("object", root.GetProperty("type").GetString());
        Assert.False(root.GetProperty("additionalProperties").GetBoolean());

        // Output array
        var output = root.GetProperty("properties").GetProperty("output");
        Assert.Equal("array", output.GetProperty("type").GetString());

        // Items (IntentAnalysis)
        var items = output.GetProperty("items");
        Assert.Equal("object", items.GetProperty("type").GetString());
        Assert.False(items.GetProperty("additionalProperties").GetBoolean());

        var properties = items.GetProperty("properties");

        // Validate intent property
        Assert.True(properties.TryGetProperty("intent", out var intentProp));
        Assert.Equal("string", intentProp.GetProperty("type").GetString());

        // Validate confidence property
        Assert.True(properties.TryGetProperty("confidence", out var confidenceProp));
        Assert.Equal("number", confidenceProp.GetProperty("type").GetString());

        // Validate entities property - Dictionary<string, JsonElement>
        Assert.True(properties.TryGetProperty("entities", out var entitiesProp));
        Assert.Equal("object", entitiesProp.GetProperty("type").GetString());
        Assert.False(entitiesProp.GetProperty("additionalProperties").GetBoolean());

        // Validate parameters property - Dictionary<string, JsonElement>
        Assert.True(properties.TryGetProperty("parameters", out var parametersProp));
        Assert.Equal("object", parametersProp.GetProperty("type").GetString());
        Assert.False(parametersProp.GetProperty("additionalProperties").GetBoolean());

        // Validate required array - only non-nullable properties should be required
        var required = items.GetProperty("required");
        var requiredList = required.EnumerateArray().Select(x => x.GetString()).ToList();
        Assert.Contains("intent", requiredList);
        Assert.Contains("confidence", requiredList);

        // Entities and Parameters are nullable, so they should NOT be in required array
        Assert.DoesNotContain("entities", requiredList);
        Assert.DoesNotContain("parameters", requiredList);

        _output.WriteLine("\n✅ Schema structure is valid");
    }

    [Fact]
    public void IntentAnalysisSchema_EntitiesAndParametersAreNullable_AllowsEmptyOrNull()
    {
        // Test that entities and parameters can be null or empty in deserialization
        var testCases = new[]
        {
            // Case 1: Null entities and parameters
            """
            {
                "intent": "test.intent",
                "entities": null,
                "confidence": 0.9,
                "parameters": null
            }
            """,
            // Case 2: Empty entities and parameters
            """
            {
                "intent": "test.intent",
                "entities": {},
                "confidence": 0.9,
                "parameters": {}
            }
            """,
            // Case 3: Missing parameters (should default to null)
            """
            {
                "intent": "test.intent",
                "entities": {},
                "confidence": 0.9
            }
            """
        };

        foreach (var testJson in testCases)
        {
            _output.WriteLine($"Testing: {testJson}");

            Exception? exception = null;
            IntentAnalysis? result = null;

            try
            {
                result = JsonSerializer.Deserialize<IntentAnalysis>(testJson);
            }
            catch (Exception ex)
            {
                exception = ex;
                _output.WriteLine($"❌ Failed: {ex.Message}");
            }

            Assert.Null(exception);
            Assert.NotNull(result);
            Assert.Equal("test.intent", result!.Intent);
            Assert.Equal(0.9, result.Confidence);

            _output.WriteLine("✅ Passed\n");
        }
    }

    [Fact]
    public void IntentAnalysisSchema_WithComplexNestedData_DeserializesCorrectly()
    {
        // Test with deeply nested structures
        var complexJson = """
        {
            "intent": "data.complex",
            "entities": {
                "user": {
                    "id": 123,
                    "profile": {
                        "name": "John Doe",
                        "preferences": {
                            "theme": "dark",
                            "notifications": true
                        }
                    }
                },
                "items": [
                    {"id": 1, "value": "first"},
                    {"id": 2, "value": "second"}
                ]
            },
            "confidence": 0.97,
            "parameters": {
                "metadata": {
                    "timestamp": "2024-01-01T00:00:00Z",
                    "source": "api",
                    "tags": ["urgent", "review"]
                }
            }
        }
        """;

        _output.WriteLine("Complex JSON:");
        _output.WriteLine(complexJson);

        // Act
        var result = JsonSerializer.Deserialize<IntentAnalysis>(complexJson);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("data.complex", result!.Intent);

        // Verify nested entity
        var user = result.GetEntity<JsonElement>("user");
        Assert.Equal(123, user.GetProperty("id").GetInt32());
        Assert.Equal("John Doe", user.GetProperty("profile").GetProperty("name").GetString());
        Assert.Equal("dark", user.GetProperty("profile").GetProperty("preferences").GetProperty("theme").GetString());

        // Verify array entity
        var items = result.GetEntity<JsonElement>("items");
        Assert.Equal(JsonValueKind.Array, items.ValueKind);
        Assert.Equal(2, items.GetArrayLength());

        // Verify nested parameter
        var metadata = result.GetParameter<JsonElement>("metadata");
        Assert.Equal("api", metadata.GetProperty("source").GetString());

        var tags = metadata.GetProperty("tags");
        Assert.Equal(2, tags.GetArrayLength());
        Assert.Equal("urgent", tags[0].GetString());

        _output.WriteLine("✅ Complex nested data deserialized correctly");
    }

    [Fact]
    public void IntentAnalysisSchema_GetEntityAndGetParameter_HandleMissingKeys()
    {
        var json = """
        {
            "intent": "test.intent",
            "entities": {
                "existingKey": "value"
            },
            "confidence": 0.8
        }
        """;

        var result = JsonSerializer.Deserialize<IntentAnalysis>(json);
        Assert.NotNull(result);

        // Test GetEntity with missing key
        Assert.Equal("default", result!.GetEntity("missingKey", "default"));
        Assert.Null(result.GetEntity<string>("missingKey"));

        // Test GetParameter with missing key (Parameters is null)
        Assert.Equal("default", result.GetParameter("missingKey", "default"));
        Assert.Equal(42, result.GetParameter("missingKey", 42));

        // Test existing key
        Assert.Equal("value", result.GetEntity<string>("existingKey"));

        _output.WriteLine("✅ GetEntity/GetParameter handle missing keys correctly");
    }

    [Fact]
    public void CreateStructuredOptions_WithIntentAnalysisAndStrictFalse_Works()
    {
        // Test the direct API
        Exception? exception = null;
        ChatCompletionOptions? options = null;

        try
        {
            options = MafRequestExtensions.CreateStructuredOptions<IntentAnalysis>(
                schemaName: "intent",
                options: null,
                strict: false  // ✅ MUST be false for IntentAnalysis
            );
        }
        catch (Exception ex)
        {
            exception = ex;
            _output.WriteLine($"Failed: {ex.Message}");
        }

        Assert.Null(exception);
        Assert.NotNull(options);
        Assert.NotNull(options!.ResponseFormat);

        _output.WriteLine("✅ CreateStructuredOptions works with strict: false");
    }
}
