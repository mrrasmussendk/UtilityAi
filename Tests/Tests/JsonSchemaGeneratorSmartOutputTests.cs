using System.Text.Json;
using System.Text.Json.Serialization;
using UtilityAi.Helpers.OpenAiStructuredOutputHelper.SchemaGenerator;
using Xunit;

namespace Tests;

/// <summary>
/// Tests for JsonSchemaGenerator.BuildOutputSchemaFrom to verify intelligent wrapping:
/// - Collections (List, Array, IEnumerable) → wrapped in array
/// - Single objects → wrapped as direct object (NOT array)
/// </summary>
public class JsonSchemaGeneratorSmartOutputTests
{
    // ─── Single Object Tests (NOT wrapped in array) ────────────────

    [Fact]
    public void BuildOutputSchemaFrom_SimpleRecord_CreatesObjectNotArray()
    {
        // Arrange & Act
        var schema = JsonSchemaGenerator.BuildOutputSchemaFrom<SimpleAreaResponse>();

        // Assert root structure
        Assert.Equal("object", schema["type"]?.ToString());
        Assert.NotNull(schema["properties"]);
        Assert.False((bool)schema["additionalProperties"]!);

        // Verify output property exists
        var properties = schema["properties"]?.AsObject();
        Assert.NotNull(properties);
        Assert.True(properties.ContainsKey("output"));

        var output = properties["output"]?.AsObject();
        Assert.NotNull(output);

        // KEY ASSERTION: output should be OBJECT, not ARRAY for simple records
        Assert.Equal("object", output["type"]?.ToString());

        // Verify the actual record properties are directly in output
        var outputProps = output["properties"]?.AsObject();
        Assert.NotNull(outputProps);
        Assert.True(outputProps.ContainsKey("area"));

        var areaSchema = outputProps["area"]?.AsObject();
        Assert.Equal("string", areaSchema?["type"]?.ToString());

        // Verify required fields
        var required = output["required"]?.AsArray();
        Assert.NotNull(required);
        Assert.Contains(required, r => r?.ToString() == "area");
    }

    [Fact]
    public void BuildOutputSchemaFrom_ComplexRecord_CreatesObjectNotArray()
    {
        // Arrange & Act
        var schema = JsonSchemaGenerator.BuildOutputSchemaFrom<PersonRecord>();

        // Assert
        var output = schema["properties"]?["output"]?.AsObject();
        Assert.NotNull(output);

        // Should be object, not array
        Assert.Equal("object", output["type"]?.ToString());

        // Properties should be directly in output
        var outputProps = output["properties"]?.AsObject();
        Assert.NotNull(outputProps);
        Assert.True(outputProps.ContainsKey("name"));
        Assert.True(outputProps.ContainsKey("age"));
        Assert.True(outputProps.ContainsKey("email"));

        // Verify types
        Assert.Equal("string", outputProps["name"]?["type"]?.ToString());
        Assert.Equal("integer", outputProps["age"]?["type"]?.ToString());
        Assert.Equal("string", outputProps["email"]?["type"]?.ToString());
    }

    [Fact]
    public void BuildOutputSchemaFrom_RecordWithNestedObject_CreatesObjectNotArray()
    {
        // Arrange & Act
        var schema = JsonSchemaGenerator.BuildOutputSchemaFrom<RecordWithNested>();

        // Assert
        var output = schema["properties"]?["output"]?.AsObject();
        Assert.NotNull(output);
        Assert.Equal("object", output["type"]?.ToString());

        var outputProps = output["properties"]?.AsObject();
        Assert.NotNull(outputProps);
        Assert.True(outputProps.ContainsKey("id"));
        Assert.True(outputProps.ContainsKey("address"));

        // Verify nested address object
        var addressSchema = outputProps["address"]?.AsObject();
        Assert.NotNull(addressSchema);
        Assert.Equal("object", addressSchema["type"]?.ToString());
    }

    // ─── Collection Tests (wrapped in array) ────────────────────────

    [Fact]
    public void BuildOutputSchemaFrom_ListOfRecords_CreatesArray()
    {
        // Arrange & Act
        var schema = JsonSchemaGenerator.BuildOutputSchemaFrom<List<SimpleAreaResponse>>();

        // Assert
        var output = schema["properties"]?["output"]?.AsObject();
        Assert.NotNull(output);

        // KEY ASSERTION: output should be ARRAY for collections
        Assert.Equal("array", output["type"]?.ToString());

        // Verify array items schema
        var items = output["items"]?.AsObject();
        Assert.NotNull(items);
        Assert.Equal("object", items["type"]?.ToString());

        // Verify the item properties
        var itemProps = items["properties"]?.AsObject();
        Assert.NotNull(itemProps);
        Assert.True(itemProps.ContainsKey("area"));

        var areaSchema = itemProps["area"]?.AsObject();
        Assert.Equal("string", areaSchema?["type"]?.ToString());
    }

