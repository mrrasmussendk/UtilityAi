using UtilityAi.Utils;

namespace UtilityAi.Consideration.General
{
    public sealed class HasFact<T> : IConsideration
    {
        public string Name => $"has:{typeof(T).Name}";
        private readonly bool _shouldHave; // true => prefer when fact exists, false => prefer when not exists
        public HasFact(bool shouldHave) => _shouldHave = shouldHave;
        public double Evaluate(Runtime rt) => (rt.Bus.TryGet<T>(out _)) == _shouldHave ? 1.0 : 0.0;
    }
}