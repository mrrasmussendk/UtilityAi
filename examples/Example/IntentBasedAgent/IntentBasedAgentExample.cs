using UtilityAi.Capabilities;
using UtilityAi.Consideration;
using UtilityAi.Consideration.General;
using UtilityAi.Consideration.Intent;
using UtilityAi.Orchestration;
using UtilityAi.Sensor.LLM;
using UtilityAi.Utils;

namespace Example.IntentBasedAgent;

/// <summary>
/// Demonstrates intent-based orchestration with rich parameter support.
/// Shows how proposals can declare intent patterns and parameters for LLM-driven systems.
/// </summary>
public static class IntentBasedAgentExample
{
    public static async Task RunAsync()
    {
        Console.WriteLine("═══════════════════════════════════════════════════════");
        Console.WriteLine("🎯 Intent-Based Agent - Rich Parameter Support");
        Console.WriteLine("═══════════════════════════════════════════════════════\n");

        // Setup EventBus and publish a rich intent analysis
        var bus = new EventBus();

        // Simulate what an LLM would produce
        var intentAnalysis = new IntentAnalysis(
            Intent: "ticket.create",
            Entities: new Dictionary<string, object>
            {
                ["issue_type"] = "service_outage",
                ["affected_service"] = "payment_api",
                ["customer_email"] = "enterprise@company.com"
            },
            Confidence: 0.96,
            Parameters: new Dictionary<string, object>
            {
                ["urgency"] = 0.95,              // Critical issue
                ["customer_tier"] = "enterprise", // Premium customer
                ["complexity"] = 0.8,             // Complex problem
                ["requires_human"] = false        // Can be automated
            }
        );

        bus.Publish(intentAnalysis);

        Console.WriteLine("📊 Intent Analysis:");
        Console.WriteLine($"  Intent: {intentAnalysis.Intent}");
        Console.WriteLine($"  Confidence: {intentAnalysis.Confidence:F2}");
        Console.WriteLine($"  Urgency: {intentAnalysis.GetParameter<double>("urgency"):F2}");
        Console.WriteLine($"  Customer Tier: {intentAnalysis.GetParameter<string>("customer_tier")}");
        Console.WriteLine($"  Complexity: {intentAnalysis.GetParameter<double>("complexity"):F2}");
        Console.WriteLine();

        // Create orchestrator with intent-aware modules
        var orchestrator = new UtilityAiOrchestrator(bus: bus)
            .AddModule(new TicketManagementModule())
            .AddModule(new EscalationModule());

        Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Console.WriteLine("🔍 Registered Capabilities:");
        Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

        var capabilities = orchestrator.GetCapabilitiesInfo();

        foreach (var capability in capabilities)
        {
            Console.WriteLine($"\n📦 {capability.ModuleName}:");
            foreach (var proposal in capability.PotentialActions)
            {
                Console.WriteLine($"  • {proposal.ProposalId}");
                Console.WriteLine($"    Description: {proposal.Description}");

                if (proposal.IntentMatch != null)
                {
                    Console.WriteLine($"    Intent: {proposal.IntentMatch.Pattern} ({proposal.IntentMatch.MatchType})");
                }

                if (proposal.IntentParameters?.Count > 0)
                {
                    Console.WriteLine("    Parameters:");
                    foreach (var param in proposal.IntentParameters)
                    {
                        var range = param.Range != null ? $" [{param.Range.Min}..{param.Range.Max}]" : "";
                        Console.WriteLine($"      - {param.ParameterName} ({param.Type}){range}: {param.Description}");
                    }
                }
            }
        }

        Console.WriteLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Console.WriteLine("🔄 Running Orchestration:");
        Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n");

        // Run orchestration
        var result = await orchestrator.RunTickAsync(
           tick: 0,
            CancellationToken.None
        );

        if (result != null)
        {
            Console.WriteLine($"✅ Chosen Action: {result.Chosen.Id}");
            Console.WriteLine($"   Utility Score: {result.ChosenUtility:F3}");
            Console.WriteLine($"\n   Top 3 Proposals:");
            foreach (var (proposal, utility) in result.Scored.Take(3))
            {
                Console.WriteLine($"     {proposal.Id,-30} → {utility:F3}");
            }
        }

        Console.WriteLine("\n═══════════════════════════════════════════════════════");
        Console.WriteLine("✨ Key Features Demonstrated:");
        Console.WriteLine("═══════════════════════════════════════════════════════");
        Console.WriteLine("  ✓ Proposals declare intent patterns they handle");
        Console.WriteLine("  ✓ Parameters are strongly-typed and documented");
        Console.WriteLine("  ✓ LLM prompt can be generated from capability metadata");
        Console.WriteLine("  ✓ Modules score based on rich intent parameters");
        Console.WriteLine("  ✓ Different proposals handle different intent types");
        Console.WriteLine();
    }
}

