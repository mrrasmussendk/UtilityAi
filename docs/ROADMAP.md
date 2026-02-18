# UtilityAI Framework Roadmap

## Current State Assessment

### ✅ What Works Well
- Core utility scoring system with geometric mean
- EventBus with history and subscriptions
- Capability-based module discovery
- Eligibility vs considerations separation
- Response curves and selection strategies
- ProposalHelper fluent API
- Comprehensive testing (69+ tests)
- Good documentation

### ⚠️ What Needs Improvement
- LLM integration requires too much boilerplate
- No built-in tool calling support
- Streaming responses not supported
- Checkpoint system underutilized
- Missing visualization/debugging tools
- Limited multi-agent patterns
- No vector store integration

---

## Priority 0: Critical (Next Release)

### 1. LLM Integration Package

**Problem:** Users have to manually wire OpenAI/Anthropic clients, build prompts, handle errors.

**Solution:** Create `UtilityAi.LLM` package with ready-to-use integrations.

```csharp
// After
using UtilityAi.LLM.OpenAI;

orchestrator
    .AddLlmModule<OpenAIModule>(config => {
        config.ApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        config.Model = "gpt-4";

        config.AddStrategy("direct_response")
              .WithSystemPrompt("You are a helpful assistant")
              .WithTemperature(0.7)
              .WithMaxTokens(500)
              .WithConsideration(new SignalConsideration<Context>(
                  name: "confidence",
                  selector: ctx => ctx.Confidence,
                  curve: x => x * x,
                  inputDomain: (0, 1)));

        config.AddStrategy("creative_writing")
              .WithSystemPrompt("You are a creative writer")
              .WithTemperature(1.2);
    });
```

**Features:**
- Abstract `ILlmProvider` interface
- Implementations: OpenAI, Anthropic, Azure OpenAI, Ollama
- Automatic conversation history from EventBus
- Error handling with retry policies
- Token counting and budget management
- Prompt template system

**Files to Create:**
```
UtilityAi.LLM/
├── ILlmProvider.cs
├── LlmCapabilityModule.cs
├── LlmConfiguration.cs
├── Providers/
│   ├── OpenAIProvider.cs
│   ├── AnthropicProvider.cs
│   ├── AzureOpenAIProvider.cs
│   └── OllamaProvider.cs
├── PromptTemplate.cs
├── ConversationBuilder.cs
└── Considerations/
    ├── TokenBudgetConsideration.cs
    └── ResponseQualityConsideration.cs
```

---

### 2. Tool Calling Framework

**Problem:** No standard way for LLMs to call tools/functions.

**Solution:** Built-in tool registration and execution framework.

```csharp
// Define a tool
public class SearchTool : ITool
{
    public string Name => "search_web";
    public string Description => "Search the web for information";

    public ToolParameter[] Parameters => new[]
    {
        new ToolParameter("query", "string", "Search query", required: true),
        new ToolParameter("max_results", "integer", "Max results", required: false)
    };

    public async Task<object> ExecuteAsync(Dictionary<string, object> args, CancellationToken ct)
    {
        var query = args["query"].ToString();
        var results = await SearchWeb(query);
        return new { results };
    }
}

// Register tools
orchestrator
    .RegisterTool(new SearchTool())
    .RegisterTool(new CalculatorTool())
    .RegisterTool(new DatabaseQueryTool());

// LLM modules automatically get tool schemas
var llmModule = new OpenAIModule(tools: orchestrator.GetRegisteredTools());
```

**Features:**
- `ITool` interface with automatic schema generation
- Tool parameter validation
- Parallel tool execution
- Tool result caching
- Tool execution history
- Integration with LLM providers (OpenAI function calling, Anthropic tool use)

**Files to Create:**
```
UtilityAi.Tools/
├── ITool.cs
├── ToolParameter.cs
├── ToolResult.cs
├── ToolRegistry.cs
├── ToolExecutor.cs
├── ToolSchemaGenerator.cs
└── BuiltIn/
    ├── SearchTool.cs
    ├── CalculatorTool.cs
    ├── DateTimeTool.cs
    └── FileTool.cs
```

