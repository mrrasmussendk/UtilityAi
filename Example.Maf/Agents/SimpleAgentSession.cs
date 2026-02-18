using System.Text.Json;
using Microsoft.Agents.AI;

namespace Example.Maf.Agents;

/// <summary>
/// A minimal AgentSession implementation for demonstration purposes.
/// In production, use InMemoryAgentSession with a ChatHistoryProvider
/// or implement a custom session backed by external storage.
/// </summary>
public sealed class SimpleAgentSession : AgentSession
{
    public override object? GetService(Type serviceType, object? serviceKey = null) => null;
}
