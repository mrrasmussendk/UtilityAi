using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using UtilityAi.Orchestration;
using UtilityAi.Utils;

namespace UtilityAi.Sensor.LLM;

/// <summary>
/// Sensor that uses an LLM to analyze user messages and publish structured facts to EventBus.
/// The LLM interprets intent and extracts entities - it does NOT pick actions.
/// The utility system then reacts naturally to these published facts.
/// </summary>
public sealed class LlmIntentSensor : ISensor
{
    private readonly ILlmClient _llm;
    private readonly Type _messageType;
    private readonly Func<object, string> _messageExtractor;
    private readonly bool _includeCapabilities;

    /// <summary>
    /// Creates an intent analysis sensor.
    /// </summary>
    /// <param name="llm">LLM client for analysis.</param>
    /// <param name="messageType">Type of message fact to analyze (e.g., UserMessage).</param>
    /// <param name="messageExtractor">Function to extract text from the message object.</param>
    /// <param name="includeCapabilities">If true, includes available actions in LLM context for better intent understanding.</param>
    public LlmIntentSensor(
        ILlmClient llm,
        Type messageType,
        Func<object, string> messageExtractor,
        bool includeCapabilities = false)
    {
        _llm = llm ?? throw new ArgumentNullException(nameof(llm));
        _messageType = messageType ?? throw new ArgumentNullException(nameof(messageType));
        _messageExtractor = messageExtractor ?? throw new ArgumentNullException(nameof(messageExtractor));
        _includeCapabilities = includeCapabilities;
    }

    /// <summary>
    /// Convenience constructor for simple string-based message types.
    /// </summary>
    public static LlmIntentSensor ForMessageType<T>(
        ILlmClient llm,
        Func<T, string> messageExtractor,
        bool includeCapabilities = false) where T : class
    {
        return new LlmIntentSensor(
            llm,
            typeof(T),
            obj => messageExtractor((T)obj),
            includeCapabilities
        );
    }

    public async Task SenseAsync(Runtime rt, CancellationToken ct)
    {
        // Check if we've already analyzed this message
        var existingAnalysis = rt.Bus.GetOrDefault<IntentAnalysis>();
        if (existingAnalysis != null) return;  // Already analyzed

        // Get the message to analyze using reflection
        var getMethod = typeof(EventBus).GetMethod(nameof(EventBus.GetOrDefault))!
            .MakeGenericMethod(_messageType);
        var message = getMethod.Invoke(rt.Bus, null);
        if (message == null) return;  // No message to analyze

        var messageText = _messageExtractor(message);
        if (string.IsNullOrWhiteSpace(messageText)) return;

        // Build prompt
        var prompt = BuildPrompt(messageText, rt);

        // Call LLM
        var response = await _llm.GenerateAsync(prompt, ct);

        // Parse and publish facts
        var analysis = ParseAnalysis(response);
        rt.Bus.Publish(analysis);
    }

    private string BuildPrompt(string messageText, Runtime rt)
    {
        var sb = new StringBuilder();

        sb.AppendLine("# Intent Analysis");
        sb.AppendLine();
        sb.AppendLine("Analyze the user's message and extract structured information.");
        sb.AppendLine();
        sb.AppendLine("## User Message");
        sb.AppendLine(messageText);
        sb.AppendLine();

        // Include capabilities context if requested
        if (_includeCapabilities)
        {
            var snapshot = rt.Bus.GetOrDefault<CapabilitiesSnapshot>();
            if (snapshot != null)
            {
                sb.AppendLine("## Available Capabilities");
                foreach (var capability in snapshot.Capabilities)
                {
                    foreach (var action in capability.PotentialActions)
                    {
                        sb.AppendLine($"- {action.ProposalId}: {action.Description ?? "No description"}");
                    }
                }
                sb.AppendLine();
            }
        }

        sb.AppendLine("## Output Format");
        sb.AppendLine("Respond with JSON containing:");
        sb.AppendLine("- `intent`: Primary user intent/goal (string)");
        sb.AppendLine("- `entities`: Key-value pairs of extracted entities (object)");
        sb.AppendLine("- `confidence`: Confidence in the analysis 0.0-1.0 (number)");
        sb.AppendLine();
        sb.AppendLine("Example:");
        sb.AppendLine("```json");
        sb.AppendLine("{");
        sb.AppendLine("  \"intent\": \"query.sales_data\",");
        sb.AppendLine("  \"entities\": {");
        sb.AppendLine("    \"timeRange\": \"last_month\",");
        sb.AppendLine("    \"dataType\": \"customers\",");
        sb.AppendLine("    \"sortBy\": \"revenue\"");
        sb.AppendLine("  },");
        sb.AppendLine("  \"confidence\": 0.95");
        sb.AppendLine("}");
        sb.AppendLine("```");

        return sb.ToString();
    }

