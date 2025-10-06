# 🧠 Utility-Based AI Orchestrator for .NET

A modular, adaptive **AI orchestration framework** inspired by **game AI decision-making**.  
Instead of relying on static workflows, this orchestrator continuously **evaluates** which AI agent should act next — balancing **cost, latency, accuracy, and context** in real time.

> Don’t script intelligence — **evaluate it.**

---

## ⚙️ Core Concepts

| Concept | Description |
|----------|-------------|
| **Sensors** | Monitor environment conditions like cost, recency, latency, and uncertainty. |
| **Actions** | Perform concrete actions such as search, summarization, verification, transcription, or text-to-speech. |
| **Policy** | Computes utility scores for each agent and selects the best one based on current context. |
| **Reward Function** | Evaluates outcomes and adjusts weights — enabling learning without retraining. |
| **Blackboard** | Shared memory where all modules write and read context data. |

Each module is fully **pluggable** and **replaceable**, allowing teams to contribute components independently.

---

## 🚀 Key Features

- 🔄 **Utility-Based Decision Loop** — continuously evaluates, acts, and learns  
- 🧩 **Modular Architecture** — sensors, agents, and policies are independent modules  
- 🧠 **Self-Correcting Logic** — adapts without retraining  
- 💼 **Enterprise-Ready** — clean, scalable architecture for distributed systems  
- 🧪 **Built with TDD** — robust test coverage and predictable behavior  
- ⚡ **.NET 8+ Support** — fully compatible with modern .NET environments  

---

## 🧩 How It Works

1. **Sensors** collect metrics (cost, latency, uncertainty)  
2. **Policy** assigns utility scores to all available agents  
3. **Orchestrator** picks the top-scoring action and executes it  
4. **Reward function** evaluates the result and updates the policy weights  
5. The loop repeats — continuously optimizing behavior  

---

## 💡 Example Use Cases

- **Knowledge Orchestration:** Coordinate search, summarization, and fact-checking dynamically  
- **Conversational AI:** Switch between LLMs or output styles based on context  
- **Monitoring & Research Systems:** Learn when to re-query or re-summarize  
- **Voice & Media Automation:** Choose between text and audio generation intelligently  
- **AI Gateways:** Balance multiple LLMs and APIs by cost, confidence, and latency  

---

## 🧰 Tech Stack

- **.NET 8**
- **xUnit** for testing  
- **Clean Architecture** principles  
- **Dependency Injection** ready  
- **Lightweight modular interfaces**

---

## 🚀 Getting Started

```bash
git clone https://github.com/yourusername/UtilityAI.Orchestrator.git
cd UtilityAI.Orchestrator
dotnet build
dotnet test
