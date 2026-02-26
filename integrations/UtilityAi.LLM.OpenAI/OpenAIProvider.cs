using OpenAI.Chat;
using System.ClientModel;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using UtilityAi.LLM.Abstractions;

namespace UtilityAi.LLM.OpenAI;

/// <summary>
/// OpenAI provider implementation for UtilityAI LLM abstractions.
/// </summary>
public class OpenAIProvider : ILlmProvider
{
    private readonly ChatClient _client;
    private readonly string _model;
    private readonly string _apiKey;
    private static readonly HttpClient ResponsesHttpClient = new();

    public string ProviderName => "OpenAI";
    public string Model => _model;

    /// <summary>
    /// Creates an OpenAI provider with the specified model and API key.
    /// </summary>
    /// <param name="model">Model name (e.g., "gpt-4", "gpt-3.5-turbo").</param>
    /// <param name="apiKey">OpenAI API key. If null, uses OPENAI_API_KEY environment variable.</param>
    public OpenAIProvider(string model = "gpt-4", string? apiKey = null)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
        apiKey ??= Environment.GetEnvironmentVariable("OPENAI_API_KEY")
                   ?? throw new InvalidOperationException(
                       "OpenAI API key must be provided or set in OPENAI_API_KEY environment variable");

        _apiKey = apiKey;
        _client = new ChatClient(model, new ApiKeyCredential(apiKey));
    }

    public async Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken ct = default)
    {
        if (HasOpenAiSkills(request.Options?.OpenAiSkills))
            return await CompleteWithResponsesApiAsync(request, ct);

        var messages = ConvertMessages(request.Messages);
        var options = BuildOptions(request.Options);

        var completion = await _client.CompleteChatAsync(messages, options, ct);
        return ConvertResponse(completion.Value);
    }

    public async IAsyncEnumerable<LlmStreamChunk> StreamAsync(
        LlmRequest request,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        if (HasOpenAiSkills(request.Options?.OpenAiSkills))
        {
            var response = await CompleteWithResponsesApiAsync(request, ct);
            if (!string.IsNullOrEmpty(response.Content))
            {
                yield return new LlmStreamChunk(
                    Delta: response.Content,
                    IsComplete: false);
            }

            yield return new LlmStreamChunk(
                Delta: null,
                IsComplete: true,
                FinishReason: response.FinishReason);
            yield break;
        }

        var messages = ConvertMessages(request.Messages);
        var options = BuildOptions(request.Options);

        var stream = _client.CompleteChatStreamingAsync(messages, options, ct);

        string fullContent = string.Empty;
        ChatFinishReason? finishReason = null;

        await foreach (var update in stream.WithCancellation(ct))
        {
            if (update.ContentUpdate.Count > 0)
            {
                var delta = string.Join("", update.ContentUpdate.Select(c => c.Text));
                fullContent += delta;

                yield return new LlmStreamChunk(
                    Delta: delta,
                    IsComplete: false);
            }

            if (update.FinishReason.HasValue)
            {
                finishReason = update.FinishReason;
            }
        }

        // Final chunk
        yield return new LlmStreamChunk(
            Delta: null,
            IsComplete: true,
            FinishReason: ConvertFinishReason(finishReason));
    }

    public int EstimateTokenCount(string text)
    {
        // Rough estimation: ~4 characters per token for English text
        // For more accurate counting, use tiktoken library
        return text.Length / 4;
    }

    private static List<ChatMessage> ConvertMessages(List<LlmMessage> messages)
    {
        return messages.Select<LlmMessage, ChatMessage>(m => m.Role switch
        {
            LlmRole.System => ChatMessage.CreateSystemMessage(m.Content),
            LlmRole.User => ChatMessage.CreateUserMessage(m.Content),
            LlmRole.Assistant => ChatMessage.CreateAssistantMessage(m.Content),
            LlmRole.Tool => ChatMessage.CreateToolMessage(m.Name ?? "", m.Content),
            _ => throw new ArgumentException($"Unknown role: {m.Role}")
        }).ToList();
    }

    private static ChatCompletionOptions? BuildOptions(LlmOptions? options)
    {
        if (options == null) return null;

        var chatOptions = new ChatCompletionOptions();

        if (options.Temperature.HasValue)
            chatOptions.Temperature = (float)options.Temperature.Value;

        if (options.MaxTokens.HasValue)
            chatOptions.MaxOutputTokenCount = options.MaxTokens.Value;

        if (options.TopP.HasValue)
            chatOptions.TopP = (float)options.TopP.Value;

        if (options.FrequencyPenalty.HasValue)
            chatOptions.FrequencyPenalty = (float)options.FrequencyPenalty.Value;

        if (options.PresencePenalty.HasValue)
            chatOptions.PresencePenalty = (float)options.PresencePenalty.Value;

        if (options.StopSequences != null)
        {
            foreach (var stop in options.StopSequences)
                chatOptions.StopSequences.Add(stop);
        }

        if (options.Tools != null)
        {
            foreach (var tool in options.Tools)
            {
                chatOptions.Tools.Add(ChatTool.CreateFunctionTool(
                    functionName: tool.Name,
                    functionDescription: tool.Description,
                    functionParameters: BinaryData.FromString(tool.ParametersSchema.RootElement.ToString())));
            }
        }

        return chatOptions;
    }

    private static LlmResponse ConvertResponse(ChatCompletion completion)
    {
        var content = string.Join("", completion.Content.Select(c => c.Text));
        var finishReason = ConvertFinishReason(completion.FinishReason);

        List<LlmToolCall>? toolCalls = null;
        if (completion.ToolCalls.Count > 0)
        {
            toolCalls = completion.ToolCalls
                .Select(tc => new LlmToolCall(
                    Id: tc.Id,
                    Name: tc.FunctionName,
                    ArgumentsJson: tc.FunctionArguments.ToString()))
                .ToList();
        }

        var usage = new LlmUsage(
            PromptTokens: completion.Usage.InputTokenCount,
            CompletionTokens: completion.Usage.OutputTokenCount,
            TotalTokens: completion.Usage.TotalTokenCount);

        return new LlmResponse(
            Content: content,
            FinishReason: finishReason,
            Usage: usage,
            ToolCalls: toolCalls);
    }

    private static LlmFinishReason ConvertFinishReason(ChatFinishReason? reason)
    {
        return reason switch
        {
            ChatFinishReason.Stop => LlmFinishReason.Stop,
            ChatFinishReason.Length => LlmFinishReason.Length,
            ChatFinishReason.ToolCalls => LlmFinishReason.ToolCalls,
            ChatFinishReason.ContentFilter => LlmFinishReason.ContentFilter,
            _ => LlmFinishReason.Other
        };
    }

    private static bool HasOpenAiSkills(OpenAiSkillsOptions? skills)
    {
        if (skills == null)
            return false;

        var hasReferences = skills.References != null && skills.References.Count > 0;
        var hasInline = skills.Inline != null && skills.Inline.Count > 0;
        return hasReferences || hasInline;
    }

    private async Task<LlmResponse> CompleteWithResponsesApiAsync(LlmRequest request, CancellationToken ct)
    {
        var payload = BuildResponsesApiRequestBody(_model, request.Messages, request.Options);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/responses")
        {
            Content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json")
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

        using var response = await ResponsesHttpClient.SendAsync(httpRequest, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"OpenAI responses API request failed with status {(int)response.StatusCode}: {body}",
                null,
                response.StatusCode);
        }

        using var json = JsonDocument.Parse(body);
        return ConvertResponsesApiResponse(json.RootElement);
    }

    internal static JsonObject BuildResponsesApiRequestBody(string model, List<LlmMessage> messages, LlmOptions? options)
    {
        var payload = new JsonObject
        {
            ["model"] = model
        };

        var input = new JsonArray();
        foreach (var message in messages)
        {
            input.Add(new JsonObject
            {
                ["role"] = message.Role switch
                {
                    LlmRole.System => "system",
                    LlmRole.User => "user",
                    LlmRole.Assistant => "assistant",
                    LlmRole.Tool => "tool",
                    _ => "user"
                },
                ["content"] = message.Content
            });
        }
        payload["input"] = input;

        if (options?.Temperature is { } temperature)
            payload["temperature"] = temperature;

        if (options?.MaxTokens is { } maxTokens)
            payload["max_output_tokens"] = maxTokens;

        var tools = new JsonArray();

        if (options?.Tools != null)
        {
            foreach (var tool in options.Tools)
            {
                tools.Add(new JsonObject
                {
                    ["type"] = "function",
                    ["name"] = tool.Name,
                    ["description"] = tool.Description,
                    ["parameters"] = JsonNode.Parse(tool.ParametersSchema.RootElement.GetRawText())
                });
            }
        }

        if (HasOpenAiSkills(options?.OpenAiSkills))
        {
            var skills = new JsonArray();
            if (options!.OpenAiSkills!.References != null)
            {
                foreach (var reference in options.OpenAiSkills.References)
                {
                    var skillRef = new JsonObject
                    {
                        ["type"] = "skill_reference",
                        ["skill_id"] = reference.SkillId
                    };
                    if (!string.IsNullOrWhiteSpace(reference.Version))
                        skillRef["version"] = reference.Version;
                    skills.Add(skillRef);
                }
            }

            if (options!.OpenAiSkills!.Inline != null)
            {
                foreach (var inline in options.OpenAiSkills.Inline)
                {
                    skills.Add(new JsonObject
                    {
                        ["type"] = "inline",
                        ["bundle"] = inline.Base64ZipBundle
                    });
                }
            }

            tools.Add(new JsonObject
            {
                ["type"] = "shell",
                ["environment"] = new JsonObject
                {
                    ["type"] = options.OpenAiSkills.EnvironmentType == OpenAiSkillEnvironmentType.Local ? "local" : "container_auto",
                    ["skills"] = skills
                }
            });
        }

        if (tools.Count > 0)
            payload["tools"] = tools;

        return payload;
    }

    private static LlmResponse ConvertResponsesApiResponse(JsonElement root)
    {
        var content = root.TryGetProperty("output_text", out var outputText)
            ? outputText.GetString() ?? string.Empty
            : ExtractOutputText(root);

        var usage = new LlmUsage(
            PromptTokens: TryGetInt(root, "usage", "input_tokens"),
            CompletionTokens: TryGetInt(root, "usage", "output_tokens"),
            TotalTokens: TryGetInt(root, "usage", "total_tokens"));

        return new LlmResponse(
            Content: content,
            FinishReason: LlmFinishReason.Stop,
            Usage: usage,
            ToolCalls: null);
    }

    private static int TryGetInt(JsonElement root, string objectName, string propertyName)
    {
        if (!root.TryGetProperty(objectName, out var obj))
            return 0;
        if (!obj.TryGetProperty(propertyName, out var value))
            return 0;
        return value.TryGetInt32(out var result) ? result : 0;
    }

    private static string ExtractOutputText(JsonElement root)
    {
        if (!root.TryGetProperty("output", out var output) || output.ValueKind != JsonValueKind.Array)
            return string.Empty;

        foreach (var item in output.EnumerateArray())
        {
            if (!item.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var chunk in content.EnumerateArray())
            {
                if (chunk.TryGetProperty("text", out var text))
                    return text.GetString() ?? string.Empty;
            }
        }

        return string.Empty;
    }
}
