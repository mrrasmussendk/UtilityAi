using UtilityAi.Utils;

namespace UtilityAi.Consideration.General;

/// <summary>
/// Returns a random value between 0.0 and 1.0.
/// Useful for exploration, randomness, or breaking ties.
/// </summary>
public sealed class RandomValue : IConsideration
{
    private static readonly Random _random = new();
    private static readonly object _lock = new();

    public string Name => "Random";

    public double Evaluate(Runtime rt)
    {
        lock (_lock)
        {
            return _random.NextDouble();
        }
    }
}
