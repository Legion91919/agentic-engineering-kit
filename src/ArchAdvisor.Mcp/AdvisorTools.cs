using System.Text;
using System.Text.Json.Nodes;
using ArchAdvisor.Core;

namespace ArchAdvisor.Mcp;

/// <summary>The tools this server exposes. Each handler is a thin adapter over ArchAdvisor.Core.</summary>
public static class AdvisorTools
{
    public static ToolRegistry Create(Func<DateOnly>? today = null)
    {
        var clock = today ?? (() => DateOnly.FromDateTime(DateTime.Now));
        var traitNames = Enum.GetNames<Trait>();

        return new ToolRegistry()
            .Add(new ToolDefinition(
                "list_traits",
                "List the project traits the advisor understands, with a one-line meaning each. Call this before recommend_architecture.",
                Schema.Empty(),
                _ => ListTraits()))
            .Add(new ToolDefinition(
                "list_reference_examples",
                "List the 20 real-world reference systems (static site, booking, banking, IoT, SaaS, chat, document extraction, reporting warehouse, piece-by-piece replacement, lending back office) and the architecture that fits each.",
                Schema.Empty(),
                _ => ListExamples()))
            .Add(new ToolDefinition(
                "recommend_architecture",
                "Recommend an architecture style, patterns, risks and evolution triggers from the traits discovery found. Deterministic: same traits, same answer.",
                Schema.Object(new JsonObject
                {
                    ["traits"] = Schema.StrArray("Traits of the problem (see list_traits).", traitNames),
                    ["notes"] = Schema.Str("Optional free-text context to echo back in the result."),
                }, "traits"),
                Recommend))
            .Add(new ToolDefinition(
                "compare_architectures",
                "Assess ten candidate designs (static site, layered, vertical slice, Clean Architecture, modular monolith, modular monolith + CQRS, event sourcing, serverless, microservices, event-driven) against the traits: rating, reasons, pros, cons and when each would be right. Use it to answer 'why not X?' in design docs and reviews.",
                Schema.Object(new JsonObject
                {
                    ["traits"] = Schema.StrArray("Traits of the problem (see list_traits).", traitNames),
                }, "traits"),
                CompareArchitectures))
            .Add(new ToolDefinition(
                "write_adr",
                "Write an Architecture Decision Record into <projectPath>/.agentic/decisions/ with the next free number. Never overwrites.",
                Schema.Object(new JsonObject
                {
                    ["projectPath"] = Schema.Str("Absolute path of the project root."),
                    ["title"] = Schema.Str("Short decision title, e.g. 'Use a modular monolith'."),
                    ["context"] = Schema.Str("The forces at play: why a decision is needed."),
                    ["decision"] = Schema.Str("What we decided, in active voice."),
                    ["alternatives"] = Schema.StrArray("Options considered and why they lost."),
                    ["consequences"] = Schema.StrArray("What becomes easier or harder."),
                }, "projectPath", "title", "context", "decision"),
                args => WriteAdr(args, clock())))
            .Add(new ToolDefinition(
                "scaffold_agentic_context",
                "Create the hidden .agentic/ context folder plus a short AGENTS.md and CLAUDE.md in a project. Skips files that already exist unless overwrite is true.",
                Schema.Object(new JsonObject
                {
                    ["projectPath"] = Schema.Str("Absolute path of an existing project root."),
                    ["projectName"] = Schema.Str("Human name of the project."),
                    ["purpose"] = Schema.Str("One sentence: what it solves and for whom."),
                    ["overwrite"] = Schema.Bool("Replace existing files. Default false."),
                }, "projectPath", "projectName"),
                Scaffold))
            .Add(new ToolDefinition(
                "check_handover_readiness",
                "Score whether a client engineer could take this project over tomorrow: docs, ADRs, tests, CI, secrets hygiene, AGENTS.md size.",
                Schema.Object(new JsonObject
                {
                    ["projectPath"] = Schema.Str("Absolute path of the project root."),
                }, "projectPath"),
                CheckHandover))
            .Add(new ToolDefinition(
                "detect_stack",
                "Report a project's stack (Unity, .NET, Angular, Node, Python, containers, IaC), its agent setup and what is missing. Reads file names only, never contents, so it is safe on private projects.",
                Schema.Object(new JsonObject
                {
                    ["projectPath"] = Schema.Str("Absolute path of the project root."),
                }, "projectPath"),
                DetectStack));
    }

    private static string DetectStack(JsonObject args)
    {
        var root = ProjectRoot(Schema.RequireString(args, "projectPath"));
        var r = StackDetector.Detect(new DiskProjectFiles(root));
        var sb = new StringBuilder("# Stack report\n\n");
        sb.Append("- Stack: ").AppendLine(string.Join(", ", r.Stacks));
        sb.Append("- Agent setup: ").AppendLine(r.AgentSetup.Count == 0 ? "none" : string.Join(", ", r.AgentSetup));
        sb.Append("- Suggested skills: ").AppendLine(string.Join(", ", r.SuggestedSkills));
        if (r.Missing.Count > 0)
        {
            sb.AppendLine().AppendLine("## Missing");
            foreach (var m in r.Missing) sb.Append("- ").AppendLine(m);
        }
        return sb.ToString();
    }

