using UtilityAi.Capabilities;
using UtilityAi.Sensor;
using UtilityAi.Utils;
using UtilityAi.Consideration;

namespace UtilityAi.Orchestration;

public sealed class UtilityAiOrchestrator(ISelectionStrategy? selector = null, bool StopAtZero = true) : IOrchestrator
{
    private readonly List<ISensor> _sensors = new();
    private readonly List<ICapabilityModule> _modules = new();
    private readonly ISelectionStrategy _selector = selector ?? new MaxUtilitySelection();

    public UtilityAiOrchestrator AddSensor(ISensor s) { _sensors.Add(s); return this; }
    public UtilityAiOrchestrator AddModule(ICapabilityModule m) { _modules.Add(m); return this; }
    public async Task RunAsync(EventBus bus, UserIntent intent, int maxTicks, CancellationToken ct, IOrchestrationSink? sink = null)
    {
        sink ??= NullSink.Instance;

        for (int tick = 0; tick < maxTicks; tick++)
        {
            if (ct.IsCancellationRequested)
            {
                var cancelledRtEarly = new Runtime(bus, intent, tick);
                sink.OnStopped(cancelledRtEarly, OrchestrationStopReason.Cancelled);
                return;
            }

            var rt = new Runtime(bus, intent, tick);
            sink.OnTickStart(rt);

            // 1) Sense<
            foreach (var s in _sensors) await s.SenseAsync(rt, ct);

            // 2) Propose
            var proposals = _modules.SelectMany(m => m.Propose(rt)).ToList();
            if (proposals.Count == 0)
            {
                sink.OnStopped(rt, OrchestrationStopReason.NoProposals);
                return;
            }

            // 3) Score via considerations
            var scored = proposals
                .Select(p => (p, u: p.Utility(rt)))
                .OrderByDescending(x => x.u)
                .ToList();

            sink.OnScored(rt, scored.Select(x => (x.p, x.u)).ToList());

            var chosen = _selector.Select(scored.Select(x => (x.p, x.u)).ToList(), rt);
            var chosenUtility = scored.First(x => ReferenceEquals(x.p, chosen)).u;

            if (StopAtZero && chosenUtility == 0)
            {
                sink.OnChosen(rt, chosen, chosenUtility);
                sink.OnStopped(rt, OrchestrationStopReason.ZeroUtility);
                return;
            }

            sink.OnChosen(rt, chosen, chosenUtility);

            // 4) Act
            await chosen.Act(ct);
            sink.OnActed(rt, chosen);
        }

        // If we reached here naturally, we hit the tick cap
        var finalRt = new Runtime(bus, intent, maxTicks);
        sink.OnStopped(finalRt, OrchestrationStopReason.MaxTicksReached);
    }
}