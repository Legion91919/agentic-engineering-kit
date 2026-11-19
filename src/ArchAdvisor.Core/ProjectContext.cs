namespace ArchAdvisor.Core;

/// <summary>Read-only view of a project's files, so checks can be tested without a disk.</summary>
public interface IProjectFiles
{
    bool FileExists(string relativePath);
    bool DirectoryExists(string relativePath);
    IEnumerable<string> ListFiles(string relativeDirectory, string searchPattern = "*");
    string ReadAllText(string relativePath);
}

public sealed class DiskProjectFiles : IProjectFiles
{
    private readonly string _root;

    public DiskProjectFiles(string root)
    {
        _root = Path.GetFullPath(root);
        if (!Directory.Exists(_root)) throw new DirectoryNotFoundException($"Project folder not found: {root}");
    }

    private string Resolve(string relative)
    {
        var full = Path.GetFullPath(Path.Combine(_root, relative));
        var rootWithSep = _root.EndsWith(Path.DirectorySeparatorChar) ? _root : _root + Path.DirectorySeparatorChar;
        if (!full.Equals(_root, StringComparison.OrdinalIgnoreCase) &&
            !full.StartsWith(rootWithSep, StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("Path escapes the project folder.");
        return full;
    }

    public bool FileExists(string relativePath) => File.Exists(Resolve(relativePath));
    public bool DirectoryExists(string relativePath) => Directory.Exists(Resolve(relativePath));
    public string ReadAllText(string relativePath) => File.ReadAllText(Resolve(relativePath));

    public IEnumerable<string> ListFiles(string relativeDirectory, string searchPattern = "*")
    {
        var dir = Resolve(relativeDirectory);
        return Directory.Exists(dir)
            ? Directory.EnumerateFiles(dir, searchPattern, SearchOption.TopDirectoryOnly).Select(p => Path.GetFileName(p))
            : Enumerable.Empty<string>();
    }
}

/// <summary>
/// The hidden-context convention. Every project keeps its knowledge in a hidden
/// <c>.agentic/</c> folder; the root stays clean with a short README and a short AGENTS.md
/// that points into it. Agents read <c>.agentic/README.md</c> first and load only what the
/// task needs, instead of one ever-growing instruction file.
/// </summary>
public static class AgenticContext
{
    public const string Folder = ".agentic";
    public const int MaxAgentsMdLines = 80;

    public static IReadOnlyList<(string Path, string Content)> Scaffold(string projectName, string oneLinePurpose)
    {
        if (string.IsNullOrWhiteSpace(projectName)) throw new ArgumentException("Project name is required.", nameof(projectName));
        var purpose = string.IsNullOrWhiteSpace(oneLinePurpose) ? "TODO: one sentence on what this solves and for whom." : oneLinePurpose.Trim();
        string F(string p) => $"{Folder}/{p}";

        return new List<(string, string)>
        {
            ("AGENTS.md", $"""
                # AGENTS.md

                {projectName}: {purpose}

                ## Read first
                Everything about this project lives in the hidden `{Folder}/` folder.
                Start with `{Folder}/README.md`; it says which file answers which question.
                Load only the files your task needs.

                ## Hard rules
                - Work in thin vertical slices: one ticket from `{Folder}/backlog.md` at a time.
                - No change without a test that fails first. Run the full test suite before you finish.
                - A decision that is hard to reverse gets an ADR in `{Folder}/decisions/`.
                - Never commit secrets, real client data or personal data. Use `.env.example`.
                - Do not grow this file. New knowledge goes in the right `{Folder}/` file; history goes in `{Folder}/changelog.md`.
                - When you learn something that would help on the next project, add it to `{Folder}/lessons.md`.
                """),
            ("CLAUDE.md", "@AGENTS.md\n"),
            (F("README.md"), $"""
                # {projectName}: project map

                | Question | File |
                |---|---|
                | What problem, for whom, what does success look like? | `product.md` |
                | How is it built and why? | `architecture.md`, `decisions/` |
                | What does it look like? | `ux/` (design tokens, screens, flows) |
                | What is being built next? | `backlog.md` |
                | Where did the last session stop? | `next-step.md` |
                | How do we know it works? | `quality.md` |
                | How do I run, deploy and support it? | `runbook.md` |
                | What does the client need to take it over? | `handover.md` |
                | What changed and when? | `changelog.md` |
                | What did we learn? | `lessons.md` |
                """),
            (F("product.md"), """
                # Product brief

                ## Problem
                TODO: the pain, in the client's words.

                ## Users
                TODO: who uses it, how often, what they are trying to get done.

                ## Outcomes
                TODO: measurable results (for example "quarterly report in minutes instead of days").

                ## Constraints
                TODO: regulation, data residency, budget, deadlines, systems we must integrate with.

                ## Out of scope
                TODO: what we are deliberately not doing.

                ## Traits (for the architecture advisor)
                TODO: pick from list_traits, for example MoneyMovement, RegulatoryAudit, DocumentIngestion.
                """),
            (F("architecture.md"), """
                # Architecture

                ## Style and why
                TODO: output of recommend_architecture, plus our judgement.

                ## Context
                ```mermaid
                flowchart LR
                  user[User] --> app[This system]
                  app --> ext[External system]
                ```

                ## Modules and boundaries
                TODO: each module, what it owns, what it may call.

                ## Cross-cutting
                TODO: auth, logging, tracing, configuration, error handling.
                """),
            (F("decisions/.gitkeep"), ""),
            (F("ux/design-tokens.json"), """
                {
                  "color": { "bg": "#ffffff", "surface": "#f6f7f9", "text": "#14171f", "muted": "#5b6474", "accent": "#2f6fed", "danger": "#c4314b", "success": "#1f8a5b" },
                  "font": { "family": "Inter, system-ui, sans-serif", "size": { "sm": "13px", "md": "15px", "lg": "20px", "xl": "28px" } },
                  "space": { "xs": "4px", "sm": "8px", "md": "16px", "lg": "24px", "xl": "40px" },
                  "radius": { "sm": "6px", "md": "10px" }
                }
                """),
            (F("ux/flows.md"), "# User flows\n\nTODO: one numbered flow per key job, each step naming its screen in `screens/`.\n"),
            (F("ux/screens/.gitkeep"), ""),
            (F("backlog.md"), """
                # Backlog

                Vertical slices, smallest valuable first. Each ticket: goal, acceptance criteria, test notes.

                ## Now
                - [ ] TODO

                ## Next

                ## Later
                """),
            (F("next-step.md"), """
                # Next step

                The resume point. Overwrite it at the end of every session so the next agent (or you, tomorrow) starts in seconds.

                - **Doing:** TODO: the slice or task in progress
                - **Done so far:** TODO
                - **Next action:** TODO: one concrete command or edit
                - **Blocked by:** nothing
                """),
            (F("quality.md"), """
                # Quality

                ## Definition of done
                - Acceptance criteria met and demonstrated.
                - Unit tests for rules, integration tests for each adapter, one end-to-end test per key flow.
                - CI green: build, tests, lint, dependency and secret scan.
                - Docs in `.agentic/` updated in the same change.

                ## Service levels
                | SLI | SLO |
                |---|---|
                | API success rate | 99.9% over 30 days |
                | p95 API latency | < 300 ms |
                """),
            (F("runbook.md"), "# Runbook\n\n## Run locally\nTODO\n\n## Deploy\nTODO\n\n## Monitor and alerts\nTODO\n\n## Common incidents\nTODO\n"),
            (F("handover.md"), """
                # Handover

                What the client needs to own this without us.

                - [ ] Source, infrastructure-as-code and pipelines in the client's own accounts
                - [ ] Every secret rotated and owned by the client
                - [ ] Architecture and decisions explained (`architecture.md`, `decisions/`)
                - [ ] Runbook rehearsed by a client engineer
                - [ ] Known issues and next steps listed below
                - [ ] AI agent setup documented so their team can keep using it

                ## Known issues

                ## Recommended next steps
                """),
            (F("changelog.md"), "# Changelog\n\n## Unreleased\n"),
            (F("lessons.md"), "# Lessons\n\nGeneric, reusable lessons only. No client names, data or secrets.\n"),
        };
    }
}
