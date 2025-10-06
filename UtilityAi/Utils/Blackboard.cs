namespace UtilityAi.Utils;

public class Blackboard : IBlackboard
{

    private readonly Dictionary<string, object> _map = new(StringComparer.OrdinalIgnoreCase);
    public T GetOr<T>(string key, T fallback) => _map.TryGetValue(key, out var v) && v is T t ? t : fallback;
    public void Set<T>(string key, T value) => _map[key] = value!;
    public bool Has(string key) => _map.ContainsKey(key);
    public IReadOnlyDictionary<string, object> Snapshot() => _map;
}