---

## Priority 1: High Value (Next 2-3 Months)

### 3. Streaming Support

**Problem:** Actions are fire-and-forget. No way to stream intermediate results.

**Solution:** `IStreamingAction` interface with chunk publishing.

```csharp
.WithStreamingAction(async (ct, stream) =>
{
    var prompt = BuildPrompt(rt);

    await foreach (var chunk in llm.StreamAsync(prompt, ct))
    {
        // Publish chunks to EventBus for real-time UI updates
        await stream.PublishChunk(new LlmChunk(chunk.Text, chunk.Delta));

        // Can also publish intermediate facts
        if (chunk.IsComplete)
        {
            rt.Bus.Publish(new AssistantResponse(chunk.FullText));
        }
    }
})
```

**Features:**
- `IStreamingAction` interface
- Chunk publishing to EventBus
- Backpressure handling
- Cancellation support
- Progress reporting

**Files to Create:**
```
UtilityAi/Streaming/
├── IStreamingAction.cs
├── StreamPublisher.cs
├── StreamChunk.cs
└── StreamingProposalHelper.cs
```

---

### 4. Enhanced Checkpoint System

**Problem:** EventBus snapshots exist but not well integrated into orchestration loop.

**Solution:** Automatic checkpointing with replay and rollback.

```csharp
var orchestrator = new UtilityAiOrchestrator(bus: bus)
    .WithCheckpointing(config => {
        config.Backend = new RedisCheckpointStore(connectionString);
        config.SaveInterval = TimeSpan.FromSeconds(10);
        config.AutoRestore = true;
        config.MaxCheckpoints = 100;
    });

// Rollback to previous state
await orchestrator.RollbackToCheckpoint(checkpointId);

// Replay from checkpoint
await orchestrator.ReplayFromCheckpoint(checkpointId, maxTicks: 50);
```

**Features:**
- Automatic checkpoint after each tick (configurable)
- Multiple storage backends (Memory, File, Redis, Postgres)
- Rollback to any checkpoint
- Replay capabilities for debugging
- Checkpoint compression
- Diff-based checkpoints (only save changes)

**Files to Create:**
```
UtilityAi.Checkpointing/
├── ICheckpointStore.cs
├── CheckpointManager.cs
├── Checkpoint.cs
├── Stores/
│   ├── InMemoryCheckpointStore.cs
│   ├── FileCheckpointStore.cs
│   ├── RedisCheckpointStore.cs
│   └── PostgresCheckpointStore.cs
└── DiffCalculator.cs
```

---

### 5. Better Examples

**Problem:** Current examples (AgentAssistant, SmartHomeAgent) are good but don't show real LLM integration.

**Solution:** Add practical, production-ready examples.

**New Examples:**
1. **ChatBot** - Real OpenAI integration with streaming
2. **RAG Agent** - Vector search + document QA
3. **CodeReview Agent** - GitHub integration + LLM analysis
4. **Multi-Agent Debate** - Two agents discussing a topic
5. **Task Planner** - LLM breaks down goals into tasks
6. **Customer Support Bot** - Ticket routing + knowledge base

---

## Priority 2: Medium Value (3-6 Months)

### 6. Visualization & Debugging Tools

**Problem:** Hard to understand why a proposal won or lost.

**Solution:** Web-based visualization dashboard.

**Features:**
- Live proposal scoring view
- Utility timeline chart
- EventBus state explorer
- Consideration breakdown per proposal
- Replay mode with step-through
- Export to JSON for analysis

**Tech Stack:**
- Blazor WebAssembly for UI
- SignalR for real-time updates
- Chart.js for visualizations

---

### 7. Multi-Agent Coordination Patterns

**Problem:** Scoped buses exist but no high-level patterns.

**Solution:** Pre-built coordination helpers.

