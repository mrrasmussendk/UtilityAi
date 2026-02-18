using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using UtilityAi.Helpers.OpenAiStructuredOutputHelper.DTO;
using UtilityAi.Helpers.OpenAiStructuredOutputHelper.MCP;
using UtilityAi.Helpers.OpenAiStructuredOutputHelper.SchemaGenerator;

namespace UtilityAi.Helpers.OpenAiStructuredOutputHelper;

/// <summary>
/// Fluent builder to assemble the request envelope with minimal boilerplate.
/// </summary>
public sealed class AiRequestBuilder
{
    private object? _model;
    private readonly List<object> _tools = new();
    private readonly List<Message> _messages = new();
    private JsonSchemaFormat? _format;

    private static readonly JsonSerializerOptions _json =
        new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = true
        };

    public static AiRequestBuilder Create() => new();

    public AiRequestBuilder WithModel(object model)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
        return this;
    }

    public AiRequestBuilder AddMcpTool(string label, string serverUrl, IEnumerable<string> allowedTools, string requireApproval = "never")
    {
        if (string.IsNullOrWhiteSpace(label)) throw new ArgumentException("Value cannot be null or whitespace.", nameof(label));
        if (string.IsNullOrWhiteSpace(serverUrl)) throw new ArgumentException("Value cannot be null or whitespace.", nameof(serverUrl));

        _tools.Add(McpTool.Create(label, serverUrl, allowedTools ?? Array.Empty<string>(), requireApproval));
        return this;
    }

    public AiRequestBuilder AddTool(string type)
    {
        if (string.IsNullOrWhiteSpace(type)) throw new ArgumentException("Value cannot be null or whitespace.", nameof(type));
        _tools.Add(SimpleTool.Of(type));
        return this;
    }

    public AiRequestBuilder AddSystem(string content)
    {
        _messages.Add(Message.System(content ?? string.Empty));
        return this;
    }

    public AiRequestBuilder AddUser(string content)
    {
        _messages.Add(Message.User(content ?? string.Empty));
        return this;
    }

    /// <summary>
    /// Provide a JSON Schema node directly.
    /// </summary>
    public AiRequestBuilder WithJsonSchema(string name, JsonObject schema, bool strict = true)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Value cannot be null or whitespace.", nameof(name));
        _format = JsonSchemaFormat.Create(name, schema ?? throw new ArgumentNullException(nameof(schema)), strict);
        return this;
    }

    /// <summary>
    /// Generate a JSON Schema from a .NET type and set it as the "text.format".
    /// Schema is of the shape: { output: array of T }.
    /// </summary>
    public AiRequestBuilder WithJsonSchemaFrom<T>(string name, SchemaGeneratorOptions? options = null, bool strict = true)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Value cannot be null or whitespace.", nameof(name));
        var schema = JsonSchemaGenerator.BuildOutputArraySchemaFrom<T>(options);
        _format = JsonSchemaFormat.Create(name, schema, strict);
        return this;
    }

    public string BuildJson(JsonSerializerOptions? options = null)
    {
        if (_model is null) throw new InvalidOperationException("Model is required.");
        if (_format is null) throw new InvalidOperationException("JSON Schema format is required.");
        if (_messages.Count == 0) throw new InvalidOperationException("At least one message is required.");

        var env = new RequestEnvelope(_model, _tools.ToArray(), _messages.ToArray(), new TextFormat(_format));
        return JsonSerializer.Serialize(env, options ?? _json);
    }
}
