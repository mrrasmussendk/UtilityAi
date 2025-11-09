using System.Text.Json.Serialization;

namespace UtilityAi.Helpers.OpenAiStructuredOutputHelper.DTO;

/// <summary>
/// Chat/message payload.
/// </summary>
public sealed record Message(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("content")] string Content)
{
    public static Message System(string content) => new("system", content);
    public static Message User(string content) => new("user", content);
}