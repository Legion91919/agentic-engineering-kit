using System.Text.Json.Nodes;

namespace ArchAdvisor.Mcp;

public sealed record ToolDefinition(string Name, string Description, JsonObject InputSchema, Func<JsonObject, string> Handler);

public sealed class ToolRegistry
{
    private readonly Dictionary<string, ToolDefinition> _tools = new(StringComparer.Ordinal);

    public ToolRegistry Add(ToolDefinition tool)
    {
        _tools.Add(tool.Name, tool);
        return this;
    }

    public ToolDefinition? Find(string name) => _tools.GetValueOrDefault(name);

    public JsonArray Describe()
    {
        var array = new JsonArray();
        foreach (var t in _tools.Values)
        {
            array.Add(new JsonObject
            {
                ["name"] = t.Name,
                ["description"] = t.Description,
                ["inputSchema"] = t.InputSchema.DeepClone(),
            });
        }
        return array;
    }
}

/// <summary>Small helpers for building JSON Schemas and reading arguments.</summary>
internal static class Schema
{
    public static JsonObject Object(JsonObject properties, params string[] required) => new()
    {
        ["type"] = "object",
        ["properties"] = properties,
        ["required"] = new JsonArray(required.Select(r => (JsonNode)r).ToArray()),
        ["additionalProperties"] = false,
    };

    public static JsonObject Empty() => Object(new JsonObject());

    public static JsonObject Str(string description) => new() { ["type"] = "string", ["description"] = description };
    public static JsonObject Bool(string description) => new() { ["type"] = "boolean", ["description"] = description };

    public static JsonObject StrArray(string description, IEnumerable<string>? allowed = null)
    {
        var items = new JsonObject { ["type"] = "string" };
        if (allowed is not null) items["enum"] = new JsonArray(allowed.Select(a => (JsonNode)a).ToArray());
        return new JsonObject { ["type"] = "array", ["items"] = items, ["description"] = description };
    }

    public static string RequireString(JsonObject args, string name)
    {
        var value = args[name]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException($"'{name}' is required.");
        return value;
    }

    public static string? OptString(JsonObject args, string name) => args[name]?.GetValue<string>();
    public static bool OptBool(JsonObject args, string name) => args[name]?.GetValue<bool>() ?? false;

    public static List<string> StringList(JsonObject args, string name) =>
        (args[name] as JsonArray)?.Select(n => n!.GetValue<string>()).ToList() ?? new List<string>();
}
