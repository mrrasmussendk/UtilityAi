# 🧠 Utility-AI Orchestrator (.NET 8)

A lightweight, modular decision loop for composing AI capabilities using classic **Utility AI** patterns (proposals + considerations). The orchestrator scores candidate actions each tick and executes the best one based on the current context and available facts.

> Don’t script workflows — evaluate them.

---

## 🏗️ Core Building Blocks

- **EventBus (Blackboard)**: A central repository for the latest facts of any type.
- **Sensors**: Services that observe the environment (or internal state) and publish facts to the EventBus.
- **Considerations**: Pluggable scoring functions (0.0 to 1.0) that evaluate the current state (Runtime).
- **Proposals**: Potential actions with a `BaseScore` and a set of `Considerations`. Their final utility is the product of all.
- **Capability Modules**: Domain-specific logic that provides relevant `Proposals` based on the current context.
- **UtilityAiOrchestrator**: The engine that runs the *Sense → Propose → Score → Act* loop.

---

## 📂 What's in this repo

- **UtilityAi**: The core framework project.
  - `Utils/EventBus`: Simple, type-safe blackboard for state management.
  - `Orchestration/UtilityAiOrchestrator`: The main loop runner.
  - `Consideration`: Built-in considerations like `HasFact<T>` and `CurveSignal<T>`.
  - `Evaluators`: Response curves (Logistic, Identity, OneMinus) for fine-grained scoring.
- **Example**: A complete, runnable **Task Management System** demo showing:
  - **Sensors**: Monitoring resources, initializing task queues, and reading user intent.
  - **Capability Modules**: Validating, prioritizing, and executing tasks with resource constraints.
  - **Orchestration**: Multiple modules competing for attention based on task priority, age, and resource availability.
  - **No External Dependencies**: Fully self-contained example with simulated work and delays.
- **Tests**: Comprehensive xUnit tests for all core components and orchestration logic.

---

## 🚀 Quick Start

### Prerequisites
- .NET 8 SDK

### Build & Run
```bash
# Build the entire solution
dotnet build

# Run tests
dotnet test

# Run the example project (Task Management System)
cd Example
dotnet run
```

### What the Example Shows
The example simulates a task management system that:
- **Validates** incoming tasks (checking prerequisites, permissions, etc.)
- **Prioritizes** tasks based on urgency, age, and mode (urgent/balanced/efficiency)
- **Executes** tasks in parallel with resource constraints (CPU, memory, max parallel)
- **Adapts** to task dependencies (some tasks must complete before others start)

Watch as multiple modules compete for attention each tick, with the orchestrator selecting the highest-utility action!

---

## 💡 How it Works (The Loop)

The `UtilityAiOrchestrator` operates in discrete **ticks**. Each tick follows these steps:

1.  **Sense**: All registered `ISensor` instances run, updating the `EventBus` with fresh facts.
2.  **Propose**: Registered `ICapabilityModule` instances return `Proposal` objects relevant to the current state.
3.  **Score**: Each `Proposal` is evaluated. Utility = `BaseScore` × `Consideration1` × `Consideration2` × ...
4.  **Select**: The orchestrator selects the proposal with the highest utility (via `MaxUtilitySelection`).
5.  **Act**: The chosen proposal's action is executed, which usually publishes new facts to the `EventBus`, influencing the next tick.

---

## 🛠️ Code Example

Here is a simplified version of the orchestrator setup from the Example project:

```csharp
using UtilityAi.Orchestration;
using UtilityAi.Utils;

// 1. Initialize the blackboard and intent
var bus = new EventBus();
var intent = new UserIntent(
    Goal: new IntentGoal("process-task-batch"),
    Slots: new Dictionary<string, object?>
    {
        ["tasks"] = new[] { "analyze-data", "generate-report", "backup-db" },
        ["priority_mode"] = "balanced",
        ["max_parallel"] = 3
    }
);

// 2. Configure the orchestrator with sensors and modules
var orch = new UtilityAiOrchestrator(null, true, bus)
    .AddSensor(new IntentSensor())
    .AddSensor(new ResourceMonitorSensor())
    .AddSensor(new TaskQueueSensor())
    .AddModule(new ValidationModule())
    .AddModule(new PrioritizationModule())
    .AddModule(new ExecutionModule());

// 3. Run with a sink for visibility
await orch.RunAsync(intent, maxTicks: 20, CancellationToken.None, sink: new DetailedConsoleSink());
```

**Output Example:**
```
[Tick 0] 🔍 Sensing environment...
[Tick 0] ⚖️ Scored Proposals:
  - validate.task-1           | Utility: 0.612
  - validate.task-4           | Utility: 0.387
  - validate.task-2           | Utility: 0.354
[Tick 0] ✨ Chosen: validate.task-1 (u=0.612)
    ✓ Validated task: generate-report
[Tick 0] ⚙️ Executed action for validate.task-1
```

---

## 📈 Observability (Sinks)

You can plug in `IOrchestrationSink` implementations to monitor the decision-making process without modifying your business logic.

```csharp
public sealed class MyCustomSink : IOrchestrationSink
{
    public void OnChosen(Runtime rt, Proposal chosen, double utility)
        => Console.WriteLine($"Tick {rt.Tick}: Decided to {chosen.Id} with utility {utility:F2}");

    // ... other methods
}
```

Built-in sinks:
- `NullSink`: Does nothing (default).
- `RecordingSink`: Captures all tick data for later analysis.
- `CompositeSink`: Forwards events to multiple sinks.

---

## 🗺️ Architecture

A high-level component diagram is available in [docs/architecture.puml](docs/architecture.puml).

Render it using the PlantUML extension in your IDE or via Docker:
```powershell
docker run --rm -v ${PWD}:/workspace plantuml/plantuml docs/architecture.puml
```
