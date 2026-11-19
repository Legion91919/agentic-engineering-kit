# Local LLM that learns from your own projects

Goal: agents (and you) can ask *"how did I solve this before?"* across every project and game you've built, **without your code leaving your machine to build the index**.

## Step 1: retrieval (start here, no training needed)

```powershell
winget install Ollama.Ollama
ollama pull nomic-embed-text        # embeddings
ollama pull qwen2.5-coder:7b        # local answers (use :14b if your GPU has 16 GB+)

python local-llm/my_code_index.py build C:/Dev D:/dev
python local-llm/my_code_index.py search "slice a sprite sheet into frames"
python local-llm/my_code_index.py stats
```

- The index is a SQLite file at `~/.agentic/my-code-index.db`, outside every repo.
- It skips `node_modules`, `Library`, `bin/obj`, build output, lock files, `.env`, keys and anything named like a secret.
- Re-running `build` only re-embeds files that changed.
- With no Ollama running it falls back to a keyword-hash embedder, which is weaker but works anywhere.

### Use it from agents
`.mcp.json` registers the `my-code` MCP server with two tools:
- `search_my_code(query, k?, project?)`: returns snippets with project/path/lines
- `ask_my_code(question, project?)`: answers with the **local** model and cites sources

> When a cloud agent calls these tools, the snippets they return go to that agent. Point the index only at code you're happy to share with it, or use `ask_my_code` with a local model in a local-only agent.

## Step 2: make it smarter without training
- Write good `lessons.md` and ADRs in every project. They're the highest-value chunks in the index.
- Ask the agent to call `search_my_code` before inventing a solution (the skills already say so).

## Step 3: fine-tune (only when retrieval isn't enough)
1. **Collect**: your own commit diffs with their messages, review comments, and `lessons.md` entries → instruction/response pairs. Keep the dataset private.
2. **Evaluate first**: build a fixed set of ~30 tasks from your own repos with known good answers. Measure the base model + retrieval. No eval, no training.
3. **Train**: QLoRA on a 7B coder model (e.g. with Unsloth) on a local GPU. A few thousand pairs is plenty for style and conventions.
4. **Serve**: export to GGUF, `ollama create my-coder -f Modelfile`, set `AGENTIC_CHAT_MODEL=my-coder`.
5. **Compare** with the eval set. Keep the fine-tune only if it beats retrieval alone.

Fine-tuning teaches *style and conventions*; retrieval supplies *facts and code*. Most of the value comes from retrieval.
