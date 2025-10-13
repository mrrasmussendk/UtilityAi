using UtilityAi.Capabilities;
using UtilityAi.Sensor;
using UtilityAi.Utils;

namespace UtilityAi.Orchestration;

public sealed class UtilityAiOrchestrator
{
    private readonly List<ISensor> _sensors = new();
    private readonly List<ICapabilityModule> _modules = new();
    private readonly ISelectionStrategy _selector;

    public UtilityAiOrchestrator(ISelectionStrategy? selector = null)
    {
        _selector = selector ?? new MaxUtilitySelection();
    }

    public UtilityAiOrchestrator AddSensor(ISensor s) { _sensors.Add(s); return this; }
    public UtilityAiOrchestrator AddModule(ICapabilityModule m) { _modules.Add(m); return this; }
    public async Task RunAsync(EventBus bus, UserIntent intent, int maxTicks, CancellationToken ct)
    {
        for (int tick = 0; tick < maxTicks; tick++)
        {
            var rt = new Runtime(bus, intent, tick);

            // 1) Sense
            foreach (var s in _sensors) await s.SenseAsync(rt, ct);

            // 2) Propose
            var proposals = _modules.SelectMany(m => m.Propose(rt)).ToList();
            if (proposals.Count == 0)
            {
                Console.WriteLine($"[tick {tick}] no proposals; stopping.");
                break;
            }

            // 3) Score via considerations
            var scored = proposals
                .Select(p => (p, u: p.Utility(rt)))
                .OrderByDescending(x => x.u)
                .ToList();

            var chosen = _selector.Select(scored.Select(x => (x.p, x.u)).ToList(), rt);
            var chosenUtility = scored.First(x => ReferenceEquals(x.p, chosen)).u;
            Console.WriteLine($"[tick {tick}] choose {chosen.Id}  utility={chosenUtility:0.000}");

            // 4) Act
            await chosen.Act(ct);
        }
    }
}