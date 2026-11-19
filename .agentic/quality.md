# Quality

## Definition of done
- Tests first; `dotnet test` and Python unittest green.
- New reference system: test proves the advisor agrees (or documents why not).
- New skill: frontmatter `name` + `description` that says when to use it.
- Privacy guard clean over the whole repo.

## Service levels
| SLI | SLO |
|---|---|
| CI success on main | 100% (main is never red for more than one commit) |
| `arch-advisor` tool call latency | < 200 ms |
