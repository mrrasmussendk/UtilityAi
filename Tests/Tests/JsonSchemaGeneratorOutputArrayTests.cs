using System.Text.Json;
using System.Text.Json.Serialization;
using UtilityAi.Helpers.OpenAiStructuredOutputHelper.SchemaGenerator;
using Xunit;

namespace Tests;

/// <summary>
/// Tests for JsonSchemaGenerator.BuildOutputArraySchemaFrom to verify schema generation
/// always wraps types in an array under "output" property.
/// </summary>
public class JsonSchemaGeneratorOutputArrayTests
{
    // ─── Simple Record Tests ────────────────────────────────────

    [Fact]
    public void BuildOutputArraySchemaFrom_SimpleRecord_CreatesArrayWrapper()
    {
        // Arrange & Act
        var schema = JsonSchemaGenerator.BuildOutputArraySchemaFrom<SimpleAreaResponse>();
        var schemaJson = schema.ToJsonString();

        // Assert
        Assert.NotNull(schema);
        
        // Verify root structure
        Assert.Equal("object", schema["type"]?.ToString());
        Assert.NotNull(schema["properties"]);
        Assert.False((bool)schema["additionalProperties"]!);
        
        // Verify output property exists and is an array
        var properties = schema["properties"]?.AsObject();
        Assert.NotNull(properties);
        Assert.True(properties.ContainsKey("output"));
        
        var output = properties["output"]?.AsObject();
        Assert.NotNull(output);
        Assert.Equal("array", output["type"]?.ToString());
        
        // Verify array items schema
        var items = output["items"]?.AsObject();
        Assert.NotNull(items);
        Assert.Equal("object", items["type"]?.ToString());
        
        // Verify the actual record properties
        var itemProps = items["properties"]?.AsObject();
        Assert.NotNull(itemProps);
        Assert.True(itemProps.ContainsKey("area"));
        
        var areaSchema = itemProps["area"]?.AsObject();
        Assert.Equal("string", areaSchema?["type"]?.ToString());
        
        // Verify required fields
        var required = items["required"]?.AsArray();
        Assert.NotNull(required);
        Assert.Contains(required, r => r?.ToString() == "area");
    }

    [Fact]
    public void BuildOutputArraySchemaFrom_RecordWithMultipleProperties_CreatesCorrectArraySchema()
    {
        // Arrange & Act
        var schema = JsonSchemaGenerator.BuildOutputArraySchemaFrom<PersonRecord>();

        // Assert
        var output = schema["properties"]?["output"]?.AsObject();
        var items = output?["items"]?.AsObject();
        var itemProps = items?["properties"]?.AsObject();
        
        Assert.NotNull(itemProps);
        Assert.True(itemProps.ContainsKey("name"));
        Assert.True(itemProps.ContainsKey("age"));
        Assert.True(itemProps.ContainsKey("email"));
        
        // Verify types
        Assert.Equal("string", itemProps["name"]?["type"]?.ToString());
        Assert.Equal("integer", itemProps["age"]?["type"]?.ToString());
        Assert.Equal("string", itemProps["email"]?["type"]?.ToString());
        
        // Verify all are required
        var required = items?["required"]?.AsArray();
        Assert.NotNull(required);
        Assert.Equal(3, required.Count);
    }

    // ─── Primitive Type Tests ────────────────────────────────────

    [Fact]
    public void BuildOutputArraySchemaFrom_StringType_CreatesArrayOfStrings()
    {
        // Arrange & Act
        var schema = JsonSchemaGenerator.BuildOutputArraySchemaFrom<string>();

        // Assert
        var output = schema["properties"]?["output"]?.AsObject();
        var items = output?["items"]?.AsObject();
        
        Assert.NotNull(items);
        Assert.Equal("object", items["type"]?.ToString()); // Wraps in object
        
        // String properties should be in the object
        var props = items["properties"]?.AsObject();
        Assert.NotNull(props);
    }

    [Fact]
    public void BuildOutputArraySchemaFrom_IntType_CreatesArrayWrapper()
    {
        // Arrange & Act
        var schema = JsonSchemaGenerator.BuildOutputArraySchemaFrom<int>();

        // Assert - even primitives get wrapped
        var output = schema["properties"]?["output"]?.AsObject();
        Assert.NotNull(output);
        Assert.Equal("array", output["type"]?.ToString());
    }

    // ─── Enum Tests ────────────────────────────────────────────