/// <summary>
/// Manages ticket lifecycle - create, query, update.
/// </summary>
public class TicketManagementModule : ICapabilityModule
{
    public IEnumerable<Proposal> Propose(Runtime rt)
    {
        // Proposal 1: Create high-priority ticket
        yield return ProposalHelper.For("ticket.create.priority")
            .WithDescription("Create a high-priority support ticket for urgent issues")
            .ForIntent("ticket.create", IntentMatchType.Exact)

            // Urgency drives priority creation
            .ScoreByIntentParameter(
                paramName: "urgency",
                curve: x => Math.Pow(x, 3), // Cubic curve - heavily favor high urgency
                range: (0, 1),
                description: "How urgent the issue is (0=low, 1=critical)")

            // Complexity affects score
            .ScoreByIntentParameter(
                paramName: "complexity",
                curve: x => x * x, // Quadratic
                range: (0, 1),
                description: "Technical complexity of the issue")

            // Customer tier provides bonus
            .UsesIntentParameter(
                name: "customer_tier",
                type: "string",
                description: "Customer subscription tier",
                allowedValues: new[] { "free", "pro", "enterprise" })
            .WithConsideration(new SignalConsideration<IntentAnalysis>(
                "customer-tier-multiplier",
                intent => intent.GetParameter<string>("customer_tier") switch
                {
                    "enterprise" => 1.0,
                    "pro" => 0.85,
                    "free" => 0.65,
                    _ => 0.5
                },
                x => x,
                (0, 1)))

            .WithAction(async ct =>
            {
                await Task.Delay(50, ct);
                Console.WriteLine("  🎫 Created high-priority ticket");
                Console.WriteLine("     → Routed to senior support team");
                Console.WriteLine("     → SLA: 1 hour response time");
            });

        // Proposal 2: Query existing ticket
        yield return ProposalHelper.For("ticket.query")
            .WithDescription("Find and display existing ticket information")
            .ForIntent("ticket.query", IntentMatchType.Exact)

            .UsesIntentParameter(
                name: "has_ticket_id",
                type: "boolean",
                description: "Whether the user provided a ticket ID")
            .WithConsideration(new SignalConsideration<IntentAnalysis>(
                "has-ticket-id",
                intent => intent.GetParameter<bool>("has_ticket_id", false) ? 1.0 : 0.2,
                x => x,
                (0, 1)))

            .WithAction(async ct =>
            {
                await Task.Delay(30, ct);
                Console.WriteLine("  🔍 Querying ticket database");
            });

        // Proposal 3: Routine ticket creation (low urgency)
        yield return ProposalHelper.For("ticket.create.routine")
            .WithDescription("Create a standard ticket for non-urgent issues")
            .ForIntent("ticket.create", IntentMatchType.Exact)

            // Inverted urgency - high score when urgency is LOW
            .ScoreByIntentParameter(
                paramName: "urgency",
                curve: x => 1.0 - x, // Inverted - prefer low urgency
                range: (0, 1),
                description: "Issue urgency (inverted for routine handling)")

            .WithPrior(0.7) // Lower prior than priority creation

            .WithAction(async ct =>
            {
                await Task.Delay(50, ct);
                Console.WriteLine("  📝 Created routine ticket");
                Console.WriteLine("     → Routed to general support queue");
                Console.WriteLine("     → SLA: 24 hour response time");
            });
    }

