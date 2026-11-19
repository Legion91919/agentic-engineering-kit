# AGENTS.md

Agentic Engineering Kit: reusable skills, subagents and MCP tools for delivering software with AI agents, from client brief to handover.

## Read first
Project knowledge lives in the hidden `.agentic/` folder. Start with `.agentic/README.md` and load only what your task needs.
Skills are in `.claude/skills/<name>/SKILL.md`; subagents in `.claude/agents/`. Other agent tools can read them as plain Markdown.

## Hard rules
- This is a **public** repo. Never add private project names, client data, personal data or secrets. The privacy guard enforces this; don't bypass it.
- C#: .NET 8, nullable on, warnings are errors, analyzers on. `ArchAdvisor.Core` stays dependency-free (no NuGet packages).
- Python tools use the standard library only, so hooks run on any machine.
- Test-first. C#: `dotnet test`. Python: `python -m unittest discover -s tests/tools`.
- Keep the advisor deterministic: same traits → same answer. A new reference system needs a test proving the advisor agrees with it (or a comment saying why not).
- A new skill needs a `name` and a `description` that says *when* to use it. Keep each SKILL.md under ~150 lines.
- Don't grow this file. New knowledge goes into `.agentic/`; history goes into `.agentic/changelog.md`.

## Commands
```
dotnet build && dotnet test
python -m unittest discover -s tests/tools
python tools/privacy_guard.py --all
dotnet run --project src/ArchAdvisor.Mcp -- check . --min 80
```
