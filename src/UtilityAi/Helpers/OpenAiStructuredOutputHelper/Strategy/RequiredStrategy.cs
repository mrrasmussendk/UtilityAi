namespace UtilityAi.Helpers.OpenAiStructuredOutputHelper.Strategy;


/// <summary>
/// Strategy for deciding which properties are "required" in the generated JSON Schema.
/// </summary>
public enum RequiredStrategy
{
    /// <summary>
    /// Non-nullable value types are required; reference/value types with [Required] are also required.
    /// </summary>
    NonNullableValueTypesAndRequiredAttribute,

    /// <summary>
    /// Only properties with [Required] are required.
    /// </summary>
    AttributesOnly,

    /// <summary>
    /// All public properties are required.
    /// </summary>
    AllProperties
}
