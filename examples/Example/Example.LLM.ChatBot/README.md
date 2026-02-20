# 🤖 ChatBot Example - OpenAI Integration

This example demonstrates how to use the UtilityAI LLM integration packages to create a simple conversational chatbot with OpenAI.

## Features

✅ **OpenAI Integration** - Uses `UtilityAi.LLM.OpenAI` provider
✅ **Conversation History** - Automatically builds context from EventBus history
✅ **Error Handling** - Built-in retry logic and error handling
✅ **Clean Code** - ~150 lines total including comments

## Architecture

```
User Input → EventBus → ChatBotModule → OpenAIProvider → GPT → Response → EventBus → Display
```

**Key Components:**
- `LlmCapabilityModule` - Base class that handles LLM interaction
- `OpenAIProvider` - Implements `ILlmProvider` for OpenAI API
- `EventBus` - Stores conversation history
- `LlmModuleConfiguration` - Configures retry, error handling, callbacks

## Running the Example

### Prerequisites

1. Get an OpenAI API key from [platform.openai.com/api-keys](https://platform.openai.com/api-keys)
2. Set the environment variable:

```bash
# Linux/Mac
export OPENAI_API_KEY=sk-...

# Windows
set OPENAI_API_KEY=sk-...

# PowerShell
$env:OPENAI_API_KEY="sk-..."
```

### Run

```bash
cd Example/Example.LLM.ChatBot
dotnet run
```

## Code Walkthrough

### 1. Create OpenAI Provider

```csharp
var provider = new OpenAIProvider("gpt-3.5-turbo", apiKey);
```

### 2. Create ChatBot Module

```csharp
public class ChatBotModule : LlmCapabilityModule
{
    public ChatBotModule(ILlmProvider provider) : base(provider, new LlmModuleConfiguration(
        DefaultOptions: new LlmOptions(Temperature: 0.7, MaxTokens: 500),
        OnResponseReceived: async (rt, response, ct) =>
        {
            // Publish response to EventBus
            rt.Bus.Publish(new AssistantMessage(response.Content));
        }))
    {
    }

    public override IEnumerable<Proposal> Propose(Runtime rt)
    {
        yield return CreateLlmProposal(
            proposalId: "chat.respond",
            rt: rt,
            messagesBuilder: BuildMessages,
            options: Configuration.DefaultOptions);
    }

    private List<LlmMessage> BuildMessages(Runtime rt)
    {
        var messages = new List<LlmMessage>();
        messages.Add(LlmMessage.System("You are a helpful assistant."));

        // Get conversation history from EventBus
        var history = rt.Bus.GetHistory<UserMessage>(maxItems: 10);
        foreach (var msg in history)
        {
            messages.Add(LlmMessage.User(msg.Value.Text));
        }

        return messages;
    }
}
```

### 3. Wire It Up

```csharp
var bus = new EventBus();
var orchestrator = new UtilityAiOrchestrator(bus: bus)
    .AddModule(new ChatBotModule(provider));

// Publish user message
bus.Publish(new UserMessage("Hello!"));

// Run orchestration
await orchestrator.RunAsync(maxTicks: 5, CancellationToken.None);

// Get response
var response = bus.GetOrDefault<AssistantMessage>();
```

## What the Framework Handles For You

✅ **Conversation History** - Automatically from EventBus
✅ **Message Formatting** - Converts to OpenAI format
✅ **Error Handling** - Retry with exponential backoff
✅ **Token Counting** - Basic estimation
✅ **Async/Await** - Proper cancellation support

## Compare: With vs Without Framework

### WITHOUT Framework (Manual - ~200 lines)

```csharp
// You write all this:
var client = new ChatClient("gpt-4", apiKey);
var messages = new List<ChatMessage>();

// Manually build history
foreach (var msg in history)
    messages.Add(new UserChatMessage(msg.Text));

// Manually handle errors
try {
    var response = await client.CompleteChatAsync(messages);
    // Manually retry on failure
} catch (Exception ex) {
    // Manually handle
}
```

### WITH Framework (This Example - ~30 lines)

```csharp
// Framework does it for you:
var module = new ChatBotModule(new OpenAIProvider("gpt-4"));
orchestrator.AddModule(module);

// That's it!
```

## Next Steps

- **Add Streaming** - Use `StreamAsync()` for real-time responses
- **Add Tools** - Let the LLM call functions (see Tool Calling example)
- **Add RAG** - Integrate vector search for document QA
- **Switch Providers** - Try Anthropic, Azure OpenAI, or Ollama

## See Also

- [LLM Abstractions](../../UtilityAi.LLM.Abstractions/) - Core interfaces
- [OpenAI Provider](../../UtilityAi.LLM.OpenAI/) - OpenAI implementation
- [AgentAssistant Example](../AgentAssistant/) - More complex multi-strategy agent
