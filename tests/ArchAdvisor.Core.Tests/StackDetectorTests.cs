using ArchAdvisor.Core;
using Xunit;

namespace ArchAdvisor.Core.Tests;

public class StackDetectorTests
{
    [Fact]
    public void Unity_project_with_python_tools_is_detected()
    {
        var f = new FakeFiles();
        f.Files["ProjectSettings/ProjectVersion.txt"] = "m_EditorVersion: 6000.0";
        f.Files["Tools/make_art.py"] = "";
        f.Files["AGENTS.md"] = "# rules";

        var r = StackDetector.Detect(f);

        Assert.Contains("Unity", r.Stacks);
        Assert.Contains("Python", r.Stacks);
        Assert.Contains("AGENTS.md", r.AgentSetup);
        Assert.Contains("unity-game", r.SuggestedSkills);
        Assert.Contains(r.Missing, m => m.StartsWith(".agentic/", StringComparison.Ordinal));
    }

    [Fact]
    public void Dotnet_and_angular_repo_with_full_agent_setup_has_nothing_missing()
    {
        var f = new FakeFiles();
        f.Files["Portal.sln"] = "";
        f.Files["angular.json"] = "{}";
        f.Files["AGENTS.md"] = "";
        f.Files[".gitignore"] = "";
        f.Files[".agentic/README.md"] = "";
        f.Files[".github/workflows/ci.yml"] = "";
        f.Files["tests/Portal.Tests/x.cs"] = "";

        var r = StackDetector.Detect(f);

        Assert.Equal(new[] { ".NET", "Angular" }, r.Stacks);
        Assert.Empty(r.Missing);
    }

    [Fact]
    public void Empty_folder_reports_unknown_stack()
    {
        var r = StackDetector.Detect(new FakeFiles());
        Assert.Single(r.Stacks);
        Assert.StartsWith("Unknown", r.Stacks[0], StringComparison.Ordinal);
    }

    [Fact]
    public void Expo_firebase_monorepo_with_only_claude_md_is_told_to_add_agents_md()
    {
        var f = new FakeFiles();
        f.Files["package.json"] = "{}";
        f.Files["firebase.json"] = "{}";
        f.Files["apps/mobile/app.json"] = "{}";
        f.Files["libs/chat/index.ts"] = "";
        f.Files["CLAUDE.md"] = "# rules";
        f.Files[".claude/commands/blueprint.md"] = "";

        var r = StackDetector.Detect(f);

        Assert.Contains("Expo / React Native", r.Stacks);
        Assert.Contains("Firebase", r.Stacks);
        Assert.Contains("Monorepo", r.Stacks);
        Assert.Contains("build-mobile-firebase", r.SuggestedSkills);
        Assert.Contains(".claude/commands/", r.AgentSetup);
        Assert.Contains(r.Missing, m => m.StartsWith("AGENTS.md (CLAUDE.md alone", StringComparison.Ordinal));
    }
}
