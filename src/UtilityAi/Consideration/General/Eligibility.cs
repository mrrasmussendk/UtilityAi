using UtilityAi.Sensor.LLM;
using UtilityAi.Utils;

namespace UtilityAi.Consideration.General;

public sealed class HasFactEligible<T> : IEligibility
{
    public string Name => $"HasFactEligible<{typeof(T).Name}>";
    public bool IsEligible(Runtime rt) => rt.Bus.TryGet<T>(out _);
}

public sealed class NotHasFactEligible<T> : IEligibility
{
    public string Name => $"NotHasFactEligible<{typeof(T).Name}>";
    public bool IsEligible(Runtime rt) => !rt.Bus.TryGet<T>(out _);
}

public sealed class NoRepeatEligible(string id) : IEligibility
{
    public bool IsEligible(Runtime rt)
    {
        rt.Bus.TryGet<Stack<string>>(out var stack);
        return stack == null || !stack.Contains(id);
    }

    public string Name { get; } = "NoRepeatEligible";
}

/// <summary>
/// Eligibility that checks if all required intent parameters exist in IntentAnalysis.
/// Use this to ensure proposals with UsesIntentParameter/ScoreByIntentParameter
/// are only eligible when those parameters are actually present.
/// </summary>
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

        if (intent.Parameters == null)
            return false;

        // All required parameters must exist in the Parameters dictionary
        return _requiredParameters.All(param => intent.Parameters.ContainsKey(param));
    }
}