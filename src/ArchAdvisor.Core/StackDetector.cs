namespace ArchAdvisor.Core;

/// <summary>What a project is built with and how agent-ready it is. Facts only, never file contents.</summary>
public sealed record StackReport(
    IReadOnlyList<string> Stacks,
    IReadOnlyList<string> AgentSetup,
    IReadOnlyList<string> Missing,
    IReadOnlyList<string> SuggestedSkills);

/// <summary>
/// Looks at marker files in a project root and reports the stack and agent setup.
/// It reads file names only, never contents, so it is safe to run over private projects:
/// the report says "Unity game with Python tools", not what the game is.
/// </summary>
public static class StackDetector
{
    private static readonly string[] TestFolders = { "tests", "test", "Tests", "Assets/Tests" };

    public static StackReport Detect(IProjectFiles files)
    {
        ArgumentNullException.ThrowIfNull(files);
        var root = files.ListFiles(".").ToList();
        bool Has(string name) => root.Contains(name, StringComparer.OrdinalIgnoreCase);
        bool Any(string ext) => root.Any(f => f.EndsWith(ext, StringComparison.OrdinalIgnoreCase));

        var stacks = new List<string>();
        var skills = new List<string>();

        if (files.FileExists("ProjectSettings/ProjectVersion.txt"))
        {
            stacks.Add("Unity");
            skills.Add("unity-game");
        }
        if (Any(".sln") || Any(".csproj") || (files.DirectoryExists("src") && files.ListFiles("src").Any(f => f.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))))
        {
            stacks.Add(".NET");
            skills.Add("build-dotnet");
        }
        if (Has("angular.json")) { stacks.Add("Angular"); skills.Add("build-web"); }
        if (Has("package.json") && !Has("angular.json"))
        {
            stacks.Add(root.Any(f => f.StartsWith("vite.config", StringComparison.OrdinalIgnoreCase)) ? "Node (Vite)" : "Node");
            skills.Add("build-web");
        }
        if (Has("pyproject.toml") || Has("requirements.txt") || Any(".py") || files.ListFiles("Tools", "*.py").Any())
            stacks.Add("Python");
        if (Has("app.json") || files.FileExists("apps/mobile/app.json")) stacks.Add("Expo / React Native");
        if (Has("firebase.json")) { stacks.Add("Firebase"); skills.Add("build-mobile-firebase"); }
        if (files.DirectoryExists("apps") && (files.DirectoryExists("libs") || files.DirectoryExists("packages"))) stacks.Add("Monorepo");
        if (Has("Dockerfile") || Has("docker-compose.yml") || Has("compose.yaml")) stacks.Add("Containers");
        if (files.DirectoryExists("infra") || Any(".bicep") || Any(".tf")) stacks.Add("Infrastructure as code");

        var agent = new List<string>();
        if (Has("AGENTS.md")) agent.Add("AGENTS.md");
        if (Has("CLAUDE.md")) agent.Add("CLAUDE.md");
        if (files.DirectoryExists(AgenticContext.Folder)) agent.Add($"{AgenticContext.Folder}/");
        if (files.DirectoryExists(".claude")) agent.Add(".claude/");
        if (Has(".mcp.json")) agent.Add(".mcp.json");
        if (files.FileExists(".github/copilot-instructions.md")) agent.Add(".github/copilot-instructions.md");
        if (files.DirectoryExists(".claude/commands")) agent.Add(".claude/commands/");

        var missing = new List<string>();
        if (!files.DirectoryExists(AgenticContext.Folder)) missing.Add($"{AgenticContext.Folder}/ hidden context (run scaffold_agentic_context)");
        if (!Has("AGENTS.md") && !Has("CLAUDE.md")) missing.Add("AGENTS.md");
        if (!files.DirectoryExists(".github/workflows") && !Has("azure-pipelines.yml")) missing.Add("CI pipeline");
        if (!TestFolders.Any(files.DirectoryExists)) missing.Add("automated tests folder");
        if (!Has(".gitignore")) missing.Add(".gitignore");
        if (Has("CLAUDE.md") && !Has("AGENTS.md")) missing.Add("AGENTS.md (CLAUDE.md alone is invisible to Copilot, Cursor and Codex)");

        skills.Add("prove-it");
        if (stacks.Count == 0) stacks.Add("Unknown (no marker files in root)");

        return new StackReport(stacks, agent, missing, skills.Distinct().ToList());
    }
}
