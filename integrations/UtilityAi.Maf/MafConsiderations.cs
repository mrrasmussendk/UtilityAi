using UtilityAi.Consideration;
using UtilityAi.Utils;

namespace UtilityAi.Maf;

/// <summary>
/// A consideration that scores based on whether a specific MAF agent is available in the catalog.
/// Returns 1.0 if the agent is available, 0.0 otherwise.
/// </summary>
public sealed class MafAgentAvailable : IConsideration
{
    private readonly string _agentName;

    /// <summary>
    /// Creates a consideration that checks if a named MAF agent is available.
    /// </summary>
    /// <param name="agentName">The name of the agent to check for availability.</param>
    public MafAgentAvailable(string agentName)
    {
        _agentName = agentName ?? throw new ArgumentNullException(nameof(agentName));
    }

    /// <inheritdoc />
    public string Name => $"maf.agent.available.{_agentName}";

    /// <inheritdoc />
    public double Evaluate(Runtime rt)
    {
        var catalog = rt.Bus.GetOrDefault<MafAgentCatalog>();
        if (catalog == null) return 0.0;

        var registration = catalog.Agents.FirstOrDefault(a => a.AgentName == _agentName);
        return registration is { IsAvailable: true } ? 1.0 : 0.0;
    }
}

/// <summary>
/// A consideration that checks whether a previous MAF agent result exists on the EventBus.
/// Useful for chaining agents: run agent B only if agent A has already produced a result.
/// </summary>
public sealed class HasMafAgentResult : IConsideration
{
    private readonly string? _agentName;
    private readonly bool _invert;

    /// <summary>
    /// Creates a consideration that checks for a MAF agent result.
    /// </summary>
    /// <param name="agentName">If specified, checks for a result from this specific agent. If null, checks for any result.</param>
    /// <param name="invert">If true, returns 1.0 when no result exists (useful for "not yet processed" checks).</param>
    public HasMafAgentResult(string? agentName = null, bool invert = false)
    {
        _agentName = agentName;
        _invert = invert;
    }

    /// <inheritdoc />
    public string Name => _agentName != null
        ? $"maf.has.result.{_agentName}"
        : "maf.has.result.any";

    /// <inheritdoc />
    public double Evaluate(Runtime rt)
    {
        var result = rt.Bus.GetOrDefault<MafAgentResult>();
        bool hasResult;

        if (_agentName != null)
            hasResult = result?.AgentName == _agentName;
        else
            hasResult = result != null;

        var score = hasResult ? 1.0 : 0.0;
        return _invert ? 1.0 - score : score;
    }
}

/// <summary>
/// An eligibility gate that requires a specific MAF agent to be registered and available.
/// If the agent is not available, the proposal is excluded from scoring entirely.
/// </summary>
public sealed class RequiresMafAgent : IEligibility
{
    private readonly string _agentName;

    /// <summary>
    /// Creates an eligibility check for a named MAF agent.
    /// </summary>
    /// <param name="agentName">The name of the required agent.</param>
    public RequiresMafAgent(string agentName)
    {
        _agentName = agentName ?? throw new ArgumentNullException(nameof(agentName));
    }

    /// <inheritdoc />
    public string Name => $"maf.requires.{_agentName}";

    /// <inheritdoc />
    public bool IsEligible(Runtime rt)
    {
        var catalog = rt.Bus.GetOrDefault<MafAgentCatalog>();
        if (catalog == null) return false;

        return catalog.Agents.Any(a => a.AgentName == _agentName && a.IsAvailable);
    }
}
