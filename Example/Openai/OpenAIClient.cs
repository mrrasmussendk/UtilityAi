using System.ClientModel;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenAI.Responses;

namespace Example.Openai;

public sealed class OpenAiClient
{
    private readonly HttpClient _http;
    private readonly string _apiKey;

    public OpenAiClient(string apiKey)
    {
        _apiKey = apiKey ?? "";
        _http = new HttpClient {BaseAddress = new Uri("https://api.openai.com/")};
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    [Experimental("OPENAI001")]
    public async Task<string> StructuredJsonAsync(
        string system,
        string user,
        JObject jsonSchema,
        string schemaName,
        string model,
        CancellationToken ct)
    {
        // Create the SDK Responses client (pull API key from env or your secret store)
        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")
                     ?? throw new InvalidOperationException("OPENAI_API_KEY not set.");
        var client = new OpenAIResponseClient(model, apiKey);

        // Local helper to safely embed strings in raw JSON
        static string J(string s) => JsonConvert.ToString(s ?? string.Empty);

        // Build the exact Responses body we want:
        // - web_search tool enabled
        // - response_format with json_schema (strict)
        // - input parts use { type: "input_text", text: ... }
        var requestJson = $@"
{{
  ""model"": {J(model)},
  ""tools"": [ {{ ""type"": ""web_search"" }} ],
  ""input"": [
    {{
      ""role"": ""system"",
      ""content"": {J(system)}
    }},
    {{
      ""role"": ""user"",
      ""content"": {J(user)}
    }}
  ],
  ""text"": {{
    ""format"": {{
      ""type"": ""json_schema"",
      ""name"": {J(schemaName)},
      ""schema"": {jsonSchema.ToString(Formatting.None)},
      ""strict"": true
    }}
  }}
}}";


        using var content = BinaryContent.Create(BinaryData.FromString(requestJson));
        var result = await client.CreateResponseAsync(content);

        // Read raw JSON so we can prefer `output_text`, else stitch from message parts
        var raw = result.GetRawResponse().Content.ToString();
        using var doc = JsonDocument.Parse(raw);

        string? outputText =
            doc.RootElement.TryGetProperty("output_text", out var ot) && ot.ValueKind == JsonValueKind.String
                ? ot.GetString()
                : null;

        if (string.IsNullOrWhiteSpace(outputText) &&
            doc.RootElement.TryGetProperty("output", out var output) &&
            output.ValueKind == JsonValueKind.Array)
        {
            var sb = new StringBuilder();
            foreach (var item in output.EnumerateArray())
            {
                if (item.TryGetProperty("type", out var t) && t.GetString() == "message" &&
                    item.TryGetProperty("content", out var c) && c.ValueKind == JsonValueKind.Array)
                {
                    foreach (var part in c.EnumerateArray())
                    {
                        if (part.TryGetProperty("text", out var txt) && txt.ValueKind == JsonValueKind.String)
                            sb.AppendLine(txt.GetString());
                    }
                }
            }

            outputText = sb.ToString().Trim();
        }

        if (string.IsNullOrWhiteSpace(outputText))
            throw new Exception("OPENAI_EMPTY_TEXT");

        // The model returns a single JSON string conforming to your schema
        return outputText!;
    }


    public async Task<string> TextAsync(string system, string user, string model, CancellationToken ct)
    {
        // Non-structured text — simpler payload
        var payload = new
        {
            model = model,
            input = new object[]
            {
                new {role = "system", content = system},
                new {role = "user", content = user}
            }
        };
        var json = JsonConvert.SerializeObject(payload);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var resp = await _http.PostAsync("v1/responses", content, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode) throw new Exception($"OPENAI_HTTP_{(int) resp.StatusCode}: {body}");
        var root = JObject.Parse(body);

        // 1) Try the convenience field if present on some responses
        var text = (string?) root["output_text"];
        if (!string.IsNullOrWhiteSpace(text)) return text!;

        // 2) Parse the structured output: pick message -> output_text blocks
        var blocks = root["output"]?
            .Children<JObject>()
            .Where(o => (string?) o["type"] == "message")
            .SelectMany(o => o["content"]!.Children<JObject>())
            .Where(c => (string?) c["type"] == "output_text")
            .Select(c => (string?) c["text"])
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .ToList();

        if (blocks is {Count: > 0})
            return string.Concat(blocks!); // join all text parts

        // 3) Fallbacks (if provider returns a single message-like shape)
        text = (string?) root.SelectToken("$.output[?(@.type=='message')].content[0].text")
               ?? (string?) root.SelectToken("$.message.content[0].text");

        if (!string.IsNullOrWhiteSpace(text)) return text!;

        throw new Exception("OPENAI_EMPTY_TEXT");
    }
}