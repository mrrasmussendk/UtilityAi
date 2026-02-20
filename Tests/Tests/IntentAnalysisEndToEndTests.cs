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
using Xunit.Abstractions;

namespace Tests;

/// <summary>
/// End-to-end tests that simulate the complete user flow from schema generation to EventBus usage
/// </summary>
public class IntentAnalysisEndToEndTests
{
    private readonly ITestOutputHelper _output;

    public IntentAnalysisEndToEndTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void EndToEnd_SchemaGeneration_ProducesValidOpenAiSchema()
    {
        // This is what users will call to generate the schema
        var schema = JsonSchemaGenerator.BuildOutputArraySchemaFrom<IntentAnalysis>(new SchemaGeneratorOptions
        {
            RequiredStrategy = RequiredStrategy.AllProperties,
            AdditionalPropertiesForItem = false
        });

        var schemaJson = JsonSerializer.Serialize(schema, new JsonSerializerOptions { WriteIndented = true });
        _output.WriteLine("Generated Schema:");
        _output.WriteLine(schemaJson);

        // Verify the schema structure matches OpenAI expectations
        var root = JsonDocument.Parse(schemaJson).RootElement;

        // Check root structure
        Assert.Equal("object", root.GetProperty("type").GetString());
        Assert.True(root.GetProperty("properties").TryGetProperty("output", out var output));
        Assert.Equal("array", output.GetProperty("type").GetString());

        // Check IntentAnalysis structure
        var items = output.GetProperty("items");
        Assert.Equal("object", items.GetProperty("type").GetString());

        var properties = items.GetProperty("properties");

        // Entities should be an object (empty with additionalProperties: false for strict mode compliance)
        Assert.True(properties.TryGetProperty("entities", out var entities));
        Assert.Equal("object", entities.GetProperty("type").GetString());
        Assert.False(entities.GetProperty("additionalProperties").GetBoolean());

        // Parameters should be an object (empty with additionalProperties: false for strict mode compliance)
        Assert.True(properties.TryGetProperty("parameters", out var parameters));
        Assert.Equal("object", parameters.GetProperty("type").GetString());
        Assert.False(parameters.GetProperty("additionalProperties").GetBoolean());

        _output.WriteLine("\n✓ Schema structure is correct for OpenAI");
    }

    [Fact]
    public void EndToEnd_OpenAiResponse_DeserializesCorrectly()
    {
        // Simulate what OpenAI returns
        var openAiResponse = """
        {
            "output": [
                {
                    "intent": "ticket.create",
                    "entities": {
                        "issueType": "bug",
                        "priority": "high",
                        "component": "payment-gateway"
                    },
                    "confidence": 0.96,
                    "parameters": {
                        "urgency": 0.9,
                        "customerTier": "enterprise",
                        "requiresEscalation": true
                    }
                }
            ]
        }
        """;

        _output.WriteLine("Simulated OpenAI Response:");
        _output.WriteLine(openAiResponse);

        // Deserialize using the output array
        using var doc = JsonDocument.Parse(openAiResponse);
        var outputArray = doc.RootElement.GetProperty("output");
        var result = JsonSerializer.Deserialize<IntentAnalysis>(outputArray[0]);

        Assert.NotNull(result);
        Assert.Equal("ticket.create", result!.Intent);
        Assert.Equal(0.96, result.Confidence);
        Assert.Equal(3, result.Entities.Count);
        Assert.Equal(3, result.Parameters!.Count);

        // Verify entity extraction
        Assert.Equal("bug", result.GetEntity<string>("issueType"));
        Assert.Equal("high", result.GetEntity<string>("priority"));
        Assert.Equal("payment-gateway", result.GetEntity<string>("component"));

        // Verify parameter extraction
        Assert.Equal(0.9, result.GetParameter<double>("urgency"));
        Assert.Equal("enterprise", result.GetParameter<string>("customerTier"));
        Assert.True(result.GetParameter<bool>("requiresEscalation"));

        _output.WriteLine("\n✓ Deserialization successful");
        _output.WriteLine($"  Intent: {result.Intent}");
        _output.WriteLine($"  Confidence: {result.Confidence}");
        _output.WriteLine($"  Entities: {result.Entities.Count}");
        _output.WriteLine($"  Parameters: {result.Parameters.Count}");
    }

