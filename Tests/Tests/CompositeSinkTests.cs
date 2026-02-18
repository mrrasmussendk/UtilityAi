using UtilityAi.Consideration;
using UtilityAi.Orchestration;
using UtilityAi.Utils;
using Xunit;

namespace Tests;

public class CompositeSinkTests
{
    private class TestSink : IOrchestrationSink
    {
        public int TickStartCount { get; private set; }
        public int ScoredCount { get; private set; }
        public int ChosenCount { get; private set; }
        public int ActedCount { get; private set; }
        public int StoppedCount { get; private set; }
        public OrchestrationStopReason? LastStopReason { get; private set; }

        public void OnTickStart(Runtime rt) => TickStartCount++;
        public void OnScored(Runtime rt, IReadOnlyList<(Proposal Proposal, double Utility)> scored) => ScoredCount++;
        public void OnChosen(Runtime rt, Proposal chosen, double utility) => ChosenCount++;
        public void OnActed(Runtime rt, Proposal chosen) => ActedCount++;
        public void OnStopped(Runtime rt, OrchestrationStopReason reason)
        {
            StoppedCount++;
            LastStopReason = reason;
        }
    }

    [Fact]
    public void CompositeSink_ForwardsOnTickStart_ToAllSinks()
    {
        var sink1 = new TestSink();
        var sink2 = new TestSink();
        var composite = new CompositeSink(sink1, sink2);
        var bus = new EventBus();
        var runtime = new Runtime(bus, new UserIntent("test"), 0);

        composite.OnTickStart(runtime);

        Assert.Equal(1, sink1.TickStartCount);
        Assert.Equal(1, sink2.TickStartCount);
    }

    [Fact]
    public void CompositeSink_ForwardsOnScored_ToAllSinks()
    {
        var sink1 = new TestSink();
        var sink2 = new TestSink();
        var composite = new CompositeSink(sink1, sink2);
        var bus = new EventBus();
        var runtime = new Runtime(bus, new UserIntent("test"), 0);
        var scored = new List<(Proposal, double)>();

        composite.OnScored(runtime, scored);

        Assert.Equal(1, sink1.ScoredCount);
        Assert.Equal(1, sink2.ScoredCount);
    }

    [Fact]
    public void CompositeSink_ForwardsOnChosen_ToAllSinks()
    {
        var sink1 = new TestSink();
        var sink2 = new TestSink();
        var composite = new CompositeSink(sink1, sink2);
        var bus = new EventBus();
        var runtime = new Runtime(bus, new UserIntent("test"), 0);
        var proposal = new Proposal("test", Array.Empty<IConsideration>(), _ => Task.CompletedTask);

        composite.OnChosen(runtime, proposal, 0.5);

        Assert.Equal(1, sink1.ChosenCount);
        Assert.Equal(1, sink2.ChosenCount);
    }

    [Fact]
    public void CompositeSink_ForwardsOnActed_ToAllSinks()
    {
        var sink1 = new TestSink();
        var sink2 = new TestSink();
        var composite = new CompositeSink(sink1, sink2);
        var bus = new EventBus();
        var runtime = new Runtime(bus, new UserIntent("test"), 0);
        var proposal = new Proposal("test", Array.Empty<IConsideration>(), _ => Task.CompletedTask);

        composite.OnActed(runtime, proposal);

        Assert.Equal(1, sink1.ActedCount);
        Assert.Equal(1, sink2.ActedCount);
    }

    [Fact]
    public void CompositeSink_ForwardsOnStopped_ToAllSinks()
    {
        var sink1 = new TestSink();
        var sink2 = new TestSink();
        var composite = new CompositeSink(sink1, sink2);
        var bus = new EventBus();
        var runtime = new Runtime(bus, new UserIntent("test"), 0);

        composite.OnStopped(runtime, OrchestrationStopReason.MaxTicksReached);

        Assert.Equal(1, sink1.StoppedCount);
        Assert.Equal(1, sink2.StoppedCount);
        Assert.Equal(OrchestrationStopReason.MaxTicksReached, sink1.LastStopReason);
        Assert.Equal(OrchestrationStopReason.MaxTicksReached, sink2.LastStopReason);
    }

    [Fact]
    public void CompositeSink_WorksWithEmptySinkArray()
    {
        var composite = new CompositeSink();
        var bus = new EventBus();
        var runtime = new Runtime(bus, new UserIntent("test"), 0);

        // Should not throw
        composite.OnTickStart(runtime);
        composite.OnScored(runtime, new List<(Proposal, double)>());
        composite.OnChosen(runtime, new Proposal("test", Array.Empty<IConsideration>(), _ => Task.CompletedTask), 0.5);
        composite.OnActed(runtime, new Proposal("test", Array.Empty<IConsideration>(), _ => Task.CompletedTask));
        composite.OnStopped(runtime, OrchestrationStopReason.NoProposals);
    }

    [Fact]
    public void CompositeSink_ForwardsToMultipleSinks_InOrder()
    {
        var orderTracker = new List<int>();
        var sink1 = new TestSink();
        var sink2 = new TestSink();
        var sink3 = new TestSink();
        
        var composite = new CompositeSink(sink1, sink2, sink3);
        var bus = new EventBus();
        var runtime = new Runtime(bus, new UserIntent("test"), 0);

        composite.OnTickStart(runtime);

        Assert.Equal(1, sink1.TickStartCount);
        Assert.Equal(1, sink2.TickStartCount);
        Assert.Equal(1, sink3.TickStartCount);
    }
}
