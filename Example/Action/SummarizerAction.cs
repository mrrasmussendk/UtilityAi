using System.Diagnostics.CodeAnalysis;
using Example.Action.Considerations;
using Example.Openai;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UtilityAi.Actions;
using UtilityAi.Consideration;
using UtilityAi.Utils;

namespace Example.Action;

public sealed class SummarizerAction : IAction
{
    private readonly OpenAiClient _client;
    private readonly string _model;
    private List<IConsideration> _considerations;

    public SummarizerAction(OpenAiClient client, string model = "gpt-5")
    {
        _client = client;
        _model = model;
        _considerations = new List<IConsideration>()
        {
            new HasValueConsideration("answer:text", true),
            new HasValueConsideration("search:results")
        };
    }

    public string Id => "summarizer_llm";

    [Experimental("OPENAI001")]
    public async Task<AgentOutcome> ActAsync(IBlackboard bb, CancellationToken ct)
    {
        var t0 = DateTimeOffset.UtcNow;
        var results = bb.GetOr("search:results", "");

        var sys = "You are a concise news summarizer remove sources from the output.";
        var usr =
            $"Summarize these latest tech headlines into a 5-7 sentence brief for locale {bb.GetOr("context:locale", "en-US")}:\n" +
            results;

        var schema = JObject.Parse(@"
            { ""type"":""object"",
              ""properties"": {
                 ""output"": { ""type"":""string""},
                 ""confidence"": { ""type"":""number"", ""minimum"":0, ""maximum"":1 }
              },
              ""required"": [""output"", ""confidence""],
              ""additionalProperties"": false
            }");
        try
        {
            var text = await _client.StructuredJsonAsync(sys, usr, schema, "summerize", "gpt-5-mini", ct);
            var obj = JObject.Parse(text);
            var content = obj["output"]?.Value<string>() ?? "";
            if (content.Length > 0)
            {
                bb.Set("answer:text", content);

                var latency = DateTimeOffset.UtcNow - t0;
                return new AgentOutcome(true, 0.04, latency);
            }

            return new AgentOutcome(false, 0.0, DateTimeOffset.UtcNow - t0);
        }
        catch (OperationCanceledException)
        {
            // Respect cancellation: do not set error, just return a non-success outcome
            return new AgentOutcome(false, 0.0, DateTimeOffset.UtcNow - t0);
        }
        catch (Exception ex)
        {
            bb.Set("answer:error", ex.GetType().Name);
            return new AgentOutcome(false, 0.0, DateTimeOffset.UtcNow - t0);
        }
    }

    private bool Gate(IBlackboard bb)
    {
        return !bb.Has("answer:text");
    }

    public double Score(IBlackboard bb)
    {
        if (!Gate(bb)) return 0.0;
        return Scoring.AggregateWithMakeup(_considerations, bb);
    }
}