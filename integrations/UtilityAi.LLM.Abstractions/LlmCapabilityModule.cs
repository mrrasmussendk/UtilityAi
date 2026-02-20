using UtilityAi.Capabilities;
using UtilityAi.Consideration;
using UtilityAi.Consideration.General;
using UtilityAi.Orchestration;
using UtilityAi.Utils;

namespace UtilityAi.LLM.Abstractions;

/// <summary>
/// Base class for creating LLM-powered capability modules.
/// Handles common patterns like conversation history building and error handling.
/// </summary>
public abstract class LlmCapabilityModule : ICapabilityModule
{
    protected readonly ILlmProvider Provider;
    protected readonly LlmModuleConfiguration Configuration;

    protected LlmCapabilityModule(ILlmProvider provider, LlmModuleConfiguration? configuration = null)
    {
        Provider = provider ?? throw new ArgumentNullException(nameof(provider));
        Configuration = configuration ?? new LlmModuleConfiguration();
    }

    public abstract IEnumerable<Proposal> Propose(Runtime rt);

    public abstract IEnumerable<ProposalDefinition> GetProposalDefinitions();

    /// <summary>
    /// Builds conversation history from EventBus for a specific message type.
    /// </summary>
    protected List<LlmMessage> BuildConversationHistory<TMessage>(
        Runtime rt,
        Func<TMessage, string> messageSelector,
        Func<TMessage, LlmRole> roleSelector,
        int maxMessages = 10)
        where TMessage : notnull
    {
        var history = rt.Bus.GetHistory<TMessage>(maxItems: maxMessages);
        return history
            .Select(e => new LlmMessage(roleSelector(e.Value), messageSelector(e.Value)))
            .ToList();
    }

    /// <summary>
    /// Creates a proposal that calls the LLM with the given configuration.
    /// </summary>
    protected Proposal CreateLlmProposal(
        string proposalId,
        Runtime rt,
        Func<Runtime, List<LlmMessage>> messagesBuilder,
        LlmOptions? options = null,
        params IConsideration[] considerations)
    {
        var builder = ProposalHelper.For(proposalId);

        // Add considerations one by one
        foreach (var consideration in considerations)
        {
            builder = builder.WithConsideration(consideration);
        }

        return builder
            .WithAction(async ct =>
            {
                try
                {
                    var messages = messagesBuilder(rt);
                    var request = new LlmRequest(messages, options ?? Configuration.DefaultOptions);

                    LlmResponse response;
                    if (Configuration.EnableRetry)
                    {
                        response = await RetryAsync(
                            () => Provider.CompleteAsync(request, ct),
                            Configuration.MaxRetries,
                            Configuration.RetryDelayMs,
                            ct);
                    }
                    else
                    {
                        response = await Provider.CompleteAsync(request, ct);
                    }

                    // Invoke user-defined response handler
                    await Configuration.OnResponseReceived?.Invoke(rt, response, ct)!;
                }
                catch (Exception ex)
                {
                    // Invoke user-defined error handler
                    Configuration.OnError?.Invoke(rt, ex);

                    // Re-throw if user didn't handle it
                    if (!Configuration.SuppressErrors)
                        throw;
                }
            })
            .Build();
    }

    private static async Task<T> RetryAsync<T>(
        Func<Task<T>> operation,
        int maxRetries,
        int delayMs,
        CancellationToken ct)
    {
        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                return await operation();
            }
            catch when (i < maxRetries - 1)
            {
                await Task.Delay(delayMs * (i + 1), ct); // Exponential backoff
            }
        }

        return await operation(); // Last attempt without catch
    }
}

/// <summary>
/// Configuration for LLM capability modules.
/// </summary>
public record LlmModuleConfiguration(
    LlmOptions? DefaultOptions = null,
    bool EnableRetry = true,
    int MaxRetries = 3,
    int RetryDelayMs = 1000,
    bool SuppressErrors = false,
    Func<Runtime, LlmResponse, CancellationToken, Task>? OnResponseReceived = null,
    Action<Runtime, Exception>? OnError = null);