    [Fact]
    public void BuildOutputArraySchemaFrom_EnumType_CreatesArrayWithEnumSchema()
    {
        // Arrange & Act
        var schema = JsonSchemaGenerator.BuildOutputArraySchemaFrom<StatusEnum>();

        // Assert
        var output = schema["properties"]?["output"]?.AsObject();
        var items = output?["items"]?.AsObject();
        var props = items?["properties"]?.AsObject();
        
        Assert.NotNull(items);
        Assert.Equal("object", items["type"]?.ToString());
    }

    [Fact]
    public void BuildOutputArraySchemaFrom_RecordWithEnum_IncludesEnumInSchema()
    {
        // Arrange & Act
        var schema = JsonSchemaGenerator.BuildOutputArraySchemaFrom<RecordWithEnum>();

        // Assert
        var output = schema["properties"]?["output"]?.AsObject();
        var items = output?["items"]?.AsObject();
        var itemProps = items?["properties"]?.AsObject();
        
        Assert.NotNull(itemProps);
        Assert.True(itemProps.ContainsKey("status"));
        
        var statusSchema = itemProps["status"]?.AsObject();
        Assert.Equal("string", statusSchema?["type"]?.ToString());
        Assert.NotNull(statusSchema?["enum"]);
        
        var enumValues = statusSchema["enum"]?.AsArray();
        Assert.NotNull(enumValues);
        Assert.Contains(enumValues, e => e?.ToString() == "Active");
        Assert.Contains(enumValues, e => e?.ToString() == "Inactive");
        Assert.Contains(enumValues, e => e?.ToString() == "Pending");
    }

    // ─── Nested Object Tests ────────────────────────────────────

    [Fact]
    public void BuildOutputArraySchemaFrom_NestedRecord_CreatesNestedObjectSchema()
    {
        // Arrange & Act
        var schema = JsonSchemaGenerator.BuildOutputArraySchemaFrom<RecordWithNested>();

        // Assert
        var output = schema["properties"]?["output"]?.AsObject();
        var items = output?["items"]?.AsObject();
        var itemProps = items?["properties"]?.AsObject();
        
        Assert.NotNull(itemProps);
        Assert.True(itemProps.ContainsKey("id"));
        Assert.True(itemProps.ContainsKey("address"));
        
        // Verify nested address object
        var addressSchema = itemProps["address"]?.AsObject();
        Assert.NotNull(addressSchema);
        Assert.Equal("object", addressSchema["type"]?.ToString());
        
        var addressProps = addressSchema["properties"]?.AsObject();
        Assert.NotNull(addressProps);
        Assert.True(addressProps.ContainsKey("street"));
        Assert.True(addressProps.ContainsKey("city"));
        Assert.True(addressProps.ContainsKey("zipCode"));
    }

    // ─── Collection Tests ────────────────────────────────────────

    [Fact]
    public void BuildOutputArraySchemaFrom_RecordWithList_CreatesArrayPropertyInItems()
    {
        // Arrange & Act
        var schema = JsonSchemaGenerator.BuildOutputArraySchemaFrom<RecordWithList>();

        // Assert
        var output = schema["properties"]?["output"]?.AsObject();
        var items = output?["items"]?.AsObject();
        var itemProps = items?["properties"]?.AsObject();
        
        Assert.NotNull(itemProps);
        Assert.True(itemProps.ContainsKey("tags"));
        
        // Verify tags is an array of strings
        var tagsSchema = itemProps["tags"]?.AsObject();
        Assert.Equal("array", tagsSchema?["type"]?.ToString());
        
        var tagsItems = tagsSchema?["items"]?.AsObject();
        Assert.Equal("string", tagsItems?["type"]?.ToString());
    }

    [Fact]
    public void BuildOutputArraySchemaFrom_RecordWithArray_CreatesArrayPropertyInItems()
    {
        // Arrange & Act
        var schema = JsonSchemaGenerator.BuildOutputArraySchemaFrom<RecordWithArray>();

        // Assert
        var output = schema["properties"]?["output"]?.AsObject();
        var items = output?["items"]?.AsObject();
        var itemProps = items?["properties"]?.AsObject();
        
        Assert.NotNull(itemProps);
        Assert.True(itemProps.ContainsKey("scores"));
        
        var scoresSchema = itemProps["scores"]?.AsObject();
        Assert.Equal("array", scoresSchema?["type"]?.ToString());
        
        var scoresItems = scoresSchema?["items"]?.AsObject();
        Assert.Equal("integer", scoresItems?["type"]?.ToString());
    }

