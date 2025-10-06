using UtilityAi.Actions;
using UtilityAi.Orchestration;
using UtilityAi.Policies;
using UtilityAi.Sensor;
using UtilityAi.Utils;

namespace Example.Orchestrator;

public static class FeatureVector
{
    public static IReadOnlyDictionary<string, double> From(IBlackboard bb) => new Dictionary<string, double>
    {
        ["recency"]     = bb.GetOr("signal:recency", 0.0),
        ["uncertainty"] = bb.GetOr("signal:uncertainty", 0.5),
        ["safety"]      = bb.GetOr("risk:safety", 0.0),
        ["budget"]      = bb.GetOr("budget:left", 1.0),
        ["sla"]         = bb.GetOr("sla:tight", 0.0),
        ["outmode_conf"]= bb.GetOr("signal:output_mode_conf", 0.7)
    };
}

public sealed class Orchestrator : IOrchestrator
{
    private readonly List<ISensor> _sensors;
    private readonly List<IAction> _agents;
    private readonly IPolicy _policy;
    private readonly IReward _reward;

    public Orchestrator(IEnumerable<ISensor> sensors, IEnumerable<IAction> agents, IPolicy policy, IReward reward)
    {
        _sensors = sensors.ToList();
        _agents = agents.ToList();
        _policy = policy;
        _reward = reward;
    }

    public async Task<OrchestrationResult> RunAsync(IBlackboard bb, CancellationToken ct)
    {
        if (bb is null) throw new ArgumentNullException(nameof(bb));

        var steps = new List<DecisionStep>(capacity: 16);

        var maxTicks   = bb.GetOr("orchestrator:max_ticks", 8);
        var ticks      = 0;
        var stagnation = 0;
        var lastHash   = Hash(bb.Snapshot());
        string doneReason = "done:max_ticks"; // default if loop exits by ticks

        while (ticks < maxTicks)
        {
            ct.ThrowIfCancellationRequested();
            ticks++;

            // 1) Sense
            foreach (var s in _sensors) await s.SenseAsync(bb, ct);

            // 2) Features
            var x = FeatureVector.From(bb);

            // 3) Candidates
            var candidates = _agents.Where(a => a.Gate(bb)).ToList();
            if (candidates.Count == 0) { doneReason = "done:no_candidates"; break; }

            // 4) Choose
            var chosenId = _policy.Choose(bb, candidates, x);
            var chosen   = candidates.First(a => a.Id == chosenId);

            // Bookkeeping
            bb.Set($"agent:{chosen.Id}:count", bb.GetOr($"agent:{chosen.Id}:count", 0) + 1);
            bb.Set($"agent:{chosen.Id}:last_used", DateTimeOffset.UtcNow);
            bb.Set("orchestrator:decisions", bb.GetOr("orchestrator:decisions", 0) + 1);
            bb.Set("orchestrator:last", chosen.Id);

            // 5) Act
            var t0 = DateTimeOffset.UtcNow;
            var outcome = await chosen.ActAsync(bb, ct);
            var latency = DateTimeOffset.UtcNow - t0;

            // Telemetry (EWMA)
            Ewma(bb, $"agent:{chosen.Id}:ewma_success", outcome.Success ? 1 : 0);
            Ewma(bb, $"agent:{chosen.Id}:ewma_latency", latency.TotalSeconds, baseVal: 1.0);
            Ewma(bb, $"agent:{chosen.Id}:ewma_cost",    outcome.Cost,        baseVal: 0.0);

            // 6) Reward & Learn
            var r = _reward.Score(bb, chosen.Id, outcome);
            _policy.Learn(chosen.Id, x, r);

            steps.Add(new DecisionStep(chosen.Id, outcome, r, latency, DateTimeOffset.UtcNow));

            // 7) Termination
            if (IsDone(bb))
            {
                doneReason = bb.GetOr("orchestrator:done_reason", "done:success");
                break;
            }

            // Stagnation guard
            var newHash = Hash(bb.Snapshot());
            stagnation = newHash == lastHash ? stagnation + 1 : 0;
            lastHash   = newHash;
            if (stagnation >= 2)
            {
                bb.Set("orchestrator:done_reason", "done:stagnation");
                doneReason = "done:stagnation";
                break;
            }
        }

        // If loop ended by ticks and no explicit reason set inside IsDone:
        if (doneReason == "done:max_ticks" && bb.Has("orchestrator:done_reason"))
            doneReason = bb.GetOr("orchestrator:done_reason", doneReason);

        return new OrchestrationResult(
            DoneReason: doneReason,
            Ticks: ticks,
            Steps: steps,
            FinalSnapshot: bb.Snapshot()
        );
    }

    private static void Ewma(IBlackboard bb, string key, double value, double alpha = 0.3, double baseVal = 0.5)
    {
        var prev = bb.GetOr(key, baseVal);
        bb.Set(key, (1 - alpha) * prev + alpha * value);
    }

    protected bool IsDone(IBlackboard bb)
    {
        var mode = bb.GetOr("task:output_mode", "text");
        var sources = bb.GetOr("search:count", 0);
        var quality = bb.GetOr("answer:verifier_score", 0.0);
        var hasAudio = bb.Has("answer:audio_url");
        var hasText = bb.Has("answer:text");

        if ((mode is "audio" or "both") && hasAudio && quality >= 0.6 && sources >= 3)
        { bb.Set("orchestrator:done_reason", "done:success"); return true; }

        if ((mode is "text" or "both") && hasText && quality >= 0.6 && sources >= 3)
        { bb.Set("orchestrator:done_reason", "done:success"); return true; }

        // budget / sla guards (optional)
        var ticks = bb.GetOr("orchestrator:ticks", 0) + 1;
        bb.Set("orchestrator:ticks", ticks);
        if (ticks >= bb.GetOr("orchestrator:max_ticks", 8)) { bb.Set("orchestrator:done_reason", "done:max_ticks"); return true; }

        if (bb.GetOr("budget:left", 0.2) <= 0.05) { bb.Set("orchestrator:done_reason", "done:budget"); return true; }
        if (bb.GetOr("sla:tight", 0.0) >= 0.95) { bb.Set("orchestrator:done_reason", "done:sla"); return true; }

        return false;
    }

    private static int Hash(IReadOnlyDictionary<string, object> snap)
    {
        // very simple structural hash for stagnation detection
        unchecked
        {
            int h = 17;
            foreach (var kv in snap.OrderBy(k => k.Key))
                h = h * 31 + (kv.Key.GetHashCode() ^ (kv.Value?.GetHashCode() ?? 0));
            return h;
        }
    }
}