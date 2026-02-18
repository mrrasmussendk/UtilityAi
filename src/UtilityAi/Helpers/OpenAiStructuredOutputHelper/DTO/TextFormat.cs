using System.Text.Json.Serialization;

namespace UtilityAi.Helpers.OpenAiStructuredOutputHelper.DTO;

/// <summary>
/// Envelope wrapper for the "text" property.
/// </summary>
public sealed record TextFormat([property: JsonPropertyName("format")] JsonSchemaFormat Format);