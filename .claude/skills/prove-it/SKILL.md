---
name: prove-it
description: Make every claim of "done" provable - test layers, CI gates (build, tests, coverage ratchet, lint, secret and dependency scans, privacy guard, readiness score), SLOs, telemetry and safe rollout. Use when setting up CI, deciding what done means, reviewing coverage, or before a release or hand-off.
---

# Prove it

"It works on my machine" is a claim. A green pipeline that anyone can re-run is proof.

## Layers of proof
| Layer | Proves | Tooling (.NET / web) | When |
|---|---|---|---|
| Rules | the domain logic is right | xUnit / Vitest | every push |
| Seams | each adapter and the database really work | WebApplicationFactory + Testcontainers | every push |
| Contracts | APIs, data feeds and AI outputs keep their shape | OpenAPI diff, JSON Schema tests | every push |
| Journeys | a real user can finish each flow | Playwright + axe | every PR |
| Accuracy | AI output is good enough | eval runner on a labelled set | when prompts or models change |
| Boundaries | modules don't reach into each other | NetArchTest / reflection tests | every push |

## The pipeline (copy `templates/ci/dotnet-ci.yml`)
1. Build with warnings as errors.
2. Tests with coverage, failing below the bar. Start at 70% and only ever raise it.
3. Format and lint.
4. Secret scan (gitleaks), vulnerable-package check, CodeQL, privacy guard (`supply-chain-security`; `python3 tools/add_security_baseline.py .` adds Dependabot, CodeQL and SECURITY.md).
5. `arch-advisor check . --min 80`.
6. Release: deploy to a staging slot, smoke test, then swap, or ramp behind a feature flag.

## Running in production
- Put SLIs and SLOs in `.agentic/quality.md`: availability, p95 latency, data freshness for jobs, accuracy for AI.
- Trace with OpenTelemetry from the edge through workers, the database and outside calls.
- Alert when the error budget is burning fast, not on every single error.

## Security proof
- Keep `docs/THREAT-MODEL.md` current (`threat-model`): every risk scoring 6 or more has a named test in the suite.
- Run `secure-code-review` on each slice; AI features and agent setup also get `ai-agent-security`.

## The critic
Before merging, hand the change to the `critic` subagent, then to the `security-reviewer`. Both work from a fresh context and have never seen the work, so they can't go easy on it. Blockers (including Critical and High security findings) get fixed and suggestions become tickets.

## Hand on when
Every gate is on and green, SLOs are written down, the threat model's high risks have passing tests, and neither the critic nor the security-reviewer found blockers.
