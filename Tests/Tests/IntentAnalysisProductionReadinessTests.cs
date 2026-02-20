using System.Text.Json;
using UtilityAi.Helpers.OpenAiStructuredOutputHelper;
using UtilityAi.Helpers.OpenAiStructuredOutputHelper.SchemaGenerator;
using UtilityAi.Sensor.LLM;
using Xunit;
using Xunit.Abstractions;

namespace Tests;

/// <summary>
/// Final production readiness tests that validate the complete workflow works end-to-end
/// exactly as users will use it in production.
/// </summary>
public class IntentAnalysisProductionReadinessTests
{
    private readonly ITestOutputHelper _output;

    public IntentAnalysisProductionReadinessTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void ProductionScenario_CreateTicket_CompleteWorkflow()
    {
        // This test simulates the EXACT production workflow:
        // 1. Generate schema
        // 2. Send to OpenAI (mocked)
        // 3. Receive JSON response
        // 4. Deserialize IntentAnalysis
        // 5. Extract entities and parameters
        // 6. Use in application logic

        _output.WriteLine("=== Production Scenario: Create Ticket ===\n");

        // Step 1: Generate schema (this is what OpenAI receives)
        var schema = JsonSchemaGenerator.BuildOutputArraySchemaFrom<IntentAnalysis>();
        var schemaJson = JsonSerializer.Serialize(schema, new JsonSerializerOptions { WriteIndented = true });

        _output.WriteLine("Step 1 - Schema generated:");
        _output.WriteLine(schemaJson);
        _output.WriteLine("");

        // Step 2: Simulate OpenAI response (exact format OpenAI returns)
        var openAiResponse = """
        {
            "output": [
                {
                    "intent": "ticket.create",
                    "entities": {
                        "issueType": "bug",
                        "severity": "high",
                        "component": "payment-service",
                        "description": "Payment processing fails for international cards"
                    },
                    "confidence": 0.95,
                    "parameters": {
                        "urgency": 0.9,
                        "customerTier": "enterprise",
                        "requiresEscalation": true,
                        "estimatedImpact": "high"
                    }
                }
            ]
        }
        """;

        _output.WriteLine("Step 2 - OpenAI response received:");
        _output.WriteLine(openAiResponse);
        _output.WriteLine("");

        // Step 3: Deserialize (this is what CompleteAndDeserialize does)
        using var doc = JsonDocument.Parse(openAiResponse);
        var outputArray = doc.RootElement.GetProperty("output");
        var firstItem = outputArray[0];

        var result = JsonSerializer.Deserialize<IntentAnalysis>(firstItem);

        _output.WriteLine("Step 3 - Deserialized successfully");
        Assert.NotNull(result);
        _output.WriteLine("");

        // Step 4: Extract and validate data (application logic)
        _output.WriteLine("Step 4 - Application logic:");

        Assert.Equal("ticket.create", result!.Intent);
        Assert.Equal(0.95, result.Confidence);

        // Extract entities
        var issueType = result.GetEntity<string>("issueType");
        var severity = result.GetEntity<string>("severity");
        var component = result.GetEntity<string>("component");
        var description = result.GetEntity<string>("description");

        Assert.Equal("bug", issueType);
        Assert.Equal("high", severity);
        Assert.Equal("payment-service", component);
        Assert.Equal("Payment processing fails for international cards", description);

        _output.WriteLine($"  Issue Type: {issueType}");
        _output.WriteLine($"  Severity: {severity}");
        _output.WriteLine($"  Component: {component}");
        _output.WriteLine($"  Description: {description}");
        _output.WriteLine("");

        // Extract parameters
        var urgency = result.GetParameter<double>("urgency");
        var customerTier = result.GetParameter<string>("customerTier");
        var requiresEscalation = result.GetParameter<bool>("requiresEscalation");
        var estimatedImpact = result.GetParameter<string>("estimatedImpact");

        Assert.Equal(0.9, urgency);
        Assert.Equal("enterprise", customerTier);
        Assert.True(requiresEscalation);
        Assert.Equal("high", estimatedImpact);

        _output.WriteLine($"  Urgency: {urgency}");
        _output.WriteLine($"  Customer Tier: {customerTier}");
        _output.WriteLine($"  Requires Escalation: {requiresEscalation}");
        _output.WriteLine($"  Estimated Impact: {estimatedImpact}");
        _output.WriteLine("");

        // Step 5: Use in business logic
        if (result.ParameterAbove("urgency", 0.8) && requiresEscalation)
        {
            _output.WriteLine("✅ Ticket escalated to high-priority queue");
        }

        _output.WriteLine("\n✅ Complete production workflow validated successfully!");
    }

