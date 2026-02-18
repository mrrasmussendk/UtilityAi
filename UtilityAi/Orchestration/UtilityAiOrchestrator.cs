using UtilityAi.Capabilities;
using UtilityAi.Sensor;
using UtilityAi.Utils;
using UtilityAi.Consideration;
using UtilityAi.Orchestration.Events;

namespace UtilityAi.Orchestration;

/// <summary>
/// The main orchestrator that runs the Sense → Propose → Score → Act loop.
/// Coordinates sensors, capability modules, and proposal selection to execute
/// the highest-utility action each tick based on the current state.
/// </summary>
/// <remarks>
/// The orchestrator follows the Utility AI pattern: each tick, sensors update the EventBus,
/// modules propose candidate actions with considerations, proposals are scored based on
/// current facts, and the highest-utility eligible proposal is executed.
/// </remarks>
public sealed class UtilityAiOrchestrator : IOrchestrator
{
    private readonly List<ISensor> _sensors = new();
    private readonly List<ICapabilityModule> _modules = new();
    private readonly ISelectionStrategy _selector;
    private readonly Stack<string> _executionStack = new Stack<string>();
    private readonly bool _stopAtZero = true;
    private EventBus _bus;

    /// <summary>
    /// Creates a new UtilityAiOrchestrator with a default EventBus.
    /// </summary>
    /// <param name="selector">Strategy for selecting the winning proposal. Defaults to MaxUtilitySelection.</param>
    /// <param name="stopAtZero">If true, stops orchestration when the chosen proposal has zero utility.</param>
    public UtilityAiOrchestrator(ISelectionStrategy? selector = null, bool stopAtZero = true)
    {
        _selector = selector ?? new MaxUtilitySelection();
        _stopAtZero = stopAtZero;
        _bus = new EventBus();
    }

    /// <summary>
    /// Creates a new UtilityAiOrchestrator with a provided EventBus.
    /// </summary>
    /// <param name="selector">Strategy for selecting the winning proposal. Defaults to MaxUtilitySelection.</param>
    /// <param name="stopAtZero">If true, stops orchestration when the chosen proposal has zero utility.</param>
    /// <param name="bus">The EventBus instance to use. If null, creates a new EventBus.</param>
    public UtilityAiOrchestrator(ISelectionStrategy? selector = null, bool stopAtZero = true, EventBus? bus = null)
    {
        _selector = selector ?? new MaxUtilitySelection();
        _stopAtZero = stopAtZero;
        _bus = bus ?? new EventBus();
    }

    /// <summary>
    /// Registers a sensor that will observe the environment and update the EventBus each tick.
    /// </summary>
    /// <param name="s">The sensor to register.</param>
    /// <returns>This orchestrator instance for fluent chaining.</returns>
    public UtilityAiOrchestrator AddSensor(ISensor s) { _sensors.Add(s); return this; }

    /// <summary>
    /// Registers a capability module that will propose candidate actions each tick.
    /// </summary>
    /// <param name="m">The module to register.</param>
    /// <returns>This orchestrator instance for fluent chaining.</returns>
    public UtilityAiOrchestrator AddModule(ICapabilityModule m) { _modules.Add(m); return this; }

    /// <summary>
    /// Runs the orchestration loop for the specified number of ticks or until a stop condition is met.
    /// </summary>
    /// <param name="intent">The user's intent, available to all sensors and modules via Runtime.</param>
    /// <param name="maxTicks">Maximum number of ticks to execute before stopping.</param>
    /// <param name="ct">Cancellation token to allow early termination.</param>
    /// <param name="sink">Optional sink for observing orchestration events. Uses NullSink if not provided.</param>
    /// <returns>A task that completes when orchestration finishes.</returns>
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
        var chosenUtility = scored.FirstOrDefault(x => ReferenceEquals(x.p, chosen)).u;

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