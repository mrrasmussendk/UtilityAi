using UtilityAi.Utils;

namespace UtilityAi.Sensor.BuiltIn;

/// <summary>
/// Tracks the frequency of specific event types over a time window.
/// Publishes frequency metrics for rate limiting and throttling decisions.
/// </summary>
/// <typeparam name="TEvent">The type of event to track.</typeparam>
/// <typeparam name="TFrequencyFact">The type of fact to publish with frequency data.</typeparam>
public sealed class EventFrequencySensor<TEvent, TFrequencyFact> : ISensor
    where TEvent : class
    where TFrequencyFact : class
{
    private readonly TimeSpan _timeWindow;
    private readonly Func<int, double, TFrequencyFact> _factFactory;

    /// <summary>
    /// Creates an event frequency sensor.
    /// </summary>
    /// <param name="timeWindow">Time window to measure frequency over.</param>
    /// <param name="factFactory">Factory function to create frequency fact from (count, eventsPerSecond).</param>
    public EventFrequencySensor(
        TimeSpan timeWindow,
        Func<int, double, TFrequencyFact> factFactory)
    {
        if (timeWindow <= TimeSpan.Zero)
            throw new ArgumentException("Time window must be positive.", nameof(timeWindow));

        _timeWindow = timeWindow;
        _factFactory = factFactory ?? throw new ArgumentNullException(nameof(factFactory));
    }

    public Task SenseAsync(Runtime rt, CancellationToken ct)
    {
        var history = rt.Bus.GetHistory<TEvent>();
        var cutoffTime = DateTimeOffset.UtcNow - _timeWindow;

        var recentEvents = history
            .Where(e => e.Timestamp >= cutoffTime)
            .ToList();

        var count = recentEvents.Count;
        var eventsPerSecond = count / _timeWindow.TotalSeconds;

        var fact = _factFactory(count, eventsPerSecond);
        rt.Bus.Publish(fact);

        return Task.CompletedTask;
    }
}
