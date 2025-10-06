namespace UtilityAi.Utils;

public interface IBlackboard
{
    T GetOr<T>(string key, T fallback);
    void Set<T>(string key, T value);
    bool Has(string key);
    IReadOnlyDictionary<string, object> Snapshot();
}
