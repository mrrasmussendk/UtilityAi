using System.Text;
using OpenAI.Chat;
using UtilityAi.Helpers.OpenAiStructuredOutputHelper;
using UtilityAi.Helpers.OpenAiStructuredOutputHelper.SchemaGenerator;
using UtilityAi.Helpers.OpenAiStructuredOutputHelper.Strategy;
using UtilityAi.Sensor;
using UtilityAi.Sensor.LLM;
using UtilityAi.Utils;

namespace UtilityAi.Maf;

/// <summary>
/// Sensor that uses OpenAI's AiRequestBuilder to analyze user messages and publish structured facts to EventBus.
/// The LLM interprets intent and extracts entities - it does NOT pick actions.
/// The utility system then reacts naturally to these published facts.
/// </summary>
public sealed class MafAiLlmIntentSensor : ISensor
{
    private readonly ChatClient _chatClient;
    private readonly string _model;
    private readonly Type _messageType;
    private readonly Func<object, string> _messageExtractor;
    private readonly bool _includeCapabilities;
    private readonly SchemaGeneratorOptions _schemaOptions;

    /// <summary>
    /// Creates an intent analysis sensor using OpenAI's AiRequestBuilder.
    /// </summary>
    /// <param name="chatClient">OpenAI ChatClient for analysis.</param>
    /// <param name="model">Model to use (e.g., "gpt-4").</param>
    /// <param name="messageType">Type of message fact to analyze (e.g., UserMessage).</param>
    /// <param name="messageExtractor">Function to extract text from the message object.</param>
    /// <param name="includeCapabilities">If true, includes available actions in LLM context for better intent understanding.</param>
    /// <param name="schemaOptions">Schema generation options for JSON schema.</param>
    public MafAiLlmIntentSensor(
        ChatClient chatClient,
        string model,
        Type messageType,
        Func<object, string> messageExtractor,
        bool includeCapabilities = false,
        SchemaGeneratorOptions? schemaOptions = null)
    {
        _chatClient = chatClient ?? throw new ArgumentNullException(nameof(chatClient));
        _model = model ?? throw new ArgumentNullException(nameof(model));
        _messageType = messageType ?? throw new ArgumentNullException(nameof(messageType));
        _messageExtractor = messageExtractor ?? throw new ArgumentNullException(nameof(messageExtractor));
        _includeCapabilities = includeCapabilities;
        _schemaOptions = schemaOptions ?? new SchemaGeneratorOptions
        {
            RequiredStrategy = RequiredStrategy.AllProperties
        };
    }

    /// <summary>
    /// Convenience constructor for simple string-based message types.
    /// </summary>
    public static MafAiLlmIntentSensor ForMessageType<T>(
        ChatClient chatClient,
        string model,
        Func<T, string> messageExtractor,
        bool includeCapabilities = false,
        SchemaGeneratorOptions? schemaOptions = null) where T : class
    {
        return new MafAiLlmIntentSensor(
            chatClient,
            model,
            typeof(T),
            obj => messageExtractor((T)obj),
            includeCapabilities,
            schemaOptions
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

        // Call LLM using AiRequestBuilder
        var completion = AiRequestBuilder.Create()
            .WithModel(_model)
            .AddUser(prompt)
            .WithJsonSchemaFrom<IntentAnalysis>("intent", _schemaOptions, strict:false)
            .CompleteAndDeserialize<IntentAnalysis>(_chatClient);


        // Publish facts
        rt.Bus.Publish(completion);
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

        return sb.ToString();
    }
}
