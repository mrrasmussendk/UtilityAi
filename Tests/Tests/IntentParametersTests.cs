using UtilityAi.Capabilities;
using UtilityAi.Consideration;
using UtilityAi.Consideration.Intent;
using UtilityAi.Orchestration;
using UtilityAi.Sensor.LLM;
using UtilityAi.Utils;
using Xunit;

namespace Tests;

public class IntentParametersTests
{
    [Fact]
    public void IntentAnalysis_GetParameter_ReturnsTypedValue()
    {
        // Arrange
        var parameters = new Dictionary<string, object>
        {
            ["urgency"] = 0.85,
            ["customer_tier"] = "premium",
            ["requires_human"] = true
        };
        var intent = new IntentAnalysis(
            Intent: "ticket.create",
            Entities: new Dictionary<string, object>(),
            Confidence: 0.95,
            Parameters: parameters
        );

        // Act & Assert
        Assert.Equal(0.85, intent.GetParameter<double>("urgency"));
        Assert.Equal("premium", intent.GetParameter<string>("customer_tier"));
        Assert.True(intent.GetParameter<bool>("requires_human"));
    }

    [Fact]
    public void IntentAnalysis_GetParameter_ReturnsDefaultWhenMissing()
    {
        // Arrange
        var intent = new IntentAnalysis(
            Intent: "test",
            Entities: new Dictionary<string, object>(),
            Confidence: 0.9
        );

        // Act & Assert
        Assert.Equal(0.5, intent.GetParameter<double>("missing", 0.5));
        Assert.Null(intent.GetParameter<string>("missing"));
    }

    [Fact]
    public void IntentAnalysis_ParameterAbove_WorksCorrectly()
    {
        // Arrange
        var parameters = new Dictionary<string, object> { ["urgency"] = 0.85 };
        var intent = new IntentAnalysis(
            Intent: "test",
            Entities: new Dictionary<string, object>(),
            Confidence: 0.9,
            Parameters: parameters
        );

        // Act & Assert
        Assert.True(intent.ParameterAbove("urgency", 0.8));
        Assert.False(intent.ParameterAbove("urgency", 0.9));
        Assert.False(intent.ParameterAbove("missing", 0.5));
    }

    [Fact]
    public void ProposalBuilder_ForIntent_SetsIntentMatch()
    {
        // Arrange & Act
        var proposal = ProposalHelper.For("test-action")
            .ForIntent("ticket.create", IntentMatchType.Exact)
            .WithAction(async ct => await Task.CompletedTask)
            .Build();

        // Assert
        Assert.NotNull(proposal.IntentMatch);
        Assert.Equal("ticket.create", proposal.IntentMatch.Pattern);
        Assert.Equal(IntentMatchType.Exact, proposal.IntentMatch.MatchType);
    }

    [Fact]
    public void ProposalBuilder_UsesIntentParameter_RegistersMetadata()
    {
        // Arrange & Act
        var proposal = ProposalHelper.For("test-action")
            .UsesIntentParameter("urgency", "number", "How urgent",
                range: new ValueRange(0, 1, "ratio"))
            .WithAction(async ct => await Task.CompletedTask)
            .Build();

        // Assert
        Assert.NotNull(proposal.IntentParameters);
        Assert.Single(proposal.IntentParameters);

        var param = proposal.IntentParameters[0];
        Assert.Equal("urgency", param.ParameterName);
        Assert.Equal("number", param.Type);
        Assert.Equal("How urgent", param.Description);
        Assert.NotNull(param.Range);
        Assert.Equal(0, param.Range.Min);
        Assert.Equal(1, param.Range.Max);
    }

    [Fact]
    public void ProposalBuilder_ScoreByIntentParameter_AddsConsiderationAndMetadata()
    {
        // Arrange & Act
        var proposal = ProposalHelper.For("test-action")
            .ScoreByIntentParameter("urgency", x => x * x, (0, 1), "Urgency score")
            .WithAction(async ct => await Task.CompletedTask)
            .Build();

        // Assert
        Assert.NotNull(proposal.IntentParameters);
        Assert.Single(proposal.IntentParameters);
        Assert.Single(proposal.Considerations);

        var param = proposal.IntentParameters[0];
        Assert.Equal("urgency", param.ParameterName);
        Assert.Equal("intent-param-urgency", param.ConsiderationName);

        var consideration = proposal.Considerations[0];
        Assert.Equal("intent-param-urgency", consideration.Name);
    }

    [Fact]
    public void ProposalBuilder_MultipleIntentParameters_AllRegistered()
    {
        // Arrange & Act
        var proposal = ProposalHelper.For("test-action")
            .ScoreByIntentParameter("urgency", x => x, (0, 1))
            .UsesIntentParameter("customer_tier", "string",
                allowedValues: new[] { "free", "premium" })
            .ScoreByIntentParameter("complexity", x => x * x, (0, 1))
            .WithAction(async ct => await Task.CompletedTask)
            .Build();

        // Assert
        Assert.NotNull(proposal.IntentParameters);
        Assert.Equal(3, proposal.IntentParameters.Count);
        Assert.Equal(2, proposal.Considerations.Count); // Only 2 ScoreBy calls
    }

