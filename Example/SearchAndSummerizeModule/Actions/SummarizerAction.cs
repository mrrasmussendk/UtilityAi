using System.Diagnostics.CodeAnalysis;
using Example.Openai;
using Example.SearchAndSummerizeModule.DTO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UtilityAi.Actions;
using UtilityAi.Utils;

namespace Example.SearchAndSummerizeModule.Actions;

public sealed class SummarizerAction : IAction<ISearchResults, Summary>
{
    private readonly OpenAiClient _client;
    private readonly string _model;


    public SummarizerAction(OpenAiClient client)
    {
        _client = client;
        _model = "gpt-5";
    }

    public string Id => "summarizer_llm";


    [Experimental("OPENAI001")]
    public async Task<Summary> ActAsync(ISearchResults request, CancellationToken ct)
    {
        var t0 = DateTimeOffset.UtcNow;
        var results = request.Items;

        var sys = "You are a concise news summarizer remove sources from the output.";
        var usr =
            $"Summarize these latest tech headlines into a 5-7 sentence brief:\n" +
            JsonConvert.SerializeObject(results);

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
                return new Summary(content);
            }

            return new Summary("");
        }
        catch (OperationCanceledException)
        {
            // Respect cancellation: do not set error, just return a non-success outcome
            throw;
        }
        catch (Exception ex)
        {
            throw;
        }
    }
}