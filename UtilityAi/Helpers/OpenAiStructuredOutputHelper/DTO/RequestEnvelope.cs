using System.Text.Json.Serialization;

namespace UtilityAi.Helpers.OpenAiStructuredOutputHelper.DTO;

/// <summary>
/// Root request envelope sent to your AI endpoint.
/// </summary>
public sealed record RequestEnvelope(
    [property: JsonPropertyName("model")] object Model,
    [property: JsonPropertyName("tools")] object[] Tools,
    [property: JsonPropertyName("input")] Message[] Input,
    [property: JsonPropertyName("text")] TextFormat Text);
