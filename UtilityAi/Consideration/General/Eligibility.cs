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
        return !stack.Contains(id);
    }

    public string Name { get; } = "NoRepeatEligible";
}