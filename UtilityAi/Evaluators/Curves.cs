namespace UtilityAi.Evaluators;

public static class Curves
{
    public static Func<double,double> Logistic(double k = 10, double m = 0.5)
        => x => 1.0 / (1.0 + Math.Exp(-k * (x - m)));

    public static Func<double,double> Identity() => x => x;
    public static Func<double,double> OneMinus() => x => 1 - x;
}