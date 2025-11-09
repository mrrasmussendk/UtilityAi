using UtilityAi.Capabilities;
using UtilityAi.Sensor;
using UtilityAi.Utils;
using UtilityAi.Consideration;
using UtilityAi.Orchestration.Events;

namespace UtilityAi.Orchestration;

public sealed class UtilityAiOrchestrator : IOrchestrator
{
    private readonly List<ISensor> _sensors = new();
    private readonly List<ICapabilityModule> _modules = new();
    private readonly ISelectionStrategy _selector;
    private readonly Stack<string> _executionStack = new Stack<string>();
    private readonly bool _stopAtZero = true;
    private EventBus _bus;
    public UtilityAiOrchestrator(ISelectionStrategy? selector = null, bool stopAtZero = true)
    {
        _selector = selector ?? new MaxUtilitySelection();
        _stopAtZero = stopAtZero;
        _bus = new EventBus();
        
    }

    public UtilityAiOrchestrator(ISelectionStrategy? selector = null, bool stopAtZero = true, EventBus? bus = null)
    {
        
        _selector = selector ?? new MaxUtilitySelection();
        _stopAtZero = stopAtZero;
        _bus = bus ?? new EventBus();
    }
    public UtilityAiOrchestrator AddSensor(ISensor s) { _sensors.Add(s); return this; }
    public UtilityAiOrchestrator AddModule(ICapabilityModule m) { _modules.Add(m); return this; }
    public async Task RunAsync(UserIntent intent, int maxTicks, CancellationToken ct, IOrchestrationSink? sink = null)
    {
        sink ??= NullSink.Instance;

        for (int tick = 0; tick < maxTicks; tick++)
        {
            if (TryHandleCancellation(_bus, intent, tick, sink, ct)) return;

            var rt = new Runtime(_bus, intent, tick);
            sink.OnTickStart(rt);

            await SenseAsyncAll(rt, ct);
            if (TryStopFromSensors(rt, sink)) return;

            var proposals = GatherProposalsOrStop(rt, sink);
            if (proposals is null) return;

            var scored = ScoreProposalsAndNotify(rt, proposals, sink);

            var choice = ChooseAndMaybeStopAtZero(rt, scored, sink, _stopAtZero);
            if (choice is null) return;

            await ActAndNotify(choice.Value.chosen, rt, sink, ct);
            _executionStack.Push(choice.Value.chosen.Id);
            _bus.Publish<Stack<string>>(_executionStack);
        }

        // If we reached here naturally, we hit the tick cap
        var finalRt = new Runtime(_bus, intent, maxTicks);
        sink.OnStopped(finalRt, OrchestrationStopReason.MaxTicksReached);
    }

    private static bool TryHandleCancellation(EventBus bus, UserIntent intent, int tick, IOrchestrationSink sink, CancellationToken ct)
    {
        if (!ct.IsCancellationRequested) return false;
        var cancelledRtEarly = new Runtime(bus, intent, tick);
        sink.OnStopped(cancelledRtEarly, OrchestrationStopReason.Cancelled);
        return true;
    }

    private async Task SenseAsyncAll(Runtime rt, CancellationToken ct)
    {
        foreach (var s in _sensors) await s.SenseAsync(rt, ct);
    }

    private static bool TryStopFromSensors(Runtime rt, IOrchestrationSink sink)
    {
        var stopEvt = rt.Bus.GetOrDefault<StopOrchestrationEvent>();
        if (stopEvt is null) return false;
        sink.OnStopped(rt, stopEvt.Reason);
        return true;
    }

    private List<Proposal>? GatherProposalsOrStop(Runtime rt, IOrchestrationSink sink)
    {
        var all = _modules.SelectMany(m => m.Propose(rt)).ToList();
        if (all.Count == 0)
        {
            sink.OnStopped(rt, OrchestrationStopReason.NoProposals);
            return null;
        }

        var eligible = all.Where(p => p.IsEligible(rt)).ToList();
        if (eligible.Count == 0)
        {
            sink.OnStopped(rt, OrchestrationStopReason.NoEligibleProposals);
            return null;
        }

        return eligible;
    }

    private List<(Proposal p, double u)> ScoreProposalsAndNotify(Runtime rt, IEnumerable<Proposal> proposals, IOrchestrationSink sink)
    {
        var scored = proposals
            .Select(p => (p, u: p.Utility(rt)))
            .OrderByDescending(x => x.u)
            .ToList();
        sink.OnScored(rt, scored.Select(x => (x.p, x.u)).ToList());
        return scored;
    }

    private (Proposal chosen, double utility)? ChooseAndMaybeStopAtZero(Runtime rt, List<(Proposal p, double u)> scored, IOrchestrationSink sink, bool stopAtZero)
    {
        var chosen = _selector.Select(scored.Select(x => (x.p, x.u)).ToList(), rt);
        var chosenUtility = scored.First(x => ReferenceEquals(x.p, chosen)).u;

        if (stopAtZero && chosenUtility == 0)
        {
            sink.OnChosen(rt, chosen, chosenUtility);
            sink.OnStopped(rt, OrchestrationStopReason.ZeroUtility);
            return null;
        }

        sink.OnChosen(rt, chosen, chosenUtility);
        return (chosen, chosenUtility);
    }

    private static async Task ActAndNotify(Proposal chosen, Runtime rt, IOrchestrationSink sink, CancellationToken ct)
    {
        await chosen.Act(ct);
        sink.OnActed(rt, chosen);
    }
}