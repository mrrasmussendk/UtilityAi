namespace UtilityAi.Utils;

public sealed record IntentGoal(string Name);

public sealed record UserIntent(
    IntentGoal Goal,
    IReadOnlyDictionary<string, object?>? Slots = null,
    string? RequestId = null,
    string? Locale = null
)
{
    /// <summary>
    /// Creates a user intent with a goal name directly, without requiring <see cref="IntentGoal"/> construction.
    /// </summary>
    public static UserIntent ForGoal(
        string goalName,
        IReadOnlyDictionary<string, object?>? slots = null,
        string? requestId = null,
        string? locale = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(goalName);
        return new UserIntent(new IntentGoal(goalName), slots, requestId, locale);
    }

    /// <summary>
    /// Creates an intent from free-text user input and stores it in the <c>query</c> slot.
    /// </summary>
    public static UserIntent FromQuery(
        string query,
        string goalName = "unspecified",
        string? requestId = null,
        string? locale = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        return new UserIntent(
            new IntentGoal(goalName),
            new Dictionary<string, object?> { ["query"] = query },
            requestId,
            locale);
    }

    /// <summary>
    /// Gets the canonical text query if present.
    /// </summary>
    public string? Query => GetSlotOrDefault<string>("query");

    /// <summary>
    /// Attempts to read a typed value from the slot bag.
    /// Returns <see langword="false"/> when the slot is missing or cannot be cast to <typeparamref name="T"/>.
    /// </summary>
    public bool TryGetSlot<T>(string slotName, out T? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slotName);

        if (Slots?.TryGetValue(slotName, out var raw) == true && raw is T typedValue)
        {
            value = typedValue;
            return true;
        }

        value = default;
        return false;
    }

    /// <summary>
    /// Reads a typed value from the slot bag, returning default when absent or incompatible.
    /// </summary>
    public T? GetSlotOrDefault<T>(string slotName)
        => TryGetSlot<T>(slotName, out var value) ? value : default;

    // Legacy convenience: treat string as a 'query' slot
    public UserIntent(string query)
        : this(new IntentGoal("unspecified"),
            Slots: new Dictionary<string, object?> {["query"] = query})
    {
    }

    // Legacy compatibility: (query, delivery, topic) captured into generic slots
    public UserIntent(string query, string delivery, string topic)
        : this(new IntentGoal("legacy"),
            Slots: new Dictionary<string, object?>
            {
                ["query"] = query,
                ["delivery"] = delivery,
                ["topic"] = topic
            })
    {
    }
}