    private IntentAnalysis ParseAnalysis(string response)
    {
        try
        {
            // Extract JSON from response (handles markdown code blocks)
            var json = ExtractJson(response);

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var parsed = JsonSerializer.Deserialize<IntentAnalysisJson>(json, options);
            if (parsed == null)
                throw new InvalidOperationException("Failed to deserialize intent analysis");

            return new IntentAnalysis(
                parsed.Intent ?? "unknown",
                parsed.Entities ?? new Dictionary<string, JsonElement>(),
                parsed.Confidence
            );
        }
        catch (Exception ex)
        {
            // Fallback: Return low-confidence unknown intent
            using var errorDoc = JsonDocument.Parse($"\"{ex.Message}\"");
            return new IntentAnalysis(
                "parse_error",
                new Dictionary<string, JsonElement> { ["error"] = errorDoc.RootElement.Clone() },
                0.0
            );
        }
    }

    private string ExtractJson(string response)
    {
        // Remove markdown code blocks if present
        var trimmed = response.Trim();

        if (trimmed.StartsWith("```json"))
        {
            var startIndex = trimmed.IndexOf('\n') + 1;
            var endIndex = trimmed.LastIndexOf("```");
            if (endIndex > startIndex)
                return trimmed.Substring(startIndex, endIndex - startIndex).Trim();
        }
        else if (trimmed.StartsWith("```"))
        {
            var startIndex = trimmed.IndexOf('\n') + 1;
            var endIndex = trimmed.LastIndexOf("```");
            if (endIndex > startIndex)
                return trimmed.Substring(startIndex, endIndex - startIndex).Trim();
        }

        // Try to find JSON object boundaries
        var jsonStart = trimmed.IndexOf('{');
        var jsonEnd = trimmed.LastIndexOf('}');
        if (jsonStart >= 0 && jsonEnd > jsonStart)
            return trimmed.Substring(jsonStart, jsonEnd - jsonStart + 1);

        return response;
    }

    private sealed record IntentAnalysisJson(
        string? Intent,
        Dictionary<string, JsonElement>? Entities,
        double Confidence
    );
}

/// <summary>
/// Fact representing LLM's analysis of user intent.
/// Published to EventBus for modules to react to.
/// </summary>
public sealed record IntentAnalysis(
    [property: JsonPropertyName("intent")] string Intent,
    [property: JsonPropertyName("entities")] Dictionary<string, JsonElement>? Entities,
    [property: JsonPropertyName("confidence")] double Confidence,
    [property: JsonPropertyName("parameters")] Dictionary<string, JsonElement>? Parameters = null
)
{
    /// <summary>
    /// Gets a typed parameter value from the Parameters dictionary.
    /// Returns defaultValue if the parameter is missing or cannot be deserialized to T.
    /// </summary>
    public T? GetParameter<T>(string name, T? defaultValue = default)
    {
        if (Parameters?.TryGetValue(name, out var element) == true)
        {
            try
            {
                return JsonSerializer.Deserialize<T>(element);
            }
            catch
            {
                return defaultValue;
            }
        }
        return defaultValue;
    }

    /// <summary>
    /// Gets a typed entity value from the Entities dictionary.
    /// Returns defaultValue if the entity is missing or cannot be deserialized to T.
    /// </summary>
    public T? GetEntity<T>(string name, T? defaultValue = default)
    {
        if (Entities?.TryGetValue(name, out var element) == true)
        {
            try
            {
                return JsonSerializer.Deserialize<T>(element);
            }
            catch
            {
                return defaultValue;
            }
        }
        return defaultValue;
    }

    /// <summary>
    /// Checks if a numeric parameter is above a threshold.
    /// Returns false if parameter is missing or not numeric.
    /// </summary>
    public bool ParameterAbove(string name, double threshold)
    {
        return GetParameter<double>(name, 0) > threshold;
    }

    /// <summary>
    /// Checks if a numeric parameter is below a threshold.
    /// Returns false if parameter is missing or not numeric.
    /// </summary>
    public bool ParameterBelow(string name, double threshold)
    {
        return GetParameter<double>(name, double.MaxValue) < threshold;
    }
};

