using UtilityAi.Actions;
using UtilityAi.Policies;
using UtilityAi.Utils;

namespace Tests.Policy
{
    public sealed class LinearEpsilonGreedyPolicyTests
    {
        // Warm-up decisions so epsilon decays to its minimum (~0.05).
        // We do this using a single candidate so exploration vs. exploitation is irrelevant.
        private static void WarmUpEpsilon(LinearEpsilonGreedyPolicy policy, IBlackboard bb,
            IReadOnlyDictionary<string, double> x, int decisions = 2000)
        {
            var solo = new[] {new FakeAction("solo")};
            for (int i = 0; i < decisions; i++)
            {
                policy.Choose(bb, solo, x);
            }
        }

        private static FakeBlackboard NewNeutralBlackboard()
        {
            var bb = new FakeBlackboard();
            bb.Set("orchestrator:decisions", 1000); // UCB log term stable
            // Neutralize reliability & last-used/stickiness across all agents
            bb.Set("orchestrator:last", "");
            return bb;
        }

        private static IReadOnlyDictionary<string, double> Feature(params (string k, double v)[] items)
            => items.ToDictionary(t => t.k, t => t.v);

        [Fact]
        public void Choose_Throws_On_Nulls_And_Empty_Candidates()
        {
            var policy = new LinearEpsilonGreedyPolicy();
            var bb = NewNeutralBlackboard();
            var x = Feature(("f1", 1));

            Assert.Throws<ArgumentNullException>(() => policy.Choose(null!, new[] {new FakeAction("a")}, x));
            Assert.Throws<ArgumentNullException>(() => policy.Choose(bb, null!, x));
            Assert.Throws<ArgumentNullException>(() => policy.Choose(bb, new[] {new FakeAction("a")}, null!));
            Assert.Throws<InvalidOperationException>(() => policy.Choose(bb, Array.Empty<IAction>(), x));
        }

        [Fact]
        public void Choose_Returns_Sole_Candidate()
        {
            var policy = new LinearEpsilonGreedyPolicy();
            var bb = NewNeutralBlackboard();
            var x = Feature(("f1", 1.0));

            var only = new[] {new FakeAction("only")};
            var chosen = policy.Choose(bb, only, x);

            Assert.Equal("only", chosen);
        }

        [Fact]
        public void Learn_Increases_Action_Preference_For_Positive_Reward()
        {
            var policy = new LinearEpsilonGreedyPolicy();
            var bb = NewNeutralBlackboard();

            // same features for both actions; start with zero weights for both
            var x = Feature(("bias", 1.0), ("f1", 0.5));

            // Warm-up so epsilon ≈ min (reduces random exploration noise)
            WarmUpEpsilon(policy, bb, x, decisions: 2500);

            var a = new FakeAction("A");
            var b = new FakeAction("B");
            var candidates = new IAction[] {a, b};

            // With zero weights + neutral BB, either is fine; learn on A with positive reward
            policy.Learn("A", x, reward: 1.0);

            // Now A should be selected the vast majority of the time (allow 5% explore)
            int trials = 200;
            int pickedA = 0;
            for (int i = 0; i < trials; i++)
            {
                // Keep BB neutral between choices (counts, last, reliability)
                bb.Set("agent:A:count", 0);
                bb.Set("agent:B:count", 0);
                bb.Set("agent:A:ewma_success", 0.5);
                bb.Set("agent:B:ewma_success", 0.5);
                bb.Set("agent:A:last_used", DateTimeOffset.MinValue);
                bb.Set("agent:B:last_used", DateTimeOffset.MinValue);
                bb.Set("orchestrator:last", "");

                var chosen = policy.Choose(bb, candidates, x);
                if (chosen == "A") pickedA++;
            }

            // With ε ≈ 0.05, expect ≥ 90% A (allowing margin for randomness)
            Assert.True(pickedA >= (int) (trials * 0.85), $"A chosen only {pickedA}/{trials} times.");
        }

