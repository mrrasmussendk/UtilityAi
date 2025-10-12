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

- A TopicSensor
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
