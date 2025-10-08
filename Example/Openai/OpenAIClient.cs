using System.ClientModel;
using System.ClientModel.Primitives;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenAI.Responses;
using JsonSerializer = Newtonsoft.Json.JsonSerializer;

namespace Example.Openai;

public sealed class OpenAiClient
{
    private readonly HttpClient _http;
    private readonly string _apiKey;

    public OpenAiClient()
    { 
        _apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")
               ?? throw new InvalidOperationException("OPENAI_API_KEY not set.");
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
        
        var client = new OpenAIResponseClient(model, _apiKey);

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


    [Experimental("OPENAI001")]
    public async Task<string> TextAsync(string system, string user, string model, CancellationToken ct)
    {
        // Non-structured text — use Responses API with input_text parts
        var payload = new
        {
            model = model,
            input = new object[]
            {
                new { role = "system", content = new object[] { new { type = "input_text", text = system } } },
                new { role = "user",   content = new object[] { new { type = "input_text", text = user } } }
            }
        };
        var client = new OpenAIResponseClient(model, _apiKey);

        // Prepare serialized payload once; we will recreate content per attempt
        var jsonPayload = JsonConvert.SerializeObject(payload);

        const int MaxAttempts = 3;
        var delay = TimeSpan.FromMilliseconds(250);

        for (int attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                using var content = BinaryContent.Create(BinaryData.FromString(jsonPayload));
                var result = await client.CreateResponseAsync(
                    content,
                    new RequestOptions { CancellationToken = ct }
                ).ConfigureAwait(false);

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
            catch (TaskCanceledException ex) when (ct.IsCancellationRequested)
            {
                // Respect external cancellation
                throw new OperationCanceledException("The OpenAI request was canceled.", ex, ct);
            }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested)
            {
                // Treat as transient timeout; retry with backoff
                if (attempt == MaxAttempts) throw;
                await Task.Delay(delay).ConfigureAwait(false);
                delay = TimeSpan.FromMilliseconds(delay.TotalMilliseconds * 2);
            }
            catch (HttpRequestException) when (!ct.IsCancellationRequested)
            {
                if (attempt == MaxAttempts) throw;
                await Task.Delay(delay).ConfigureAwait(false);
                delay = TimeSpan.FromMilliseconds(delay.TotalMilliseconds * 2);
            }
            catch (IOException) when (!ct.IsCancellationRequested)
            {
                if (attempt == MaxAttempts) throw;
                await Task.Delay(delay).ConfigureAwait(false);
                delay = TimeSpan.FromMilliseconds(delay.TotalMilliseconds * 2);
            }
        }

        // Should not reach here
        throw new Exception("OPENAI_UNREACHABLE");
    }
}