        [Fact]
        public void EpsilonGreedy_Still_Prefers_Higher_Score_Most_Of_The_Time()
        {
            var policy = new LinearEpsilonGreedyPolicy();
            var bb = NewNeutralBlackboard();

            // Features that give A a clear edge after its learning step
            var x = Feature(("bias", 1.0), ("f1", 1.0));

            // Warm-up to reach ε ≈ min
            WarmUpEpsilon(policy, bb, x, decisions: 2500);

            var a = new FakeAction("A");
            var b = new FakeAction("B");
            var candidates = new IAction[] {a, b};

            // Make A strong via learning; keep B untrained
            policy.Learn("A", x, reward: 1.0);
            policy.Learn("A", x, reward: 1.0);

            // Neutralize extraneous BB factors so score difference comes from weights
            bb.Set("agent:A:count", 0);
            bb.Set("agent:B:count", 0);
            bb.Set("agent:A:ewma_success", 0.5);
            bb.Set("agent:B:ewma_success", 0.5);
            bb.Set("agent:A:last_used", DateTimeOffset.MinValue);
            bb.Set("agent:B:last_used", DateTimeOffset.MinValue);
            bb.Set("orchestrator:last", "");

            // Run multiple trials to average out the 5% exploration noise
            int trials = 200;
            int pickedA = 0;
            for (int i = 0; i < trials; i++)
            {
                var chosen = policy.Choose(bb, candidates, x);
                if (chosen == "A") pickedA++;
            }

            Assert.True(pickedA >= (int) (trials * 0.85), $"A chosen only {pickedA}/{trials} times.");
        }

        [Fact]
        public void AntiStickiness_Shifts_Winner_When_It_Is_The_Only_Signal()
        {
            var policy = new LinearEpsilonGreedyPolicy();
            var bb = NewNeutralBlackboard();
            var x = Feature(("bias", 1.0));

            // Drive epsilon down to its floor (~0.05) to reduce randomness.
            WarmUpEpsilon(policy, bb, x, decisions: 3000);

            var a = new FakeAction("A");
            var b = new FakeAction("B");
            var candidates = new IAction[] {a, b}; // IMPORTANT: fixed order [A, B]

            int trials = 400;

            // Helper to fully neutralize everything BUT the stickiness signal
            void Neutralize()
            {
                // Zero UCB
                bb.Set("orchestrator:decisions", 0); // log(1)=0 => UCB=0

                // Equal counts
                bb.Set("agent:A:count", 0);
                bb.Set("agent:B:count", 0);

                // Equal reliability at the neutral midpoint with fully decayed influence
                bb.Set("agent:A:ewma_success", 0.5);
                bb.Set("agent:B:ewma_success", 0.5);
                bb.Set("agent:A:last_used", DateTimeOffset.MinValue);
                bb.Set("agent:B:last_used", DateTimeOffset.MinValue);
            }

            // Phase 1: Apply anti-stickiness against A
            int pickedA_withPenalty = 0;
            Neutralize();
            for (int i = 0; i < trials; i++)
            {
                bb.Set("orchestrator:last", "A"); // A penalized
                var chosen = policy.Choose(bb, candidates, x);
                if (chosen == "A") pickedA_withPenalty++;
            }

            // Phase 2: No stickiness (tie -> stable order favors A)
            int pickedA_noPenalty = 0;
            Neutralize();
            for (int i = 0; i < trials; i++)
            {
                bb.Set("orchestrator:last", ""); // no penalty
                var chosen = policy.Choose(bb, candidates, x);
                if (chosen == "A") pickedA_noPenalty++;
            }

            // Expectations:
            // - With penalty, B should dominate (A chosen rarely; allow epsilon noise)
            // - Without penalty, A should dominate due to tie + stable ordering
            Assert.True(pickedA_withPenalty <= (int) (trials * 0.20),
                $"With penalty, A chosen too often: {pickedA_withPenalty}/{trials} (expected ≲ 20%).");
            Assert.True(pickedA_noPenalty >= (int) (trials * 0.80),
                $"Without penalty, A chosen too rarely: {pickedA_noPenalty}/{trials} (expected ≳ 80%).");
        }


