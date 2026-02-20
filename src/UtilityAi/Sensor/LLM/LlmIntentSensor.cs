using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using UtilityAi.Facts;
using UtilityAi.Orchestration;
using UtilityAi.Utils;

namespace UtilityAi.Sensor.LLM;

/// <summary>
/// Sensor that uses an LLM to analyze user messages and publish structured facts to EventBus.
/// The LLM interprets intent and extracts entities - it does NOT pick actions.
/// The utility system then reacts naturally to these published facts.
/// 
/// Re-analysis behavior:
/// - By default, analyzes once per message and never again (backward compatible)
/// - With reanalyzeAfterActions=true, re-analyzes after each action execution
/// - Uses ExecutionHistory on the bus to detect when new actions have run
/// - Example: After research completes, LLM determines next step (summarize, search again, etc.)
/// 
/// Example with re-analysis:
/// <code>
/// var sensor = LlmIntentSensor.ForMessageType&lt;UserMessage&gt;(
///     llm: myLlmClient,
///     messageExtractor: msg => msg.Text,
///     includeCapabilities: true,
///     reanalyzeAfterActions: true
/// );
/// </code>
/// </summary>
public sealed class LlmIntentSensor : ISensor
{
    private readonly ILlmClient _llm;
    private readonly Type _messageType;
    private readonly Func<object, string> _messageExtractor;
    private readonly bool _includeCapabilities;
    private readonly bool _reanalyzeAfterActions;

    /// <summary>
    /// Creates an intent analysis sensor.
    /// </summary>
    /// <param name="llm">LLM client for analysis.</param>
    /// <param name="messageType">Type of message fact to analyze (e.g., UserMessage).</param>
    /// <param name="messageExtractor">Function to extract text from the message object.</param>
    /// <param name="includeCapabilities">If true, includes available actions in LLM context for better intent understanding.</param>
    /// <param name="reanalyzeAfterActions">If true, re-analyzes intent after actions execute to determine next steps based on results. Default is false for backward compatibility.</param>
    public LlmIntentSensor(
        ILlmClient llm,
        Type messageType,
        Func<object, string> messageExtractor,
        bool includeCapabilities = false,
        bool reanalyzeAfterActions = false)
    {
        _llm = llm ?? throw new ArgumentNullException(nameof(llm));
        _messageType = messageType ?? throw new ArgumentNullException(nameof(messageType));
        _messageExtractor = messageExtractor ?? throw new ArgumentNullException(nameof(messageExtractor));
        _includeCapabilities = includeCapabilities;
        _reanalyzeAfterActions = reanalyzeAfterActions;
    }

    /// <summary>
    /// Convenience constructor for simple string-based message types.
    /// </summary>
    public static LlmIntentSensor ForMessageType<T>(
        ILlmClient llm,
        Func<T, string> messageExtractor,
        bool includeCapabilities = false,
        bool reanalyzeAfterActions = false) where T : class
    {
        return new LlmIntentSensor(
            llm,
            typeof(T),
            obj => messageExtractor((T)obj),
            includeCapabilities,
            reanalyzeAfterActions
        );
    }

    public async Task SenseAsync(Runtime rt, CancellationToken ct)
    {
        // Get the message to analyze using reflection
        var getMethod = typeof(EventBus).GetMethod(nameof(EventBus.GetOrDefault))!
            .MakeGenericMethod(_messageType);
        var message = getMethod.Invoke(rt.Bus, null);
        if (message == null) return;  // No message to analyze

        var messageText = _messageExtractor(message);
        if (string.IsNullOrWhiteSpace(messageText)) return;

        // Check if we should re-analyze based on execution history
        if (!ShouldAnalyze(rt.Bus)) return;

        // Build prompt with full context
        var prompt = BuildPrompt(messageText, rt);

        // Call LLM
        var response = await _llm.GenerateAsync(prompt, ct);

        // Parse and publish facts
        var analysis = ParseAnalysis(response);
        rt.Bus.Publish(analysis);
        
        // Track what we've analyzed
        var executionHistory = rt.Bus.GetOrDefault<ExecutionHistory>();
        var actionCount = executionHistory?.Actions.Count ?? 0;
        rt.Bus.Publish(new LastIntentAnalysisContext(actionCount));
    }

    private bool ShouldAnalyze(EventBus bus)
    {
        var lastContext = bus.GetOrDefault<LastIntentAnalysisContext>();
        
        // First time analyzing - always proceed
        if (lastContext == null) return true;
        
        // If reanalyzeAfterActions is false, only analyze once
        if (!_reanalyzeAfterActions) return false;
        
        // Check if new actions have executed since last analysis
        var executionHistory = bus.GetOrDefault<ExecutionHistory>();
        var currentActionCount = executionHistory?.Actions.Count ?? 0;
        
        // Re-analyze if action count has changed (new actions executed)
        return currentActionCount > lastContext.ActionCount;
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
    [property: JsonPropertyName("parameters"), Obsolete("Use Entities instead. Parameters property will be removed in a future version.")] Dictionary<string, JsonElement>? Parameters = null
)
{
    /// <summary>
    /// Gets a typed parameter value from the Entities dictionary.
    /// Returns defaultValue if the parameter is missing or cannot be deserialized to T.
    /// </summary>
    public T? GetParameter<T>(string name, T? defaultValue = default)
    {
        if (TryGetParameterElement(name, out var element))
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

    private bool TryGetParameterElement(string name, out JsonElement element)
    {
        if (Entities?.TryGetValue(name, out element) == true)
            return true;

        #pragma warning disable CS0618 // Type or member is obsolete
        if (Parameters?.TryGetValue(name, out element) == true)
            return true;
        #pragma warning restore CS0618

        element = default;
        return false;
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
    /// Reads from Entities dictionary.
    /// Returns false if parameter is missing or not numeric.
    /// </summary>
    public bool ParameterAbove(string name, double threshold)
    {
        return GetParameter<double>(name, 0) > threshold;
    }

    /// <summary>
    /// Checks if a numeric parameter is below a threshold.
    /// Reads from Entities dictionary.
    /// Returns false if parameter is missing or not numeric.
    /// </summary>
    public bool ParameterBelow(string name, double threshold)
    {
        return GetParameter<double>(name, double.MaxValue) < threshold;
    }
};

/// <summary>
/// Fact tracking when LlmIntentSensor last analyzed intent.
/// Stores the action count from ExecutionHistory at the time of analysis.
/// Used to detect when new actions have executed and re-analysis should occur.
/// </summary>
public sealed record LastIntentAnalysisContext(int ActionCount);
