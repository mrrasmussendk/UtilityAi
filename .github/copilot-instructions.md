# GitHub Copilot Instructions for UtilityAI

## Project Overview

This is a lightweight, modular .NET 8 framework for building AI agent orchestration systems using classic Utility AI decision-making patterns. The framework evaluates and scores candidate actions each tick, executing the highest-utility option based on current context.

**Key Architecture Pattern**: Sense → Propose → Score → Act

## Coding Conventions

### Language & Framework
- **Target Framework**: .NET 8.0
- **Language**: C# with nullable reference types enabled
- **Implicit Usings**: Enabled
- Follow C# naming conventions (PascalCase for types and members, camelCase for local variables and parameters)

### Code Style
- Use nullable reference types consistently - all reference types must be explicitly nullable with `?` or non-nullable
- Enable XML documentation comments for public APIs
- Keep methods focused and single-purpose
- Prefer composition over inheritance
- Use clear, descriptive names for types, methods, and variables

### Project Structure
- `UtilityAi/` - Core framework library
  - `Utils/` - EventBus, Runtime
  - `Orchestration/` - UtilityAiOrchestrator, OrchestratorExtensions
  - `Sensor/` - ISensor interface
  - `Capabilities/` - ICapabilityModule, Attributes
  - `Consideration/` - Proposal, IConsideration, built-in considerations
  - `Evaluators/` - Response curves (Logistic, Power, etc.)
- `Example/` - Demonstration projects
- `Tests/` - xUnit test suite
- `docs/` - Architecture and integration documentation

## Build & Test

### Build Commands
```bash
# Restore dependencies
dotnet restore

# Build the solution (all projects)
dotnet build --configuration Release

# Build specific project
dotnet build UtilityAi/UtilityAi.csproj --configuration Release
```

### Testing
```bash
# Run all tests
dotnet test --configuration Release --verbosity normal

# Run tests for a specific project
dotnet test Tests/Tests.csproj --configuration Release
```

**Test Requirements**:
- All new features must have corresponding xUnit tests
- Maintain or improve test coverage (currently 69 comprehensive tests)
- Tests should be deterministic and not rely on external dependencies
- Use descriptive test names following the pattern: `MethodName_Scenario_ExpectedResult`

## Core Framework Concepts

### EventBus (Blackboard Pattern)
- Central state container with history, subscriptions, and scoping
- Thread-safe event publishing and retrieval
- Supports timestamped event history for LLM context
- Scoped buses for multi-agent isolation with shared parent state

### Sensors (ISensor)
- Observe environment and publish facts to EventBus
- Called before each orchestration tick
- Should be stateless and idempotent

### Capability Modules (ICapabilityModule)
- Propose candidate actions (Proposals) based on current state
- Support attribute-based registration with `[Capability]`, `[RequiresFact<T>]`, `[ActiveWhen]`
- Each module focuses on a specific capability domain

### Proposals & Considerations
- Proposals represent candidate actions with scoring logic
- Considerations score proposals from 0.0 to 1.0
- Utility formula: `utility = prior × (geometric_mean_of_considerations)^temperature`

### Orchestrator
- Coordinates the sense-propose-score-act loop
- Selects and executes the highest-utility proposal each tick
- Supports observability via IOrchestrationSink

## Documentation Standards

### Code Documentation
- Add XML doc comments for all public types, methods, and properties
- Explain non-obvious design decisions with inline comments
- Keep comments concise and up-to-date with code changes

### Architecture Documentation
- Major features should be documented in `docs/` folder
- Update `README.md` for significant changes to public API or features
- Update `ARCHITECTURE.md` for new architectural patterns
- Update `INTEGRATION.md` for new integration examples

## Pull Request Guidelines

### Before Submitting
- Ensure all tests pass: `dotnet test`
- Build succeeds in Release configuration: `dotnet build --configuration Release`
- Add tests for bug fixes and new features
- Update documentation for API changes

### PR Description
- Reference related issues (e.g., "Fixes #123")
- Provide clear summary of changes and motivation
- Include breaking changes section if applicable

## Common Patterns

### Adding a New Consideration
```csharp
public class MyConcern : IConsideration
{
    public float Evaluate(Runtime rt)
    {
        // Return score between 0.0 and 1.0
        return rt.Bus.TryGet<MyFact>(out var fact) ? 1.0f : 0.0f;
    }
}
```

### Creating a Capability Module
```csharp
[Capability(Priority = 100, Domain = "my-domain")]
[RequiresFact<MyRequiredFact>]
public class MyModule : ICapabilityModule
{
    public IEnumerable<Proposal> Propose(Runtime rt)
    {
        yield return new Proposal(
            id: "my.action",
            cons: new[] { new HasFact<MyFact>() },
            act: async ct => { /* action logic */ }
        );
    }
}
```

### Using EventBus History
```csharp
// Get recent events with timestamps
var history = bus.GetHistory<UserMessage>(maxItems: 10);
foreach (var evt in history)
    Console.WriteLine($"{evt.Timestamp}: {evt.Value.Text}");
```

### Scoped State for Multi-Agent
```csharp
var rootBus = new EventBus();
var agent1Bus = rootBus.CreateScope("agent-1");
var agent2Bus = rootBus.CreateScope("agent-2");

// Isolated state per agent
agent1Bus.Publish(new AgentStatus("busy"));

// Shared state from parent
rootBus.Publish(new GlobalConfig("production"));
agent1Bus.TryGetWithFallback<GlobalConfig>(out var config); // ✅ Found
```

## Security & Best Practices

- Never commit secrets or API keys to the repository
- Use dependency injection for external dependencies (API clients, databases)
- Validate inputs at public API boundaries
- Handle exceptions gracefully and provide meaningful error messages
- Follow SOLID principles and maintain low coupling
- Keep the public API surface minimal and well-documented

## Package & Release

- Version is managed in `UtilityAi/UtilityAi.csproj`
- Releases are automated via GitHub Actions (`build-and-release.yml`)
- Package is published to NuGet as `UtilityAi`
- See `RELEASE.md` for release process details

## Additional Resources

- [Architecture Guide](../docs/ARCHITECTURE.md) - Framework design and patterns
- [Integration Guide](../docs/INTEGRATION.md) - LLM integration examples
- [Example Project](../Example/) - Working task management system
- [Proposal Patterns](../docs/PROPOSAL_PATTERNS.md) - Common scoring patterns
