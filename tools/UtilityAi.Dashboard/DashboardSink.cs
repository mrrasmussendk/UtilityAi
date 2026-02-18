using UtilityAi.Consideration;
using UtilityAi.Orchestration;
using UtilityAi.Utils;

namespace UtilityAi.Dashboard;

/// <summary>
/// An <see cref="IOrchestrationSink"/> that captures orchestration events into a
/// <see cref="DashboardState"/> for real-time visualization.
/// </summary>
/// <remarks>
/// Wire this sink into the orchestrator to feed the dashboard:
/// <code>
/// var state = new DashboardState();
/// var sink = new DashboardSink(state);
/// await orchestrator.RunAsync(intent, maxTicks: 10, ct, sink: sink);
/// </code>
/// </remarks>
public sealed class DashboardSink : IOrchestrationSink
{
    private readonly DashboardState _state;

    /// <summary>
    /// Creates a new <see cref="DashboardSink"/> that writes to the given state.
    /// </summary>
    public DashboardSink(DashboardState state)
    {
        _state = state ?? throw new ArgumentNullException(nameof(state));
    }

    /// <summary>
    /// The underlying state object this sink writes to.
    /// </summary>
    public DashboardState State => _state;

    /// <inheritdoc />
    public void OnTickStart(Runtime rt) { }

    /// <inheritdoc />
    public void OnScored(Runtime rt, IReadOnlyList<(Proposal Proposal, double Utility)> scored)
    {
        _state.RecordScored(rt.Tick, scored, rt);
    }

    /// <inheritdoc />
    public void OnChosen(Runtime rt, Proposal chosen, double utility)
    {
        _state.RecordChosen(rt.Tick, chosen, utility);
    }

    /// <inheritdoc />
    public void OnActed(Runtime rt, Proposal chosen)
    {
        _state.RecordActed(chosen);
    }

    /// <inheritdoc />
    public void OnStopped(Runtime rt, OrchestrationStopReason reason)
    {
        _state.RecordStopped(reason);
    }
}