        [Fact]
        public void AntiStickiness_Shifts_Winner_When_Not_Squashed_By_Clamp()
        {
            var policy = new LinearEpsilonGreedyPolicy();
            var bb = NewNeutralBlackboard();

            // Single feature; we will raise both actions' utility > gamma (0.03)
            var x = Feature(("bias", 1.0));

            // Drive epsilon to its floor to reduce randomness.
            WarmUpEpsilon(policy, bb, x, decisions: 3000);

            var a = new FakeAction("A");
            var b = new FakeAction("B");
            var candidates = new IAction[] {a, b}; // keep order [A, B]

            // --- Prime equal positive utilities for BOTH actions ---
            // LearningRate = 0.2; reward = 0.5; x = {bias:1}
            // => w += 0.2 * 0.5 * 1 = 0.1  => u ≈ 0.1 (> gamma=0.03)
            policy.Learn("A", x, reward: 0.5);
            policy.Learn("B", x, reward: 0.5);

            // Helper: fully neutralize all signals except stickiness
            void Neutralize()
            {
                // kill UCB
                bb.Set("orchestrator:decisions", 0); // log(1)=0 -> UCB=0

                // equalize counts (UCB denominator)
                bb.Set("agent:A:count", 0);
                bb.Set("agent:B:count", 0);

                // reliability centered & fully decayed
                bb.Set("agent:A:ewma_success", 0.5);
                bb.Set("agent:B:ewma_success", 0.5);
                bb.Set("agent:A:last_used", DateTimeOffset.MinValue);
                bb.Set("agent:B:last_used", DateTimeOffset.MinValue);
            }

            int trials = 400;

            // Phase 1: last = "A" -> A gets -gamma (u_A ≈ 0.07, u_B ≈ 0.10)
            Neutralize();
            int pickedA_withPenalty = 0;
            for (int i = 0; i < trials; i++)
            {
                bb.Set("orchestrator:last", "A");
                var chosen = policy.Choose(bb, candidates, x);
                if (chosen == "A") pickedA_withPenalty++;
            }

            // Phase 2: no stickiness -> tie (both ~0.10) -> stable sort favors A
            Neutralize();
            int pickedA_noPenalty = 0;
            for (int i = 0; i < trials; i++)
            {
                bb.Set("orchestrator:last", "");
                var chosen = policy.Choose(bb, candidates, x);
                if (chosen == "A") pickedA_noPenalty++;
            }

            // With penalty, expect A near ε (~5%); without penalty, expect A near (1-ε) (~95%)
            Assert.True(pickedA_withPenalty <= (int) (trials * 0.20),
                $"With penalty, A chosen too often: {pickedA_withPenalty}/{trials} (expected ≲20%).");
            Assert.True(pickedA_noPenalty >= (int) (trials * 0.80),
                $"Without penalty, A chosen too rarely: {pickedA_noPenalty}/{trials} (expected ≳80%).");
        }


        [Fact]
        public void ReliabilityPrior_Boosts_HighReliability_Action_All_Else_Equal()
        {
            var policy = new LinearEpsilonGreedyPolicy();
            var bb = NewNeutralBlackboard();
            var x = Feature(("bias", 1.0));

            WarmUpEpsilon(policy, bb, x, decisions: 2500);

            var a = new FakeAction("A");
            var b = new FakeAction("B");
            var candidates = new IAction[] {a, b};

            // Equal weights, counts, no stickiness; set A as more reliable
            bb.Set("agent:A:count", 0);
            bb.Set("agent:B:count", 0);
            bb.Set("agent:A:ewma_success", 0.9);
            bb.Set("agent:B:ewma_success", 0.1);
            bb.Set("agent:A:last_used", DateTimeOffset.UtcNow); // age small => less decay
            bb.Set("agent:B:last_used", DateTimeOffset.UtcNow);
            bb.Set("orchestrator:last", "");

            int trials = 200;
            int pickedA = 0;
            for (int i = 0; i < trials; i++)
            {
                var chosen = policy.Choose(bb, candidates, x);
                if (chosen == "A") pickedA++;
            }

            Assert.True(pickedA >= (int) (trials * 0.70), $"A chosen only {pickedA}/{trials} times.");
        }
    }

    // ---------------------- Test Doubles ------------------------------------

    internal sealed class FakeBlackboard : IBlackboard
    {
        private readonly Dictionary<string, object> _map = new();

        public T GetOr<T>(string key, T fallback)
        {
            if (_map.TryGetValue(key, out var val) && val is T t) return t;
            return fallback;
        }

        public void Set<T>(string key, T value) => _map[key] = value!;

        public bool Has(string key) => _map.ContainsKey(key);

        public IReadOnlyDictionary<string, object> Snapshot() => _map;
    }

    internal sealed class FakeAction : IAction
    {
        public string Id { get; }

        public FakeAction(string id) => Id = id;

        public bool Gate(IBlackboard bb) => true;

        public Task<AgentOutcome> ActAsync(IBlackboard bb, CancellationToken ct)
            => Task.FromResult(new AgentOutcome(true, 0, TimeSpan.Zero));
    }
}