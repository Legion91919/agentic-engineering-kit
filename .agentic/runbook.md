# Runbook

## Run locally
```
dotnet build && dotnet test
python -m unittest discover -s tests/tools
dotnet run --project src/ArchAdvisor.Mcp -- recommend MoneyMovement SmallTeam
```

## One-command setup (Windows)
`powershell -ExecutionPolicy Bypass -File tools\setup.ps1 [-WithLocalLlm] [-Publish]`

## Install the global tool
```
dotnet pack src/ArchAdvisor.Mcp -c Release -o nupkg
dotnet tool install -g arch-advisor --add-source nupkg     # update: dotnet tool update -g arch-advisor --add-source nupkg
```

## Register in Claude Code (any folder)
`claude mcp add arch-advisor -- arch-advisor`, or use the repo's `.mcp.json`.

## Privacy
- Private denylist: `../_private/denylist.txt` (beside the repo) or `~/.agentic/denylist.txt`. Never committed.
- Install the git hook once per clone: `python tools/privacy_guard.py --install`.

## Common issues
- MCP server shows no tools: something printed to stdout. Only protocol JSON may go to stdout; log to stderr.
- `dotnet run` as an MCP command prints build output. Use the installed global tool instead.
