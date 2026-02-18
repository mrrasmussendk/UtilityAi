using UtilityAi.Utils;

namespace UtilityAi.Consideration;

public interface IEligibility
{
    // Return true if the proposal should be considered at all for this tick.
    bool IsEligible(Runtime rt);
    string Name { get; }
}
