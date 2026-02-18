using UtilityAi.Utils;

namespace UtilityAi.Consideration.General;

/// <summary>
/// Evaluates whether all items in a collection from a fact match a predicate.
/// Returns 1.0 if all match (or collection is empty), 0.0 otherwise.
/// </summary>
/// <typeparam name="TFact">The type of fact containing the collection.</typeparam>
/// <typeparam name="TItem">The type of items in the collection.</typeparam>
public sealed class AllMatch<TFact, TItem> : IConsideration where TFact : class
{
    private readonly Func<TFact, IEnumerable<TItem>> _collectionSelector;
    private readonly Func<TItem, bool> _predicate;

    /// <summary>
    /// Creates an all-match consideration.
    /// </summary>
    /// <param name="collectionSelector">Function to extract the collection from the fact.</param>
    /// <param name="predicate">Predicate to test each item against.</param>
    public AllMatch(Func<TFact, IEnumerable<TItem>> collectionSelector, Func<TItem, bool> predicate)
    {
        _collectionSelector = collectionSelector ?? throw new ArgumentNullException(nameof(collectionSelector));
        _predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
    }

    public string Name => $"AllMatch<{typeof(TFact).Name}, {typeof(TItem).Name}>";

    public double Evaluate(Runtime rt)
    {
        var fact = rt.Bus.GetOrDefault<TFact>();
        if (fact == null) return 0.0;

        var collection = _collectionSelector(fact);
        return collection.All(_predicate) ? 1.0 : 0.0;
    }
}
