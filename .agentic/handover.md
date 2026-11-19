# Handover

What someone needs to own this without the author.

- [x] Source and CI in one public repo; no cloud resources to transfer
- [x] No secrets required to build or test
- [x] Architecture and decisions explained (`architecture.md`, `decisions/`)
- [x] Runbook covers build, install, register and common issues
- [ ] Contributor guide with the skill-writing rules
- [x] AI agent setup documented (`README.md`, `.claude/`, `docs/safe-ai-usage.md`)

## Known issues
- Hand-written MCP protocol covers tools only (no resources or prompts).

## Recommended next steps
- Publish to NuGet so projects can `dotnet tool install -g arch-advisor` without cloning.
