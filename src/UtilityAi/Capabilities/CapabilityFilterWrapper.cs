using System.Reflection;
using UtilityAi.Consideration;
using UtilityAi.Utils;

namespace UtilityAi.Capabilities;

/// <summary>
/// Wraps an ICapabilityModule to apply attribute-based filters and conditions.
/// </summary>
/// <remarks>
/// This wrapper checks [ActiveWhen] and [RequiresFact] attributes before delegating
/// to the underlying module's Propose method.
/// </remarks>
internal sealed class CapabilityFilterWrapper : ICapabilityModule
{
    private readonly ICapabilityModule _inner;
    private readonly ActiveWhenAttribute[]? _activeWhenAttrs;
    private readonly Type[]? _requiredFacts;

    public CapabilityFilterWrapper(ICapabilityModule inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));

        var type = inner.GetType();
        _activeWhenAttrs = type.GetCustomAttributes<ActiveWhenAttribute>().ToArray();

        // Extract required fact types from RequiresFactAttribute<T>
        _requiredFacts = type.GetCustomAttributes()
            .Where(attr => attr.GetType().IsGenericType &&
                          attr.GetType().GetGenericTypeDefinition() == typeof(RequiresFactAttribute<>))
            .Select(attr => attr.GetType().GetGenericArguments()[0])
            .ToArray();
    }

    public IEnumerable<Proposal> Propose(Runtime rt)
    {
        // Check ActiveWhen conditions
        if (_activeWhenAttrs is not null && _activeWhenAttrs.Length > 0)
        {
            foreach (var condition in _activeWhenAttrs)
            {
                if (!EvaluateCondition(condition, rt))
                {
                    yield break; // Condition not met, don't propose
                }
            }
        }

        // Check RequiresFact conditions
        if (_requiredFacts is not null)
        {
            foreach (var factType in _requiredFacts)
            {
                var tryGetMethod = typeof(EventBus).GetMethod(nameof(EventBus.TryGet))!
                    .MakeGenericMethod(factType);
                var parameters = new object?[] { null };
                var hasFact = (bool)tryGetMethod.Invoke(rt.Bus, parameters)!;

                if (!hasFact)
                {
                    yield break; // Required fact missing
                }
            }
        }

        // All conditions met, delegate to inner module
        foreach (var proposal in _inner.Propose(rt))
        {
            yield return proposal;
        }
    }

    private static bool EvaluateCondition(ActiveWhenAttribute condition, Runtime rt)
    {
        // Fact-based condition
        if (condition.FactType is not null)
        {
            var tryGetMethod = typeof(EventBus).GetMethod(nameof(EventBus.TryGet))!
                .MakeGenericMethod(condition.FactType);
            var parameters = new object?[] { null };
            return (bool)tryGetMethod.Invoke(rt.Bus, parameters)!;
        }

        // Intent slot condition
        if (condition.IntentSlot is not null && condition.AllowedValues is not null)
        {
            if (rt.Intent.Slots is null || !rt.Intent.Slots.TryGetValue(condition.IntentSlot, out var slotValue))
                return false;

            var slotStr = slotValue?.ToString() ?? "";
            return condition.AllowedValues.Contains(slotStr, StringComparer.OrdinalIgnoreCase);
        }

        return true; // No condition specified, always active
    }
}
