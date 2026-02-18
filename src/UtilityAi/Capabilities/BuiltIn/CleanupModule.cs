using UtilityAi.Consideration;
using UtilityAi.Consideration.General;
using UtilityAi.Facts;
using UtilityAi.Utils;

namespace UtilityAi.Capabilities.BuiltIn;

/// <summary>
/// A module that periodically proposes cleanup actions to clear old facts from the EventBus.
/// Helps manage memory by removing stale data after a specified period.
/// </summary>
[Capability(Priority = -500, Domain = "maintenance")]
public sealed class CleanupModule : ICapabilityModule
{
    private readonly Type[] _typesToClean;
    private readonly TimeSpan _cleanupInterval;
    private readonly TimeSpan _cooldownPeriod;

    /// <summary>
    /// Creates a cleanup module.
    /// </summary>
    /// <param name="typesToClean">Types of facts to clean up periodically.</param>
    /// <param name="cleanupInterval">How often to propose cleanup. Default is 5 minutes.</param>
    /// <param name="cooldownPeriod">Minimum time between cleanup actions. Default is 1 minute.</param>
    public CleanupModule(
        Type[] typesToClean,
        TimeSpan? cleanupInterval = null,
        TimeSpan? cooldownPeriod = null)
    {
        _typesToClean = typesToClean ?? throw new ArgumentNullException(nameof(typesToClean));
        _cleanupInterval = cleanupInterval ?? TimeSpan.FromMinutes(5);
        _cooldownPeriod = cooldownPeriod ?? TimeSpan.FromMinutes(1);
    }

    public IEnumerable<Proposal> Propose(Runtime rt)
    {
        // Only propose cleanup if enough time has elapsed
        var timeSinceStart = rt.Bus.GetOrDefault<ElapsedTime>();
        if (timeSinceStart == null || timeSinceStart.Value < _cleanupInterval)
            yield break;

        yield return new Proposal(
            id: "cleanup.old-facts",
            cons: new IConsideration[]
            {
                new Cooldown<CleanupExecuted>(_cooldownPeriod),
                new ConstantValue(0.3) // Low priority
            },
            act: ct =>
            {
                // Clear specified types from EventBus
                foreach (var type in _typesToClean)
                {
                    var clearMethod = typeof(EventBus)
                        .GetMethod(nameof(EventBus.Clear))!
                        .MakeGenericMethod(type);

                    clearMethod.Invoke(rt.Bus, null);
                }

                // Record cleanup execution
                rt.Bus.Publish(new CleanupExecuted(DateTimeOffset.UtcNow));
                return Task.CompletedTask;
            }
        );
    }
}

/// <summary>
/// Fact indicating a cleanup was executed.
/// </summary>
public sealed record CleanupExecuted(DateTimeOffset Timestamp);
