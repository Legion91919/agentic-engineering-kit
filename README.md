# Agentic Engineering Kit

**Set up the agents properly first, then let them build.**

A reusable kit for building software with AI agents that stays reliable from the first brief to the day someone else owns it. It's built around the **Baton model**: every agent picks up the same baton (the project's hidden `.agentic/` folder), runs one leg, writes back what it learned, and hands it on. The last leg hands the baton to the system's owner, agents included.

```
 brief                                                                      owner
   │                                                                          ▲
   ▼                                                                          │
 FRAME ─▶ MAP ─▶ DECIDE ─▶ SKETCH ─▶ ┌────────── SLICE LOOP ──────────┐ ─▶ PASS THE BATON
                                     │ failing test ▶ build ▶ prove ▶ demo │
                                     └───────────────┬────────────────┘
                                                     │ lessons.md feeds the next race
```

Works with Claude Code out of the box. `AGENTS.md`, the MCP servers and the skills are plain files, so Copilot, Cursor, Codex and Gemini can use them too.

## What's inside

| Part | What it does |
|---|---|
| **`arch-advisor` MCP server** (C#/.NET 8, no dependencies) | Turns *traits* found while mapping the domain into an architecture recommendation (style, patterns, risks, when to evolve) and explains it by analogy with **18 reference systems**, from a static site to a lending back office. Also writes ADRs, scaffolds the hidden `.agentic/` context, detects a project's stack and scores **handover readiness** for CI. |
| **16 skills** (`.claude/skills/`) | `baton` (orchestrator) · legs: `frame`, `map-domain`, `decide-architecture`, `sketch-ui` (variant-first UI design) · slice loop: `blueprint` (planner → executor → review), `build-dotnet`, `build-web`, `build-mobile-firebase`, `data-flows`, `ai-in-product` · `prove-it`, `pass-the-baton` · support: `project-context`, `unity-game`, `privacy-guard` |
| **11 subagents** (`.claude/agents/`) | One runner per leg (`framer`, `domain-mapper`, `architect`, `ui-sketcher`, four builders, `prover`, `handoff-writer`) plus a fresh-eyes `critic` that never reviews its own work |
| **Hidden-context convention** | Every project keeps its knowledge in `.agentic/` (one file per question). The root stays clean, and agents load only what the task needs. |
| **Privacy guard** | A pre-commit hook and a Claude Code hook that block private project names (from a denylist that's never committed), secrets, emails and SA ID numbers |
| **Local LLM** (`local-llm/`) | A private index of *your own* past projects with local embeddings (Ollama), served to agents as an MCP tool: "how did I solve this before?" |
| **Project generator** | `tools/new_project.py` creates an agent-ready repo: context, skills, agents, hooks, CI and the first architecture recommendation |

## Quick start

**Windows, one command** (tests, installs the global tool, hooks, MCP registration, privacy check):
```powershell
powershell -ExecutionPolicy Bypass -File tools\setup.ps1              # add -WithLocalLlm and/or -Publish
```

**Step by step:**
```bash
# 1. Install the architecture advisor as a global tool
dotnet pack src/ArchAdvisor.Mcp -c Release -o nupkg
dotnet tool install -g arch-advisor --add-source nupkg

# 2. Try it
arch-advisor recommend DocumentIngestion RegulatoryAudit SmallTeam
arch-advisor examples
arch-advisor stack D:/dev/SomeProject          # file names only, safe on private code

# 3. Start a project with the whole agent setup in place
python tools/new_project.py D:/dev/agentic/showcase/my-app "My App" "Who can't do what, and why it matters" \
       --traits SeveralBusinessAreas MoneyMovement SmallTeam

# 4. Open it in Claude Code and run the pipeline
claude  →  /baton
```

`.mcp.json` registers `arch-advisor` for anyone who opens the kit. `tools\setup.ps1 -WithLocalLlm` registers `my-code` at user level, so you can search your own code from any project.

## Choosing an architecture: the reference systems

| # | System | Best fit |
|---|---|---|
| 1 | Simple static site | No backend: static hosting + one serverless function |
| 2 | Employee tool | Layered monolith, or Clean Architecture if rules are complex |
| 3 | E-commerce | Modular monolith + cached catalogue read model |
| 4 | Online gambling | Microservices, append-only wallet ledger, idempotent everything |
| 5 | Streaming | Microservices + CDN + playback event stream |
| 6 | Internet provider | Modular monolith + sagas + operator adapters |
| 7 | Food delivery | Modular monolith, split live tracking early |
| 8 | Car rental | Modular monolith (microservices + event sourcing at global scale) |
| 9 | Banking / payments | Modular monolith + append-only ledger |
| 10 | Hotel / airline booking | Modular monolith + cached search side |
| 11 | Clinic / hospital | Clean Architecture + full audit history |
| 12 | IoT / smart meters | Separate ingestion pipeline + simple portal |
| 13 | Multi-tenant SaaS | Modular monolith with enforced tenant isolation |
| 14 | Chat / messaging | Microservices built for live connections |
| 15 | Document extraction with human review | Clean Architecture + queue-fed worker + review queue |
| 16 | Reporting warehouse fed by many sources | One re-runnable import job per source + one warehouse + plain reporting app |
| 17 | Replacing an old system piece by piece | Strangler fig routing layer in front of a new modular monolith |
| 18 | Lending back office | Modular monolith + append-only cash ledger + sagas |

The advisor is **deterministic**: the same traits always give the same answer, and the rules are readable C#. The agent adds judgement on top (team skills, deadlines, existing cloud) and records the decision as an ADR with the alternatives it rejected.

## The hidden `.agentic/` convention

```
my-project/
  AGENTS.md            ← under 80 lines: purpose, hard rules, "read .agentic/README.md"
  CLAUDE.md            ← @AGENTS.md
  README.md
  .agentic/            ← hidden project memory (committed, not ignored)
    README.md          ← map: which file answers which question
    next-step.md       ← resume point, overwritten at the end of every session
    product.md  architecture.md  decisions/  ux/  backlog.md
    quality.md  runbook.md  handover.md  changelog.md  lessons.md
```

## Repository layout

```
src/ArchAdvisor.Core     pure domain logic (advisor, catalogue, ADRs, context, readiness, stack detection)
src/ArchAdvisor.Mcp      MCP stdio server + CLI, packed as the `arch-advisor` dotnet tool
tests/                   xUnit tests (C#) and unittest tests (Python tools)
.claude/skills|agents    the skills library and subagents
tools/                   privacy guard, Claude Code hook, project generator
local-llm/               private code index + MCP server (Ollama)
templates/               CI, gitignore, MCP config copied into new projects
docs/                    pipeline, safe AI usage, local LLM guide
```

## Showcase projects built with the kit
Six small products for an invented lender, each run through the Baton model and ready to hand over. See `docs/showcases.md`.

## License
MIT
