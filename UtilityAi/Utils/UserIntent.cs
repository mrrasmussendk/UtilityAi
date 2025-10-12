namespace UtilityAi.Utils;

public sealed record UserIntent(string Query, string Delivery = "email", string Topic = "employment");