    // ─── Dictionary Tests ────────────────────────────────────────

    [Fact]
    public void BuildOutputArraySchemaFrom_RecordWithDictionary_CreatesObjectWithAdditionalProperties()
    {
        // Arrange & Act
        var schema = JsonSchemaGenerator.BuildOutputArraySchemaFrom<RecordWithDictionary>();

        // Assert
        var output = schema["properties"]?["output"]?.AsObject();
        var items = output?["items"]?.AsObject();
        var itemProps = items?["properties"]?.AsObject();
        
        Assert.NotNull(itemProps);
        Assert.True(itemProps.ContainsKey("metadata"));
        
        // Dictionary<string, string> becomes object with additionalProperties
        var metadataSchema = itemProps["metadata"]?.AsObject();
        Assert.Equal("object", metadataSchema?["type"]?.ToString());
        
        var additionalProps = metadataSchema?["additionalProperties"];
        Assert.NotNull(additionalProps);
        
        // Should be a schema object for string values
        var additionalPropsSchema = additionalProps?.AsObject();
        Assert.Equal("string", additionalPropsSchema?["type"]?.ToString());
    }

    [Fact]
    public void BuildOutputArraySchemaFrom_RecordWithJsonElementDictionary_AllowsDynamicKeys()
    {
        // Arrange & Act
        var schema = JsonSchemaGenerator.BuildOutputArraySchemaFrom<RecordWithJsonElementDict>();

        // Assert
        var output = schema["properties"]?["output"]?.AsObject();
        var items = output?["items"]?.AsObject();
        var itemProps = items?["properties"]?.AsObject();
        
        Assert.NotNull(itemProps);
        Assert.True(itemProps.ContainsKey("entities"));
        
        // Dictionary<string, JsonElement> should allow additionalProperties: true
        var entitiesSchema = itemProps["entities"]?.AsObject();
        Assert.Equal("object", entitiesSchema?["type"]?.ToString());
        Assert.True((bool)entitiesSchema["additionalProperties"]!);
    }

    // ─── Nullable Type Tests ────────────────────────────────────

    [Fact]
    public void BuildOutputArraySchemaFrom_RecordWithNullableInt_MarksAsNotRequired()
    {
        // Arrange & Act
        var schema = JsonSchemaGenerator.BuildOutputArraySchemaFrom<RecordWithNullable>();

        // Assert
        var output = schema["properties"]?["output"]?.AsObject();
        var items = output?["items"]?.AsObject();
        var itemProps = items?["properties"]?.AsObject();
        
        Assert.NotNull(itemProps);
        Assert.True(itemProps.ContainsKey("optionalAge"));
        
        var ageSchema = itemProps["optionalAge"]?.AsObject();
        Assert.Equal("integer", ageSchema?["type"]?.ToString());
        
        // Verify optionalAge is NOT in required array
        var required = items?["required"]?.AsArray();
        Assert.NotNull(required);
        Assert.DoesNotContain(required, r => r?.ToString() == "optionalAge");
    }

    [Fact]
    public void BuildOutputArraySchemaFrom_RecordWithNullableString_HandlesCorrectly()
    {
        // Arrange & Act
        var schema = JsonSchemaGenerator.BuildOutputArraySchemaFrom<RecordWithNullableString>();

        // Assert
        var output = schema["properties"]?["output"]?.AsObject();
        var items = output?["items"]?.AsObject();
        var itemProps = items?["properties"]?.AsObject();
        
        Assert.NotNull(itemProps);
        Assert.True(itemProps.ContainsKey("description"));
        
        // Nullable reference types don't affect schema the same way as value types
        var descSchema = itemProps["description"]?.AsObject();
        Assert.Equal("string", descSchema?["type"]?.ToString());
    }

    // ─── DateTime Type Tests ────────────────────────────────────

    [Fact]
    public void BuildOutputArraySchemaFrom_RecordWithDateTime_UsesDateTimeFormat()
    {
        // Arrange & Act
        var schema = JsonSchemaGenerator.BuildOutputArraySchemaFrom<RecordWithDateTime>();

        // Assert
        var output = schema["properties"]?["output"]?.AsObject();
        var items = output?["items"]?.AsObject();
        var itemProps = items?["properties"]?.AsObject();
        
        Assert.NotNull(itemProps);
        Assert.True(itemProps.ContainsKey("createdAt"));
        
        var dateSchema = itemProps["createdAt"]?.AsObject();
        Assert.Equal("string", dateSchema?["type"]?.ToString());
        Assert.Equal("date-time", dateSchema?["format"]?.ToString());
    }