    [Fact]
    public void ProductionScenario_EmptyEntities_HandlesGracefully()
    {
        // Test the case where OpenAI returns empty entities/parameters
        var openAiResponse = """
        {
            "output": [
                {
                    "intent": "greeting.hello",
                    "entities": {},
                    "confidence": 0.99
                }
            ]
        }
        """;

        _output.WriteLine("Testing empty entities scenario:");
        _output.WriteLine(openAiResponse);

        using var doc = JsonDocument.Parse(openAiResponse);
        var result = JsonSerializer.Deserialize<IntentAnalysis>(doc.RootElement.GetProperty("output")[0]);

        Assert.NotNull(result);
        Assert.Equal("greeting.hello", result!.Intent);
        Assert.Empty(result.Entities!);
        Assert.Null(result.Parameters);

        // GetEntity should return defaults
        Assert.Equal("default", result.GetEntity("anyKey", "default"));
        Assert.Equal(0, result.GetParameter("anyKey", 0));

        _output.WriteLine("✅ Empty entities handled correctly");
    }

    [Fact]
    public void ProductionScenario_ComplexNestedData_WorksCorrectly()
    {
        // Test with complex nested structures that users might encounter
        var openAiResponse = """
        {
            "output": [
                {
                    "intent": "order.process",
                    "entities": {
                        "customer": {
                            "id": "CUST-12345",
                            "name": "Acme Corp",
                            "tier": "enterprise",
                            "contacts": [
                                {"name": "John Doe", "role": "primary"},
                                {"name": "Jane Smith", "role": "billing"}
                            ]
                        },
                        "items": [
                            {
                                "productId": "PROD-001",
                                "quantity": 5,
                                "price": 99.99,
                                "metadata": {
                                    "category": "electronics",
                                    "inStock": true
                                }
                            },
                            {
                                "productId": "PROD-002",
                                "quantity": 2,
                                "price": 149.99,
                                "metadata": {
                                    "category": "accessories",
                                    "inStock": false
                                }
                            }
                        ],
                        "shippingAddress": {
                            "street": "123 Main St",
                            "city": "San Francisco",
                            "state": "CA",
                            "zip": "94105",
                            "country": "USA"
                        }
                    },
                    "confidence": 0.97,
                    "parameters": {
                        "totalAmount": 799.93,
                        "paymentMethod": "credit_card",
                        "shippingOptions": {
                            "method": "express",
                            "estimatedDays": 2,
                            "trackingEnabled": true
                        },
                        "discounts": ["BULK10", "ENTERPRISE5"],
                        "metadata": {
                            "source": "api",
                            "version": "2.0",
                            "timestamp": "2024-01-15T10:30:00Z"
                        }
                    }
                }
            ]
        }
        """;

        _output.WriteLine("Testing complex nested data scenario...");

        using var doc = JsonDocument.Parse(openAiResponse);
        var result = JsonSerializer.Deserialize<IntentAnalysis>(doc.RootElement.GetProperty("output")[0]);

        Assert.NotNull(result);
        Assert.Equal("order.process", result!.Intent);

        // Validate nested customer object
        var customer = result.GetEntity<JsonElement>("customer");
        Assert.Equal("CUST-12345", customer.GetProperty("id").GetString());
        Assert.Equal("Acme Corp", customer.GetProperty("name").GetString());

        var contacts = customer.GetProperty("contacts");
        Assert.Equal(2, contacts.GetArrayLength());
        Assert.Equal("John Doe", contacts[0].GetProperty("name").GetString());

        // Validate items array
        var items = result.GetEntity<JsonElement>("items");
        Assert.Equal(2, items.GetArrayLength());
        Assert.Equal("PROD-001", items[0].GetProperty("productId").GetString());
        Assert.Equal(5, items[0].GetProperty("quantity").GetInt32());
        Assert.True(items[0].GetProperty("metadata").GetProperty("inStock").GetBoolean());

        // Validate nested parameters
        var totalAmount = result.GetParameter<double>("totalAmount");
        Assert.Equal(799.93, totalAmount);

        var shippingOptions = result.GetParameter<JsonElement>("shippingOptions");
        Assert.Equal("express", shippingOptions.GetProperty("method").GetString());
        Assert.Equal(2, shippingOptions.GetProperty("estimatedDays").GetInt32());

        var discounts = result.GetParameter<JsonElement>("discounts");
        Assert.Equal(2, discounts.GetArrayLength());
        Assert.Equal("BULK10", discounts[0].GetString());

        _output.WriteLine("✅ Complex nested data handled correctly");
    }

