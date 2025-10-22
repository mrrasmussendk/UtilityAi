namespace UtilityAi.Actions;

public interface IAction<in TReq, TRes>
{
    Task<TRes> ActAsync(TReq request, CancellationToken ct);
}