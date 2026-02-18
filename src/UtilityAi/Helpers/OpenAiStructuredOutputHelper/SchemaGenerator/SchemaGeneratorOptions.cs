using UtilityAi.Helpers.OpenAiStructuredOutputHelper.Strategy;

namespace UtilityAi.Helpers.OpenAiStructuredOutputHelper.SchemaGenerator;

/// <summary>
/// Options for JSON Schema generation from a .NET type.
/// </summary>
public sealed class SchemaGeneratorOptions
{
    public bool AdditionalPropertiesForItem { get; init; } = false;
    public RequiredStrategy RequiredStrategy { get; init; } =
        RequiredStrategy.NonNullableValueTypesAndRequiredAttribute;
}