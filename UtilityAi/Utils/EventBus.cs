namespace UtilityAi.Utils;

public sealed class EventBus
{
    private readonly Dictionary<Type, object?> _latest = new();

    public void Publish<T>(T msg) => _latest[typeof(T)] = msg!;

    public bool TryGet<T>(out T value)
    {
        if (_latest.TryGetValue(typeof(T), out var v) && v is T t) { value = t; return true; }
        value = default!; return false;
    }

    public T? GetOrDefault<T>() => _latest.TryGetValue(typeof(T), out var v) ? (T?)v : default;
}