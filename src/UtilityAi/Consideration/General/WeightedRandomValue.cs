using System.Globalization;
using UtilityAi.Utils;

namespace UtilityAi.Consideration.General;

/// <summary>
/// Returns a weighted random value that combines deterministic score with randomness.
/// Formula: (weight * score) + ((1 - weight) * random)
/// Weight of 1.0 = fully deterministic, 0.0 = fully random, 0.5 = balanced.
/// </summary>
/// <typeparam name="T">The type of fact containing the base score.</typeparam>
public sealed class WeightedRandomValue<T> : IConsideration where T : class
{
    private readonly Func<T, double> _scoreSelector;
    private readonly double _deterministicWeight;
    private static readonly Random _random = new();
    private static readonly object _lock = new();

    /// <summary>
    /// Creates a weighted random consideration.
    /// </summary>
    /// <param name="scoreSelector">Function to extract the base score (0.0 to 1.0) from the fact.</param>
    /// <param name="deterministicWeight">Weight for deterministic vs random (0.0 to 1.0). Default is 0.5.</param>
    public WeightedRandomValue(Func<T, double> scoreSelector, double deterministicWeight = 0.5)
    {
        _scoreSelector = scoreSelector ?? throw new ArgumentNullException(nameof(scoreSelector));
        _deterministicWeight = Math.Clamp(deterministicWeight, 0.0, 1.0);
    }

    public string Name => $"WeightedRandom<{typeof(T).Name}>({_deterministicWeight.ToString("F2", CultureInfo.InvariantCulture)})";

    public double Evaluate(Runtime rt)
    {
        var fact = rt.Bus.GetOrDefault<T>();
        if (fact == null) return 0.0;

        var score = Math.Clamp(_scoreSelector(fact), 0.0, 1.0);

        double randomValue;
        lock (_lock)
        {
            randomValue = _random.NextDouble();
        }

        return (_deterministicWeight * score) + ((1.0 - _deterministicWeight) * randomValue);
    }
}
