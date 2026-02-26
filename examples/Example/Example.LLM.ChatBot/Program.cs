using UtilityAi.Capabilities;
using UtilityAi.Consideration;
using UtilityAi.Consideration.General;
using UtilityAi.LLM.Abstractions;
using UtilityAi.LLM.OpenAI;
using UtilityAi.Orchestration;
using UtilityAi.Utils;

// ════════════════════════════════════════════════════════════════════════
// 🤖 Simple ChatBot Example with OpenAI Integration
// ════════════════════════════════════════════════════════════════════════

Console.WriteLine("═══════════════════════════════════════════════════════");
Console.WriteLine("🤖 UtilityAI ChatBot with OpenAI Integration");
Console.WriteLine("═══════════════════════════════════════════════════════\n");

// Check for API key
var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
if (string.IsNullOrEmpty(apiKey))
{
    Console.WriteLine("❌ Error: OPENAI_API_KEY environment variable not set");
    Console.WriteLine("\nTo run this example:");
    Console.WriteLine("  1. Get an API key from https://platform.openai.com/api-keys");
    Console.WriteLine("  2. Set environment variable: export OPENAI_API_KEY=sk-...");
    Console.WriteLine("  3. Run again: dotnet run");
    return;
}

Console.WriteLine("✅ API key found");
Console.WriteLine($"📝 Model: gpt-3.5-turbo\n");

var skillId = Environment.GetEnvironmentVariable("OPENAI_SKILL_ID");
var skillVersion = Environment.GetEnvironmentVariable("OPENAI_SKILL_VERSION");
var effectiveSkillVersion = string.IsNullOrWhiteSpace(skillVersion) ? "latest" : skillVersion;
if (!string.IsNullOrWhiteSpace(skillId))
{
    Console.WriteLine($"🧰 OpenAI skill enabled: {skillId} ({effectiveSkillVersion})");
}

// ════════════════════════════════════════════════════════════════════════
// Setup EventBus and Orchestrator
// ════════════════════════════════════════════════════════════════════════

var bus = new EventBus();
var orchestrator = new UtilityAiOrchestrator(bus: bus)
    .AddModule(new ChatBotModule(
        new OpenAIProvider("gpt-3.5-turbo", apiKey),
        skillId,
        effectiveSkillVersion));

// ════════════════════════════════════════════════════════════════════════
// Chat Loop
// ════════════════════════════════════════════════════════════════════════

Console.WriteLine("Type 'exit' or 'quit' to end the conversation\n");
Console.WriteLine("─────────────────────────────────────────────────────────\n");

while (true)
{
    Console.Write("You: ");
    var userInput = Console.ReadLine();

    if (string.IsNullOrWhiteSpace(userInput) ||
        userInput.Equals("exit", StringComparison.OrdinalIgnoreCase) ||
        userInput.Equals("quit", StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine("\n👋 Goodbye!");
        break;
    }

    // Publish user message
    bus.Publish(new UserMessage(userInput));

    // Run orchestration
    var intent = new UserIntent(Goal: new IntentGoal("chat"));
    await orchestrator.RunAsync(intent, maxTicks: 5, CancellationToken.None);

    // Get assistant response
    var response = bus.GetOrDefault<AssistantMessage>();
    if (response != null)
    {
        Console.WriteLine($"\nBot: {response.Text}\n");
    }
    else
    {
        Console.WriteLine("\nBot: [No response generated]\n");
    }
}

// ════════════════════════════════════════════════════════════════════════
// Fact Types
// ════════════════════════════════════════════════════════════════════════

public record UserMessage(string Text);
public record AssistantMessage(string Text);

// ════════════════════════════════════════════════════════════════════════
// ChatBot Module
// ════════════════════════════════════════════════════════════════════════

[Capability(Priority = 100, Domain = "chat")]
[RequiresFact<UserMessage>]
public class ChatBotModule : LlmCapabilityModule
{
    public ChatBotModule(ILlmProvider provider, string? skillId, string skillVersion) : base(provider, new LlmModuleConfiguration(
        DefaultOptions: new LlmOptions(
            Temperature: 0.7,
            MaxTokens: 500,
            OpenAiSkills: string.IsNullOrWhiteSpace(skillId)
                ? null
                : new OpenAiSkillsOptions(
                    EnvironmentType: OpenAiSkillEnvironmentType.Local,
                    References: new[] { new OpenAiSkillReference(skillId, skillVersion) })),
        OnResponseReceived: async (rt, response, ct) =>
        {
            // Publish assistant response to EventBus
            rt.Bus.Publish(new AssistantMessage(response.Content));
        }))
    {
    }

    public override IEnumerable<Proposal> Propose(Runtime rt)
    {
        // Only respond if we haven't already
        var hasResponse = rt.Bus.GetOrDefault<AssistantMessage>() != null;
        if (hasResponse)
            yield break;

        yield return CreateLlmProposal(
            proposalId: "chat.respond",
            rt: rt,
            messagesBuilder: BuildMessages,
            options: Configuration.DefaultOptions,
            // Score high when we have a user message and no response yet
            new FixedValueConsideration("ready", 1.0));
    }

    private List<LlmMessage> BuildMessages(Runtime rt)
    {
        var messages = new List<LlmMessage>();

        // System prompt
        messages.Add(LlmMessage.System(
            "You are a helpful assistant. Keep your responses concise and friendly."));

        // Get conversation history from EventBus
        var userHistory = rt.Bus.GetHistory<UserMessage>(maxItems: 10);
        var assistantHistory = rt.Bus.GetHistory<AssistantMessage>(maxItems: 10);

        // Interleave user and assistant messages chronologically
        var allMessages = userHistory
            .Select(e => (Time: e.Timestamp, IsUser: true, Text: e.Value.Text))
            .Concat(assistantHistory.Select(e => (Time: e.Timestamp, IsUser: false, Text: e.Value.Text)))
            .OrderBy(m => m.Time)
            .ToList();

        foreach (var msg in allMessages)
        {
            messages.Add(msg.IsUser
                ? LlmMessage.User(msg.Text)
                : LlmMessage.Assistant(msg.Text));
        }

        return messages;
    }
}
