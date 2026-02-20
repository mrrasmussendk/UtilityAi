using System.Text;
using OpenAI.Chat;
using UtilityAi.Facts;
using UtilityAi.Helpers.OpenAiStructuredOutputHelper;
using UtilityAi.Helpers.OpenAiStructuredOutputHelper.SchemaGenerator;
using UtilityAi.Helpers.OpenAiStructuredOutputHelper.Strategy;
using UtilityAi.Orchestration;
using UtilityAi.Sensor;
using UtilityAi.Sensor.LLM;
using UtilityAi.Utils;

namespace UtilityAi.Maf;

/// <summary>
/// Sensor that uses OpenAI's AiRequestBuilder to analyze user messages and publish structured facts to EventBus.
/// The LLM interprets intent and extracts entities - it does NOT pick actions.
/// The utility system then reacts naturally to these published facts.
/// 
/// Re-analysis behavior:
/// - By default, analyzes once per message and never again (backward compatible)
/// - With reanalyzeAfterActions=true, re-analyzes after each action execution
/// - Uses ExecutionHistory on the bus to detect when new actions have run
/// - Example: After research completes, LLM determines next step (summarize, search again, etc.)
/// 
/// Important: IntentAnalysis is advisory, not directive.
/// The LLM's intent analysis informs considerations but does not override utility scoring.
/// Your considerations encode the true business logic - the LLM provides contextual guidance.
/// </summary>
public sealed class MafAiLlmIntentSensor : ISensor
{
    private readonly ChatClient _chatClient;
    private readonly string _model;
    private readonly Type _messageType;
    private readonly Func<object, string> _messageExtractor;
    private readonly bool _includeCapabilities;
    private readonly bool _reanalyzeAfterActions;
    private readonly SchemaGeneratorOptions _schemaOptions;

    /// <summary>
    /// Creates an intent analysis sensor using OpenAI's AiRequestBuilder.
    /// </summary>
    /// <param name="chatClient">OpenAI ChatClient for analysis.</param>
    /// <param name="model">Model to use (e.g., "gpt-4").</param>
    /// <param name="messageType">Type of message fact to analyze (e.g., UserMessage).</param>
    /// <param name="messageExtractor">Function to extract text from the message object.</param>
    /// <param name="includeCapabilities">If true, includes available actions in LLM context for better intent understanding.</param>
    /// <param name="reanalyzeAfterActions">If true, re-analyzes intent after actions execute to determine next steps based on results. Default is false for backward compatibility.</param>
    /// <param name="schemaOptions">Schema generation options for JSON schema.</param>
    public MafAiLlmIntentSensor(
        ChatClient chatClient,
        string model,
        Type messageType,
        Func<object, string> messageExtractor,
        bool includeCapabilities = false,
        bool reanalyzeAfterActions = false,
        SchemaGeneratorOptions? schemaOptions = null)
    {
        _chatClient = chatClient ?? throw new ArgumentNullException(nameof(chatClient));
        _model = model ?? throw new ArgumentNullException(nameof(model));
        _messageType = messageType ?? throw new ArgumentNullException(nameof(messageType));
        _messageExtractor = messageExtractor ?? throw new ArgumentNullException(nameof(messageExtractor));
        _includeCapabilities = includeCapabilities;
        _reanalyzeAfterActions = reanalyzeAfterActions;
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
        bool reanalyzeAfterActions = false,
        SchemaGeneratorOptions? schemaOptions = null) where T : class
    {
        return new MafAiLlmIntentSensor(
            chatClient,
            model,
            typeof(T),
            obj => messageExtractor((T)obj),
            includeCapabilities,
            reanalyzeAfterActions,
            schemaOptions
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

        // Include execution history to show what actions have already been taken
        var executionHistory = rt.Bus.GetOrDefault<ExecutionHistory>();
        if (executionHistory != null && executionHistory.Actions.Count > 0)
        {
            sb.AppendLine("## Actions Already Executed");
            foreach (var action in executionHistory.Actions)
            {
                sb.AppendLine($"- Tick {action.TickNumber}: {action.ProposalId}");
                if (!string.IsNullOrWhiteSpace(action.Description))
                {
                    sb.AppendLine($"  Description: {action.Description}");
                }
            }
            sb.AppendLine();
        }

        sb.AppendLine("## User Message");
        sb.AppendLine(messageText);
        sb.AppendLine();

        // Include capabilities context if requested
        if (_includeCapabilities)
        {
            var snapshot = rt.Bus.GetOrDefault<CapabilitiesSnapshot>();
            if (snapshot != null)
            {
                sb.AppendLine("## Available System Actions");
                sb.AppendLine("Extract entities based on these available actions and their required parameters:");
                sb.AppendLine();
                foreach (var capability in snapshot.Capabilities)
                {
                    foreach (var action in capability.PotentialActions)
                    {
                        sb.AppendLine($"### {action.ProposalId}");
                        sb.AppendLine($"Description: {action.Description ?? "No description"}");

                        // Include parameters if available
                        if (action.IntentParameters != null && action.IntentParameters.Count > 0)
                        {
                            sb.AppendLine("Required entities:");
                            foreach (var param in action.IntentParameters)
                            {
                                var paramDesc = param.Description ?? param.Type;
                                sb.AppendLine($"  - {param.ParameterName}: {paramDesc}");
                            }
                        }
                        sb.AppendLine();
                    }
                }
            }
        }

        sb.AppendLine("## Instructions");
        sb.AppendLine("1. Review the execution history - if an action was already executed that fulfills the user's request, DO NOT extract entities for that action again");
        sb.AppendLine("2. Identify the user's current intent based on their message and what has NOT yet been done");
        sb.AppendLine("3. Extract entities that match the parameters required by relevant system actions that have NOT been executed");
        sb.AppendLine("4. Use entity keys that correspond to action parameters (not generic keys like 'country', 'unit')");
        sb.AppendLine("5. If the user's request is already fulfilled, set intent to 'none' or leave entities empty");
        sb.AppendLine();
        sb.AppendLine("## Output Format");
        sb.AppendLine("Respond with JSON containing:");
        sb.AppendLine("- `intent`: Primary user intent/goal (string)");
        sb.AppendLine("- `entities`: Key-value pairs matching action parameters (object)");
        sb.AppendLine("- `confidence`: Confidence in the analysis 0.0-1.0 (number)");

        return sb.ToString();
    }
}
