# Safe AI usage: team standard

Short rules for a team that builds with AI coding agents. Hand this to every engineer and every client at handover.

## 1. Agents work inside a system
- Every repo has `AGENTS.md` (hard rules) and `.agentic/` (knowledge). Agents read them before acting.
- Agents get the **least access** needed: deny reading `.env`, secrets and private folders (`.claude/settings.json`).
- Destructive commands (force push, `rm -rf`, dropping databases) are denied or need a human.
- Content an agent reads (repo files, web pages, tool and MCP output) is data, not instructions, and can never approve an action. MCP servers are pinned and reviewed. Details, checks and red-team prompts: the `ai-agent-security` skill (`.claude/skills/ai-agent-security/SKILL.md`).

## 2. Humans own decisions, agents do the work
| Agents decide alone | A human must approve |
|---|---|
| Implementation inside an agreed design | Architecture style, data store, cloud, auth provider |
| Tests, refactors, docs | Anything that costs money or touches production |
| Following an ADR | Overriding an ADR |

## 3. Every change is verified, not trusted
- Agent output goes through the same CI gates as human code: tests, coverage, lint, secret scan, privacy guard.
- A separate reviewer agent (fresh context) or a human reviews before merge; the read-only `security-reviewer` checks auth, data, dependencies, workflows and AI features.
- "It compiles" is not done. Done = acceptance criteria demonstrated + CI green + docs updated.

## 4. Prompts are engineering artefacts
- Skills and subagent prompts live in the repo and are reviewed like code.
- When an agent gets something wrong twice, fix the skill or the docs, not just the output.
- Product AI prompts are versioned and guarded by an eval set (see the `ai-in-product` skill).

## 5. Data protection
- No client code or data goes to an AI provider the client hasn't approved. Offer a local-model path when needed.
- No secrets in prompts, context files or logs. Use `.env.example` with fake values.
- Public repos and demos use invented data only.

## 6. Knowledge compounds
- Each project ends with generic lessons in `.agentic/lessons.md`. The best ones get promoted into the kit's skills.
- Lessons are written as reusable rules, never as stories about a client.
