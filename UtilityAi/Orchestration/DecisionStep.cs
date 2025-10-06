using UtilityAi.Actions;

namespace UtilityAi.Orchestration;

public sealed record DecisionStep(
    string ActionId,
    AgentOutcome Outcome,
    double Reward,
    TimeSpan Latency,
    DateTimeOffset Timestamp
);
