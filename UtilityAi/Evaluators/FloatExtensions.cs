namespace UtilityAi.Evaluators;

internal static class FloatExtensions
{
    public static float Clamp01(this float v)
    {
        if (v < 0f) return 0f;
        if (v > 1f) return 1f;
        return v;
    }
}