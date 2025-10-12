using UtilityAi.Utils;

namespace UtilityAi.Sensor;
public interface ISensor
{
    Task SenseAsync(Runtime rt, CancellationToken ct);
}
