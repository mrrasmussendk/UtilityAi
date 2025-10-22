using UtilityAi.Consideration;
using UtilityAi.Utils;

namespace UtilityAi.Orchestration;

/// <summary>
/// Explains why the orchestrator stopped executing ticks.
/// </summary>
public enum OrchestrationStopReason
{
    /// <summary>No capability module produced any <see cref="Proposal"/> for the current tick.</summary>
    NoProposals,
    /// <summary>The chosen proposal's utility evaluated to zero and StopAtZero was enabled.</summary>
    ZeroUtility,
    /// <summary>The orchestrator executed the configured maximum number of ticks.</summary>
    MaxTicksReached,
    /// <summary>The provided <see cref="CancellationToken"/> was signaled.</summary>
    Cancelled,
    GoalAchieved,
    SensorRequestedStop,
}

/// <summary>
/// A snapshot of one decision tick: scored candidates, the chosen proposal and its utility.
/// </summary>
/// <param name="Tick">Tick index starting from zero.</param>
/// <param name="Scored">All candidates with their computed utility for this tick.</param>
/// <param name="Chosen">The proposal selected by the selection strategy.</param>
/// <param name="ChosenUtility">Utility of the chosen proposal (0..1).</param>
public sealed record OrchestrationTick(
    int Tick,
    IReadOnlyList<(Proposal Proposal, double Utility)> Scored,
    Proposal Chosen,
    double ChosenUtility
);

/// <summary>
/// Observer/telemetry sink for the orchestrator lifecycle. Implementations can log, record,
/// export metrics, or build a report without affecting control flow.
/// </summary>
public interface IOrchestrationSink
{
    /// <summary>Called at the start of each tick after creating the <see cref="Runtime"/>.</summary>
    void OnTickStart(Runtime rt);
    /// <summary>Called after proposals have been evaluated to utilities for this tick.</summary>
    void OnScored(Runtime rt, IReadOnlyList<(Proposal Proposal, double Utility)> scored);
    /// <summary>Called with the selected <see cref="Proposal"/> and its utility.</summary>
    void OnChosen(Runtime rt, Proposal chosen, double utility);
    /// <summary>Called after the chosen proposal's action has completed.</summary>
    void OnActed(Runtime rt, Proposal chosen);
    /// <summary>Called exactly once when the orchestrator stops and indicates the reason.</summary>
    void OnStopped(Runtime rt, OrchestrationStopReason reason);
}

/// <summary>
/// Default no-op sink. Use when you do not need any output or logging.
/// </summary>
public sealed class NullSink : IOrchestrationSink
{
    /// <summary>A single reusable instance.</summary>
    public static readonly NullSink Instance = new();
    private NullSink() {}
    public void OnTickStart(Runtime rt) {}
    public void OnScored(Runtime rt, IReadOnlyList<(Proposal Proposal, double Utility)> scored) {}
    public void OnChosen(Runtime rt, Proposal chosen, double utility) {}
    public void OnActed(Runtime rt, Proposal chosen) {}
    public void OnStopped(Runtime rt, OrchestrationStopReason reason) {}
}

/// <summary>
/// Fan-out sink that forwards all events to multiple sinks.
/// </summary>
public sealed class CompositeSink : IOrchestrationSink
{
    private readonly IOrchestrationSink[] _sinks;
    /// <summary>Create a composite forwarding to the provided sinks in order.</summary>
    public CompositeSink(params IOrchestrationSink[] sinks) { _sinks = sinks; }
    public void OnTickStart(Runtime rt) { foreach (var s in _sinks) s.OnTickStart(rt); }
    public void OnScored(Runtime rt, IReadOnlyList<(Proposal Proposal, double Utility)> scored) { foreach (var s in _sinks) s.OnScored(rt, scored); }
    public void OnChosen(Runtime rt, Proposal chosen, double utility) { foreach (var s in _sinks) s.OnChosen(rt, chosen, utility); }
    public void OnActed(Runtime rt, Proposal chosen) { foreach (var s in _sinks) s.OnActed(rt, chosen); }
    public void OnStopped(Runtime rt, OrchestrationStopReason reason) { foreach (var s in _sinks) s.OnStopped(rt, reason); }
}

/// <summary>
/// Simple in-memory sink that records per-tick decisions for later inspection (tests, debugging, reports).
/// </summary>
public sealed class RecordingSink : IOrchestrationSink
{
    private readonly List<OrchestrationTick> _ticks = new();
    private IReadOnlyList<(Proposal Proposal, double Utility)> _lastScored = Array.Empty<(Proposal, double)>();

    /// <summary>Immutable list of recorded ticks in order.</summary>
    public IReadOnlyList<OrchestrationTick> Ticks => _ticks;

    /// <inheritdoc />
    public void OnTickStart(Runtime rt)
    {
        _lastScored = Array.Empty<(Proposal, double)>();
    }

    /// <inheritdoc />
    public void OnScored(Runtime rt, IReadOnlyList<(Proposal Proposal, double Utility)> scored)
    {
        // Copy to avoid future mutation issues
        _lastScored = scored.ToList();
    }

    /// <inheritdoc />
    public void OnChosen(Runtime rt, Proposal chosen, double utility)
    {
        _ticks.Add(new OrchestrationTick(rt.Tick, _lastScored, chosen, utility));
    }

    /// <inheritdoc />
    public void OnActed(Runtime rt, Proposal chosen) { }

    /// <inheritdoc />
    public void OnStopped(Runtime rt, OrchestrationStopReason reason) { }
}
