using UtilityAi.Utils;

namespace UtilityAi.Sensor;

public interface ISensor { Task SenseAsync(IBlackboard bb, CancellationToken ct); }
