# 🧠 Utility-AI Orchestrator (.NET 8)

A lightweight, modular decision loop for composing AI capabilities using classic Utility AI patterns (proposals + considerations). The orchestrator scores candidate actions each tick and executes the best one based on current facts.

> Don’t script workflows — evaluate them.

---

## Core Building Blocks

- EventBus (blackboard): publish/subscribe of the latest fact per type.
- Considerations: pluggable scoring functions (0..1) that evaluate the current Runtime.
- Proposal: an action candidate with BaseScore and a set of Considerations; its Utility is the product of all.
- Capability Modules: provide proposals to the orchestrator based on context.
- Sensors: write facts into the EventBus (e.g., derived signals, intents).
- UtilityAiOrchestrator: runs the sense → propose → score → act loop.

These map to types in the UtilityAi project: UtilityAi.Utils.EventBus/Runtime, UtilityAi.Consideration.*, UtilityAi.Capabilities.ICapabilityModule, UtilityAi.Orchestration.UtilityAiOrchestrator, and actions via UtilityAi.Actions.IAction.

---

## What’s in this repo

- UtilityAi: core abstractions (EventBus, Runtime, Considerations, Orchestrator, etc.).
- Example: runnable demo wiring sensors and modules:
  - SearchAndSummarizeModule: proposes news.search then news.summarize.
    - NewsSearchAction: calls NewsAPI.org (requires NEWSAPI_KEY).
    - SummarizerAction: uses OpenAI Responses API to produce a short brief (requires OPENAI_API_KEY).
  - OutputModule (example): shows how to route the final summary (e.g., SMS action stub).
  - Sensors: e.g., TopicSensor that can infer or normalize a topic from intent/LLM.
- Tests: xUnit tests covering EventBus, considerations, proposals, orchestrator flow, and the Search+Summarize module. All tests pass on .NET 8.

---

## Quick start

Prereqs: .NET 8 SDK, and if you want to run the example with real APIs, set environment variables.

- Windows (PowerShell):
  - $env:OPENAI_API_KEY = "sk-..."
  - $env:NEWSAPI_KEY = "your_newsapi_key"
- macOS/Linux (bash):
  - export OPENAI_API_KEY="sk-..."
  - export NEWSAPI_KEY="your_newsapi_key"

Build and test:

- dotnet build
- dotnet test

Run the example:

- cd Example
- dotnet run

The example wires:

- A TopicSensor and an IntentSensor (reads intent Slots to publish SignalOutputMode)
- A SummaryToOutputAdapter (converts Summary -> OutputTextMessage for OutputModule)
- SearchAndSummarizeModule(new NewsSearchAction(new HttpClient()), new SummarizerAction(new OpenAiClient()))
- An OutputModule with a TwilloOutputAction stub

The orchestrator prints chosen proposals per tick and publishes results back to EventBus.

---

## Key types (snapshot)

- Proposal: Utility is BaseScore multiplied by each consideration, clamped to [0,1].
- HasFact<T>: scores 1.0 if fact exists (or doesn’t, when inverted), else 0.0.
- CurveSignal<TSignal>: projects a signal to [0,1] and applies a response curve.
- UtilityAiOrchestrator: per tick: Sense → gather Propose → score → choose → Act.
- EventBus: Publish<T>(msg), TryGet<T>(out _), GetOrDefault<T>().

---

## Notes

- The Example project uses OpenAI experimental Responses API; tests suppress the OPENAI001 analyzer where required.
- Networked actions (OpenAI/NewsAPI) are injected via IAction interfaces so tests run offline.
- You can add your own capability modules by returning Proposal instances whose actions write facts to EventBus.

---

## Architecture diagram

A high-level component diagram of the core system and the Example wiring is available as PlantUML:

- PlantUML source: docs/architecture.puml

Render options:

- Using Docker (PowerShell):
  - docker run --rm -v ${PWD}:/workspace plantuml/plantuml docs/architecture.puml
- Using PlantUML extension in Rider/IntelliJ/VS Code: open docs/architecture.puml and preview.

---

## Observability and outputs (sinks)

As of this version, the orchestrator supports pluggable sinks so you can capture what happened each tick without changing your business logic. This addresses cases where:

- You don’t need any output (use NullSink — the default)
- You want to log chosen actions/utilities each tick
- You want to capture a full decision history for testing or analytics

Key types: UtilityAi.Orchestration.IOrchestrationSink, NullSink, CompositeSink, RecordingSink, and OrchestrationStopReason.

Example: record decisions for later

```csharp
using UtilityAi.Orchestration;
using UtilityAi.Utils;

var bus = new EventBus();
var orchestrator = new UtilityAiOrchestrator();
var sink = new RecordingSink();

await orchestrator.RunAsync(bus, new UserIntent("news.summary"), maxTicks: 10, ct: CancellationToken.None, sink: sink);

foreach (var t in sink.Ticks)
{
    Console.WriteLine($"tick={t.Tick} chosen={t.Chosen.Id} utility={t.ChosenUtility:0.000}");
}
```

Example: simple console logging sink

```csharp
public sealed class ConsoleSink : IOrchestrationSink
{
    public void OnTickStart(Runtime rt) => Console.WriteLine($"[tick {rt.Tick}] start");
    public void OnScored(Runtime rt, IReadOnlyList<(Proposal Proposal, double Utility)> scored)
        => Console.WriteLine($"[tick {rt.Tick}] scored: {string.Join(", ", scored.Select(s => $"{s.Proposal.Id}:{s.Utility:0.000}"))}");
    public void OnChosen(Runtime rt, Proposal chosen, double utility)
        => Console.WriteLine($"[tick {rt.Tick}] chosen: {chosen.Id} u={utility:0.000}");
    public void OnActed(Runtime rt, Proposal chosen) => Console.WriteLine($"[tick {rt.Tick}] acted: {chosen.Id}");
    public void OnStopped(Runtime rt, OrchestrationStopReason reason) => Console.WriteLine($"stopped: {reason}");
}

// Usage
await orchestrator.RunAsync(bus, intent, maxTicks: 10, ct: CancellationToken.None, sink: new ConsoleSink());
```

Composing sinks

```csharp
var sink = new CompositeSink(new ConsoleSink(), new RecordingSink());
await orchestrator.RunAsync(bus, intent, 10, CancellationToken.None, sink);
```

Stop reasons are surfaced via OnStopped with OrchestrationStopReason.NoProposals, ZeroUtility, MaxTicksReached, or Cancelled.