    [Fact]
    public void BuildOutputArraySchemaFrom_RecordWithDateOnly_UsesDateFormat()
    {
        // Arrange & Act
        var schema = JsonSchemaGenerator.BuildOutputArraySchemaFrom<RecordWithDateOnly>();

        // Assert
        var output = schema["properties"]?["output"]?.AsObject();
        var items = output?["items"]?.AsObject();
        var itemProps = items?["properties"]?.AsObject();
        
        Assert.NotNull(itemProps);
        Assert.True(itemProps.ContainsKey("birthDate"));
        
        var dateSchema = itemProps["birthDate"]?.AsObject();
        Assert.Equal("string", dateSchema?["type"]?.ToString());
        Assert.Equal("date", dateSchema?["format"]?.ToString());
    }

    // ─── Guid and Uri Tests ────────────────────────────────────

    [Fact]
    public void BuildOutputArraySchemaFrom_RecordWithGuid_UsesUuidFormat()
    {
        // Arrange & Act
        var schema = JsonSchemaGenerator.BuildOutputArraySchemaFrom<RecordWithGuid>();

        // Assert
        var output = schema["properties"]?["output"]?.AsObject();
        var items = output?["items"]?.AsObject();
        var itemProps = items?["properties"]?.AsObject();
        
        Assert.NotNull(itemProps);
        Assert.True(itemProps.ContainsKey("id"));
        
        var guidSchema = itemProps["id"]?.AsObject();
        Assert.Equal("string", guidSchema?["type"]?.ToString());
        Assert.Equal("uuid", guidSchema?["format"]?.ToString());
    }

    [Fact]
    public void BuildOutputArraySchemaFrom_RecordWithUri_UsesUriFormat()
    {
        // Arrange & Act
        var schema = JsonSchemaGenerator.BuildOutputArraySchemaFrom<RecordWithUri>();

        // Assert
        var output = schema["properties"]?["output"]?.AsObject();
        var items = output?["items"]?.AsObject();
        var itemProps = items?["properties"]?.AsObject();
        
        Assert.NotNull(itemProps);
        Assert.True(itemProps.ContainsKey("website"));
        
        var uriSchema = itemProps["website"]?.AsObject();
        Assert.Equal("string", uriSchema?["type"]?.ToString());
        Assert.Equal("uri", uriSchema?["format"]?.ToString());
    }

    // ─── Complex Composition Tests ────────────────────────────────

    [Fact]
    public void BuildOutputArraySchemaFrom_ComplexRecord_GeneratesFullSchema()
    {
        // Arrange & Act
        var schema = JsonSchemaGenerator.BuildOutputArraySchemaFrom<ComplexRecord>();

        // Assert
        var output = schema["properties"]?["output"]?.AsObject();
        var items = output?["items"]?.AsObject();
        var itemProps = items?["properties"]?.AsObject();
        
        Assert.NotNull(itemProps);
        
        // Verify all property types
        Assert.True(itemProps.ContainsKey("id"));
        Assert.True(itemProps.ContainsKey("name"));
        Assert.True(itemProps.ContainsKey("status"));
        Assert.True(itemProps.ContainsKey("tags"));
        Assert.True(itemProps.ContainsKey("metadata"));
        Assert.True(itemProps.ContainsKey("createdAt"));
        Assert.True(itemProps.ContainsKey("score"));
        
        // Verify complex types
        Assert.Equal("string", itemProps["id"]?["type"]?.ToString());
        Assert.Equal("uuid", itemProps["id"]?["format"]?.ToString());
        
        Assert.Equal("string", itemProps["status"]?["type"]?.ToString());
        Assert.NotNull(itemProps["status"]?["enum"]);
        
        Assert.Equal("array", itemProps["tags"]?["type"]?.ToString());
        
        Assert.Equal("object", itemProps["metadata"]?["type"]?.ToString());
        
        Assert.Equal("string", itemProps["createdAt"]?["type"]?.ToString());
        Assert.Equal("date-time", itemProps["createdAt"]?["format"]?.ToString());
    }

    // ─── Root Structure Tests ────────────────────────────────────

