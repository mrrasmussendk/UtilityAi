using Microsoft.Agents.AI;
using UtilityAi.Consideration;
using UtilityAi.Orchestration;
using UtilityAi.Utils;

namespace UtilityAi.Maf;

/// <summary>
/// Extension methods for integrating MAF agents with the UtilityAI orchestrator.
/// </summary>
public static class MafOrchestratorExtensions
{
    /// <summary>
    /// Registers a MAF agent as a capability module with the orchestrator.
    /// The agent will be proposed as a candidate action each tick, scored by its considerations.
    /// </summary>
    /// <param name="orchestrator">The orchestrator to register the agent with.</param>
    /// <param name="agent">The MAF agent to register.</param>
    /// <param name="agentName">A unique name for this agent in the orchestration system.</param>
    /// <param name="considerations">Considerations that score this agent's proposals.</param>
    /// <param name="eligibilities">Optional eligibility gates.</param>
    /// <param name="messageProvider">Optional function to extract the user message from runtime context.</param>
    /// <param name="prior">Base prior probability (0.0-1.0). Default is 1.0.</param>
    /// <returns>The orchestrator instance for fluent chaining.</returns>
    public static UtilityAiOrchestrator AddMafAgent(
        this UtilityAiOrchestrator orchestrator,
        AIAgent agent,
        string agentName,
        IReadOnlyList<IConsideration> considerations,
        IReadOnlyList<IEligibility>? eligibilities = null,
        Func<Runtime, string>? messageProvider = null,
        double prior = 1.0)
    {
        var module = new MafAgentCapabilityModule(
            agent, agentName, considerations, eligibilities, messageProvider, prior);
        orchestrator.AddModule(module);
        return orchestrator;
    }

    /// <summary>
    /// Registers a MAF agent sensor that tracks agent availability.
    /// Call this once after registering all MAF agents to enable agent catalog sensing.
    /// </summary>
    /// <param name="orchestrator">The orchestrator to register the sensor with.</param>
    /// <param name="registrations">The agent registrations to track.</param>
    /// <returns>The orchestrator instance for fluent chaining.</returns>
    public static UtilityAiOrchestrator AddMafAgentSensor(
        this UtilityAiOrchestrator orchestrator,
        params MafAgentRegistration[] registrations)
    {
        var sensor = new MafAgentSensor();
        foreach (var reg in registrations)
            sensor.Register(reg);

        orchestrator.AddSensor(sensor);
        return orchestrator;
    }
}
