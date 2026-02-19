# 🧠 UtilityAI Framework (.NET 8)

[![NuGet](https://img.shields.io/nuget/v/UtilityAi?color=blue)](https://www.nuget.org/packages/UtilityAi)
[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![Tests](https://img.shields.io/badge/tests-203%20passing-brightgreen)](https://github.com/mrrasmussendk/UtilityAi)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](https://github.com/mrrasmussendk/UtilityAi/blob/main/LICENSE)

A lightweight, modular .NET 8 framework for building **AI agent orchestration** systems using classic **Utility AI** decision-making patterns. The framework evaluates and scores candidate actions each tick, executing the highest-utility option based on current context — no hardcoded workflows required.

> **Don't script workflows — evaluate them.**

---

## ✨ Features

- 🎯 **Utility-Based Decision Making** — Actions compete based on dynamic scoring with response curves
- 🧠 **LLM Intent Interpretation** — Proposals declare parameters; framework exposes metadata for LLM prompt generation
- 🔗 **MAF Integration** — Orchestrate Microsoft Agent Framework agents with utility-based selection
- 🏷️ **Attribute-Based Registration** — Declarative module configuration with auto-discovery
- 📝 **Event History & Subscriptions** — Timestamped history and type-safe callbacks
- 🏗️ **Scoped State** — Isolate multi-agent state while sharing global facts
- 💾 **Memory Management** — Two-tier memory with automatic archival to long-term storage
- 📊 **Dashboard** — Real-time web UI for visualizing proposals, scores, and tick history
- 🔌 **Pluggable Architecture** — Sensors, modules, considerations, and selection strategies are fully extensible
- 🧪 **Well Tested** — 203+ tests covering all core functionality

---

## 🚀 Quick Start

### Installation

```bash
dotnet add package UtilityAi
```

### Minimal Example

```csharp
using UtilityAi.Orchestration;
using UtilityAi.Utils;

var bus = new EventBus();
var orchestrator = new UtilityAiOrchestrator(bus: bus)
    .AddSensor(new MyEnvironmentSensor())
    .AddModule(new MyCapabilityModule());

var intent = UserIntent.ForGoal("my-goal");
await orchestrator.RunAsync(intent, maxTicks: 10, CancellationToken.None);
```

### Attribute-Based Registration

```csharp
[Capability(Priority = 100, Domain = "validation")]
[RequiresFact<TaskQueue>]
[ActiveWhen("priority_mode", "urgent", "balanced")]
public class ValidationModule : ICapabilityModule
{
    public IEnumerable<Proposal> Propose(Runtime rt) { /* ... */ }
}

var orchestrator = new UtilityAiOrchestrator(bus: bus)
    .AddSensor(new MySensor())
    .DiscoverCapabilities(Assembly.GetExecutingAssembly());
```

---

## 🏗️ Architecture Overview

The framework follows a **Sense → Propose → Score → Act** loop:

```
┌─────────────────────────────────────────────────────┐
│                   EventBus                          │
│         (Shared state with history & scoping)       │
└──────────▲──────────────────────┬──────────────────┘
           │                      │
    ┌──────┴──────┐        ┌─────▼──────┐
    │   Sensors   │        │   Modules  │
    │  (Observe)  │        │  (Propose) │
    └─────────────┘        └─────┬──────┘
                                 │
                          ┌──────▼──────┐
                          │  Proposals  │
                          │   + Score   │
                          └──────┬──────┘
                                 │
                          ┌──────▼──────┐
                          │Orchestrator │
                          │ Select Best │
                          │    & Act    │
                          └─────────────┘
```

### Key Components

| Component | Purpose | Extensibility |
|-----------|---------|---------------|
| **EventBus** | Central state container with history, subscriptions, and scoping | Use as-is or wrap for persistence |
| **ISensor** | Observe environment and publish facts | Implement for your data sources |
| **ICapabilityModule** | Propose candidate actions | Implement + use attributes for auto-discovery |
| **IConsideration** | Score proposals (0.0–1.0) | Implement custom scoring logic |
| **IOrchestrationSink** | Observe orchestration events | Implement for logging/metrics |

### Utility Formula

```
utility = prior × (geometric_mean_of_considerations) ^ temperature
```

---

## 🎯 Use Cases

- **AI Agent Systems** — Coordinate multiple agents with shared and isolated state
- **LLM-Based Applications** — Build context from event history for prompts
- **Dynamic Workflows** — Let actions emerge from current state
- **Game AI** — Classic utility AI for NPCs and decision-making
- **Task Orchestration** — Prioritize tasks based on resources and constraints
- **IoT / Smart Home** — Balance competing objectives (comfort, energy, security)

---

## 📚 Documentation

Full documentation, examples, and integration guides are available on GitHub:

- **[Architecture Guide](https://github.com/mrrasmussendk/UtilityAi/blob/main/docs/ARCHITECTURE.md)** — Framework design and patterns
- **[Integration Guide](https://github.com/mrrasmussendk/UtilityAi/blob/main/docs/INTEGRATION.md)** — MAF, OpenAI, Anthropic, Azure AI
- **[Built-in Components](https://github.com/mrrasmussendk/UtilityAi/blob/main/docs/BUILT_IN_COMPONENTS.md)** — Sensors, considerations, and modules reference
- **[Proposal Patterns](https://github.com/mrrasmussendk/UtilityAi/blob/main/docs/PROPOSAL_PATTERNS.md)** — Best practices and anti-patterns
- **[Examples](https://github.com/mrrasmussendk/UtilityAi/tree/main/examples)** — Agent Assistant, Smart Home, ChatBot, Intent-Based Agent, MAF

---

## 📄 License

MIT License — see [LICENSE](https://github.com/mrrasmussendk/UtilityAi/blob/main/LICENSE) for details.

---

<p align="center">
  <sub>Built with ❤️ for the AI agent community</sub>
</p>
