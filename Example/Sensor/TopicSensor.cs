using System.Diagnostics.CodeAnalysis;
using Example.Openai;
using Example.SearchAndSummerizeModule.DTO;
using Newtonsoft.Json.Linq;
using UtilityAi.Sensor;
using UtilityAi.Utils;

namespace Example.Sensor;

public class TopicSensor : ISensor
{
    private readonly OpenAiClient _client;
    private readonly string _model;

    public TopicSensor(OpenAiClient client, string model = "gpt-5-nano")
    {
        _client = client;
        _model = model;
    }

    [Experimental("OPENAI001")]
    public async Task SenseAsync(Runtime rt, CancellationToken ct)
    {
        if(rt.Bus.GetOrDefault<Topic>() is not null) return;

        var prompt = rt.Intent.Query + rt.Intent.Topic;
        var schema = JObject.Parse(@"
            { ""type"":""object"",
              ""properties"": {
                 ""topic"": { ""type"":""string""}
              },
              ""required"": [""topic""],
              ""additionalProperties"": false
            }");


        var sys =
            "Return exactly one canonical news topic label from the allowed set. Do not include adjectives (e.g., 'latest'), delivery methods (e.g., 'sms'), or long phrases. Pick the closest single topic only.";
        var usr = $"Prompt: {prompt}";
        try
        {
            var json = await _client.StructuredJsonAsync(sys, usr, schema, "topic_sensor", _model, ct);
            var obj = JObject.Parse(json);
            var topic = obj["topic"]?.Value<string>();
            rt.Bus.Publish(new Topic(topic ?? ""));
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }
}