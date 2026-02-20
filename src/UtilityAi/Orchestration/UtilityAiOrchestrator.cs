using UtilityAi.Capabilities;
using UtilityAi.Facts;
using UtilityAi.Sensor;
using UtilityAi.Utils;
using UtilityAi.Consideration;
using UtilityAi.Orchestration.Events;
using UtilityAi.Sensor.LLM;

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
    public UtilityAiOrchestrator AddSensor(ISensor s)
    {
        _sensors.Add(s);
        return this;
    }

    /// <summary>
    /// Registers a capability module that will propose candidate actions each tick.
    /// </summary>
    /// <param name="m">The module to register.</param>
    /// <returns>This orchestrator instance for fluent chaining.</returns>
    public UtilityAiOrchestrator AddModule(ICapabilityModule m)
    {
        _modules.Add(m);
        return this;
    }

    /// <summary>
    /// Runs the orchestration loop for the specified number of ticks or until a stop condition is met.
    /// </summary>
    /// <param name="maxTicks">Maximum number of ticks to execute before stopping.</param>
    /// <param name="ct">Cancellation token to allow early termination.</param>
    /// <param name="sink">Optional sink for observing orchestration events. Uses NullSink if not provided.</param>
    /// <returns>A task that completes when orchestration finishes.</returns>
    public async Task RunAsync(int maxTicks, CancellationToken ct, IOrchestrationSink? sink = null)
    {
        sink ??= NullSink.Instance;

        for (int tick = 0; tick < maxTicks; tick++)
        {
            var tickResult = await RunTickAsync(tick, ct, sink);
            if (tickResult == null) return;
        }

        // If we reached here naturally, we hit the tick cap
        var finalRt = new Runtime(_bus, maxTicks);
        sink.OnStopped(finalRt, OrchestrationStopReason.MaxTicksReached);
    }

    /// <summary>
    /// Runs the orchestration loop until it reaches quiescence (utility below threshold) or hit max ticks.
    /// Perfect for chat agents where you want to "finish the thought".
    /// </summary>
    public async Task RunUntilQuiescentAsync(double threshold, int maxTicks, CancellationToken ct,
        IOrchestrationSink? sink = null)
    {
        sink ??= NullSink.Instance;
        for (int tick = 0; tick < maxTicks; tick++)
        {
            var result = await RunTickAsync(tick, ct, sink);
            if (result == null) return;

            if (result.ChosenUtility < threshold)
            {
                var rt = new Runtime(_bus, tick);
                sink.OnStopped(rt, OrchestrationStopReason.Quiescent);
                return;
            }
        }

        var finalRt = new Runtime(_bus, maxTicks);
        sink.OnStopped(finalRt, OrchestrationStopReason.MaxTicksReached);
    }

    public async Task<OrchestrationTick?> RunTickAsync(int tick, CancellationToken ct, IOrchestrationSink? sink = null)
    {
        sink ??= NullSink.Instance;

        if (TryHandleCancellation(_bus, tick, sink, ct)) return null;

        var rt = new Runtime(_bus, tick);
        
        CreateCapAbilitySnapShot(rt);
        sink.OnTickStart(rt);

        await SenseAsyncAll(rt, ct);
        if (TryStopFromSensors(rt, sink)) return null;

        var proposals = GatherProposalsOrStop(rt, sink);
        if (proposals is null) return null;

        var scored = ScoreProposalsAndNotify(rt, proposals, sink);

        var choice = ChooseAndMaybeStopAtZero(rt, scored, sink, _stopAtZero);
        if (choice is null) return null;

        await ActAndNotify(choice.Value.chosen, rt, sink, ct);

        _executionStack.Push(choice.Value.chosen.Id);
        _bus.Publish<IReadOnlyList<string>>(_executionStack.ToList());

        // Update execution history
        var existingHistory = _bus.GetOrDefault<ExecutionHistory>();
        var executedAction = new ExecutedAction(
            ProposalId: choice.Value.chosen.Id,
            Description: choice.Value.chosen.Description,
            TickNumber: tick,
            Timestamp: DateTimeOffset.UtcNow
        );

        var newHistory = existingHistory == null
            ? new ExecutionHistory(new[] { executedAction })
            : existingHistory.WithAction(executedAction);

        _bus.Publish(newHistory);

        return new OrchestrationTick(tick, scored, choice.Value.chosen, choice.Value.utility);
    }

    private static bool TryHandleCancellation(EventBus bus, int tick, IOrchestrationSink sink, CancellationToken ct)
    {
        if (!ct.IsCancellationRequested) return false;
        var cancelledRtEarly = new Runtime(bus, tick);
        sink.OnStopped(cancelledRtEarly, OrchestrationStopReason.Cancelled);
        return true;
    }

    private void CreateCapAbilitySnapShot(Runtime rt)
    {
        var capabilities = this.GetCapabilitiesInfo();
        rt.Bus.Publish(capabilities);
        rt.Bus.Publish(new CapabilitiesSnapshot(capabilities));
    }

    private async Task SenseAsyncAll(Runtime rt, CancellationToken ct)
    {
        await Task.WhenAll(_sensors.Select(s => s.SenseAsync(rt, ct)));
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

    private List<(Proposal p, double u)> ScoreProposalsAndNotify(Runtime rt, IEnumerable<Proposal> proposals,
        IOrchestrationSink sink)
    {
        var scored = proposals
            .Select(p => (p, u: p.Utility(rt)))
            .OrderByDescending(x => x.u)
            .ToList();
        sink.OnScored(rt, scored.Select(x => (x.p, x.u)).ToList());
        return scored;
    }

    private (Proposal chosen, double utility)? ChooseAndMaybeStopAtZero(Runtime rt, List<(Proposal p, double u)> scored,
        IOrchestrationSink sink, bool stopAtZero)
    {
        var chosen = _selector.Select(scored.Select(x => (x.p, x.u)).ToList(), rt);
        var match = scored.FirstOrDefault(x =>
            ReferenceEquals(x.p, chosen) ||
            string.Equals(x.p.Id, chosen.Id, StringComparison.Ordinal));

        // If chosen proposal is not found in scored list, this indicates a selector bug
        if (match.p == null)
        {
            throw new InvalidOperationException("Selection strategy returned a proposal not in the scored list.");
        }

        var chosenProposal = match.p;
        var chosenUtility = match.u;

        // Note: We use a small epsilon check because Proposal.Utility uses 1e-6 as a floor for considerations.
        // If utility is at or below this floor, we treat it as "zero" for stopping purposes.
        if (stopAtZero && (chosenUtility <= 1.1e-6))
        {
            sink.OnStopped(rt, OrchestrationStopReason.ZeroUtility);
            return null;
        }

        sink.OnChosen(rt, chosenProposal, chosenUtility);
        return (chosenProposal, chosenUtility);
    }

    private static async Task ActAndNotify(Proposal chosen, Runtime rt, IOrchestrationSink sink, CancellationToken ct)
    {
        await chosen.Act(ct);
        sink.OnActed(rt, chosen);
    }

    /// <summary>
    /// Introspects all registered capability modules and returns metadata about their potential actions.
    /// Useful for planning, LLM context building, and debugging.
    /// </summary>
    /// <returns>A list of capability information including all proposals each module can generate.</returns>
    public IReadOnlyList<CapabilityInfo> GetCapabilitiesInfo()
    {
        // Create a dummy runtime to get proposals from modules
        var dummyBus = new EventBus();
        var dummyRt = new Runtime(dummyBus, 0);

        return _modules.Select(module =>
        {
            var moduleName = module.GetType().Name;
            var moduleTypeName = module.GetType().FullName ?? moduleName;

            var proposals = module.Propose(dummyRt).Select(p => new ProposalInfo(
                ProposalId: p.Id,
                Description: p.Description,
                Prior: p.Prior,
                Temperature: p.Temperature,
                ConsiderationNames: p.Considerations.Select(c => c.Name).ToList(),
                EligibilityNames: p.Eligibilities.Select(e => e.GetType().Name).ToList(),
                NoRepeat: p.NoRepeat,
                JsonOutput: p.JsonOutput,
                IntentMatch: p.IntentMatch,
                IntentParameters: p.IntentParameters
            )).ToList();

            return new CapabilityInfo(moduleName, moduleTypeName, proposals);
        }).ToList();
    }

    /// <summary>
    /// Introspects all registered capability modules and returns metadata about their potential actions.
    /// Useful for planning, LLM context building, and debugging.
    /// </summary>
    /// <returns>A list of capability information including all proposals each module can generate.</returns>
    public IReadOnlyList<CapabilityInfo> GetCapabilitiesInfo(Runtime rt)
    {
        return _modules.Select(module =>
        {
            var moduleName = module.GetType().Name;
            var moduleTypeName = module.GetType().FullName ?? moduleName;

            var proposals = module.Propose(rt).Select(p => new ProposalInfo(
                ProposalId: p.Id,
                Description: p.Description,
                Prior: p.Prior,
                Temperature: p.Temperature,
                ConsiderationNames: p.Considerations.Select(c => c.Name).ToList(),
                EligibilityNames: p.Eligibilities.Select(e => e.GetType().Name).ToList(),
                NoRepeat: p.NoRepeat,
                JsonOutput: p.JsonOutput,
                IntentMatch: p.IntentMatch,
                IntentParameters: p.IntentParameters
            )).ToList();

            return new CapabilityInfo(moduleName, moduleTypeName, proposals);
        }).ToList();
    }
}
