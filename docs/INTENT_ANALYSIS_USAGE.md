# IntentAnalysis Usage Guide

## Important: OpenAI Strict Mode Limitation

**⚠️ CRITICAL:** When using `IntentAnalysis` with OpenAI structured outputs, you **MUST** set `strict: false` due to OpenAI's strict mode limitations with dynamic dictionaries.

### The Problem

`IntentAnalysis` uses `Dictionary<string, JsonElement>` for `Entities` and `Parameters` to allow flexible, dynamic key-value pairs. However, OpenAI's strict mode (`strict: true`) requires:
- `additionalProperties: false` for all objects
- All properties defined in the schema must be in the `required` array
- No dynamic or unknown properties

This makes it **impossible** to use dynamic dictionaries with strict mode.

### The Solution

**Always set `strict: false` when using `IntentAnalysis`:**

```csharp
var builder = AiRequestBuilder.Create()
    .WithModel("gpt-4")
    .AddUser("Create a ticket")
    .WithJsonSchemaFrom<IntentAnalysis>("intent", strict: false);  // ⚠️ REQUIRED!
```

### Why This Works

With `strict: false`:
- OpenAI relaxes validation rules
- Dynamic dictionaries are allowed
- The LLM can return arbitrary key-value pairs in `entities` and `parameters`
- Deserialization still works correctly

### Example

```csharp
using UtilityAi.Helpers.OpenAiStructuredOutputHelper;
using UtilityAi.Maf;
using UtilityAi.Sensor.LLM;

// ✅ CORRECT - strict: false
var result = AiRequestBuilder.Create()
    .WithModel("gpt-4")
    .AddUser("I need to create a high-priority bug ticket")
    .WithJsonSchemaFrom<IntentAnalysis>("intent", strict: false)  // Must be false!
    .CompleteAndDeserialize<IntentAnalysis>(chatClient);

// OpenAI can now return:
// {
//   "output": [{
//     "intent": "ticket.create",
//     "entities": {
//       "issueType": "bug",          // ✅ Dynamic keys work!
//       "priority": "high"
//     },
//     "confidence": 0.95,
//     "parameters": {
//       "urgency": 0.9,               // ✅ Any parameters work!
//       "requiresEscalation": true
//     }
//   }]
// }

// ❌ INCORRECT - strict: true will fail!
var result = AiRequestBuilder.Create()
    .WithModel("gpt-4")
    .AddUser("Create a ticket")
    .WithJsonSchemaFrom<IntentAnalysis>("intent", strict: true)  // ❌ This will error!
    .CompleteAndDeserialize<IntentAnalysis>(chatClient);

// Error: "In context=('properties', 'output', 'items', 'properties', 'entities'),
//         'additionalProperties' is required to be supplied and to be false."
```

### Alternative: Use Fixed Schema

If you need strict mode, define your own type with fixed properties:

```csharp
public record FixedIntentAnalysis(
    [property: JsonPropertyName("intent")] string Intent,
    [property: JsonPropertyName("issueType")] string? IssueType,     // Fixed properties
    [property: JsonPropertyName("priority")] string? Priority,        // Fixed properties
    [property: JsonPropertyName("confidence")] double Confidence
);

// ✅ This works with strict: true
var result = AiRequestBuilder.Create()
    .WithModel("gpt-4")
    .AddUser("Create a ticket")
    .WithJsonSchemaFrom<FixedIntentAnalysis>("intent", strict: true)  // ✅ OK!
    .CompleteAndDeserialize<FixedIntentAnalysis>(chatClient);
```

### Summary

| Feature | `strict: false` | `strict: true` |
|---------|-----------------|----------------|
| IntentAnalysis with dynamic entities/parameters | ✅ Works | ❌ Fails |
| Fixed schema types | ✅ Works | ✅ Works |
| OpenAI validation | Relaxed | Strict |
| Use case | Flexible, dynamic intents | Fixed, predictable schemas |

**For `IntentAnalysis`, always use `strict: false`.**
