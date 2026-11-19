# local-llm

Private index of your own projects, plus an MCP server so agents can search it.

- `my_code_index.py`: build, search and stats (SQLite index at `~/.agentic/my-code-index.db`)
- `my_code_mcp.py`: MCP stdio server with `search_my_code` and `ask_my_code`

Full guide, including when to fine-tune: [`docs/local-llm.md`](../docs/local-llm.md).
