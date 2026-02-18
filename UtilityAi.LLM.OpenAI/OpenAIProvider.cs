using OpenAI.Chat;
using System.ClientModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using UtilityAi.LLM.Abstractions;

namespace UtilityAi.LLM.OpenAI;

/// <summary>
/// OpenAI provider implementation for UtilityAI LLM abstractions.
/// </summary>
public class OpenAIProvider : ILlmProvider
{
    private readonly ChatClient _client;
    private readonly string _model;

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

        _client = new ChatClient(model, new ApiKeyCredential(apiKey));
    }

    public async Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken ct = default)
    {
        var messages = ConvertMessages(request.Messages);
        var options = BuildOptions(request.Options);

        var completion = await _client.CompleteChatAsync(messages, options, ct);
        return ConvertResponse(completion.Value);
    }

    public async IAsyncEnumerable<LlmStreamChunk> StreamAsync(
        LlmRequest request,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
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
}
