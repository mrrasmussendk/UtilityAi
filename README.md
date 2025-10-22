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
- Tests: xUnit tests covering EventBus, considerations, proposals, orchestrator flow, and slot-based intent sensing. All tests pass on .NET 8.

---

## How it works

This framework follows a Blackboard architecture combined with a Perception–Decision–Action loop and Utility-based selection:

1) Perception (Sensors)
- Sensors implement ISensor and write typed facts to the EventBus (the blackboard).
- In the Example project, Intent is kept generic via UserIntent with Slots (a dictionary). Example sensors translate slots into typed signals. For instance, IntentSensor reads Slots["delivery"] and publishes a SignalOutputMode("sms"|"email"|...).
- Other sensors can normalize user query into a Topic fact (TopicSensor via OpenAI in the example).

2) Decision (Proposals + Considerations)
- Each capability module implements ICapabilityModule and returns Proposal instances based on current facts.
- A Proposal has a BaseScore and a set of Considerations; its Utility is the product of BaseScore and all consideration scores (each clamped to [0,1]).
- Considerations are small scoring policies, e.g., HasFact<T> or CurveSignal<TSignal>.

3) Action (Command)
- The orchestrator scores proposals and, by default, chooses the max-utility proposal via a pluggable ISelectionStrategy (default: MaxUtilitySelection).
- The chosen proposal’s Act delegate runs (async), typically publishing new facts to the EventBus.

Separation of concerns
- Framework core is domain-agnostic: it knows nothing about delivery modes, topics, or queries.
- Example-specific concepts live under the Example project. They consume generic intent Slots and publish example DTOs such as Topic or SignalOutputMode.

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

## Minimal usage sketch

- Create an EventBus and a UserIntent:

  var bus = new EventBus();
  var intent = new UserIntent(
      Goal: new IntentGoal("search-and-summarize"),
      Slots: new Dictionary<string, object?>
      {
          ["query"] = "latest news",
          ["delivery"] = "sms"
      }
  );

- Wire sensors and modules:

  var orch = new UtilityAiOrchestrator()
      .AddSensor(new IntentSensor())
      .AddModule(new YourModule());

- Run:

  await orch.RunAsync(bus, intent, maxTicks: 5, ct: CancellationToken.None);

---

## Tests

- The Tests project (xUnit) validates key behavior:
  - EventBus publishes/reads latest fact per type.
  - Proposal.Utility multiplies base score by consideration scores and clamps.
  - Orchestrator selects highest-utility proposal via MaxUtilitySelection.
  - IntentSensor translates intent Slots["delivery"] into SignalOutputMode.
  - Legacy UserIntent(string) maps to a "query" slot; the (string,string,string) legacy ctor maps query/delivery/topic into Slots.

Run all tests:

- dotnet test

---

## Architecture diagram

A high-level component diagram of the core system and the Example wiring is available as PlantUML:

- PlantUML source: docs/architecture.puml

Render options:

- Using Docker (PowerShell):
  - docker run --rm -v ${PWD}:/workspace plantuml/plantuml docs/architecture.puml
- Using PlantUML extension in Rider/IntelliJ/VS Code: open docs/architecture.puml and preview.


---

## Observability and outputs (UtilityAi sinks)

The core orchestrator now supports pluggable sinks for logging/recording decisions without changing control flow. See detailed documentation and examples in UtilityAi/README.md, section "Observability and outputs (sinks)".

Quick usage example:

```csharp
using UtilityAi.Orchestration;

var orchestrator = new UtilityAiOrchestrator();
await orchestrator.RunAsync(bus, intent, 10, CancellationToken.None, sink: new RecordingSink());
```
