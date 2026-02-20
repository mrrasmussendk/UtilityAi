using UtilityAi.Evaluators;
using UtilityAi.Utils;

namespace UtilityAi.Consideration.General;

/// <summary>
/// Evaluates the size of a collection from a fact through a response curve.
/// Useful for prioritizing work based on queue length, task count, etc.
/// </summary>
/// <typeparam name="T">The type of fact containing the collection.</typeparam>
public sealed class CollectionSize<T> : IConsideration where T : class
{
    private readonly Func<T, int> _sizeSelector;
    private readonly ICurve _curve;
    private readonly (int min, int max) _inputDomain;

    /// <summary>
    /// Creates a collection size consideration.
    /// </summary>
    /// <param name="sizeSelector">Function to extract the collection size from the fact.</param>
    /// <param name="curve">Response curve to map size to utility (0.0 to 1.0).</param>
    /// <param name="inputDomain">Expected range of collection sizes (min, max).</param>
    public CollectionSize(Func<T, int> sizeSelector, ICurve curve, (int min, int max) inputDomain)
    {
        _sizeSelector = sizeSelector ?? throw new ArgumentNullException(nameof(sizeSelector));
        _curve = curve ?? throw new ArgumentNullException(nameof(curve));

        if (inputDomain.max <= inputDomain.min)
            throw new ArgumentException("Input domain max must be greater than min.", nameof(inputDomain));

        _inputDomain = inputDomain;
    }

    public string Name => $"CollectionSize<{typeof(T).Name}>";

    public double Evaluate(Runtime rt)
    {
        var fact = rt.Bus.GetOrDefault<T>();
        if (fact == null) return 0.0;

        var size = _sizeSelector(fact);

        // Normalize size to 0-1 range
        var normalized = (double)(size - _inputDomain.min) / (_inputDomain.max - _inputDomain.min);
        normalized = Math.Clamp(normalized, 0.0, 1.0);

        return _curve.Evaluate(normalized);
    }
}
