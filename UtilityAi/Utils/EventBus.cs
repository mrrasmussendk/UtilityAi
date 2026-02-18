namespace UtilityAi.Utils;

/// <summary>
/// A type-safe publish/subscribe blackboard for sharing facts between sensors, modules, and actions.
/// Supports latest-value retrieval, event history, subscriptions, and scoped buses.
/// </summary>
/// <remarks>
/// The EventBus serves as the central state container in the orchestration loop.
/// It stores facts published by sensors and actions, which are then evaluated by considerations
/// during proposal scoring. Thread-safe for concurrent publish/subscribe operations.
/// </remarks>
public sealed class EventBus : IDisposable
{
    private readonly Dictionary<Type, object?> _latest = new();
    private readonly Dictionary<Type, List<TimestampedEvent>> _history = new();
    private readonly Dictionary<Type, List<Delegate>> _subscriptions = new();
    private readonly object _lock = new();
    private readonly EventBus? _parent;
    private readonly string? _scopeId;
    private readonly int _maxHistoryPerType;
    private bool _disposed;

    /// <summary>
    /// Creates a new root EventBus instance.
    /// </summary>
    /// <param name="maxHistoryPerType">Maximum number of historical events to retain per type. Default is 100.</param>
    public EventBus(int maxHistoryPerType = 100)
    {
        _maxHistoryPerType = maxHistoryPerType;
    }

    private EventBus(EventBus parent, string scopeId, int maxHistoryPerType)
    {
        _parent = parent;
        _scopeId = scopeId;
        _maxHistoryPerType = maxHistoryPerType;
    }

    /// <summary>
    /// Gets the scope identifier for this EventBus, or null if this is a root bus.
    /// </summary>
    public string? ScopeId => _scopeId;

    /// <summary>
    /// Gets the parent EventBus if this is a scoped bus, or null if this is a root bus.
    /// </summary>
    public EventBus? Parent => _parent;

    /// <summary>
    /// Publishes a fact to the bus, replacing any previous value of the same type.
    /// Notifies all subscribers and stores the event in history.
    /// </summary>
    /// <typeparam name="T">The type of fact to publish.</typeparam>
    /// <param name="msg">The fact instance to publish.</param>
    /// <exception cref="ObjectDisposedException">Thrown if the EventBus has been disposed.</exception>
    public void Publish<T>(T msg)
    {
        ThrowIfDisposed();

        lock (_lock)
        {
            var type = typeof(T);
            _latest[type] = msg;

            // Store in history
            if (!_history.ContainsKey(type))
                _history[type] = new List<TimestampedEvent>();

            _history[type].Add(new TimestampedEvent(msg!, DateTimeOffset.UtcNow));

            // Trim history if needed
            if (_history[type].Count > _maxHistoryPerType)
                _history[type].RemoveAt(0);

            // Notify subscribers
            if (_subscriptions.TryGetValue(type, out var handlers))
            {
                foreach (var handler in handlers.ToList()) // ToList to avoid modification during iteration
                {
                    try
                    {
                        (handler as Action<T>)?.Invoke(msg);
                    }
                    catch (Exception)
                    {
                        // Swallow subscriber exceptions to prevent cascading failures
                        // Note: All exceptions are caught to ensure stability
                    }
                }
            }
        }
    }

    /// <summary>
    /// Attempts to retrieve the latest published fact of the specified type.
    /// </summary>
    /// <typeparam name="T">The type of fact to retrieve.</typeparam>
    /// <param name="value">When this method returns, contains the latest fact if found; otherwise, the default value.</param>
    /// <returns>True if a fact of the specified type was found; otherwise, false.</returns>
    /// <exception cref="ObjectDisposedException">Thrown if the EventBus has been disposed.</exception>
    public bool TryGet<T>(out T value)
    {
        ThrowIfDisposed();

        lock (_lock)
        {
            if (_latest.TryGetValue(typeof(T), out var v) && v is T t)
            {
                value = t;
                return true;
            }
        }

        value = default!;
        return false;
    }

    /// <summary>
    /// Gets the latest published fact of the specified type, or the default value if not found.
    /// </summary>
    /// <typeparam name="T">The type of fact to retrieve.</typeparam>
    /// <returns>The latest fact of the specified type, or default(T) if not found.</returns>
    /// <exception cref="ObjectDisposedException">Thrown if the EventBus has been disposed.</exception>
    public T? GetOrDefault<T>()
    {
        ThrowIfDisposed();

        lock (_lock)
        {
            return _latest.TryGetValue(typeof(T), out var v) ? (T?)v : default;
        }
    }

    /// <summary>
    /// Retrieves the event history for the specified type, ordered from oldest to newest.
    /// </summary>
    /// <typeparam name="T">The type of events to retrieve.</typeparam>
    /// <param name="maxItems">Maximum number of historical events to return. If null or greater than available, returns all.</param>
    /// <returns>A read-only list of timestamped events of the specified type.</returns>
    /// <exception cref="ObjectDisposedException">Thrown if the EventBus has been disposed.</exception>
    public IReadOnlyList<TimestampedEvent<T>> GetHistory<T>(int? maxItems = null)
    {
        ThrowIfDisposed();

        lock (_lock)
        {
            if (!_history.TryGetValue(typeof(T), out var events))
                return Array.Empty<TimestampedEvent<T>>();

            var typed = events
                .Where(e => e.Value is T)
                .Select(e => new TimestampedEvent<T>((T)e.Value, e.Timestamp))
                .ToList();

            if (maxItems.HasValue && maxItems.Value < typed.Count)
                return typed.Skip(typed.Count - maxItems.Value).ToList();

            return typed;
        }
    }

