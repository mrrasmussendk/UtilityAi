using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Example.Openai;
using Newtonsoft.Json.Linq;
using UtilityAi.Sensor;
using UtilityAi.Utils;

namespace Example.Sensor;

[Experimental("OPENAI001")]
public sealed class OutputModeSensorEnsemble : ISensor
{
    // Simple robust detector: keyword lexicon + fallback LLM (optional)
    private readonly OpenAiClient? _client;
    private readonly string _model;

    public OutputModeSensorEnsemble(OpenAiClient? client = null, string model = "gpt-5-mini")
    {
        _client = client;
        _model = model;
    }

    public async Task SenseAsync(IBlackboard bb, CancellationToken ct)
    {
        if (bb.Has("task:output_mode")) return;
        
        var prompt = (bb.GetOr("prompt", "") ?? "");
        var text = prompt.ToLowerInvariant();
        // A) Heuristic lexicon
        var audioLex = new[] {"podcast", "voice", "read it out", "sound", "audio", "narrate", "listen", "radio"};
        var textLex = new[] {"write", "article", "transcript", "summary", "report"};
        var remote = new[] {"sms"};
        double audioHit = audioLex.Any(text.Contains) ? 0.9 : 0.0;
        double textHit = textLex.Any(text.Contains) ? 0.7 : 0.0;
        double remoteHit = remote.Any(text.Contains) ? 1 : 0.0;
        string mode = "text";
        if (remoteHit > 0)
        {
            mode = "sms";
        }
        else
        {
            mode = audioHit > 0 && audioHit >= textHit ? "audio" : "text";
        }

        double conf = Math.Max(audioHit, textHit);

        // B) Optional LLM backstop for ambiguous phrasing
        if (_client != null && conf < 0.65)
        {
            var schema = JObject.Parse(@"
            { ""type"":""object"",
              ""properties"": {
                 ""output_mode"": { ""type"":""string"", ""enum"": [""text"",""audio"",""both"", ""sms""] },
                 ""confidence"": { ""type"":""number"", ""minimum"":0, ""maximum"":1 }
              },
              ""required"": [""output_mode"", ""confidence""],
              ""additionalProperties"": false
            }");

            var sys = "Decide the intended output modality. Return strict JSON only.";
            var usr = $"Prompt: {prompt}";
            try
            {
                var json = await _client.StructuredJsonAsync(sys, usr, schema, "mode_decision", _model, ct);
                var obj = JObject.Parse(json);
                mode = obj["output_mode"]?.Value<string>() ?? mode;
                conf = Math.Max(conf, obj["confidence"]?.Value<double>() ?? 0.0);
            }
            catch (Exception e)
            {
                Debug.Write(e);
            }
        }

        bb.Set("task:output_mode", mode);
        bb.Set("signal:output_mode_conf", Math.Clamp(conf, 0, 1));
    }
}