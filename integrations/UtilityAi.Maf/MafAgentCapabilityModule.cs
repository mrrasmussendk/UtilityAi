using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using UtilityAi.Capabilities;
using UtilityAi.Consideration;
using UtilityAi.Utils;

namespace UtilityAi.Maf;

/// <summary>
/// A UtilityAI capability module that wraps a Microsoft Agent Framework (MAF) <see cref="AIAgent"/>.
/// Each registered MAF agent produces a proposal whose action invokes the agent's RunAsync method.
/// The agent's response is published to the EventBus as a <see cref="MafAgentResult"/>.
/// </summary>
/// <remarks>
/// <para>
/// This module bridges the UtilityAI decision-making system with MAF agents. The utility scoring
/// determines WHICH agent runs, while MAF handles the actual agent execution.
/// </para>
/// <para>
/// Usage: Register MAF agents using <see cref="MafOrchestratorExtensions.AddMafAgent"/> and the
/// orchestrator will use utility-based scoring to select the best agent for each tick.
/// </para>
/// </remarks>
public sealed class MafAgentCapabilityModule : ICapabilityModule
{
    private readonly AIAgent _agent;
    private readonly string _agentName;
    private readonly IReadOnlyList<IConsideration> _considerations;
    private readonly IReadOnlyList<IEligibility> _eligibilities;
    private readonly Func<Runtime, string>? _messageProvider;
    private readonly double _prior;

    /// <summary>
    /// Creates a new MAF agent capability module.
    /// </summary>
    /// <param name="agent">The MAF agent to wrap.</param>
    /// <param name="agentName">A unique name identifying this agent in the orchestration system.</param>
    /// <param name="considerations">Considerations that score this agent's proposals (0.0-1.0).</param>
    /// <param name="eligibilities">Optional eligibility gates that must pass before the agent is considered.</param>
    /// <param name="messageProvider">
    /// Optional function that extracts the user message from the runtime context.
    /// If null, reads from the EventBus using the <c>UserIntent</c> query slot.
    /// </param>
    /// <param name="prior">Base prior probability for this agent's proposal (0.0-1.0). Default is 1.0.</param>
    public MafAgentCapabilityModule(
        AIAgent agent,
        string agentName,
        IReadOnlyList<IConsideration> considerations,
        IReadOnlyList<IEligibility>? eligibilities = null,
        Func<Runtime, string>? messageProvider = null,
        double prior = 1.0)
    {
        _agent = agent ?? throw new ArgumentNullException(nameof(agent));
        _agentName = agentName ?? throw new ArgumentNullException(nameof(agentName));
        _considerations = considerations;
        _eligibilities = eligibilities ?? Array.Empty<IEligibility>();
        _messageProvider = messageProvider;
        _prior = prior;
    }

    /// <inheritdoc />
    public IEnumerable<Proposal> Propose(Runtime rt)
    {
        // Check if this agent is registered and available
        var catalog = rt.Bus.GetOrDefault<MafAgentCatalog>();
        var registration = catalog?.Agents.FirstOrDefault(a => a.AgentName == _agentName);

        if (registration != null && !registration.IsAvailable)
            yield break;

        yield return new Proposal(
            id: $"maf.agent.{_agentName}",
            cons: _considerations,
            act: async ct =>
            {
                var message = GetMessage(rt);
                var session = registration?.Session ?? await _agent.CreateSessionAsync(ct);
                var response = await _agent.RunAsync(message, session, cancellationToken: ct);

                rt.Bus.Publish(new MafAgentResult(
                    AgentName: _agentName,
                    Response: response,
                    CompletedAt: DateTimeOffset.UtcNow
                ));
            },
            eligibilities: _eligibilities.Count > 0 ? _eligibilities : null
        )
        {
            Prior = _prior
        };
    }

    private string GetMessage(Runtime rt)
    {
        if (_messageProvider != null)
            return _messageProvider(rt);

        // Default: extract from UserIntent query slot
        if (rt.Intent.Slots?.TryGetValue("query", out var query) == true && query is string q)
            return q;

        return rt.Intent.Goal.Name;
    }
}
