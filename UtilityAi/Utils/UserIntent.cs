namespace UtilityAi.Utils;

public sealed record IntentGoal(string Name);

public sealed record IntentConstraints(
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    int? MaxItems = null
);

public sealed record UserIntent(
    IntentGoal Goal,
    IReadOnlyDictionary<string, object?>? Slots = null,
    IntentConstraints? Constraints = null,
    string? RequestId = null,
    string? Locale = null
)
{
    // Legacy convenience: treat string as a 'query' slot
    public UserIntent(string query)
        : this(new IntentGoal("unspecified"),
            Slots: new Dictionary<string, object?> { ["query"] = query }) { }

    // Legacy compatibility: (query, delivery, topic) captured into generic slots
    public UserIntent(string query, string delivery, string topic)
        : this(new IntentGoal("legacy"),
            Slots: new Dictionary<string, object?>
            {
                ["query"] = query,
                ["delivery"] = delivery,
                ["topic"] = topic
            }) { }
}