    [Fact]
    public void BuildOutputArraySchemaFrom_AnyType_AlwaysHasOutputProperty()
    {
        // Test that regardless of type, the schema always has "output" at root
        var schemas = new[]
        {
            JsonSchemaGenerator.BuildOutputArraySchemaFrom<SimpleAreaResponse>(),
            JsonSchemaGenerator.BuildOutputArraySchemaFrom<PersonRecord>(),
            JsonSchemaGenerator.BuildOutputArraySchemaFrom<RecordWithNested>(),
            JsonSchemaGenerator.BuildOutputArraySchemaFrom<ComplexRecord>()
        };

        foreach (var schema in schemas)
        {
            // Verify root structure
            Assert.Equal("object", schema["type"]?.ToString());
            Assert.False((bool)schema["additionalProperties"]!);
            
            // Verify output property exists
            var props = schema["properties"]?.AsObject();
            Assert.NotNull(props);
            Assert.Single(props); // Should only have "output"
            Assert.True(props.ContainsKey("output"));
            
            // Verify required array contains "output"
            var required = schema["required"]?.AsArray();
            Assert.NotNull(required);
            Assert.Single(required);
            Assert.Equal("output", required[0]?.ToString());
        }
    }

    [Fact]
    public void BuildOutputArraySchemaFrom_AnyType_OutputIsAlwaysArray()
    {
        // Test that "output" property is always type "array"
        var schemas = new[]
        {
            JsonSchemaGenerator.BuildOutputArraySchemaFrom<SimpleAreaResponse>(),
            JsonSchemaGenerator.BuildOutputArraySchemaFrom<int>(),
            JsonSchemaGenerator.BuildOutputArraySchemaFrom<StatusEnum>(),
            JsonSchemaGenerator.BuildOutputArraySchemaFrom<ComplexRecord>()
        };

        foreach (var schema in schemas)
        {
            var output = schema["properties"]?["output"]?.AsObject();
            Assert.NotNull(output);
            Assert.Equal("array", output["type"]?.ToString());
            Assert.NotNull(output["items"]);
        }
    }

    // ─── Test Models ────────────────────────────────────────────

    private sealed record SimpleAreaResponse(
        [property: JsonPropertyName("area")] string Area);

    private sealed record PersonRecord(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("age")] int Age,
        [property: JsonPropertyName("email")] string Email);

    private enum StatusEnum
    {
        Active,
        Inactive,
        Pending
    }

    private sealed record RecordWithEnum(
        [property: JsonPropertyName("status")] StatusEnum Status);

    private sealed record Address(
        [property: JsonPropertyName("street")] string Street,
        [property: JsonPropertyName("city")] string City,
        [property: JsonPropertyName("zipCode")] string ZipCode);

    private sealed record RecordWithNested(
        [property: JsonPropertyName("id")] int Id,
        [property: JsonPropertyName("address")] Address Address);

    private sealed record RecordWithList(
        [property: JsonPropertyName("tags")] List<string> Tags);

    private sealed record RecordWithArray(
        [property: JsonPropertyName("scores")] int[] Scores);

    private sealed record RecordWithDictionary(
        [property: JsonPropertyName("metadata")] Dictionary<string, string> Metadata);

    private sealed record RecordWithJsonElementDict(
        [property: JsonPropertyName("entities")] Dictionary<string, JsonElement> Entities);

    private sealed record RecordWithNullable(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("optionalAge")] int? OptionalAge);

    private sealed record RecordWithNullableString(
        [property: JsonPropertyName("description")] string? Description);

    private sealed record RecordWithDateTime(
        [property: JsonPropertyName("createdAt")] DateTime CreatedAt);

    private sealed record RecordWithDateOnly(
        [property: JsonPropertyName("birthDate")] DateOnly BirthDate);

    private sealed record RecordWithGuid(
        [property: JsonPropertyName("id")] Guid Id);

    private sealed record RecordWithUri(
        [property: JsonPropertyName("website")] Uri Website);

    private sealed record ComplexRecord(
        [property: JsonPropertyName("id")] Guid Id,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("status")] StatusEnum Status,
        [property: JsonPropertyName("tags")] List<string> Tags,
        [property: JsonPropertyName("metadata")] Dictionary<string, string> Metadata,
        [property: JsonPropertyName("createdAt")] DateTime CreatedAt,
        [property: JsonPropertyName("score")] double? Score);
}
