using ArchAdvisor.Core;
using Xunit;

namespace ArchAdvisor.Core.Tests;

public class AdrWriterTests
{
    [Fact]
    public void Next_number_follows_the_highest_existing_one()
    {
        var existing = new[] { "0001-use-postgres.md", "0003-modular-monolith.md", "notes.md" };
        Assert.Equal(4, AdrWriter.NextNumber(existing));
    }

    [Fact]
    public void First_adr_is_number_one()
    {
        Assert.Equal(1, AdrWriter.NextNumber(Array.Empty<string>()));
    }

    [Fact]
    public void File_name_is_numbered_and_slugged()
    {
        Assert.Equal("0007-use-an-append-only-ledger-for-cash.md", AdrWriter.FileName(7, "Use an append-only ledger for cash!"));
    }

    [Fact]
    public void Render_contains_every_section()
    {
        var md = AdrWriter.Render(2, new AdrInput(
            "Use a modular monolith",
            "Three business areas, one small team.",
            "We build one deployable with strict modules.",
            new[] { "Microservices: too much operations work for the team." },
            new[] { "Module boundaries must be enforced by tests." }), new DateOnly(2026, 11, 19));

        Assert.StartsWith("# 0002. Use a modular monolith", md, StringComparison.Ordinal);
        Assert.Contains("- Date: 2026-11-19", md, StringComparison.Ordinal);
        Assert.Contains("## Alternatives considered", md, StringComparison.Ordinal);
        Assert.Contains("Microservices: too much", md, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_refuses_an_adr_without_a_decision()
    {
        Assert.Throws<ArgumentException>(() =>
            AdrWriter.Render(1, new AdrInput("Title", "ctx", " ", Array.Empty<string>(), Array.Empty<string>()), new DateOnly(2026, 1, 1)));
    }
}

public class HandoverReadinessTests
{
    private static FakeFiles FullyReadyProject()
    {
        var f = new FakeFiles();
        foreach (var (path, content) in AgenticContext.Scaffold("Demo", "Shows the check."))
            f.Files[path] = content.Replace("TODO", "Done", StringComparison.Ordinal).Replace("- [ ]", "- [x]", StringComparison.Ordinal);
        f.Files["README.md"] = "# Demo";
        f.Files[".agentic/decisions/0001-style.md"] = "# 0001";
        f.Files["tests/DemoTests.cs"] = "";
        f.Files[".github/workflows/ci.yml"] = "";
        return f;
    }

    [Fact]
    public void Scaffolded_and_completed_project_is_ready()
    {
        var report = HandoverReadiness.Check(FullyReadyProject());

        Assert.True(report.ReadyToHandOver, string.Join("\n", report.Checks.Select(c => $"{c.Result} {c.Name} {c.Detail}")));
        Assert.Equal(100, report.Score);
    }

    [Fact]
    public void Empty_project_is_blocked()
    {
        var report = HandoverReadiness.Check(new FakeFiles());
        Assert.False(report.ReadyToHandOver);
        Assert.True(report.Score < 30);
    }

    [Fact]
    public void Long_agents_md_is_a_warning_not_a_blocker()
    {
        var f = FullyReadyProject();
        f.Files["AGENTS.md"] = string.Join("\n", Enumerable.Repeat("rule", 500));

        var report = HandoverReadiness.Check(f);
        var check = report.Checks.Single(c => c.Name == "AGENTS.md");

        Assert.Equal(CheckResult.Warn, check.Result);
        Assert.True(report.ReadyToHandOver);
    }

    [Fact]
    public void Env_file_without_example_blocks_handover()
    {
        var f = FullyReadyProject();
        f.Files[".env"] = "API_KEY=secret";
        f.Files.Remove(".env.example");

        Assert.False(HandoverReadiness.Check(f).ReadyToHandOver);
    }

    [Fact]
    public void Freshly_scaffolded_project_warns_about_todos_and_open_items()
    {
        var f = new FakeFiles();
        foreach (var (path, content) in AgenticContext.Scaffold("Demo", ""))
            f.Files[path] = content;

        var report = HandoverReadiness.Check(f);
        Assert.Equal(CheckResult.Warn, report.Checks.Single(c => c.Name == "Docs filled in").Result);
        Assert.Equal(CheckResult.Warn, report.Checks.Single(c => c.Name == "Handover checklist").Result);
    }

    [Fact]
    public void Scaffold_keeps_agents_md_short()
    {
        var agents = AgenticContext.Scaffold("Demo", "x").Single(f => f.Path == "AGENTS.md").Content;
        Assert.True(agents.Split('\n').Length <= AgenticContext.MaxAgentsMdLines);
        Assert.Contains(".agentic/README.md", agents, StringComparison.Ordinal);
    }
}

/// <summary>In-memory project for tests.</summary>
internal sealed class FakeFiles : IProjectFiles
{
    public Dictionary<string, string> Files { get; } = new(StringComparer.Ordinal);

    public bool FileExists(string relativePath) => Files.ContainsKey(relativePath);

    public bool DirectoryExists(string relativePath) =>
        Files.Keys.Any(k => k.StartsWith(relativePath.TrimEnd('/') + "/", StringComparison.Ordinal));

    public IEnumerable<string> ListFiles(string relativeDirectory, string searchPattern = "*")
    {
        var dir = relativeDirectory is "." or "" ? "" : relativeDirectory.TrimEnd('/');
        var prefix = dir.Length == 0 ? "" : dir + "/";
        var ext = searchPattern.StartsWith("*.", StringComparison.Ordinal) ? searchPattern[1..] : null;
        return Files.Keys
            .Where(k => k.StartsWith(prefix, StringComparison.Ordinal) && !k[prefix.Length..].Contains('/', StringComparison.Ordinal))
            .Select(k => k[prefix.Length..])
            .Where(n => ext is null || n.EndsWith(ext, StringComparison.Ordinal));
    }

    public string ReadAllText(string relativePath) => Files[relativePath];
}
