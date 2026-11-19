# 0001. Clean core with a thin MCP adapter

- Status: Accepted
- Date: 2026-11-19

## Context
The architecture advisor must be trusted by humans and called by agents. Its rules have to be readable, testable without I/O, and identical whether called from an agent, CI or a terminal. The author is a small team, and there should be no dependency churn.

## Decision
All rules live in `ArchAdvisor.Core` with no NuGet dependencies. `ArchAdvisor.Mcp` implements the small part of MCP we need (initialize, tools/list, tools/call) by hand over stdio, and doubles as a CLI. It's packed as the `arch-advisor` dotnet global tool.

## Alternatives considered
- Official C# MCP SDK: richer, but adds a dependency for three methods and ties releases to the SDK's preview cadence. Revisit when the SDK is stable and we need resources or prompts.
- TypeScript MCP server: easiest ecosystem, but the target audience (and the author's role) is C#/.NET.
- Rules inside skill prompts only: no tests and no determinism. Agents would answer differently each time.

## Consequences
- Easier: unit-testing every rule, running offline, using it from CI.
- Harder: we maintain protocol code ourselves (covered by `McpProtocolTests`).
