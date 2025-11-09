using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Nodes;
using UtilityAi.Helpers.OpenAiStructuredOutputHelper;

namespace Tests.OpenAiStructuredOutputHelper;

public class AiRequestBuilderTests
{
    private sealed class SampleItem
    {
        [Required]
        [Description("The display name")]
        public string Name { get; set; } = string.Empty;

        public int? Age { get; set; }

        public bool Active { get; set; }

        public List<string>? Tags { get; set; }
    }

    [Fact]
    public void BuildJson_WithAllParts_ProducesExpectedEnvelope()
    {
        var json = AiRequestBuilder.Create()
            .WithModel("gpt-4o")
            .AddMcpTool("server-label", "http://mcp.local", new[] { "search", "browse" }, "auto")
            .AddTool("web_search")
            .AddSystem("You are a helpful assistant.")
            .AddUser("List top 3 items.")
            .WithJsonSchemaFrom<SampleItem>("TopItems")
            .BuildJson();

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("gpt-4o", root.GetProperty("model").GetString());

        var tools = root.GetProperty("tools");
        Assert.Equal(2, tools.GetArrayLength());
        Assert.Equal("mcp", tools[0].GetProperty("type").GetString());
        Assert.Equal("web_search", tools[1].GetProperty("type").GetString());

        var input = root.GetProperty("input");
        Assert.Equal(2, input.GetArrayLength());
        Assert.Equal("system", input[0].GetProperty("role").GetString());
        Assert.Equal("user", input[1].GetProperty("role").GetString());

        var text = root.GetProperty("text");
        var format = text.GetProperty("format");
        Assert.Equal("json_schema", format.GetProperty("type").GetString());
        Assert.Equal("TopItems", format.GetProperty("name").GetString());

        var schema = JsonNode.Parse(format.GetProperty("schema").GetRawText())!.AsObject();
        Assert.Equal("object", schema["type"]!.GetValue<string>());
        var properties = schema["properties"]!.AsObject();
        var output = properties["output"]!.AsObject();
        Assert.Equal("array", output["type"]!.GetValue<string>());

        var items = output["items"]!.AsObject();
        Assert.Equal("object", items["type"]!.GetValue<string>());
        var itemProps = items["properties"]!.AsObject();
        Assert.True(itemProps.ContainsKey("name"));
        Assert.True(itemProps.ContainsKey("age"));
        Assert.True(itemProps.ContainsKey("active"));
        Assert.True(itemProps.ContainsKey("tags"));

        // Verify required rules: Non-nullable bool and [Required] string should be required
        var required = items["required"]!.AsArray();
        var requiredSet = new HashSet<string>(required.Select(x => x!.GetValue<string>()));
        Assert.Contains("name", requiredSet);
        Assert.Contains("active", requiredSet);
        // Optional int? and List<string>? should not be required
        Assert.DoesNotContain("age", requiredSet);
        Assert.DoesNotContain("tags", requiredSet);
    }

    [Fact]
    public void BuildJson_WithoutModel_Throws()
    {
        var builder = AiRequestBuilder.Create()
            .AddUser("hello")
            .WithJsonSchemaFrom<SampleItem>("Items");

        Assert.Throws<InvalidOperationException>(() => builder.BuildJson());
    }

    [Fact]
    public void BuildJson_WithoutFormat_Throws()
    {
        var builder = AiRequestBuilder.Create()
            .WithModel("gpt")
            .AddUser("hello");

        Assert.Throws<InvalidOperationException>(() => builder.BuildJson());
    }

    [Fact]
    public void BuildJson_WithoutMessages_Throws()
    {
        var builder = AiRequestBuilder.Create()
            .WithModel("gpt")
            .WithJsonSchemaFrom<SampleItem>("Items");

        Assert.Throws<InvalidOperationException>(() => builder.BuildJson());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void AddTool_InvalidType_Throws(string? type)
    {
        var builder = AiRequestBuilder.Create().WithModel("gpt");
        Assert.Throws<ArgumentException>(() => builder.AddTool(type!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void AddMcpTool_InvalidLabel_Throws(string? label)
    {
        var builder = AiRequestBuilder.Create().WithModel("gpt");
        Assert.Throws<ArgumentException>(() => builder.AddMcpTool(label!, "http://server", Array.Empty<string>()));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void AddMcpTool_InvalidServerUrl_Throws(string? url)
    {
        var builder = AiRequestBuilder.Create().WithModel("gpt");
        Assert.Throws<ArgumentException>(() => builder.AddMcpTool("label", url!, Array.Empty<string>()));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void WithJsonSchema_InvalidName_Throws(string? name)
    {
        var builder = AiRequestBuilder.Create().WithModel("gpt");
        var schema = JsonNode.Parse("{\"type\":\"object\"}")!.AsObject();
        Assert.Throws<ArgumentException>(() => builder.WithJsonSchema(name!, schema));
    }

    [Fact]
    public void WithJsonSchema_NullSchema_Throws()
    {
        var builder = AiRequestBuilder.Create().WithModel("gpt");
        Assert.Throws<ArgumentNullException>(() => builder.WithJsonSchema("Items", null!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void WithJsonSchemaFrom_InvalidName_Throws(string? name)
    {
        var builder = AiRequestBuilder.Create().WithModel("gpt");
        Assert.Throws<ArgumentException>(() => builder.WithJsonSchemaFrom<SampleItem>(name!));
    }
}