    [Fact]
    public void BuildOutputSchemaFrom_ArrayOfRecords_CreatesArray()
    {
        // Arrange & Act
        var schema = JsonSchemaGenerator.BuildOutputSchemaFrom<PersonRecord[]>();

        // Assert
        var output = schema["properties"]?["output"]?.AsObject();
        Assert.NotNull(output);

        // Should be array
        Assert.Equal("array", output["type"]?.ToString());

        // Verify array items
        var items = output["items"]?.AsObject();
        Assert.NotNull(items);
        Assert.Equal("object", items["type"]?.ToString());

        var itemProps = items["properties"]?.AsObject();
        Assert.NotNull(itemProps);
        Assert.True(itemProps.ContainsKey("name"));
        Assert.True(itemProps.ContainsKey("age"));
        Assert.True(itemProps.ContainsKey("email"));
    }

    [Fact]
    public void BuildOutputSchemaFrom_IEnumerableOfRecords_CreatesArray()
    {
        // Arrange & Act
        var schema = JsonSchemaGenerator.BuildOutputSchemaFrom<IEnumerable<SimpleAreaResponse>>();

        // Assert
        var output = schema["properties"]?["output"]?.AsObject();
        Assert.NotNull(output);

        // Should be array
        Assert.Equal("array", output["type"]?.ToString());

        var items = output["items"]?.AsObject();
        Assert.NotNull(items);
        Assert.Equal("object", items["type"]?.ToString());
    }

    // ─── Edge Cases ──────────────────────────────────────────────────

    [Fact]
    public void BuildOutputSchemaFrom_String_IsNotTreatedAsCollection()
    {
        // String is NOT a collection, even though it's IEnumerable<char>
        // Arrange & Act
        var schema = JsonSchemaGenerator.BuildOutputSchemaFrom<string>();

        // Assert
        var output = schema["properties"]?["output"]?.AsObject();
        Assert.NotNull(output);

        // Should be OBJECT (wrapping a string property), not ARRAY
        Assert.Equal("object", output["type"]?.ToString());
    }

    [Fact]
    public void BuildOutputSchemaFrom_Dictionary_IsNotTreatedAsCollection()
    {
        // Dictionary is NOT treated as a collection (it's an object with additionalProperties)
        // Arrange & Act
        var schema = JsonSchemaGenerator.BuildOutputSchemaFrom<Dictionary<string, string>>();

        // Assert
        var output = schema["properties"]?["output"]?.AsObject();
        Assert.NotNull(output);

        // Should be OBJECT, not ARRAY
        Assert.Equal("object", output["type"]?.ToString());
    }

    [Fact]
    public void BuildOutputSchemaFrom_RecordWithListProperty_NotTreatedAsCollection()
    {
        // A record that CONTAINS a list is not itself a collection
        // Arrange & Act
        var schema = JsonSchemaGenerator.BuildOutputSchemaFrom<RecordWithList>();

        // Assert
        var output = schema["properties"]?["output"]?.AsObject();
        Assert.NotNull(output);

        // The output should be OBJECT (for the record), not ARRAY
        Assert.Equal("object", output["type"]?.ToString());

        // But the tags property INSIDE should be an array
        var outputProps = output["properties"]?.AsObject();
        Assert.NotNull(outputProps);
        Assert.True(outputProps.ContainsKey("tags"));

        var tagsSchema = outputProps["tags"]?.AsObject();
        Assert.Equal("array", tagsSchema?["type"]?.ToString());
    }

    // ─── Comparison with Old Method ──────────────────────────────────

    [Fact]
    public void BuildOutputSchemaFrom_VsOld_SingleObjectBehaviorDiffers()
    {
        // Compare new vs old method behavior for single objects

        #pragma warning disable CS0618 // Type or member is obsolete
        var oldSchema = JsonSchemaGenerator.BuildOutputArraySchemaFrom<SimpleAreaResponse>();
        #pragma warning restore CS0618

        var newSchema = JsonSchemaGenerator.BuildOutputSchemaFrom<SimpleAreaResponse>();

        // Old method ALWAYS creates array
        var oldOutput = oldSchema["properties"]?["output"]?.AsObject();
        Assert.Equal("array", oldOutput?["type"]?.ToString());

        // New method creates object for single records
        var newOutput = newSchema["properties"]?["output"]?.AsObject();
        Assert.Equal("object", newOutput?["type"]?.ToString());
    }