```csharp
// Hierarchical agent pattern
var coordinator = new HierarchicalCoordinator(rootBus)
    .AddWorkerAgent("researcher", new ResearchAgent())
    .AddWorkerAgent("writer", new WriterAgent())
    .AddWorkerAgent("reviewer", new ReviewerAgent())
    .WithWorkflow(async (context) => {
        var research = await coordinator.Delegate("researcher", context);
        var draft = await coordinator.Delegate("writer", research);
        var final = await coordinator.Delegate("reviewer", draft);
        return final;
    });

// Debate pattern
var debateCoordinator = new DebateCoordinator(rootBus)
    .AddAgent("pro", new ProAgent())
    .AddAgent("con", new ConAgent())
    .AddAgent("moderator", new ModeratorAgent())
    .WithRounds(3);
```

**Files to Create:**
```
UtilityAi.MultiAgent/
├── ICoordinator.cs
├── Patterns/
│   ├── HierarchicalCoordinator.cs
│   ├── DebateCoordinator.cs
│   ├── ConsensusCoordinator.cs
│   └── PipelineCoordinator.cs
├── AgentRegistry.cs
└── MessageRouter.cs
```

---

## Priority 3: Nice to Have (6-12 Months)

### 8. Vector Store Integration

**Problem:** No built-in semantic search or embeddings.

**Solution:** `IVectorStore` abstraction with RAG patterns.

```csharp
orchestrator
    .WithVectorStore(new PineconeVectorStore(apiKey))
    .AddEmbeddingModel(new OpenAIEmbeddings("text-embedding-ada-002"));

// Semantic similarity consideration
.WithConsideration(new SemanticSimilarityConsideration(
    query: "user query",
    threshold: 0.7))
```

**Features:**
- `IVectorStore` interface
- Providers: Pinecone, Qdrant, Weaviate, Chroma
- Embedding model abstraction
- RAG module templates
- Semantic considerations

---

### 9. Testing Utilities

**Problem:** Testing complex orchestration scenarios requires boilerplate.

**Solution:** Test helpers and assertion library.

```csharp
[Fact]
public async Task Agent_Should_ResearchBeforeResponding()
{
    // Arrange
    var scenario = new OrchestrationScenario()
        .WithModule<ResearchModule>()
        .WithModule<ResponseModule>()
        .StartsWith(new UserMessage("What is quantum computing?"))
        .ExpectFact<ResearchResults>(within: 5.Ticks())
        .ExpectFact<AssistantResponse>(within: 10.Ticks())
        .ExpectProposalExecuted("research.web", beforeTick: 5)
        .ExpectProposalExecuted("respond.direct", afterTick: 5);

    // Act & Assert
    await scenario.RunAndAssert();
}
```

---

### 10. Performance Optimizations

**Current Performance:**
- ~1000 proposals/second evaluation
- ~100 ticks/second orchestration

**Optimizations:**
- Parallel consideration evaluation
- Consideration result caching
- Incremental proposal generation
- SIMD for geometric mean calculation
- Memory pool for proposal objects

**Target:**
- 10,000 proposals/second
- 1,000 ticks/second

---

## Implementation Timeline

### Q2 2025
- ✅ Eligibility vs Considerations improvements (DONE)
- ✅ Selection strategies (DONE)
- 🏗️ LLM Integration Package (P0)
- 🏗️ Tool Calling Framework (P0)

### Q3 2025
- Streaming Support (P1)
- Enhanced Checkpoint System (P1)
- Better Examples (P1)

### Q4 2025
- Visualization Tools (P2)
- Multi-Agent Patterns (P2)

### Q1 2026
- Vector Store Integration (P3)
- Testing Utilities (P3)
- Performance Optimizations (P3)

---

## Success Metrics

### Adoption
- 1,000+ GitHub stars
- 100+ NuGet downloads/month
- 10+ community contributors

### Quality
- 200+ tests
- 90%+ code coverage
- < 5 open critical bugs

### Performance
- Sub-1ms tick time for typical scenarios
- 10,000+ proposals/second
- < 100MB memory for typical workloads

### Community
- 50+ documentation pages
- 20+ example projects
- Active Discord/Discussions

---

## How to Contribute

See [CONTRIBUTING.md](./CONTRIBUTING.md) for details on:
- Coding standards
- Pull request process
- Issue reporting
- Feature proposals
