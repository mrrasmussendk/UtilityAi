using UtilityAi.Sensor.LLM;
using UtilityAi.Utils;

namespace UtilityAi.Consideration.General;

/// <summary>
/// Eligibility that requires a specific fact to exist on the EventBus.
/// Use this for hard requirements where a proposal should not even be considered
/// if the fact is missing.
/// </summary>
/// <typeparam name="T">The type of fact that must exist.</typeparam>
/// <remarks>
/// This is a hard gate - if the fact doesn't exist, the proposal is completely excluded
/// from consideration (not scored at all). Compare this to <see cref="HasFact{T}"/> which
/// is a consideration that returns 0.0 when the fact is missing but still allows the
/// proposal to be scored.
///
/// Use eligibility when:
/// - The proposal cannot function without this fact
/// - You want to avoid wasting CPU cycles scoring impossible proposals
///
/// Use consideration when:
/// - Missing the fact should just lower the score
/// - Other considerations might still make the proposal viable
/// </remarks>
/// <example>
/// <code>
/// // Only propose "respond" action when UserMessage exists
/// yield return ProposalHelper.For("respond")
///     .WithEligibility(new HasFactEligible&lt;UserMessage&gt;())
///     .WithAction(async ct => await Respond(rt.Bus.GetOrDefault&lt;UserMessage&gt;()!, ct));
/// </code>
/// </example>
public sealed class HasFactEligible<T> : IEligibility
{
    public string Name => $"HasFactEligible<{typeof(T).Name}>";
    public bool IsEligible(Runtime rt) => rt.Bus.TryGet<T>(out _);
}

/// <summary>
/// Eligibility that requires a specific fact to NOT exist on the EventBus.
/// Use this for proposals that should only be considered when a fact is absent.
/// </summary>
/// <typeparam name="T">The type of fact that must NOT exist.</typeparam>
/// <remarks>
/// This is the inverse of <see cref="HasFactEligible{T}"/> - the proposal is only
/// eligible when the specified fact is missing from the EventBus.
///
/// Common use cases:
/// - Initialization actions that should only run before a fact exists
/// - Fallback actions when primary data is unavailable
/// - Cleanup actions after a fact has been cleared
/// </remarks>
/// <example>
/// <code>
/// // Only propose "initialize" when config doesn't exist yet
/// yield return ProposalHelper.For("initialize")
///     .WithEligibility(new NotHasFactEligible&lt;AppConfig&gt;())
///     .WithAction(async ct => await Initialize(rt, ct));
/// </code>
/// </example>
public sealed class NotHasFactEligible<T> : IEligibility
{
    public string Name => $"NotHasFactEligible<{typeof(T).Name}>";
    public bool IsEligible(Runtime rt) => !rt.Bus.TryGet<T>(out _);
}

/// <summary>
/// Eligibility that prevents a proposal from being executed if it was recently executed.
/// Essential for preventing repetitive loops in conversational agents.
/// </summary>
/// <remarks>
/// Checks the execution history published by the orchestrator to determine if this
/// proposal has already been executed. The history is maintained as a list of proposal IDs,
/// with the most recent execution at index 0.
///
/// This is particularly useful for chat agents to prevent:
/// - Asking the same question twice in a row
/// - Repeating the same response
/// - Getting stuck in action loops
///
/// The orchestrator automatically publishes <see cref="IReadOnlyList{T}"/> of <see cref="string"/>
/// containing executed proposal IDs to the EventBus after each tick.
/// </remarks>
/// <example>
/// <code>
/// // Prevent asking for clarification twice in a row
/// yield return ProposalHelper.For("ask-clarification")
///     .WithEligibility(new NoRepeatEligible("ask-clarification"))
///     .WithAction(async ct => await AskForClarification(ct));
/// </code>
/// </example>
/// <param name="id">The proposal ID to check against execution history.</param>
public sealed class NoRepeatEligible(string id) : IEligibility
{
    public bool IsEligible(Runtime rt)
    {
        if (rt.Bus.TryGet<IReadOnlyList<string>>(out var history) && history is not null)
        {
            return !history.Contains(id);
        }
        return true;
    }

    public string Name { get; } = "NoRepeatEligible";
}

/// <summary>
/// Eligibility that checks if all required intent parameters exist in IntentAnalysis.
/// Use this to ensure proposals with UsesIntentParameter/ScoreByIntentParameter
/// are only eligible when those parameters are actually present.
/// </summary>
/// <remarks>
/// This eligibility is designed for use with LLM-based intent analysis. It checks that
/// the <see cref="IntentAnalysis"/> fact on the EventBus contains all required parameter
/// names in its Entities dictionary.
///
/// This prevents proposals from being scored when the LLM hasn't extracted the necessary
/// parameters from user input, ensuring that parameter-dependent proposals only execute
/// when all required data is available.
///
/// The check is performed against the <c>Entities</c> dictionary in <see cref="IntentAnalysis"/>,
/// which contains extracted named entities like "task_name", "priority", "due_date", etc.
/// </remarks>
/// <example>
/// <code>
/// // Only eligible when LLM has extracted both "task_name" and "priority" parameters
/// var requiredParams = new[] { "task_name", "priority" };
/// yield return ProposalHelper.For("create-task")
///     .WithEligibility(new HasIntentParametersEligible(requiredParams))
///     .WithAction(async ct => await CreateTask(
///         intent.Entities["task_name"],
///         intent.Entities["priority"],
///         ct));
/// </code>
/// </example>
/// <param name="requiredParameters">List of parameter names that must exist in IntentAnalysis.Entities.</param>
public sealed class HasIntentParametersEligible : IEligibility
{
    private readonly IReadOnlyList<string> _requiredParameters;

    public HasIntentParametersEligible(IReadOnlyList<string> requiredParameters)
    {
        _requiredParameters = requiredParameters ?? throw new ArgumentNullException(nameof(requiredParameters));
    }

    public string Name => "HasIntentParametersEligible";

    public bool IsEligible(Runtime rt)
    {
        if (_requiredParameters.Count == 0)
            return true;

        if (!rt.Bus.TryGet<IntentAnalysis>(out var intent))
            return false;

        if (intent.Entities == null)
            return false;

        // All required parameters must exist in the Entities dictionary
        return _requiredParameters.All(param => intent.Entities.ContainsKey(param));
    }
}