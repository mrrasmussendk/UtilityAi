using UtilityAi.Actions;

namespace UtilityAi.Utils;

public sealed class DefaultReward : IReward
{
    public double Score(IBlackboard bb, string agentId, AgentOutcome o)
    {
        var factual = bb.GetOr("answer:verifier_score", 0.0); // 0..1
        var thumbs = bb.GetOr("user:thumbs_up", 0.5);         // 0..1
        var slaTight = bb.GetOr("sla:tight", 0.0);            // 0..1
        var latencyPenalty = Clamp01(slaTight * Sigmoid(o.Latency.TotalSeconds / 5.0));
        var costPenalty = Clamp01(o.Cost);
        var r = 0.5 * factual + 0.3 * thumbs + 0.2 * (1 - latencyPenalty) - 0.1 * costPenalty;
        return Clamp01(r);
    }
    private static double Clamp01(double v) => v < 0 ? 0 : (v > 1 ? 1 : v);
    private static double Sigmoid(double z) => 1.0 / (1.0 + Math.Exp(-z));
}