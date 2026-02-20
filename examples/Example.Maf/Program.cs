using UtilityAi.Capabilities;
using UtilityAi.Consideration;
using UtilityAi.Maf;
using UtilityAi.Orchestration;
using UtilityAi.Utils;

// =============================================================================
// MAF + UTILITY AI INTEGRATION EXAMPLE
// =============================================================================
// This example shows how to use MafClient to create Azure agents in proposals.
//
// Key Pattern:
// 1. Create a MafClient with Azure credentials
// 2. In Propose(), call mafClient.CreateAgent() and use it in your action
// 3. Agents are cached automatically - no manual setup needed!
// =============================================================================

Console.WriteLine("═══════════════════════════════════════════════════════");
Console.WriteLine("🤖 MAF + UtilityAI Integration Example");
Console.WriteLine("═══════════════════════════════════════════════════════");
Console.WriteLine();

// --- Step 1: Create MAF client (one time setup) ---
var mafClient = new MafClient("https://your-resource.openai.azure.com");

Console.WriteLine("✅ MAF Client initialized");
Console.WriteLine();

// --- Step 2: Set up orchestrator with a module that uses MAF agents ---
var bus = new EventBus();
var orchestrator = new UtilityAiOrchestrator(bus: bus)
    .AddModule(new MyModule(mafClient));

// --- Step 3: Run the orchestrator ---
var intent = new UserIntent(
    Goal: new IntentGoal("answer-question"),
    Slots: new Dictionary<string, object?>
    {
        ["query"] = "What are the benefits of utility-based AI?"
    }
);

Console.WriteLine("🔄 Running orchestration...\n");
await orchestrator.RunAsync(intent, maxTicks: 2, CancellationToken.None);

Console.WriteLine("\n✅ Done!");

// Example module that uses MAF agents in proposals
file sealed class MyModule : ICapabilityModule
{
    private readonly MafClient _mafClient;

    public MyModule(MafClient mafClient)
    {
        _mafClient = mafClient;
    }

    public IEnumerable<Proposal> Propose(Runtime rt)
    {
        // Create a proposal that uses a MAF agent
        yield return new Proposal(
            id: "research-answer",
            cons: new IConsideration[]
            {
                new ConstantConsideration("always-available", 1.0)
            },
            act: async ct =>
            {
                // Get prompt from intent
                var prompt = rt.Intent.Slots?["query"]?.ToString() ?? "No query provided";

                // Create agent using MafClient
                var agent = _mafClient.CreateAgent(
                    name: "researcher",
                    instructions: "You are a helpful research assistant. Provide clear, concise answers."
                );

                var agentsClient = _mafClient.GetAgentsClient();

                // Create thread and add message
                var thread = agentsClient.Threads.CreateThread();
                agentsClient.Messages.CreateMessage(thread.Value.Id, Azure.AI.Agents.Persistent.MessageRole.User, prompt);

                // Run the agent
                var run = agentsClient.Runs.CreateRun(thread.Value.Id, agent.Id);

                // Wait for completion
                do
                {
                    await Task.Delay(500, ct);
                    run = await agentsClient.Runs.GetRunAsync(thread.Value.Id, run.Value.Id, ct);
                }
                while (run.Value.Status == Azure.AI.Agents.Persistent.RunStatus.Queued
                    || run.Value.Status == Azure.AI.Agents.Persistent.RunStatus.InProgress);

                // Get the response
                var messages = agentsClient.Messages.GetMessages(thread.Value.Id, order: Azure.AI.Agents.Persistent.ListSortOrder.Ascending);
                var responseText = string.Empty;

                foreach (var message in messages)
                {
                    if (message.Role.ToString() == "assistant")
                    {
                        foreach (var contentItem in message.ContentItems)
                        {
                            if (contentItem is Azure.AI.Agents.Persistent.MessageTextContent textItem)
                            {
                                responseText = textItem.Text;
                                break;
                            }
                        }
                    }
                }

                Console.WriteLine($"Agent response: {responseText}");
                rt.Bus.Publish(new AnswerReceived(responseText));

                // Cleanup
                agentsClient.Threads.DeleteThread(thread.Value.Id);
            }
        );
    }

    public IEnumerable<ProposalDefinition> GetProposalDefinitions()
    {
        yield return new ProposalDefinition(
            ProposalId: "research-answer",
            Description: null,
            Prior: 1.0,
            Temperature: 1.0,
            ConsiderationNames: new[] { "always-available" },
            EligibilityNames: Array.Empty<string>(),
            NoRepeat: false,
            JsonOutput: null
        );
    }
}

file sealed class ConstantConsideration : IConsideration
{
    public string Name { get; }
    private readonly double _value;

    public ConstantConsideration(string name, double value)
    {
        Name = name;
        _value = value;
    }

    public double Evaluate(Runtime rt) => _value;
}

file record AnswerReceived(string Text);

