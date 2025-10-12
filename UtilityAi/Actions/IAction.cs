using UtilityAi.Utils;

namespace UtilityAi.Actions;

public interface IAction<in TReq, TRes>
{
    Task<TRes> ActAsync(TReq request, IBlackboard? blackboard, CancellationToken ct);
}