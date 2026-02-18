using Microsoft.Agents.AI;

namespace UtilityAi.Maf;

/// <summary>
/// Represents the registration of a MAF <see cref="AIAgent"/> within the UtilityAI orchestration system.
/// Published to the EventBus to indicate available agents and their state.
/// </summary>
public sealed record MafAgentRegistration(
    string AgentName,
    AIAgent Agent,
    AgentSession? Session = null,
    bool IsAvailable = true
);

/// <summary>
/// Represents the result of a MAF agent invocation, published to the EventBus
/// after a <see cref="MafAgentCapabilityModule"/> executes an agent.
/// </summary>
public sealed record MafAgentResult(
    string AgentName,
    AgentResponse Response,
    DateTimeOffset CompletedAt
)
{
    /// <summary>
    /// Gets the text content of the agent response.
    /// </summary>
    public string Text => Response.Text;
}

/// <summary>
/// Represents the set of all registered MAF agents available to the orchestrator.
/// Published to the EventBus by <see cref="MafAgentSensor"/>.
/// </summary>
public sealed record MafAgentCatalog(IReadOnlyList<MafAgentRegistration> Agents);
