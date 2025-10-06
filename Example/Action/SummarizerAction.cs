using Example.Openai;
using Newtonsoft.Json;
using UtilityAi.Actions;
using UtilityAi.Utils;

namespace Example.Action;

public sealed class SummarizerAction : IAction
{
    private readonly OpenAiClient _client;
    private readonly string _model;
    public SummarizerAction(OpenAiClient client, string model = "gpt-5") { _client = client; _model = model; }

    public string Id => "summarizer_llm";
    public bool Gate(IBlackboard bb) => bb.GetOr("search:count", 0) >= 3;

    public async Task<AgentOutcome> ActAsync(IBlackboard bb, CancellationToken ct)
    {
        var t0 = DateTimeOffset.UtcNow;
        var results = bb.GetOr("search:results", "");

        var sys = "You are a concise news summarizer. Cite sources inline.";
        var usr = $"Summarize these latest tech headlines into a 5-7 sentence brief for locale {bb.GetOr("context:locale","en-US")}:\n" +
                  results;

        var text = await _client.TextAsync(sys, usr, _model, ct);
        bb.Set("answer:text", text);
        var latency = DateTimeOffset.UtcNow - t0;
        return new AgentOutcome(true, 0.04, latency);
    }
}