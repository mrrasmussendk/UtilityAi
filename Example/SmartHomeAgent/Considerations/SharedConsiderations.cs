using UtilityAi.Consideration;
using UtilityAi.Utils;

namespace Example.SmartHomeAgent.Considerations;

/// <summary>
/// Consideration that reads a signal from EventBus and applies a response curve.
/// </summary>
public sealed class SignalConsideration<T>(
    string name,
    Func<T, double> selector,
    Func<double, double> curve,
    (double min, double max) inputDomain) : IConsideration where T : notnull
{
    public string Name => name;

    public double Evaluate(Runtime rt)
    {
        var fact = rt.Bus.GetOrDefault<T>();
        if (fact == null) return 0.0;

        var rawValue = selector(fact);
        var normalized = (rawValue - inputDomain.min) / (inputDomain.max - inputDomain.min);
        var clamped = Math.Clamp(normalized, 0.0, 1.0);
        return curve(clamped);
    }
}

/// <summary>
/// Consideration that checks if a fact exists on EventBus and optionally validates it.
/// </summary>
public sealed class HasFact<T>(string name, Func<T, bool>? selector = null) : IConsideration where T : notnull
{
    public string Name => name;

    public double Evaluate(Runtime rt)
    {
        var fact = rt.Bus.GetOrDefault<T>();
        if (fact == null) return selector != null && !selector(default(T)!) ? 1.0 : 0.0;
        return selector == null || selector(fact) ? 1.0 : 0.0;
    }
}

/// <summary>
/// Consideration that returns a fixed constant value.
/// </summary>
public sealed class ConstantValue(string name, double value) : IConsideration
{
    public string Name => name;
    public double Evaluate(Runtime rt) => Math.Clamp(value, 0.0, 1.0);
}
