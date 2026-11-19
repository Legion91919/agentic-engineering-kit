# Product brief

## Problem
Engineers using AI coding agents get fast but unreliable results: no discovery, ad-hoc architecture, no design, weak tests, nothing a client could own. Agents work without a delivery system.

## Users
- Senior engineers who build client or product software with agents (daily).
- Client engineers who inherit those projects (at handover).
- Hiring managers reviewing the author's work (occasionally).

## Outcomes
- A new project goes from brief to a recorded architecture decision and an agent-ready repo in under 30 minutes.
- Every project built with the kit passes `arch-advisor check --min 80` before handover.
- Zero private names or secrets reach a public repo (privacy guard on every commit).

## Constraints
- Public repo: generic content and invented demo domains only.
- Core tooling has no third-party dependencies (C# Core: none; Python: standard library) so it runs anywhere.
- Works with Claude Code and degrades gracefully to plain Markdown for other agents.

## Out of scope
- Hosting, auth or a web UI for the kit itself.
- Replacing human architectural judgement. The advisor gives a defensible default, and people decide.

## Traits (for the architecture advisor)
- SmallTeam: "one author, open-source contributors later"
- ComplexBusinessRules: "the advisor's rules must be testable without I/O"
