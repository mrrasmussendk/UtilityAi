using System.Diagnostics.CodeAnalysis;
using Example.Openai;
using Newtonsoft.Json.Linq;
using UtilityAi.Sensor;
using UtilityAi.Utils;

namespace Example.Sensor;

public class TopicSensor : ISensor
{
    private readonly OpenAiClient? _client;
    private readonly string _model;
    public TopicSensor(OpenAiClient? client = null, string model = "gpt-5-nano")
    {
        _client = client;
        _model = model;
    }
    [Experimental("OPENAI001")]
    public async Task SenseAsync(IBlackboard bb, CancellationToken ct)
    {
        if(bb.Has("context:topic")) return;
        var prompt = (bb.GetOr("prompt", "") ?? "");
        var schema = JObject.Parse(@"
            { ""type"":""object"",
              ""properties"": {
                 ""topic"": { ""type"":""string""}
              },
              ""required"": [""topic""],
              ""additionalProperties"": false
            }");
       
        

        var sys = "Return exactly one canonical news topic label from the allowed set. Do not include adjectives (e.g., 'latest'), delivery methods (e.g., 'sms'), or long phrases. Pick the closest single topic only.";
        var usr = $"Prompt: {prompt}";
        try
        {
            var json = await _client.StructuredJsonAsync(sys, usr, schema, "topic_sensor", _model, ct);
            var obj = JObject.Parse(json);

            var topic = obj["topic"]?.Value<string>();
            bb.Set("context:topic", topic);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }

    }
}