    private static readonly Dictionary<Trait, string> TraitMeaning = new()
    {
        [Trait.StaticContent] = "Same content for every visitor.",
        [Trait.InternalUsersOnly] = "Used by staff of one organisation only.",
        [Trait.ComplexBusinessRules] = "Rules that need careful testing (approvals, pricing, eligibility).",
        [Trait.SeveralBusinessAreas] = "Distinct areas with their own language (catalogue, orders, billing...).",
        [Trait.ReadHeavy] = "Far more reading/searching than writing.",
        [Trait.MoneyMovement] = "Moves or records money.",
        [Trait.RegulatoryAudit] = "Regulators or clients need full history of who did what.",
        [Trait.MediaDelivery] = "Serves large media files.",
        [Trait.LongRunningWorkflows] = "Processes that last days or weeks and wait on others.",
        [Trait.ExternalPartnerIntegrations] = "Talks to outside systems with their own APIs.",
        [Trait.LiveTracking] = "Live positions or status pushed to users.",
        [Trait.LimitedInventory] = "Scarce items that must not be sold twice.",
        [Trait.PersonalData] = "Holds personal data (POPIA/GDPR apply).",
        [Trait.HighVolumeDeviceData] = "Constant stream of device or sensor readings.",
        [Trait.MultiTenant] = "One deployment serves many client companies.",
        [Trait.MillionsOfLiveConnections] = "Very many simultaneous open connections.",
        [Trait.GlobalScale] = "Global traffic that needs independent scaling.",
        [Trait.SmallTeam] = "Built and run by a small team (roughly 1-6 engineers).",
        [Trait.DocumentIngestion] = "Pulls data out of emails, PDFs or scans, often with AI.",
        [Trait.LegacySystems] = "Must integrate with or replace an old system.",
        [Trait.BatchDataFeeds] = "Regular feeds from many sources (files, market data, platforms) to clean and consolidate.",
    };

    private static string ListTraits()
    {
        var sb = new StringBuilder("# Traits\n\n");
        foreach (var t in Enum.GetValues<Trait>())
            sb.Append("- `").Append(t).Append("`: ").AppendLine(TraitMeaning.GetValueOrDefault(t, ""));
        return sb.ToString();
    }

    private static string ListExamples()
    {
        var sb = new StringBuilder("# Reference examples\n\n| # | System | Style | Best fit |\n|---|---|---|---|\n");
        foreach (var e in ReferenceCatalogue.All)
            sb.Append("| ").Append(e.Id).Append(" | ").Append(e.Name).Append(" | ").Append(e.Style).Append(" | ").Append(e.BestFit).AppendLine(" |");
        return sb.ToString();
    }

    private static HashSet<Trait> ParseTraits(JsonObject args)
    {
        var raw = Schema.StringList(args, "traits");
        if (raw.Count == 0) throw new ArgumentException("Give at least one trait. Call list_traits to see them.");
        var traits = new HashSet<Trait>();
        foreach (var r in raw)
        {
            if (!Enum.TryParse<Trait>(r, ignoreCase: true, out var t))
                throw new ArgumentException($"Unknown trait '{r}'. Call list_traits.");
            traits.Add(t);
        }
        return traits;
    }

    internal static string CompareArchitectures(JsonObject args)
    {
        var traits = ParseTraits(args);
        var results = ArchitectureComparison.Compare(new ProjectProfile(traits));
        var sb = new StringBuilder("# Architecture options compared\n\n");
        sb.Append("Traits: ").AppendLine(string.Join(", ", traits.OrderBy(t => t.ToString(), StringComparer.Ordinal))).AppendLine();
        sb.AppendLine("| Option | Fit | Complexity (1-5) | Main reason |").AppendLine("|---|---|---|---|");
        foreach (var a in results)
            sb.Append("| ").Append(a.Candidate.Name).Append(" | ").Append(FitLabel(a.Fit)).Append(" | ")
              .Append(a.Candidate.Complexity).Append(" | ").Append(a.Reasons[0]).AppendLine(" |");
        foreach (var a in results)
        {
            sb.AppendLine().Append("## ").Append(a.Candidate.Name).Append(" (").Append(FitLabel(a.Fit)).AppendLine(")");
            sb.AppendLine(a.Candidate.Summary).AppendLine();
            foreach (var r in a.Reasons) sb.Append("- ").AppendLine(r);
            sb.Append("- Pros: ").AppendLine(string.Join("; ", a.Candidate.Pros));
            sb.Append("- Cons: ").AppendLine(string.Join("; ", a.Candidate.Cons));
            sb.Append("- Choose it when: ").AppendLine(a.Candidate.ChooseWhen);
        }
        return sb.ToString();
    }

