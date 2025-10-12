namespace UtilityAi.Utils;

public sealed record Runtime(EventBus Bus, UserIntent Intent, int Tick);