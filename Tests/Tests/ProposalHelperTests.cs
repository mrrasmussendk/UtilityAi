using UtilityAi.Capabilities;
using UtilityAi.Consideration;
using UtilityAi.Consideration.General;
using UtilityAi.Utils;
using Xunit;

namespace Tests;

public class ProposalHelperTests
{
    [Fact]
    public void ProposalHelper_For_CreatesBuilder()
    {
        var builder = ProposalHelper.For("test-id");
        Assert.NotNull(builder);
    }

    [Fact]
    public void ProposalBuilder_Build_WithAction_CreatesProposal()
    {
        var proposal = ProposalHelper.For("test")
            .WithAction(_ => Task.CompletedTask)
            .Build();

        Assert.NotNull(proposal);
        Assert.Equal("test", proposal.Id);
    }

    [Fact]
    public void ProposalBuilder_Build_WithoutAction_ThrowsException()
    {
        var builder = ProposalHelper.For("test");
        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }

    [Fact]
    public void ProposalBuilder_WithConsideration_AddsConsideration()
    {
        var consideration = new HasFact<int>();
        var proposal = ProposalHelper.For("test")
            .WithConsideration(consideration)
            .WithAction(_ => Task.CompletedTask)
            .Build();

        var bus = new EventBus();
        var rt = new Runtime(bus, new UserIntent("t"), 0);
        
        // Without the fact, consideration evaluates to 0, but Proposal adds epsilon protection
        var utility1 = proposal.Utility(rt);
        Assert.True(utility1 < 1e-5); // Should be very close to 0 (epsilon protected)

        // With the fact, utility should be 1
        bus.Publish(42);
        var utility2 = proposal.Utility(rt);
        Assert.InRange(utility2, 1.0 - 1e-9, 1.0 + 1e-9);
    }

    [Fact]
    public void ProposalBuilder_WithValue_AddsFixedValueConsideration()
    {
        var proposal = ProposalHelper.For("test")
            .WithValue("test-value", 0.75)
            .WithAction(_ => Task.CompletedTask)
            .Build();

        var bus = new EventBus();
        var rt = new Runtime(bus, new UserIntent("t"), 0);
        
        var utility = proposal.Utility(rt);
        Assert.Equal(0.75, utility);
    }

    [Fact]
    public void ProposalBuilder_WithValue_ClampsValue()
    {
        var proposalHigh = ProposalHelper.For("test1")
            .WithValue("high", 2.0)
            .WithAction(_ => Task.CompletedTask)
            .Build();

        var proposalLow = ProposalHelper.For("test2")
            .WithValue("low", -1.0)
            .WithAction(_ => Task.CompletedTask)
            .Build();

        var bus = new EventBus();
        var rt = new Runtime(bus, new UserIntent("t"), 0);
        
        Assert.InRange(proposalHigh.Utility(rt), 1.0 - 1e-5, 1.0 + 1e-5);
        Assert.True(proposalLow.Utility(rt) < 1e-5); // Should be very close to 0 (epsilon protected)
    }

    [Fact]
    public void ProposalBuilder_WithEligibility_AddsEligibility()
    {
        var eligibility = new HasFactEligible<int>();
        var proposal = ProposalHelper.For("test")
            .WithEligibility(eligibility)
            .WithAction(_ => Task.CompletedTask)
            .Build();

        var bus = new EventBus();
        var rt = new Runtime(bus, new UserIntent("t"), 0);
        
        // Without the fact, should not be eligible
        Assert.False(proposal.IsEligible(rt));

        // With the fact, should be eligible
        bus.Publish(42);
        Assert.True(proposal.IsEligible(rt));
    }

    [Fact]
    public void ProposalBuilder_WithPrior_SetsPrior()
    {
        var proposal = ProposalHelper.For("test")
            .WithPrior(0.5)
            .WithValue("base", 1.0)
            .WithAction(_ => Task.CompletedTask)
            .Build();

        Assert.Equal(0.5, proposal.Prior);

        var bus = new EventBus();
        var rt = new Runtime(bus, new UserIntent("t"), 0);
        
        // Utility should be prior * consideration
        var utility = proposal.Utility(rt);
        Assert.Equal(0.5, utility);
    }

    [Fact]
    public void ProposalBuilder_WithTemperature_SetsTemperature()
    {
        var proposal = ProposalHelper.For("test")
            .WithTemperature(2.0)
            .WithValue("base", 0.5)
            .WithAction(_ => Task.CompletedTask)
            .Build();

        Assert.Equal(2.0, proposal.Temperature);
    }

    [Fact]
    public void ProposalBuilder_FluentChaining_WorksCorrectly()
    {
        var proposal = ProposalHelper.For("chained")
            .WithConsideration(new HasFact<int>())
            .WithValue("fixed", 0.8)
            .WithEligibility(new HasFactEligible<string>())
            .WithPrior(0.9)
            .WithTemperature(1.5)
            .WithAction(_ => Task.CompletedTask)
            .Build();

        Assert.Equal("chained", proposal.Id);
        Assert.Equal(0.9, proposal.Prior);
        Assert.Equal(1.5, proposal.Temperature);
    }

    [Fact]
    public void ProposalBuilder_ImplicitConversion_Works()
    {
        Proposal proposal = ProposalHelper.For("implicit")
            .WithValue("test", 1.0)
            .WithAction(_ => Task.CompletedTask);

        Assert.NotNull(proposal);
        Assert.Equal("implicit", proposal.Id);
    }

    [Fact]
    public async Task ProposalBuilder_WithAction_ExecutesCorrectly()
    {
        bool executed = false;
        var proposal = ProposalHelper.For("action-test")
            .WithAction(_ =>
            {
                executed = true;
                return Task.CompletedTask;
            })
            .Build();

        await proposal.Act(CancellationToken.None);
        Assert.True(executed);
    }

    [Fact]
    public void ProposalBuilderAttribute_DefaultValues()
    {
        var attr = new ProposalBuilderAttribute();
        Assert.Null(attr.IdPrefix);
        Assert.Null(attr.Description);
    }

    [Fact]
    public void ProposalBuilderAttribute_WithProperties()
    {
        var attr = new ProposalBuilderAttribute
        {
            IdPrefix = "test-",
            Description = "Test description"
        };

        Assert.Equal("test-", attr.IdPrefix);
        Assert.Equal("Test description", attr.Description);
    }
}
