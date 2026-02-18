# UtilityAI Dashboard

An optional extension to the [UtilityAI Framework](../README.md) that provides a **real-time web dashboard** to visualize actions, considerations, and state variables during orchestration.

![Dashboard Screenshot](https://github.com/user-attachments/assets/ab562e6f-8030-45e6-af5e-d92e4ef13d97)

## Features

- **Visual proposal scoring** — see all proposals ranked by utility with color-coded bars
- **Consideration breakdown** — inspect each consideration's individual score per proposal
- **Tick timeline** — navigate through orchestration history tick-by-tick
- **Active action indicator** — see which proposal is currently executing
- **Parameter overrides** — interactively adjust Prior and Temperature values via sliders
- **Real-time updates** — Server-Sent Events (SSE) stream live orchestration state
- **Zero dependencies** — single embedded HTML page, no npm/webpack required

## Quick Start

### 1. Add the package reference

```xml
<PackageReference Include="UtilityAi.Dashboard" Version="1.0.0" />
```

### 2. Wire up in your ASP.NET Core app

```csharp
using UtilityAi.Dashboard;

// Create shared state and sink
var dashboardState = new DashboardState();
var dashboardSink = new DashboardSink(dashboardState);

// Map dashboard endpoints
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
app.MapUtilityAiDashboard(dashboardState);
app.Run();

// Pass the sink to your orchestrator
await orchestrator.RunAsync(intent, maxTicks: 10, ct, sink: dashboardSink);
```

### 3. Open the dashboard

Navigate to `http://localhost:5000/utilityai/dashboard`

## API Endpoints

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/utilityai/dashboard` | Embedded HTML dashboard |
| `GET` | `/utilityai/api/state` | Full state snapshot (JSON) |
| `GET` | `/utilityai/api/ticks` | Tick history (JSON) |
| `GET` | `/utilityai/api/events` | SSE stream for real-time updates |
| `POST` | `/utilityai/api/overrides/prior` | Set prior override `{ proposalId, value }` |
| `POST` | `/utilityai/api/overrides/temperature` | Set temperature override `{ proposalId, value }` |
| `DELETE` | `/utilityai/api/overrides/{proposalId}` | Remove all overrides for a proposal |
| `POST` | `/utilityai/api/reset` | Clear all recorded state |

## Architecture

```
┌─────────────────────┐       ┌──────────────────┐
│  UtilityAi          │       │  UtilityAi       │
│  Orchestrator       │──────▶│  Dashboard       │
│                     │ sink  │                   │
│  RunAsync(sink)     │       │  DashboardSink    │
└─────────────────────┘       │  DashboardState   │
                              │  DashboardEndpoints│
                              └────────┬───────────┘
                                       │ SSE / REST
                              ┌────────▼───────────┐
                              │  Browser Dashboard  │
                              │  (embedded HTML)    │
                              └─────────────────────┘
```

### Key Classes

| Class | Purpose |
|-------|---------|
| `DashboardSink` | Implements `IOrchestrationSink` to capture events |
| `DashboardState` | Thread-safe state container with tick history |
| `DashboardEndpoints` | ASP.NET Core endpoint mapping extension |

## Using Parameter Overrides

The dashboard allows you to adjust `Prior` and `Temperature` values interactively. These overrides are stored in `DashboardState` and can be read in your capability modules:

```csharp
public class MyModule : ICapabilityModule
{
    private readonly DashboardState _dashboard;

    public MyModule(DashboardState dashboard) => _dashboard = dashboard;

    public IEnumerable<Proposal> Propose(Runtime rt)
    {
        var prior = _dashboard.PriorOverrides.TryGetValue("my.action", out var p) ? p : 0.8;

        yield return new Proposal("my.action",
            new[] { new HasFact<MyFact>(true) },
            async ct => { /* action */ })
        { Prior = prior };
    }
}
```

## Extending the Dashboard

The dashboard is designed to be easily extensible:

- **Custom sinks**: Use `CompositeSink` to combine `DashboardSink` with your own sinks
- **Custom endpoints**: Add your own endpoints alongside the dashboard
- **Custom URL prefix**: `app.MapUtilityAiDashboard(state, prefix: "/my-dashboard")`