    /// <summary>
    /// Subscribes to events of the specified type. The handler will be invoked whenever a fact of type T is published.
    /// </summary>
    /// <typeparam name="T">The type of events to subscribe to.</typeparam>
    /// <param name="handler">The callback to invoke when events are published.</param>
    /// <returns>An IDisposable that, when disposed, unsubscribes the handler.</returns>
    /// <exception cref="ArgumentNullException">Thrown if handler is null.</exception>
    /// <exception cref="ObjectDisposedException">Thrown if the EventBus has been disposed.</exception>
    public IDisposable Subscribe<T>(Action<T> handler)
    {
        if (handler == null) throw new ArgumentNullException(nameof(handler));
        ThrowIfDisposed();

        lock (_lock)
        {
            var type = typeof(T);
            if (!_subscriptions.ContainsKey(type))
                _subscriptions[type] = new List<Delegate>();

            _subscriptions[type].Add(handler);
        }

        return new Subscription<T>(this, handler);
    }

    /// <summary>
    /// Creates a scoped child EventBus that inherits read access from this parent but maintains isolated write state.
    /// Useful for per-agent, per-conversation, or per-module state isolation.
    /// </summary>
    /// <param name="scopeId">A unique identifier for this scope (e.g., "agent-1", "conversation-abc").</param>
    /// <param name="maxHistoryPerType">Maximum history items to retain in the scoped bus. Defaults to parent's setting.</param>
    /// <returns>A new scoped EventBus instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown if scopeId is null or whitespace.</exception>
    /// <exception cref="ObjectDisposedException">Thrown if the EventBus has been disposed.</exception>
    public EventBus CreateScope(string scopeId, int? maxHistoryPerType = null)
    {
        if (string.IsNullOrWhiteSpace(scopeId))
            throw new ArgumentNullException(nameof(scopeId));

        ThrowIfDisposed();

        return new EventBus(this, scopeId, maxHistoryPerType ?? _maxHistoryPerType);
    }

    /// <summary>
    /// Attempts to retrieve the latest fact from this scope first, falling back to parent scopes if not found.
    /// </summary>
    /// <typeparam name="T">The type of fact to retrieve.</typeparam>
    /// <param name="value">When this method returns, contains the latest fact if found; otherwise, the default value.</param>
    /// <returns>True if a fact was found in this scope or any parent scope; otherwise, false.</returns>
    /// <exception cref="ObjectDisposedException">Thrown if the EventBus has been disposed.</exception>
    public bool TryGetWithFallback<T>(out T value)
    {
        ThrowIfDisposed();

        // Try local scope first
        if (TryGet<T>(out value))
            return true;

        // Fall back to parent
        if (_parent != null)
            return _parent.TryGetWithFallback(out value);

        value = default!;
        return false;
    }

    /// <summary>
    /// Clears all stored facts and history for the specified type.
    /// </summary>
    /// <typeparam name="T">The type to clear.</typeparam>
    /// <exception cref="ObjectDisposedException">Thrown if the EventBus has been disposed.</exception>
    public void Clear<T>()
    {
        ThrowIfDisposed();

        lock (_lock)
        {
            var type = typeof(T);
            _latest.Remove(type);
            _history.Remove(type);
        }
    }

    /// <summary>
    /// Clears all stored facts and history from the bus.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown if the EventBus has been disposed.</exception>
    public void ClearAll()
    {
        ThrowIfDisposed();

        lock (_lock)
        {
            _latest.Clear();
            _history.Clear();
        }
    }

    private void Unsubscribe<T>(Action<T> handler)
    {
        lock (_lock)
        {
            var type = typeof(T);
            if (_subscriptions.TryGetValue(type, out var handlers))
            {
                handlers.Remove(handler);
                if (handlers.Count == 0)
                    _subscriptions.Remove(type);
            }
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(EventBus));
    }

    /// <summary>
    /// Disposes the EventBus, clearing all subscriptions and releasing resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        lock (_lock)
        {
            _subscriptions.Clear();
            _disposed = true;
        }
    }

    private sealed class Subscription<T> : IDisposable
    {
        private readonly EventBus _bus;
        private readonly Action<T> _handler;
        private bool _disposed;

        public Subscription(EventBus bus, Action<T> handler)
        {
            _bus = bus;
            _handler = handler;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _bus.Unsubscribe(_handler);
            _disposed = true;
        }
    }

    private record TimestampedEvent(object Value, DateTimeOffset Timestamp);
}

/// <summary>
/// Represents a timestamped event in the EventBus history.
/// </summary>
/// <typeparam name="T">The type of the event value.</typeparam>
public sealed record TimestampedEvent<T>(T Value, DateTimeOffset Timestamp);
