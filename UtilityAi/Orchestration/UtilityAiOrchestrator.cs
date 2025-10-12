using UtilityAi.Capabilities;
using UtilityAi.Sensor;
using UtilityAi.Utils;

namespace UtilityAi.Orchestration;

public sealed class UtilityAiOrchestrator
{
    private readonly List<ISensor> _sensors = new();
    private readonly List<ICapabilityModule> _modules = new();

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

            var best = scored.First();
            Console.WriteLine($"[tick {tick}] choose {best.p.Id}  utility={best.u:0.000}");

            // 4) Act
            await best.p.Act(ct);
        }
    }
}