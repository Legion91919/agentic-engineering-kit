# Lessons

Generic, reusable lessons only. No client names, data or secrets.

- Keep an MCP server's stdout for protocol only; one stray log line breaks the client silently.
- A short root AGENTS.md plus a hidden context folder beats one long instruction file: agents load less and stay on task.
- Give agents a deterministic tool for decisions that must be consistent (architecture choice), and let them add judgement on top.
- Unity: state "what is live" in AGENTS.md; agents waste most time on legacy code paths.
- Asset tools should be single-purpose, idempotent and write to a staging folder before `Assets/`.
