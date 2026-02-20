using System.Reflection;
using UtilityAi.Consideration;
using UtilityAi.Orchestration;
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
    private static readonly MethodInfo TryGetMethodDefinition = typeof(EventBus).GetMethod(nameof(EventBus.TryGet))
        ?? throw new InvalidOperationException("Unable to locate EventBus.TryGet method.");

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
                if (!TryHasFact(rt.Bus, factType))
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
            return TryHasFact(rt.Bus, condition.FactType);
        }


        return true; // No condition specified, always active
    }

    private static bool TryHasFact(EventBus bus, Type factType)
    {
        var tryGetMethod = TryGetMethodDefinition.MakeGenericMethod(factType);
        var parameters = new object?[] { null };
        var result = tryGetMethod.Invoke(bus, parameters);
        return result is bool hasFact && hasFact;
    }
}
