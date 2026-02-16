using UtilityAi.Consideration;
using UtilityAi.Utils;

namespace Example.TaskManagement.Considerations;

/// <summary>
/// Simple consideration that returns a fixed value.
/// </summary>
public sealed class FixedValue(string name, double value) : IConsideration
{
    public string Name => name;
    public double Evaluate(Runtime rt) => Math.Clamp(value, 0, 1);
}

/// <summary>
/// Consideration that applies a curve to an input value.
/// </summary>
public sealed class CurveValue(string name, Func<double> getValue, Func<double, double> curve) : IConsideration
{
    public string Name => name;
    public double Evaluate(Runtime rt) => Math.Clamp(curve(Math.Clamp(getValue(), 0, 1)), 0, 1);
}

/// <summary>
/// Consideration that checks a boolean condition.
/// </summary>
public sealed class BooleanCheck(string name, Func<bool> predicate, double trueValue = 1.0, double falseValue = 0.0) : IConsideration
{
    public string Name => name;
    public double Evaluate(Runtime rt) => predicate() ? trueValue : falseValue;
}