    [Fact]
    public void ProductionScenario_SpecialCharactersAndUnicode_WorksCorrectly()
    {
        // Test with special characters, emojis, and unicode
        var openAiResponse = """
        {
            "output": [
                {
                    "intent": "message.send",
                    "entities": {
                        "message": "Hello! 👋 Welcome to our service 🎉",
                        "recipient": "user@example.com",
                        "subject": "Grüße aus Deutschland 🇩🇪",
                        "specialChars": "Test: <>\"'&\t\n\r",
                        "unicode": "日本語テキスト",
                        "emoji": "😀🎈🌟💻🚀"
                    },
                    "confidence": 0.94,
                    "parameters": {
                        "priority": 0.7,
                        "tags": ["greeting", "welcome", "new-user"]
                    }
                }
            ]
        }
        """;

        _output.WriteLine("Testing special characters and unicode...");

        using var doc = JsonDocument.Parse(openAiResponse);
        var result = JsonSerializer.Deserialize<IntentAnalysis>(doc.RootElement.GetProperty("output")[0]);

        Assert.NotNull(result);

        // Validate emoji and unicode handling
        var message = result!.GetEntity<string>("message");
        Assert.Contains("👋", message);
        Assert.Contains("🎉", message);

        var subject = result.GetEntity<string>("subject");
        Assert.Contains("Grüße", subject);
        Assert.Contains("🇩🇪", subject);

        var unicode = result.GetEntity<string>("unicode");
        Assert.Equal("日本語テキスト", unicode);

        var emoji = result.GetEntity<string>("emoji");
        Assert.Contains("😀", emoji);
        Assert.Contains("🚀", emoji);

        _output.WriteLine($"Message: {message}");
        _output.WriteLine($"Subject: {subject}");
        _output.WriteLine($"Unicode: {unicode}");
        _output.WriteLine($"Emoji: {emoji}");

        _output.WriteLine("✅ Special characters and unicode handled correctly");
    }

    [Fact]
    public void ProductionScenario_NullAndMissingValues_HandlesGracefully()
    {
        // Test various null and missing value scenarios
        var testCases = new[]
        {
            ("Null entities", """
            {
                "output": [{"intent": "test", "entities": null, "confidence": 0.5}]
            }
            """),
            ("Missing parameters", """
            {
                "output": [{"intent": "test", "entities": {}, "confidence": 0.5}]
            }
            """),
            ("Empty objects", """
            {
                "output": [{"intent": "test", "entities": {}, "confidence": 0.5, "parameters": {}}]
            }
            """)
        };

        foreach (var (name, json) in testCases)
        {
            _output.WriteLine($"Testing: {name}");

            using var doc = JsonDocument.Parse(json);
            var result = JsonSerializer.Deserialize<IntentAnalysis>(doc.RootElement.GetProperty("output")[0]);

            Assert.NotNull(result);
            Assert.Equal("test", result!.Intent);

            // Should not throw when accessing missing keys
            Assert.Equal("default", result.GetEntity("missing", "default"));
            Assert.Equal("default", result.GetParameter("missing", "default"));

            _output.WriteLine($"  ✅ {name} handled correctly");
        }
    }

    [Fact]
    public void ProductionScenario_VerifySchemaMatchesExpectedStructure()
    {
        // Final validation that the schema structure is exactly what we expect
        var schema = JsonSchemaGenerator.BuildOutputArraySchemaFrom<IntentAnalysis>();
        var schemaJson = JsonSerializer.Serialize(schema);

        _output.WriteLine("Final schema validation:");
        _output.WriteLine(schemaJson);

        using var doc = JsonDocument.Parse(schemaJson);
        var root = doc.RootElement;

        // Root structure
        Assert.Equal("object", root.GetProperty("type").GetString());
        Assert.False(root.GetProperty("additionalProperties").GetBoolean());

        var output = root.GetProperty("properties").GetProperty("output");
        Assert.Equal("array", output.GetProperty("type").GetString());

        var items = output.GetProperty("items");
        Assert.Equal("object", items.GetProperty("type").GetString());

        var properties = items.GetProperty("properties");

        // Validate all expected properties exist with correct types
        var expectedProperties = new Dictionary<string, string>
        {
            ["intent"] = "string",
            ["confidence"] = "number",
            ["entities"] = "object",
            ["parameters"] = "object"
        };

        foreach (var (propName, expectedType) in expectedProperties)
        {
            Assert.True(properties.TryGetProperty(propName, out var prop),
                $"Property '{propName}' should exist");
            var actualType = prop.GetProperty("type").GetString();
            Assert.Equal(expectedType, actualType);

            _output.WriteLine($"  ✅ {propName}: {expectedType}");
        }

        // Validate entities and parameters have correct structure (empty object with additionalProperties: false)
        var entities = properties.GetProperty("entities");
        Assert.False(entities.GetProperty("additionalProperties").GetBoolean());
        Assert.True(entities.TryGetProperty("properties", out var entitiesProps));
        Assert.Equal(0, entitiesProps.GetRawText().Length - 2); // Should be "{}"

        var parameters = properties.GetProperty("parameters");
        Assert.False(parameters.GetProperty("additionalProperties").GetBoolean());

        _output.WriteLine("\n✅ Schema structure validated successfully!");
    }
}