    [Fact]
    public void BuildOutputSchemaFrom_VsOld_CollectionBehaviorSame()
    {
        // Compare new vs old method behavior for collections

        #pragma warning disable CS0618 // Type or member is obsolete
        var oldSchema = JsonSchemaGenerator.BuildOutputArraySchemaFrom<List<SimpleAreaResponse>>();
        #pragma warning restore CS0618

        var newSchema = JsonSchemaGenerator.BuildOutputSchemaFrom<List<SimpleAreaResponse>>();

        // Both should create array for collections
        var oldOutput = oldSchema["properties"]?["output"]?.AsObject();
        Assert.Equal("array", oldOutput?["type"]?.ToString());

        var newOutput = newSchema["properties"]?["output"]?.AsObject();
        Assert.Equal("array", newOutput?["type"]?.ToString());

        // Item schemas should be equivalent
        var oldItems = oldOutput?["items"]?.AsObject();
        var newItems = newOutput?["items"]?.AsObject();

        Assert.Equal(oldItems?["type"]?.ToString(), newItems?["type"]?.ToString());
    }

    // ─── Complex Type Tests ──────────────────────────────────────────

    [Fact]
    public void BuildOutputSchemaFrom_ComplexRecordWithAllTypes_GeneratesCorrectSchema()
    {
        // Arrange & Act
        var schema = JsonSchemaGenerator.BuildOutputSchemaFrom<ComplexRecord>();

        // Assert
        var output = schema["properties"]?["output"]?.AsObject();
        Assert.NotNull(output);

        // Should be object for single complex record
        Assert.Equal("object", output["type"]?.ToString());

        var outputProps = output["properties"]?.AsObject();
        Assert.NotNull(outputProps);

        // Verify all property types
        Assert.True(outputProps.ContainsKey("id"));
        Assert.True(outputProps.ContainsKey("name"));
        Assert.True(outputProps.ContainsKey("status"));
        Assert.True(outputProps.ContainsKey("tags"));
        Assert.True(outputProps.ContainsKey("metadata"));
        Assert.True(outputProps.ContainsKey("createdAt"));
        Assert.True(outputProps.ContainsKey("score"));

        // Verify specific types
        Assert.Equal("string", outputProps["id"]?["type"]?.ToString());
        Assert.Equal("uuid", outputProps["id"]?["format"]?.ToString());

        Assert.Equal("string", outputProps["status"]?["type"]?.ToString());
        Assert.NotNull(outputProps["status"]?["enum"]);

        Assert.Equal("array", outputProps["tags"]?["type"]?.ToString());

        Assert.Equal("object", outputProps["metadata"]?["type"]?.ToString());
    }

    [Fact]
    public void BuildOutputSchemaFrom_ListOfComplexRecords_GeneratesArrayWithComplexItems()
    {
        // Arrange & Act
        var schema = JsonSchemaGenerator.BuildOutputSchemaFrom<List<ComplexRecord>>();

        // Assert
        var output = schema["properties"]?["output"]?.AsObject();
        Assert.NotNull(output);

        // Should be array for list
        Assert.Equal("array", output["type"]?.ToString());

        var items = output["items"]?.AsObject();
        Assert.NotNull(items);
        Assert.Equal("object", items["type"]?.ToString());

        var itemProps = items["properties"]?.AsObject();
        Assert.NotNull(itemProps);

        // Verify complex item has all properties
        Assert.True(itemProps.ContainsKey("id"));
        Assert.True(itemProps.ContainsKey("name"));
        Assert.True(itemProps.ContainsKey("status"));
        Assert.True(itemProps.ContainsKey("tags"));
    }

    // ─── Test Models ────────────────────────────────────────────────

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

    private sealed record Address(
        [property: JsonPropertyName("street")] string Street,
        [property: JsonPropertyName("city")] string City,
        [property: JsonPropertyName("zipCode")] string ZipCode);

    private sealed record RecordWithNested(
        [property: JsonPropertyName("id")] int Id,
        [property: JsonPropertyName("address")] Address Address);

    private sealed record RecordWithList(
        [property: JsonPropertyName("tags")] List<string> Tags);

    private sealed record ComplexRecord(
        [property: JsonPropertyName("id")] Guid Id,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("status")] StatusEnum Status,
        [property: JsonPropertyName("tags")] List<string> Tags,
        [property: JsonPropertyName("metadata")] Dictionary<string, string> Metadata,
        [property: JsonPropertyName("createdAt")] DateTime CreatedAt,
        [property: JsonPropertyName("score")] double? Score);
}
