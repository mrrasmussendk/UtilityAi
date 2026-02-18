using UtilityAi.Utils;

namespace UtilityAi.Consideration;

public interface IConsideration
{
    // Return 0..1 utility contribution based on current runtime (signals/facts)
    double Evaluate(Runtime rt);
    string Name { get; }
}

