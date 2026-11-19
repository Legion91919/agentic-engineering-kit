using System.Text.Json.Nodes;
using ArchAdvisor.Mcp;
using Xunit;

namespace ArchAdvisor.Core.Tests;

public class McpProtocolTests
{
    private static McpServer NewServer() => new(AdvisorTools.Create(() => new DateOnly(2026, 11, 19)), TextWriter.Null);

    [Fact]
    public void Initialize_reports_protocol_version_and_tools_capability()
    {
        var res = NewServer().Handle("""{"jsonrpc":"2.0","id":1,"method":"initialize","params":{}}""")!;
        Assert.Equal(McpServer.ProtocolVersion, res["result"]!["protocolVersion"]!.GetValue<string>());
        Assert.NotNull(res["result"]!["capabilities"]!["tools"]);
    }

    [Fact]
    public void Notifications_get_no_reply()
    {
        Assert.Null(NewServer().Handle("""{"jsonrpc":"2.0","method":"notifications/initialized"}"""));
    }

    [Fact]
    public void Tools_list_includes_every_tool_with_a_schema()
    {
        var res = NewServer().Handle("""{"jsonrpc":"2.0","id":2,"method":"tools/list"}""")!;
        var tools = res["result"]!["tools"]!.AsArray();
        var names = tools.Select(t => t!["name"]!.GetValue<string>()).ToList();

        Assert.Contains("recommend_architecture", names);
        Assert.Contains("check_handover_readiness", names);
        Assert.All(tools, t => Assert.Equal("object", t!["inputSchema"]!["type"]!.GetValue<string>()));
    }

    [Fact]
    public void Recommend_tool_returns_markdown()
    {
        var res = NewServer().Handle("""
            {"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"recommend_architecture","arguments":{"traits":["MoneyMovement","SeveralBusinessAreas"]}}}
            """)!;
        var text = res["result"]!["content"]![0]!["text"]!.GetValue<string>();

        Assert.False(res["result"]!["isError"]!.GetValue<bool>());
        Assert.Contains("ModularMonolith", text, StringComparison.Ordinal);
        Assert.Contains("AppendOnlyLedger", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Unknown_trait_is_a_tool_error_the_model_can_read()
    {
        var res = NewServer().Handle("""
            {"jsonrpc":"2.0","id":4,"method":"tools/call","params":{"name":"recommend_architecture","arguments":{"traits":["Blockchain"]}}}
            """)!;
        Assert.True(res["result"]!["isError"]!.GetValue<bool>());
        Assert.Contains("list_traits", res["result"]!["content"]![0]!["text"]!.GetValue<string>(), StringComparison.Ordinal);
    }

    [Fact]
    public void Unknown_method_is_a_json_rpc_error()
    {
        var res = NewServer().Handle("""{"jsonrpc":"2.0","id":5,"method":"resources/list"}""")!;
        Assert.Equal(-32601, res["error"]!["code"]!.GetValue<int>());
    }

    [Fact]
    public void Malformed_json_is_a_parse_error()
    {
        var res = NewServer().Handle("{not json")!;
        Assert.Equal(-32700, res["error"]!["code"]!.GetValue<int>());
    }

    [Fact]
    public void Scaffold_then_adr_then_check_works_end_to_end_on_disk()
    {
        var dir = Directory.CreateTempSubdirectory("arch-advisor-").FullName;
        try
        {
            var server = NewServer();
            string Call(string name, JsonObject args)
            {
                var req = new JsonObject
                {
                    ["jsonrpc"] = "2.0", ["id"] = 9, ["method"] = "tools/call",
                    ["params"] = new JsonObject { ["name"] = name, ["arguments"] = args },
                };
                var res = server.Handle(req.ToJsonString())!;
                Assert.False(res["result"]!["isError"]!.GetValue<bool>(), res.ToJsonString());
                return res["result"]!["content"]![0]!["text"]!.GetValue<string>();
            }

            Call("scaffold_agentic_context", new JsonObject { ["projectPath"] = dir, ["projectName"] = "Demo" });
            Assert.True(File.Exists(Path.Combine(dir, ".agentic", "product.md")));

            var second = Call("scaffold_agentic_context", new JsonObject { ["projectPath"] = dir, ["projectName"] = "Demo" });
            Assert.Contains("Skipped", second, StringComparison.Ordinal);

            var adr = Call("write_adr", new JsonObject
            {
                ["projectPath"] = dir, ["title"] = "Use a modular monolith",
                ["context"] = "ctx", ["decision"] = "We will.",
            });
            Assert.Contains("0001-use-a-modular-monolith.md", adr, StringComparison.Ordinal);

            var check = Call("check_handover_readiness", new JsonObject { ["projectPath"] = dir });
            Assert.Contains("Handover readiness", check, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
