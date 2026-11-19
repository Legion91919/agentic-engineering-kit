using ArchAdvisor.Core;
using ArchAdvisor.Mcp;

// No arguments: run as an MCP server over stdio (how agents use it).
// With arguments: run as a CLI (how scripts and CI use it).
if (args.Length > 0) return Cli.Run(args, Console.Out, Console.Error);

// stdout is the protocol channel; everything human-readable goes to stderr.
var server = new McpServer(AdvisorTools.Create(), Console.Error);
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

try
{
    await server.RunAsync(Console.In, Console.Out, cts.Token);
}
catch (OperationCanceledException)
{
    // normal shutdown
}
return 0;

internal static class Cli
{
    private const string Usage = """
        arch-advisor                       run as MCP server (stdio)
        arch-advisor traits                list traits
        arch-advisor examples              list the 20 reference systems
        arch-advisor recommend <Trait>...  recommend an architecture
        arch-advisor compare <Trait>...    compare ten candidate designs ("why not X?")
        arch-advisor scaffold <path> <name> [purpose]   create .agentic/ context
        arch-advisor check <path> [--min <score>]       handover readiness (exit 1 if blocked or below min)
        arch-advisor stack <path>                       stack + agent setup report (file names only)
        """;

    public static int Run(string[] args, TextWriter output, TextWriter error)
    {
        var tools = AdvisorTools.Create();
        string Call(string name, System.Text.Json.Nodes.JsonObject a) => tools.Find(name)!.Handler(a);

        try
        {
            switch (args[0])
            {
                case "traits":
                    output.WriteLine(Call("list_traits", new()));
                    return 0;
                case "examples":
                    output.WriteLine(Call("list_reference_examples", new()));
                    return 0;
                case "compare" when args.Length > 1:
                    output.WriteLine(Call("compare_architectures", new()
                    {
                        ["traits"] = new System.Text.Json.Nodes.JsonArray(args.Skip(1).Select(t => (System.Text.Json.Nodes.JsonNode)t).ToArray()),
                    }));
                    return 0;
                case "recommend" when args.Length > 1:
                    output.WriteLine(Call("recommend_architecture", new()
                    {
                        ["traits"] = new System.Text.Json.Nodes.JsonArray(args.Skip(1).Select(t => (System.Text.Json.Nodes.JsonNode)t).ToArray()),
                    }));
                    return 0;
                case "scaffold" when args.Length > 2:
                    output.WriteLine(Call("scaffold_agentic_context", new()
                    {
                        ["projectPath"] = args[1], ["projectName"] = args[2], ["purpose"] = args.Length > 3 ? args[3] : "",
                    }));
                    return 0;
                case "stack" when args.Length > 1:
                    output.WriteLine(Call("detect_stack", new() { ["projectPath"] = args[1] }));
                    return 0;
                case "check" when args.Length > 1:
                    var report = HandoverReadiness.Check(new DiskProjectFiles(args[1]));
                    output.WriteLine(Call("check_handover_readiness", new() { ["projectPath"] = args[1] }));
                    var min = args.Length > 3 && args[2] == "--min" ? int.Parse(args[3], System.Globalization.CultureInfo.InvariantCulture) : 0;
                    return report.ReadyToHandOver && report.Score >= min ? 0 : 1;
                default:
                    error.WriteLine(Usage);
                    return 2;
            }
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or FormatException or UnauthorizedAccessException)
        {
            error.WriteLine(ex.Message);
            return 2;
        }
    }
}
