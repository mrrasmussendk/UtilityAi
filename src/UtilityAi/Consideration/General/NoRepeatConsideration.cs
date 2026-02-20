using UtilityAi.Utils;

namespace UtilityAi.Consideration;

/// <summary>
/// A consideration that penalizes or blocks an action if it has been executed recently.
/// Essential for chat agents to prevent repetitive loops (e.g. asking the same question twice).
/// </summary>
public sealed class NoRepeatConsideration : IConsideration
{
    private readonly string _proposalId;
    private readonly int _lookback;
    private readonly double _penalty;

    /// <summary>
    /// Creates a new NoRepeatConsideration.
    /// </summary>
    /// <param name="proposalId">The ID of the proposal to check in the execution history.</param>
    /// <param name="lookback">How many recent ticks to check. Defaults to 5.</param>
    /// <param name="penalty">The score to return if the action was found. Defaults to 0.0 (total block).</param>
    public NoRepeatConsideration(string proposalId, int lookback = 5, double penalty = 0.0)
    {
        _proposalId = proposalId;
        _lookback = lookback;
        _penalty = penalty;
    }

    public string Name => $"NoRepeat({_proposalId})";

    public double Evaluate(Runtime rt)
    {
        // Retrieve the standardized execution history from the EventBus.
        // The orchestrator publishes this as an IReadOnlyList<string> at the end of each tick.
        if (rt.Bus.TryGet<IReadOnlyList<string>>(out var historyList) && historyList is not null)
        {
            // Check the most recent N ticks (defined by _lookback).
            // historyList[0] is the most recent execution.
            if (historyList.Take(_lookback).Any(id => id == _proposalId))
            {
                return _penalty;
            }
        }

        return 1.0; 
    }
}
