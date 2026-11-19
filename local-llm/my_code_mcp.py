#!/usr/bin/env python3
"""MCP server (stdio) over your private code index. Register it in .mcp.json or your user config.

Tools:
  search_my_code(query, k?, project?)  semantic search over your own projects
  ask_my_code(question, project?)      search + answer with a LOCAL Ollama model (default qwen2.5-coder:7b)

Results come from your machine. If you ask a cloud agent to call these tools, the returned snippets
are sent to that agent, so keep the index to code you are happy to share with it.
"""
from __future__ import annotations

import json
import os
import sys
import urllib.request
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import my_code_index as idx  # noqa: E402

PROTOCOL = "2025-06-18"
TOOLS = [
    {"name": "search_my_code",
     "description": "Search the developer's own past projects (local index) for code, scripts and notes relevant to a query. Use before inventing a solution from scratch.",
     "inputSchema": {"type": "object", "properties": {
         "query": {"type": "string", "description": "What you're looking for, in plain words or identifiers."},
         "k": {"type": "integer", "description": "Number of results (default 5, max 20)."},
         "project": {"type": "string", "description": "Optional project folder name to restrict to."}},
         "required": ["query"], "additionalProperties": False}},
    {"name": "ask_my_code",
     "description": "Answer a question using the developer's own past projects, with a local Ollama model. Returns the answer plus the sources used.",
     "inputSchema": {"type": "object", "properties": {
         "question": {"type": "string"},
         "project": {"type": "string"}},
         "required": ["question"], "additionalProperties": False}},
]


def ollama_generate(prompt: str) -> str:
    host = os.environ.get("OLLAMA_HOST", "http://localhost:11434").rstrip("/")
    model = os.environ.get("AGENTIC_CHAT_MODEL", "qwen2.5-coder:7b")
    body = json.dumps({"model": model, "prompt": prompt, "stream": False}).encode()
    req = urllib.request.Request(f"{host}/api/generate", body, {"Content-Type": "application/json"})
    with urllib.request.urlopen(req, timeout=300) as r:  # noqa: S310 - local server
        return json.load(r)["response"]


def call_tool(name: str, args: dict, con, emb, generate=ollama_generate) -> str:
    if name == "search_my_code":
        k = max(1, min(int(args.get("k", 5)), 20))
        hits = idx.search(con, emb, args["query"], k, args.get("project"))
        if not hits:
            return "No results. Build the index first: python local-llm/my_code_index.py build C:/Dev D:/dev"
        return "\n\n".join(f"### {h['project']}/{h['path']}:{h['lines']} (score {h['score']})\n```\n{h['text'][:1500]}\n```" for h in hits)
    if name == "ask_my_code":
        hits = idx.search(con, emb, args["question"], 6, args.get("project"))
        context = "\n\n".join(f"[{i + 1}] {h['project']}/{h['path']}:{h['lines']}\n{h['text'][:1500]}" for i, h in enumerate(hits))
        prompt = ("You answer questions about the developer's own past code. Use only the context. Cite sources as [n]. "
                  "If the context does not answer it, say so.\n\nContext:\n" + context + "\n\nQuestion: " + args["question"])
        answer = generate(prompt)
        sources = "\n".join(f"[{i + 1}] {h['project']}/{h['path']}:{h['lines']}" for i, h in enumerate(hits))
        return f"{answer}\n\nSources:\n{sources}"
    raise ValueError(f"Unknown tool: {name}")


def handle(msg: dict, con, emb, generate=ollama_generate) -> dict | None:
    mid, method = msg.get("id"), msg.get("method")
    if mid is None:
        return None  # notification
    try:
        if method == "initialize":
            result = {"protocolVersion": msg.get("params", {}).get("protocolVersion", PROTOCOL),
                      "capabilities": {"tools": {}}, "serverInfo": {"name": "my-code", "version": "0.1.0"}}
        elif method == "tools/list":
            result = {"tools": TOOLS}
        elif method == "tools/call":
            p = msg.get("params", {})
            try:
                text = call_tool(p.get("name", ""), p.get("arguments", {}) or {}, con, emb, generate)
                result = {"content": [{"type": "text", "text": text}], "isError": False}
            except (ValueError, KeyError, OSError) as e:
                result = {"content": [{"type": "text", "text": f"Error: {e}"}], "isError": True}
        elif method == "ping":
            result = {}
        else:
            return {"jsonrpc": "2.0", "id": mid, "error": {"code": -32601, "message": f"Method not found: {method}"}}
        return {"jsonrpc": "2.0", "id": mid, "result": result}
    except Exception as e:  # noqa: BLE001 - never crash the server
        return {"jsonrpc": "2.0", "id": mid, "error": {"code": -32603, "message": str(e)}}


def main() -> int:
    con, emb = idx.connect(idx.index_path()), idx.default_embedder()
    print(f"my-code MCP server ready ({emb.name}, index {idx.index_path()})", file=sys.stderr)
    for line in sys.stdin:
        line = line.strip()
        if not line:
            continue
        try:
            msg = json.loads(line)
        except json.JSONDecodeError:
            out = {"jsonrpc": "2.0", "id": None, "error": {"code": -32700, "message": "Parse error"}}
        else:
            out = handle(msg, con, emb)
        if out is not None:
            sys.stdout.write(json.dumps(out) + "\n")
            sys.stdout.flush()
    return 0


if __name__ == "__main__":
    sys.exit(main())
