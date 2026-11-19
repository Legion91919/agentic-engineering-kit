using System.Text.RegularExpressions;

namespace ArchAdvisor.Core;

public enum CheckResult { Pass, Warn, Fail }

public sealed record ReadinessCheck(string Name, CheckResult Result, string Detail);

public sealed record ReadinessReport(IReadOnlyList<ReadinessCheck> Checks)
{
    public int Score => Checks.Count == 0 ? 0 :
        (int)Math.Round(100.0 * Checks.Sum(c => c.Result switch { CheckResult.Pass => 1.0, CheckResult.Warn => 0.5, _ => 0 }) / Checks.Count);
    public bool ReadyToHandOver => Checks.All(c => c.Result != CheckResult.Fail);
}

/// <summary>
/// Answers one question: could a client engineer take this project over tomorrow?
/// Checks structure and hygiene only; it never judges code quality, which is the reviewer's job.
/// </summary>
public static partial class HandoverReadiness
{
    public static ReadinessReport Check(IProjectFiles files)
    {
        var checks = new List<ReadinessCheck>
        {
            Exists(files, "README.md", "Root README", fail: true),
            AgentsMd(files),
            Dir(files, AgenticContext.Folder, "Hidden context folder"),
            Exists(files, $"{AgenticContext.Folder}/product.md", "Product brief", fail: true),
            Exists(files, $"{AgenticContext.Folder}/architecture.md", "Architecture doc", fail: true),
            Adrs(files),
            Exists(files, $"{AgenticContext.Folder}/runbook.md", "Runbook", fail: true),
            HandoverDoc(files),
            Tests(files),
            Ci(files),
            EnvExample(files),
            NoTodoLeft(files),
        };
        return new ReadinessReport(checks);
    }

    private static ReadinessCheck Exists(IProjectFiles f, string path, string name, bool fail) =>
        f.FileExists(path)
            ? new(name, CheckResult.Pass, path)
            : new(name, fail ? CheckResult.Fail : CheckResult.Warn, $"Missing {path}");

    private static ReadinessCheck Dir(IProjectFiles f, string path, string name) =>
        f.DirectoryExists(path) ? new(name, CheckResult.Pass, path) : new(name, CheckResult.Fail, $"Missing {path}/");

    private static ReadinessCheck AgentsMd(IProjectFiles f)
    {
        if (!f.FileExists("AGENTS.md")) return new("AGENTS.md", CheckResult.Fail, "Missing AGENTS.md (agents will not know the rules).");
        var lines = f.ReadAllText("AGENTS.md").Split('\n').Length;
        return lines <= AgenticContext.MaxAgentsMdLines
            ? new("AGENTS.md", CheckResult.Pass, $"{lines} lines")
            : new("AGENTS.md", CheckResult.Warn,
                $"{lines} lines. Keep it under {AgenticContext.MaxAgentsMdLines}; move detail into .agentic/ and history into changelog.md.");
    }

    private static ReadinessCheck Adrs(IProjectFiles f)
    {
        var count = f.ListFiles($"{AgenticContext.Folder}/decisions", "*.md").Count();
        return count > 0
            ? new("Decision records", CheckResult.Pass, $"{count} ADR(s)")
            : new("Decision records", CheckResult.Warn, "No ADRs. Record at least the architecture style decision.");
    }

    private static ReadinessCheck HandoverDoc(IProjectFiles f)
    {
        var path = $"{AgenticContext.Folder}/handover.md";
        if (!f.FileExists(path)) return new("Handover checklist", CheckResult.Fail, $"Missing {path}");
        var text = f.ReadAllText(path);
        int open = OpenBox().Count(text), done = DoneBox().Count(text);
        if (open + done == 0) return new("Handover checklist", CheckResult.Warn, "No checklist items found.");
        return open == 0
            ? new("Handover checklist", CheckResult.Pass, $"{done}/{done} done")
            : new("Handover checklist", CheckResult.Warn, $"{done}/{open + done} done");
    }

    private static ReadinessCheck Tests(IProjectFiles f)
    {
        string[] candidates = { "tests", "test", "src/tests", "e2e" };
        var found = candidates.FirstOrDefault(f.DirectoryExists);
        return found is null
            ? new("Automated tests", CheckResult.Fail, "No tests/ folder found.")
            : new("Automated tests", CheckResult.Pass, found);
    }

    private static ReadinessCheck Ci(IProjectFiles f)
    {
        var workflows = f.ListFiles(".github/workflows", "*.yml").Concat(f.ListFiles(".github/workflows", "*.yaml")).ToList();
        if (workflows.Count > 0) return new("CI pipeline", CheckResult.Pass, string.Join(", ", workflows));
        return f.FileExists("azure-pipelines.yml")
            ? new("CI pipeline", CheckResult.Pass, "azure-pipelines.yml")
            : new("CI pipeline", CheckResult.Fail, "No .github/workflows or azure-pipelines.yml.");
    }

    private static ReadinessCheck EnvExample(IProjectFiles f)
    {
        if (f.FileExists(".env") && !f.FileExists(".env.example"))
            return new("Secrets hygiene", CheckResult.Fail, ".env present without .env.example; make sure .env is git-ignored and document keys in .env.example.");
        return new("Secrets hygiene", CheckResult.Pass, f.FileExists(".env.example") ? ".env.example documents settings" : "No .env files");
    }

    private static ReadinessCheck NoTodoLeft(IProjectFiles f)
    {
        string[] docs = { "product.md", "architecture.md", "runbook.md" };
        var withTodo = docs.Where(d => f.FileExists($"{AgenticContext.Folder}/{d}") &&
                                       f.ReadAllText($"{AgenticContext.Folder}/{d}").Contains("TODO", StringComparison.Ordinal)).ToList();
        return withTodo.Count == 0
            ? new("Docs filled in", CheckResult.Pass, "No TODO markers in core docs")
            : new("Docs filled in", CheckResult.Warn, $"TODO left in: {string.Join(", ", withTodo)}");
    }

    [GeneratedRegex(@"^\s*- \[ \]", RegexOptions.Multiline)]
    private static partial Regex OpenBox();

    [GeneratedRegex(@"^\s*- \[[xX]\]", RegexOptions.Multiline)]
    private static partial Regex DoneBox();
}