    [Fact]
    public void EndToEnd_CompleteFlow_WithMockChatClient()
    {
        // Simulate the complete user flow
        var openAiResponse = """
        {
            "output": [
                {
                    "intent": "order.create",
                    "entities": {
                        "product": {
                            "id": 12345,
                            "name": "Premium Widget",
                            "price": 99.99
                        },
                        "customer": {
                            "email": "customer@example.com",
                            "tier": "gold"
                        }
                    },
                    "confidence": 0.98,
                    "parameters": {
                        "quantity": 5,
                        "expressShipping": true,
                        "giftWrap": false
                    }
                }
            ]
        }
        """;

        var mockClient = new MockChatClient(openAiResponse);

        // Build the request exactly as a user would
        var builder = AiRequestBuilder.Create()
            .WithModel("gpt-4")
            .AddUser("I want to order 5 Premium Widgets with express shipping")
            .WithJsonSchemaFrom<IntentAnalysis>("intent", new SchemaGeneratorOptions
            {
                RequiredStrategy = RequiredStrategy.AllProperties
            });

        // Complete and deserialize
        var result = builder.CompleteAndDeserialize<IntentAnalysis>(mockClient);

        Assert.NotNull(result);
        Assert.Equal("order.create", result.Intent);
        Assert.Equal(0.98, result.Confidence);

        // Extract nested entities
        var product = result.GetEntity<JsonElement>("product");
        Assert.Equal(12345, product.GetProperty("id").GetInt32());
        Assert.Equal("Premium Widget", product.GetProperty("name").GetString());
        Assert.Equal(99.99, product.GetProperty("price").GetDouble());

        var customer = result.GetEntity<JsonElement>("customer");
        Assert.Equal("customer@example.com", customer.GetProperty("email").GetString());
        Assert.Equal("gold", customer.GetProperty("tier").GetString());

        // Extract parameters
        Assert.Equal(5, result.GetParameter<int>("quantity"));
        Assert.True(result.GetParameter<bool>("expressShipping"));
        Assert.False(result.GetParameter<bool>("giftWrap"));

        _output.WriteLine("\n✓ Complete flow successful");
    }

    [Fact]
    public void EndToEnd_WithEventBus_FullIntegration()
    {
        // Complete integration test with EventBus and Runtime
        var openAiResponse = """
        {
            "output": [
                {
                    "intent": "message.send",
                    "entities": {
                        "msg.response": "Hello! How can I help you today?",
                        "recipient": "user123",
                        "channel": "chat"
                    },
                    "confidence": 0.95,
                    "parameters": {
                        "priority": 0.7,
                        "requiresAck": true
                    }
                }
            ]
        }
        """;

        var mockClient = new MockChatClient(openAiResponse);
        var bus = new EventBus();
        var rt = new Runtime(bus, 0);

        // Build and execute request
        var builder = AiRequestBuilder.Create()
            .WithModel("gpt-4")
            .AddUser("Send a greeting message")
            .WithJsonSchemaFrom<IntentAnalysis>("intent");

        var result = builder.CompleteAndDeserialize<IntentAnalysis>(mockClient);

        // Publish to EventBus
        bus.Publish(result);

        // Retrieve from Runtime (simulating what a Proposal would do)
        var intentFromBus = rt.Bus.GetOrDefault<IntentAnalysis>();
        Assert.NotNull(intentFromBus);

        // Use the intent like a real Proposal would
        var response = intentFromBus!.GetEntity("msg.response", "");
        Assert.Equal("Hello! How can I help you today?", response);

        Assert.True(intentFromBus.ParameterAbove("priority", 0.5));
        Assert.Equal(true, intentFromBus.GetParameter<bool>("requiresAck"));

        _output.WriteLine("\n✓ EventBus integration successful");
        _output.WriteLine($"  Retrieved from bus: {intentFromBus.Intent}");
        _output.WriteLine($"  Response message: {response}");
    }

    [Fact]
    public void EndToEnd_EmptyEntitiesAndParameters_HandlesGracefully()
    {
        // Test with minimal response
        var openAiResponse = """
        {
            "output": [
                {
                    "intent": "unknown",
                    "entities": {},
                    "confidence": 0.3
                }
            ]
        }
        """;

        var mockClient = new MockChatClient(openAiResponse);

        var builder = AiRequestBuilder.Create()
            .WithModel("gpt-4")
            .AddUser("asdfghjkl")
            .WithJsonSchemaFrom<IntentAnalysis>("intent");

        var result = builder.CompleteAndDeserialize<IntentAnalysis>(mockClient);

        Assert.NotNull(result);
        Assert.Equal("unknown", result.Intent);
        Assert.Equal(0.3, result.Confidence);
        Assert.Empty(result.Entities);
        Assert.Null(result.Parameters);

        // Verify GetEntity/GetParameter return defaults for missing values
        Assert.Equal("default", result.GetEntity("missing", "default"));
        Assert.Equal(99, result.GetParameter("missing", 99));

        _output.WriteLine("\n✓ Empty entities/parameters handled correctly");
    }

