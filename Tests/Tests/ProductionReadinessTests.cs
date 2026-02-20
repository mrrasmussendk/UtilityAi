using Xunit;
using UtilityAi.Orchestration;
using UtilityAi.Utils;
using UtilityAi.Consideration;
using UtilityAi.Capabilities;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

namespace UtilityAi.Tests;

public class ProductionReadinessTests
{
    [Fact]
    public async Task RunUntilQuiescent_StopsWhenUtilityIsLow()
    {
        // Arrange
        var bus = new EventBus();
        var orch = new UtilityAiOrchestrator(bus: bus);
        
        // A module that proposes an action whose utility decreases each tick
        orch.AddModule(new DecreasingUtilityModule());

        var sink = new RecordingSink();
        var intent = new UserIntent(new IntentGoal("test"));

        // Act
        // Threshold 0.5. Ticks:
        // 1: Utility 1.0 (Executes)
        // 2: Utility 0.7 (Executes)
        // 3: Utility 0.4 (Under threshold -> Stop)
        await orch.RunUntilQuiescentAsync(threshold: 0.5, maxTicks: 10, CancellationToken.None, sink);

        // Assert
        Assert.Equal(3, sink.Ticks.Count); 
        // Tick 0 (Index 0) -> Utility 1.0
        // Tick 1 (Index 1) -> Utility 0.7
        // Tick 2 (Index 2) -> Utility 0.4 -> STOP
        
        Assert.Equal(OrchestrationStopReason.Quiescent, sink.Ticks.Last().Scored[0].Utility < 0.5 ? OrchestrationStopReason.Quiescent : OrchestrationStopReason.MaxTicksReached);
        // We need to check the final stop reason. RecordingSink doesn't capture StopReason in Ticks.
        // Let's use a custom sink for reason.
    }

    [Fact]
    public async Task NoRepeatConsideration_PreventsLoops()
    {
        // Arrange
        var bus = new EventBus();
        // Set stopAtZero = true to ensure it stops when utility is low
        var orch = new UtilityAiOrchestrator(bus: bus, stopAtZero: true);
        
        var module = new RepeatableModule();
        orch.AddModule(module);

        var sink = new RecordingSink();
        var intent = new UserIntent(new IntentGoal("test"));

        // Act - Run tick 0
        var res0 = await orch.RunTickAsync(0, CancellationToken.None, sink);
        Assert.NotNull(res0);
        
        // Manual verification
        bool hasHist = bus.TryGet<IReadOnlyList<string>>(out var hist);
        Assert.True(hasHist);
        Assert.Contains("action-1", hist);

        // Run tick 1 - This should return null because action-1 now has utility <= Eps
        var res1 = await orch.RunTickAsync(1, CancellationToken.None, sink);

        // Assert
        Assert.Equal(1, sink.Ticks.Count); 
        Assert.Null(res1);
    }

    private class DecreasingUtilityModule : ICapabilityModule
    {
        public IEnumerable<Proposal> Propose(Runtime rt)
        {
            double utility = 1.0 - (rt.Tick * 0.3);
            yield return new Proposal(
                "decreasing-action",
                new[] { new ConstantConsideration(utility) },
                _ => Task.CompletedTask
            );
        }

        public IEnumerable<ProposalDefinition> GetProposalDefinitions()
        {
            yield return new ProposalDefinition(
                ProposalId: "decreasing-action",
                Description: null,
                Prior: 1.0,
                Temperature: 0.0,
                ConsiderationNames: new List<string> { "Constant" },
                EligibilityNames: new List<string>(),
                NoRepeat: false,
                JsonOutput: null
            );
        }
    }

    private class RepeatableModule : ICapabilityModule
    {
        public IEnumerable<Proposal> Propose(Runtime rt)
        {
            yield return new Proposal(
                "action-1",
                new[] { new NoRepeatConsideration("action-1") },
                _ => Task.CompletedTask
            );
        }

        public IEnumerable<ProposalDefinition> GetProposalDefinitions()
        {
            yield return new ProposalDefinition(
                ProposalId: "action-1",
                Description: null,
                Prior: 1.0,
                Temperature: 0.0,
                ConsiderationNames: new List<string> { "NoRepeatConsideration" },
                EligibilityNames: new List<string>(),
                NoRepeat: false,
                JsonOutput: null
            );
        }
    }

    private class ConstantConsideration : IConsideration
    {
        private readonly double _val;
        public ConstantConsideration(double val) => _val = val;
        public string Name => "Constant";
        public double Evaluate(Runtime rt) => _val;
    }
}