    private static string FitLabel(Fit fit) => fit switch
    {
        Fit.Recommended => "Recommended",
        Fit.GoodAlternative => "Good alternative",
        Fit.Possible => "Possible",
        _ => "Poor fit",
    };

    internal static string Recommend(JsonObject args)
    {
        var traits = ParseTraits(args);

        var rec = ArchitectureAdvisor.Recommend(new ProjectProfile(traits, Schema.OptString(args, "notes")));
        var sb = new StringBuilder();
        sb.Append("# Recommendation: ").AppendLine(rec.Style.ToString()).AppendLine();
        sb.AppendLine(rec.StyleReason).AppendLine();
        if (Schema.OptString(args, "notes") is { Length: > 0 } notes) sb.Append("Notes: ").AppendLine(notes).AppendLine();

        sb.AppendLine("## Patterns");
        foreach (var p in rec.Patterns) sb.Append("- **").Append(p.Pattern).Append("**: ").AppendLine(p.Reason);
        if (rec.Patterns.Count == 0) sb.AppendLine("- None beyond the style itself.");

        sb.AppendLine().AppendLine("## Closest reference systems");
        foreach (var c in rec.ClosestExamples)
            sb.Append("- ").Append(c.Example.Name).Append(" (similarity ").Append(c.Similarity.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture))
              .Append("): ").AppendLine(c.Example.BestFit);

        sb.AppendLine().AppendLine("## Risks to watch");
        foreach (var r in rec.Risks) sb.Append("- ").AppendLine(r);

        sb.AppendLine().AppendLine("## Evolve when");
        foreach (var e in rec.EvolveWhen) sb.Append("- ").AppendLine(e);

        sb.AppendLine().AppendLine("Record the final choice with write_adr, including what you rejected.");
        return sb.ToString();
    }

    private static string WriteAdr(JsonObject args, DateOnly date)
    {
        var root = ProjectRoot(Schema.RequireString(args, "projectPath"));
        var decisions = Path.Combine(root, AgenticContext.Folder, "decisions");
        Directory.CreateDirectory(decisions);

        var input = new AdrInput(
            Schema.RequireString(args, "title"),
            Schema.RequireString(args, "context"),
            Schema.RequireString(args, "decision"),
            Schema.StringList(args, "alternatives"),
            Schema.StringList(args, "consequences"));

        var number = AdrWriter.NextNumber(Directory.EnumerateFiles(decisions, "*.md"));
        var path = Path.Combine(decisions, AdrWriter.FileName(number, input.Title));
        using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write))
        using (var writer = new StreamWriter(stream))
        {
            writer.Write(AdrWriter.Render(number, input, date));
        }
        return $"Wrote {Path.GetRelativePath(root, path)}";
    }

    private static string Scaffold(JsonObject args)
    {
        var root = ProjectRoot(Schema.RequireString(args, "projectPath"));
        var overwrite = Schema.OptBool(args, "overwrite");
        var files = AgenticContext.Scaffold(Schema.RequireString(args, "projectName"), Schema.OptString(args, "purpose") ?? "");
        var guard = new DiskProjectFiles(root); // validates paths stay inside root

        var written = new List<string>();
        var skipped = new List<string>();
        foreach (var (relative, content) in files)
        {
            _ = guard.FileExists(relative);
            var full = Path.Combine(root, relative);
            if (File.Exists(full) && !overwrite) { skipped.Add(relative); continue; }
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, content);
            written.Add(relative);
        }

        var sb = new StringBuilder();
        sb.Append("Wrote ").Append(written.Count).AppendLine(" file(s):");
        foreach (var w in written) sb.Append("- ").AppendLine(w);
        if (skipped.Count > 0)
        {
            sb.Append("Skipped ").Append(skipped.Count).AppendLine(" existing file(s) (pass overwrite=true to replace):");
            foreach (var s in skipped) sb.Append("- ").AppendLine(s);
        }
        sb.AppendLine("On Windows, hide the folder with: attrib +h .agentic");
        return sb.ToString();
    }

    private static string CheckHandover(JsonObject args)
    {
        var root = ProjectRoot(Schema.RequireString(args, "projectPath"));
        var report = HandoverReadiness.Check(new DiskProjectFiles(root));
        var sb = new StringBuilder();
        sb.Append("# Handover readiness: ").Append(report.Score).Append("% ")
          .AppendLine(report.ReadyToHandOver ? "(no blockers)" : "(blocked)").AppendLine();
        foreach (var c in report.Checks)
        {
            var icon = c.Result switch { CheckResult.Pass => "PASS", CheckResult.Warn => "WARN", _ => "FAIL" };
            sb.Append("- [").Append(icon).Append("] ").Append(c.Name).Append(": ").AppendLine(c.Detail);
        }
        return sb.ToString();
    }

    private static string ProjectRoot(string path)
    {
        var full = Path.GetFullPath(path);
        if (!Directory.Exists(full)) throw new ArgumentException($"Folder not found: {path}");
        return full;
    }
}
