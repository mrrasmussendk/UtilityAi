using UtilityAi.Actions;

namespace UtilityAi.Utils;

public readonly record struct ScorePart(string Name, double Value);
public readonly record struct ScoredDecision(IAction Action, double Utility, IReadOnlyList<ScorePart> Parts);
