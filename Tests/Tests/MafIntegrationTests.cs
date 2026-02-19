using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using UtilityAi.Consideration;
using UtilityAi.Maf;
using UtilityAi.Orchestration;
using UtilityAi.Utils;
using Xunit;

namespace Tests;

/// <summary>
/// Tests for the Microsoft Agent Framework (MAF) integration.
/// </summary>
public class MafIntegrationTests
{
    // ─── MafClient ────────────────────────────────────────────────


    [Fact]
    public void MafClient_GetAgentsClient_ReturnsClient()
    {
        var client = new MafClient("https://example.openai.azure.com");

        var agentsClient = client.GetAgentsClient();

        Assert.NotNull(agentsClient);
    }



    // ─── Test Helpers ────────────────────────────────────────────
    

    private sealed class StubSession : AgentSession
    {
        public override object? GetService(Type serviceType, object? serviceKey = null) => null;
    }

    /// <summary>
    /// A simple fixed-value consideration for testing.
    /// </summary>
    private sealed class FixedScore : IConsideration
    {
        private readonly double _score;
        public FixedScore(double score) => _score = score;
        public string Name => "fixed";
        public double Evaluate(Runtime rt) => _score;
    }

    /// <summary>
    /// Test result record for EventBus.
    /// </summary>
    private record TestResult(string Text);
}
