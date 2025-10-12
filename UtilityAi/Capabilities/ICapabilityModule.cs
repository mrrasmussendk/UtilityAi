using UtilityAi.Consideration;
using UtilityAi.Utils;

namespace UtilityAi.Capabilities;

public interface ICapabilityModule
{
    IEnumerable<Proposal> Propose(Runtime rt);
}