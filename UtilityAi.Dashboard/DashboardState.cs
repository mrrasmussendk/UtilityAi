using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using UtilityAi.Consideration;
using UtilityAi.Orchestration;

[assembly: InternalsVisibleTo("UtilityAi.Dashboard.Tests")]

namespace UtilityAi.Dashboard;

/// <summary>
/// Snapshot of a single proposal with its computed utility and consideration details.
/// </summary>
public sealed class ProposalSnapshot
{
    public string Id { get; init; } = "";
    public double Utility { get; init; }
    public double Prior { get; init; }
    public double Temperature { get; init; }
    public bool IsChosen { get; init; }
    public List<ConsiderationSnapshot> Considerations { get; init; } = new();
    public List<EligibilitySnapshot> Eligibilities { get; init; } = new();
}

/// <summary>
/// Snapshot of a single consideration's evaluation result.
/// </summary>
public sealed class ConsiderationSnapshot
{
    public string Name { get; init; } = "";
    public double Score { get; init; }
}

/// <summary>
/// Snapshot of a single eligibility gate's result.
/// </summary>
public sealed class EligibilitySnapshot
{
    public string Name { get; init; } = "";
    public bool IsEligible { get; init; }
}

/// <summary>
/// Snapshot of one orchestration tick.
/// </summary>
public sealed class TickSnapshot
{
    public int Tick { get; init; }
    public string? ChosenProposalId { get; init; }
    public double ChosenUtility { get; init; }
    public List<ProposalSnapshot> Proposals { get; init; } = new();
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Holds the current state of the dashboard, updated by <see cref="DashboardSink"/>.
/// Thread-safe for concurrent reads and writes.
/// </summary>
public sealed class DashboardState
{
    private readonly object _lock = new();
    private readonly List<TickSnapshot> _ticks = new();
    private readonly ConcurrentDictionary<string, double> _priorOverrides = new();
    private readonly ConcurrentDictionary<string, double> _temperatureOverrides = new();

    private TickSnapshot? _currentTick;
    private int _currentTickIndex = -1;
    private string? _activeProposalId;
    private OrchestrationStopReason? _stopReason;
    private long _version;

    /// <summary>
    /// Monotonically increasing version number. Changes on every state update.
    /// </summary>
    public long Version => Interlocked.Read(ref _version);

    /// <summary>
    /// The most recent tick snapshot, or null if no ticks have been recorded.
    /// </summary>
    public TickSnapshot? CurrentTick
    {
        get { lock (_lock) return _currentTick; }
    }

    /// <summary>
    /// The ID of the currently executing proposal, if any.
    /// </summary>
    public string? ActiveProposalId
    {
        get { lock (_lock) return _activeProposalId; }
    }

    /// <summary>
    /// The reason orchestration stopped, if it has stopped.
    /// </summary>
    public OrchestrationStopReason? StopReason
    {
        get { lock (_lock) return _stopReason; }
    }

    /// <summary>
    /// All recorded tick snapshots.
    /// </summary>
    public IReadOnlyList<TickSnapshot> Ticks
    {
        get { lock (_lock) return _ticks.ToList(); }
    }

    /// <summary>
    /// User-specified prior overrides for proposals, keyed by proposal ID.
    /// </summary>
    public IReadOnlyDictionary<string, double> PriorOverrides => _priorOverrides;

    /// <summary>
    /// User-specified temperature overrides for proposals, keyed by proposal ID.
    /// </summary>
    public IReadOnlyDictionary<string, double> TemperatureOverrides => _temperatureOverrides;

    internal void RecordScored(int tick, IReadOnlyList<(Proposal Proposal, double Utility)> scored,
        Utils.Runtime rt)
    {
        var proposals = scored.Select(s =>
        {
            var considerations = s.Proposal.Considerations.Select(c => new ConsiderationSnapshot
            {
                Name = c.Name,
                Score = c.Evaluate(rt)
            }).ToList();

            var eligibilities = s.Proposal.Eligibilities.Select(e => new EligibilitySnapshot
            {
                Name = e.Name,
                IsEligible = e.IsEligible(rt)
            }).ToList();

            return new ProposalSnapshot
            {
                Id = s.Proposal.Id,
                Utility = s.Utility,
                Prior = s.Proposal.Prior,
                Temperature = s.Proposal.Temperature,
                Considerations = considerations,
                Eligibilities = eligibilities
            };
        }).ToList();

        lock (_lock)
        {
            _currentTickIndex = tick;
            _currentTick = new TickSnapshot
            {
                Tick = tick,
                Proposals = proposals,
                Timestamp = DateTimeOffset.UtcNow
            };
        }

        Interlocked.Increment(ref _version);
    }

    internal void RecordChosen(int tick, Proposal chosen, double utility)
    {
        lock (_lock)
        {
            _activeProposalId = chosen.Id;
            if (_currentTick != null)
            {
                _currentTick = new TickSnapshot
                {
                    Tick = _currentTick.Tick,
                    ChosenProposalId = chosen.Id,
                    ChosenUtility = utility,
                    Timestamp = _currentTick.Timestamp,
                    Proposals = _currentTick.Proposals.Select(p => p.Id == chosen.Id
                        ? new ProposalSnapshot
                        {
                            Id = p.Id,
                            Utility = p.Utility,
                            Prior = p.Prior,
                            Temperature = p.Temperature,
                            IsChosen = true,
                            Considerations = p.Considerations,
                            Eligibilities = p.Eligibilities
                        }
                        : p).ToList()
                };
            }
        }

        Interlocked.Increment(ref _version);
    }

    internal void RecordActed(Proposal chosen)
    {
        lock (_lock)
        {
            if (_currentTick != null)
                _ticks.Add(_currentTick);
        }

        Interlocked.Increment(ref _version);
    }

    internal void RecordStopped(OrchestrationStopReason reason)
    {
        lock (_lock)
        {
            _stopReason = reason;
            _activeProposalId = null;
        }

        Interlocked.Increment(ref _version);
    }

    /// <summary>
    /// Set a prior override for a proposal. The override is available via <see cref="PriorOverrides"/>
    /// and can be applied by the user in their capability modules.
    /// </summary>
    public void SetPriorOverride(string proposalId, double prior)
    {
        _priorOverrides[proposalId] = Math.Clamp(prior, 0.0, 1.0);
        Interlocked.Increment(ref _version);
    }

    /// <summary>
    /// Set a temperature override for a proposal. The override is available via <see cref="TemperatureOverrides"/>
    /// and can be applied by the user in their capability modules.
    /// </summary>
    public void SetTemperatureOverride(string proposalId, double temperature)
    {
        _temperatureOverrides[proposalId] = Math.Max(temperature, 0.0);
        Interlocked.Increment(ref _version);
    }

    /// <summary>
    /// Remove a prior override for a proposal.
    /// </summary>
    public void RemovePriorOverride(string proposalId)
    {
        _priorOverrides.TryRemove(proposalId, out _);
        Interlocked.Increment(ref _version);
    }

    /// <summary>
    /// Remove a temperature override for a proposal.
    /// </summary>
    public void RemoveTemperatureOverride(string proposalId)
    {
        _temperatureOverrides.TryRemove(proposalId, out _);
        Interlocked.Increment(ref _version);
    }

    /// <summary>
    /// Clear all recorded state. Useful when starting a new orchestration run.
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            _ticks.Clear();
            _currentTick = null;
            _currentTickIndex = -1;
            _activeProposalId = null;
            _stopReason = null;
        }

        Interlocked.Increment(ref _version);
    }
}
