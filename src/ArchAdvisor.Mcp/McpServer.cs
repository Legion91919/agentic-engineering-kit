using System.Text.Json;
using System.Text.Json.Nodes;

namespace ArchAdvisor.Mcp;

/// <summary>
/// A minimal Model Context Protocol server over stdio (newline-delimited JSON-RPC 2.0).
/// Hand-rolled on purpose: zero dependencies, ~150 lines, and every message the protocol
/// needs for tools is visible here. Swap in the official ModelContextProtocol NuGet package
/// when you want resources, prompts or HTTP transport.
/// </summary>
public sealed class McpServer
{
    public const string ProtocolVersion = "2025-06-18";
    private readonly ToolRegistry _tools;
    private readonly TextWriter _log;

    public McpServer(ToolRegistry tools, TextWriter log)
    {
        _tools = tools;
        _log = log;
    }

    public async Task RunAsync(TextReader input, TextWriter output, CancellationToken ct = default)
    {
        string? line;
        while (!ct.IsCancellationRequested && (line = await input.ReadLineAsync(ct)) is not null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var response = Handle(line);
            if (response is null) continue; // notifications get no reply
            await output.WriteLineAsync(response.ToJsonString());
            await output.FlushAsync(ct);
        }
    }

    /// <summary>Handles one JSON-RPC message. Returns null for notifications.</summary>
    public JsonObject? Handle(string json)
    {
        JsonObject? request;
        try
        {
            request = JsonNode.Parse(json) as JsonObject;
        }
        catch (JsonException)
        {
            return Error(null, -32700, "Parse error");
        }
        if (request is null) return Error(null, -32600, "Invalid request");

        var id = request["id"]?.DeepClone();
        var method = request["method"]?.GetValue<string>();
        var @params = request["params"] as JsonObject ?? new JsonObject();

        if (id is null) // notification, e.g. notifications/initialized
        {
            _log.WriteLine($"notification: {method}");
            return null;
        }

        try
        {
            return method switch
            {
                "initialize" => Result(id, Initialize()),
                "ping" => Result(id, new JsonObject()),
                "tools/list" => Result(id, new JsonObject { ["tools"] = _tools.Describe() }),
                "tools/call" => Result(id, CallTool(@params)),
                _ => Error(id, -32601, $"Method not found: {method}"),
            };
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or JsonException)
        {
            _log.WriteLine($"error in {method}: {ex.Message}");
            return Error(id, -32602, ex.Message);
        }
    }

    private static JsonObject Initialize() => new()
    {
        ["protocolVersion"] = ProtocolVersion,
        ["capabilities"] = new JsonObject { ["tools"] = new JsonObject { ["listChanged"] = false } },
        ["serverInfo"] = new JsonObject { ["name"] = "arch-advisor", ["version"] = typeof(McpServer).Assembly.GetName().Version?.ToString(3) ?? "0.0.0" },
        ["instructions"] = "Architecture advisor for agent-built software. Call list_traits, then recommend_architecture with the traits discovery found. Use scaffold_agentic_context to set up a project's hidden .agentic/ folder, write_adr to record decisions and check_handover_readiness before handing a project to a client.",
    };

    private JsonObject CallTool(JsonObject @params)
    {
        var name = @params["name"]?.GetValue<string>() ?? throw new ArgumentException("Missing tool name.");
        var args = @params["arguments"] as JsonObject ?? new JsonObject();
        var tool = _tools.Find(name) ?? throw new ArgumentException($"Unknown tool: {name}");

        try
        {
            var text = tool.Handler(args);
            return ToolContent(text, isError: false);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            // Tool failures are reported inside the result so the model can see and correct them.
            return ToolContent(ex.Message, isError: true);
        }
    }

    private static JsonObject ToolContent(string text, bool isError) => new()
    {
        ["content"] = new JsonArray(new JsonObject { ["type"] = "text", ["text"] = text }),
        ["isError"] = isError,
    };

    private static JsonObject Result(JsonNode id, JsonNode result) =>
        new() { ["jsonrpc"] = "2.0", ["id"] = id, ["result"] = result };

    private static JsonObject Error(JsonNode? id, int code, string message) =>
        new() { ["jsonrpc"] = "2.0", ["id"] = id, ["error"] = new JsonObject { ["code"] = code, ["message"] = message } };
}