    public IEnumerable<ProposalDefinition> GetProposalDefinitions()
    {
        yield return new ProposalDefinition(
            ProposalId: "ticket.create.priority",
            Description: "Create a high-priority support ticket for urgent issues",
            Prior: 1.0,
            Temperature: 0.0,
            ConsiderationNames: new List<string> { "customer-tier-multiplier" },
            EligibilityNames: new List<string>(),
            NoRepeat: false,
            JsonOutput: null,
            IntentMatch: new IntentMatchSpec("ticket.create", IntentMatchType.Exact),
            IntentParameters: new List<IntentParameterUsage>
            {
                new IntentParameterUsage("urgency", "number", "How urgent the issue is (0=low, 1=critical)"),
                new IntentParameterUsage("complexity", "number", "Technical complexity of the issue"),
                new IntentParameterUsage("customer_tier", "string", "Customer subscription tier", null, new[] { "free", "pro", "enterprise" })
            }
        );

        yield return new ProposalDefinition(
            ProposalId: "ticket.query",
            Description: "Find and display existing ticket information",
            Prior: 1.0,
            Temperature: 0.0,
            ConsiderationNames: new List<string> { "has-ticket-id" },
            EligibilityNames: new List<string>(),
            NoRepeat: false,
            JsonOutput: null,
            IntentMatch: new IntentMatchSpec("ticket.query", IntentMatchType.Exact),
            IntentParameters: new List<IntentParameterUsage>
            {
                new IntentParameterUsage("has_ticket_id", "boolean", "Whether the user provided a ticket ID")
            }
        );

        yield return new ProposalDefinition(
            ProposalId: "ticket.create.routine",
            Description: "Create a standard ticket for non-urgent issues",
            Prior: 0.7,
            Temperature: 0.0,
            ConsiderationNames: new List<string>(),
            EligibilityNames: new List<string>(),
            NoRepeat: false,
            JsonOutput: null,
            IntentMatch: new IntentMatchSpec("ticket.create", IntentMatchType.Exact),
            IntentParameters: new List<IntentParameterUsage>
            {
                new IntentParameterUsage("urgency", "number", "Issue urgency (inverted for routine handling)")
            }
        );
    }
}

/// <summary>
/// Handles escalation to human agents when needed.
/// </summary>
public class EscalationModule : ICapabilityModule
{
    public IEnumerable<Proposal> Propose(Runtime rt)
    {
        yield return ProposalHelper.For("escalate.human")
            .WithDescription("Escalate to human agent for complex or sensitive issues")
            .ForIntent("ticket.*", IntentMatchType.Prefix) // Matches any ticket intent

            // Escalate on very high urgency
            .ScoreByIntentParameter(
                paramName: "urgency",
                curve: x => x > 0.9 ? Math.Pow(x, 2) : 0.0, // Threshold at 0.9
                range: (0, 1),
                description: "Escalates when urgency exceeds 0.9")

            // Or if explicitly marked for human
            .UsesIntentParameter(
                name: "requires_human",
                type: "boolean",
                description: "Whether the issue explicitly needs human judgment")
            .WithConsideration(new SignalConsideration<IntentAnalysis>(
                "requires-human",
                intent => intent.GetParameter<bool>("requires_human", false) ? 1.0 : 0.1,
                x => x,
                (0, 1)))

            .WithPrior(0.6) // Medium prior

            .WithAction(async ct =>
            {
                await Task.Delay(40, ct);
                Console.WriteLine("  🚨 Escalating to human agent");
                Console.WriteLine("     → Assigning to available senior agent");
                Console.WriteLine("     → Customer will receive immediate notification");
            });
    }

    public IEnumerable<ProposalDefinition> GetProposalDefinitions()
    {
        yield return new ProposalDefinition(
            ProposalId: "escalate.human",
            Description: "Escalate to human agent for complex or sensitive issues",
            Prior: 0.6,
            Temperature: 0.0,
            ConsiderationNames: new List<string> { "requires-human" },
            EligibilityNames: new List<string>(),
            NoRepeat: false,
            JsonOutput: null,
            IntentMatch: new IntentMatchSpec("ticket.*", IntentMatchType.Prefix),
            IntentParameters: new List<IntentParameterUsage>
            {
                new IntentParameterUsage("urgency", "number", "Escalates when urgency exceeds 0.9"),
                new IntentParameterUsage("requires_human", "boolean", "Whether the issue explicitly needs human judgment")
            }
        );
    }
}
