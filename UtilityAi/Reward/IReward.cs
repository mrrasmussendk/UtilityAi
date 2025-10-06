using UtilityAi.Actions;

namespace UtilityAi.Utils;

public interface IReward
{
    double Score(IBlackboard bb, string agentId, AgentOutcome outcome);
}