    [Fact]
    public void EndToEnd_ComplexNestedStructures_DeserializeCorrectly()
    {
        var openAiResponse = """
        {
            "output": [
                {
                    "intent": "data.analyze",
                    "entities": {
                        "dataset": {
                            "name": "sales_2024",
                            "records": [
                                {"id": 1, "amount": 150.50},
                                {"id": 2, "amount": 275.00},
                                {"id": 3, "amount": 89.99}
                            ],
                            "metadata": {
                                "source": "api",
                                "validated": true
                            }
                        }
                    },
                    "confidence": 0.92,
                    "parameters": {
                        "analysisType": "summary",
                        "filters": {
                            "dateRange": "2024-01-01,2024-12-31",
                            "minAmount": 50.0
                        },
                        "outputFormats": ["json", "csv"]
                    }
                }
            ]
        }
        """;

        var mockClient = new MockChatClient(openAiResponse);

        var builder = AiRequestBuilder.Create()
            .WithModel("gpt-4")
            .AddUser("Analyze sales data")
            .WithJsonSchemaFrom<IntentAnalysis>("intent");

        var result = builder.CompleteAndDeserialize<IntentAnalysis>(mockClient);

        Assert.NotNull(result);
        Assert.Equal("data.analyze", result.Intent);

        // Navigate complex nested structure
        var dataset = result.GetEntity<JsonElement>("dataset");
        Assert.Equal("sales_2024", dataset.GetProperty("name").GetString());

        var records = dataset.GetProperty("records");
        Assert.Equal(JsonValueKind.Array, records.ValueKind);
        Assert.Equal(3, records.GetArrayLength());
        Assert.Equal(150.50, records[0].GetProperty("amount").GetDouble());

        var metadata = dataset.GetProperty("metadata");
        Assert.True(metadata.GetProperty("validated").GetBoolean());

        // Check nested parameters
        var filters = result.GetParameter<JsonElement>("filters");
        Assert.Equal(50.0, filters.GetProperty("minAmount").GetDouble());

        var formats = result.GetParameter<JsonElement>("outputFormats");
        Assert.Equal(2, formats.GetArrayLength());
        Assert.Equal("json", formats[0].GetString());

        _output.WriteLine("\n✓ Complex nested structures handled correctly");
    }

    [Fact]
    public void EndToEnd_SpecialCharactersInKeys_HandlesCorrectly()
    {
        var openAiResponse = """
        {
            "output": [
                {
                    "intent": "test.special",
                    "entities": {
                        "msg.response": "Hello",
                        "user.email": "test@example.com",
                        "data-value": 123,
                        "some_key": "underscore"
                    },
                    "confidence": 0.9,
                    "parameters": {
                        "api-key": "secret123",
                        "max_retries": 3
                    }
                }
            ]
        }
        """;

        var mockClient = new MockChatClient(openAiResponse);

        var builder = AiRequestBuilder.Create()
            .WithModel("gpt-4")
            .AddUser("Test")
            .WithJsonSchemaFrom<IntentAnalysis>("intent");

        var result = builder.CompleteAndDeserialize<IntentAnalysis>(mockClient);

        Assert.NotNull(result);

        // Verify special characters in keys work
        Assert.Equal("Hello", result.GetEntity<string>("msg.response"));
        Assert.Equal("test@example.com", result.GetEntity<string>("user.email"));
        Assert.Equal(123, result.GetEntity<int>("data-value"));
        Assert.Equal("underscore", result.GetEntity<string>("some_key"));

        Assert.Equal("secret123", result.GetParameter<string>("api-key"));
        Assert.Equal(3, result.GetParameter<int>("max_retries"));

        _output.WriteLine("\n✓ Special characters in keys handled correctly");
    }

    [Fact]
    public void EndToEnd_NullAndMissingValues_HandlesGracefully()
    {
        var openAiResponse = """
        {
            "output": [
                {
                    "intent": "test.nulls",
                    "entities": {
                        "validKey": "value",
                        "nullKey": null
                    },
                    "confidence": 0.8,
                    "parameters": {
                        "validParam": 42
                    }
                }
            ]
        }
        """;

        var mockClient = new MockChatClient(openAiResponse);

        var builder = AiRequestBuilder.Create()
            .WithModel("gpt-4")
            .AddUser("Test nulls")
            .WithJsonSchemaFrom<IntentAnalysis>("intent");

        var result = builder.CompleteAndDeserialize<IntentAnalysis>(mockClient);

        Assert.NotNull(result);

        // Valid values work
        Assert.Equal("value", result.GetEntity<string>("validKey"));
        Assert.Equal(42, result.GetParameter<int>("validParam"));

        // Null/missing values return defaults
        Assert.Null(result.GetEntity<string>("nullKey"));
        Assert.Equal("default", result.GetEntity("missingKey", "default"));
        Assert.Equal(0, result.GetParameter<int>("missingParam"));

        _output.WriteLine("\n✓ Null and missing values handled correctly");
    }
}
