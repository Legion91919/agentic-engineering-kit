# Changelog

## Unreleased
- Visuals: "See it in action" terminal demo (`docs/media/kit-demo.gif`/`.mp4`) from real `arch-advisor` and privacy-guard output; `tools/demo/terminal.mjs` replays captured command output as MP4 + GIF or a PNG; fixed the pre-commit hook running the guard twice after a block (`python || python3` fallback).
- Security: skills `threat-model`, `secure-code-review`, `supply-chain-security`, `ai-agent-security`; read-only `security-reviewer` subagent in the Prove leg; templates `github/dependabot.yml`, `github/codeql.yml`, `SECURITY.md`; `tools/add_security_baseline.py` with tests.
- `compare_architectures` tool and `arch-advisor compare` (ten candidate designs rated with reasons); `design-dossier` skill.
- arch-advisor: 20 reference systems (19 photo-sharing feed, 20 community network). Earlier: 18 reference systems (added document processing, data platform, legacy modernisation, loan administration), `BatchDataFeeds` trait, `detect_stack` tool, packed as a global tool.
- Skills `blueprint` (planner/executor split, resume point) and `build-mobile-firebase`; `.agentic/next-step.md` in the scaffold; Expo, Firebase and monorepo detection.
- Skills library (14), subagents (11), privacy guard + Claude Code hook, project generator, local code index + MCP server, CI.