    [Fact]
    public async Task Proposal_WithIntentParameter_ScoresCorrectly()
    {
        // Arrange
        var bus = new EventBus();
        bus.Publish(new IntentAnalysis(
            Intent: "ticket.create",
            Entities: new Dictionary<string, object>(),
            Confidence: 0.9,
            Parameters: new Dictionary<string, object> { ["urgency"] = 0.8 }
        ));

        var proposal = ProposalHelper.For("test")
            .ScoreByIntentParameter("urgency", x => x, (0, 1))
            .WithAction(async ct => await Task.CompletedTask)
            .Build();

        var rt = new Runtime(bus, 0);

        // Act
        var utility = proposal.Utility(rt);

        // Assert
        Assert.True(utility > 0.7); // Should be close to 0.8
        Assert.True(utility < 0.9);
    }

    [Fact]
    public void GetCapabilitiesInfo_IncludesIntentMetadata()
    {
        // Arrange
        var bus = new EventBus();
        var orchestrator = new UtilityAiOrchestrator(bus: bus)
            .AddModule(new TestIntentModule());

        // Act
        var capabilities = orchestrator.GetCapabilitiesInfo();

        // Assert
        Assert.Single(capabilities);
        var module = capabilities[0];
        Assert.Contains("TestIntentModule", module.ModuleName); // file-scoped classes have mangled names
        Assert.Equal(2, module.PotentialActions.Count);

        var createProposal = module.PotentialActions[0];
        Assert.NotNull(createProposal.IntentMatch);
        Assert.Equal("ticket.create", createProposal.IntentMatch.Pattern);
        Assert.NotNull(createProposal.IntentParameters);
        Assert.Equal(2, createProposal.IntentParameters.Count);
    }

    [Fact]
    public async Task IntentBasedOrchestration_ChoosesCorrectProposal()
    {
        // Arrange
        var bus = new EventBus();
        bus.Publish(new IntentAnalysis(
            Intent: "ticket.create",
            Entities: new Dictionary<string, object>(),
            Confidence: 0.95,
            Parameters: new Dictionary<string, object>
            {
                ["urgency"] = 0.9,
                ["customer_tier"] = "premium"
            }
        ));

        var orchestrator = new UtilityAiOrchestrator(bus: bus)
            .AddModule(new TestIntentModule());


        // Act
        var result = await orchestrator.RunTickAsync(0, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("ticket.create", result.Chosen.Id);
        Assert.True(result.ChosenUtility > 0.8); // High urgency + premium
    }

    [Fact]
    public async Task IntentBasedOrchestration_LowUrgency_ChoosesQueryProposal()
    {
        // Arrange
        var bus = new EventBus();
        bus.Publish(new IntentAnalysis(
            Intent: "ticket.query",
            Entities: new Dictionary<string, object>(),
            Confidence: 0.95,
            Parameters: new Dictionary<string, object>
            {
                ["has_ticket_id"] = true
            }
        ));

        var orchestrator = new UtilityAiOrchestrator(bus: bus)
            .AddModule(new TestIntentModule());


        // Act
        var result = await orchestrator.RunTickAsync(0, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("ticket.query", result.Chosen.Id);
    }
}

// Test module demonstrating intent-based proposals
file class TestIntentModule : ICapabilityModule
{
    public IEnumerable<Proposal> Propose(Runtime rt)
    {
        // Proposal 1: Create ticket
        yield return ProposalHelper.For("ticket.create")
            .WithDescription("Create a new support ticket")
            .ForIntent("ticket.create", IntentMatchType.Exact)
            .ScoreByIntentParameter("urgency", x => x * x, (0, 1), "Ticket urgency")
            .UsesIntentParameter("customer_tier", "string",
                allowedValues: new[] { "free", "premium", "enterprise" })
            .WithConsideration(new UtilityAi.Consideration.General.SignalConsideration<IntentAnalysis>(
                "customer-tier-bonus",
                intent => intent.GetParameter<string>("customer_tier") switch
                {
                    "enterprise" => 1.0,
                    "premium" => 0.9,
                    _ => 0.7
                },
                x => x,
                (0, 1)))
            .WithAction(async ct => await Task.CompletedTask);

        // Proposal 2: Query ticket
        yield return ProposalHelper.For("ticket.query")
            .WithDescription("Query existing ticket")
            .ForIntent("ticket.query", IntentMatchType.Exact)
            .UsesIntentParameter("has_ticket_id", "boolean", "Whether user provided ticket ID")
            .WithConsideration(new UtilityAi.Consideration.General.SignalConsideration<IntentAnalysis>(
                "has-ticket-id",
                intent => intent.GetParameter<bool>("has_ticket_id") ? 1.0 : 0.3,
                x => x,
                (0, 1)))
            .WithAction(async ct => await Task.CompletedTask);
    }

}
