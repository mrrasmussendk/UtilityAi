using System.Diagnostics.CodeAnalysis;
using Example.Openai;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UtilityAi.Actions;
using UtilityAi.Utils;

namespace Example.Action;

[Experimental("OPENAI001")]
public sealed class VerifierAction : IAction
{
    private readonly OpenAiClient _client;
    private readonly string _model;
    public VerifierAction(OpenAiClient client, string model = "gpt-5") { _client = client; _model = model; }

    public string Id => "verifier_llm";
    public bool Gate(IBlackboard bb) => bb.Has("answer:text") && bb.GetOr("signal:uncertainty", 1.0) >= 0.2;

    public async Task<AgentOutcome> ActAsync(IBlackboard bb, CancellationToken ct)
    {
        var t0 = DateTimeOffset.UtcNow;

        var schema = JObject.Parse(@"
        { ""type"":""object"",
          ""properties"": {
             ""factuality"": { ""type"":""number"", ""minimum"":0, ""maximum"":1 },
             ""notes"": { ""type"":""array"", ""items"": { ""type"":""string"" } }
          },
          ""required"": [""factuality"", ""notes""],
          ""additionalProperties"": false
        }");

        var sys = "Assess factual alignment of the summary with the provided sources. Return strict JSON only. sources are listed inside the text";
        var usr = $"Summary:\n{bb.GetOr("answer:text","")}\n\nSources:\n{JsonConvert.SerializeObject(bb.GetOr("search:results", new List<object>()))}";

        var json = await _client.StructuredJsonAsync(sys, usr, schema, "verify", _model, ct);
        var obj = JObject.Parse(json);
        var factual = obj["factuality"]?.Value<double>() ?? 0.5;
        bb.Set("answer:verifier_score", Math.Clamp(factual, 0, 1));
        bb.Set("verifier:notes", obj["notes"]?.ToObject<List<string>>() ?? new List<string>());

        var latency = DateTimeOffset.UtcNow - t0;
        return new AgentOutcome(true, 0.03, latency);
    }
    public double Score(IBlackboard bb) => bb.GetOr("answer:verifier_score", 0.0);
}