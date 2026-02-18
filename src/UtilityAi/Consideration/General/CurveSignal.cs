using UtilityAi.Utils;

namespace UtilityAi.Consideration.General;

public sealed class CurveSignal<TSignal> : IConsideration
{
    public string Name { get; }

    private readonly Func<TSignal, double> _project; // get 0..1 from signal
    private readonly Func<double, double> _curve;    // response curve
    private readonly double _defaultValue;

    public CurveSignal(string name, Func<TSignal, double> project, Func<double,double> curve, double defaultValue = 0.5)
    {
        Name = name;
        _project = project;
        _curve = curve;
        _defaultValue = defaultValue;
    }

    public double Evaluate(Runtime rt)
    {
        var sig = rt.Bus.GetOrDefault<TSignal>();
        var v = sig is null ? _defaultValue : _project(sig);
        return Math.Clamp(_curve(Math.Clamp(v, 0, 1)), 0, 1);
    }
}