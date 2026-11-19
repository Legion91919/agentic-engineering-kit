#!/usr/bin/env python3
"""Private, local index of your own projects, so agents can ask "how did I solve this before?".

  python local-llm/my_code_index.py build C:/Dev D:/dev    index every project under both folders
  python local-llm/my_code_index.py search "sprite sheet slicing"
  python local-llm/my_code_index.py stats

Everything stays on your machine:
  * embeddings come from a local Ollama server (default model nomic-embed-text)
  * the index is a SQLite file at ~/.agentic/my-code-index.db (override with AGENTIC_INDEX)
  * nothing is ever written inside the repos you index

Only the standard library is used. Without Ollama it falls back to a keyword-hash embedder, which
is weaker but lets you try everything (and lets the tests run anywhere).
"""
from __future__ import annotations

import argparse
import hashlib
import json
import math
import os
import re
import sqlite3
import sys
import urllib.request
from dataclasses import dataclass
from pathlib import Path
from typing import Iterable, Protocol

CODE_EXT = {".cs", ".py", ".ts", ".tsx", ".js", ".jsx", ".md", ".json", ".yml", ".yaml", ".ps1", ".bat",
            ".sh", ".sql", ".html", ".css", ".scss", ".shader", ".asmdef", ".csproj", ".toml"}
SKIP_DIRS = {".git", "node_modules", "bin", "obj", "Library", "Temp", "Logs", "UserSettings", "Builds", "build",
             "dist", ".vs", ".idea", "__pycache__", ".venv", "venv", "Packages", ".gradle", "_private"}
SKIP_FILES = {"package-lock.json", "yarn.lock", "pnpm-lock.yaml", "denylist.txt"}
SECRET_FILE = re.compile(r"(?i)(^\.env|secret|credential|\.pfx$|\.key$|\.pem$)")
MAX_FILE_BYTES = 200_000
CHUNK_LINES = 60
OVERLAP = 10


@dataclass(frozen=True)
class Chunk:
    project: str
    path: str
    start: int
    end: int
    text: str


class Embedder(Protocol):
    name: str
    def embed(self, text: str) -> list[float]: ...


class HashEmbedder:
    """Keyword-hash vectors: no model needed. Good enough for tests and a first try."""
    name = "hash-512"

    def embed(self, text: str) -> list[float]:
        v = [0.0] * 512
        for tok in re.findall(r"[A-Za-z_][A-Za-z0-9_]{2,}", text.lower()):
            for part in re.findall(r"[a-z][a-z0-9]*", re.sub(r"([a-z])([A-Z])", r"\1 \2", tok)) or [tok]:
                h = int(hashlib.md5(part.encode()).hexdigest(), 16)
                v[h % 512] += 1.0 if (h >> 9) & 1 else -1.0
        n = math.sqrt(sum(x * x for x in v)) or 1.0
        return [x / n for x in v]


class OllamaEmbedder:
    def __init__(self, model: str = "nomic-embed-text", host: str | None = None):
        self.model = model
        self.host = (host or os.environ.get("OLLAMA_HOST", "http://localhost:11434")).rstrip("/")
        self.name = f"ollama:{model}"

    def embed(self, text: str) -> list[float]:
        body = json.dumps({"model": self.model, "prompt": text[:8000]}).encode()
        req = urllib.request.Request(f"{self.host}/api/embeddings", body, {"Content-Type": "application/json"})
        with urllib.request.urlopen(req, timeout=60) as r:  # noqa: S310 - local server
            return json.load(r)["embedding"]

    def available(self) -> bool:
        try:
            with urllib.request.urlopen(f"{self.host}/api/tags", timeout=2) as r:  # noqa: S310
                return r.status == 200
        except OSError:
            return False


def default_embedder() -> Embedder:
    o = OllamaEmbedder(os.environ.get("AGENTIC_EMBED_MODEL", "nomic-embed-text"))
    return o if o.available() else HashEmbedder()


def iter_files(root: Path) -> Iterable[Path]:
    for dirpath, dirnames, filenames in os.walk(root):
        dirnames[:] = [d for d in dirnames if d not in SKIP_DIRS]
        for f in filenames:
            p = Path(dirpath) / f
            if p.suffix.lower() in CODE_EXT and f not in SKIP_FILES and not SECRET_FILE.search(f):
                try:
                    if p.stat().st_size <= MAX_FILE_BYTES:
                        yield p
                except OSError:
                    continue


def chunk_file(project: str, rel: str, text: str) -> list[Chunk]:
    lines = text.splitlines()
    if not lines:
        return []
    out, i = [], 0
    while i < len(lines):
        j = min(i + CHUNK_LINES, len(lines))
        body = "\n".join(lines[i:j]).strip()
        if body:
            out.append(Chunk(project, rel, i + 1, j, body))
        if j == len(lines):
            break
        i = j - OVERLAP
    return out


def project_of(root: Path, path: Path) -> str:
    rel = path.relative_to(root).parts
    return rel[0] if len(rel) > 1 else root.name


def connect(db_path: Path) -> sqlite3.Connection:
    db_path.parent.mkdir(parents=True, exist_ok=True)
    con = sqlite3.connect(db_path)
    con.execute("""CREATE TABLE IF NOT EXISTS chunks(
        id INTEGER PRIMARY KEY, project TEXT, path TEXT, start INT, end INT, text TEXT,
        embedder TEXT, vector TEXT, file_hash TEXT)""")
    con.execute("CREATE INDEX IF NOT EXISTS ix_path ON chunks(path)")
    return con


def index_path() -> Path:
    return Path(os.environ.get("AGENTIC_INDEX", Path.home() / ".agentic" / "my-code-index.db"))


def build(root: Path, con: sqlite3.Connection, emb: Embedder, log=print) -> int:
    root = root.resolve()
    added = 0
    for p in iter_files(root):
        rel = str(p.relative_to(root)).replace("\\", "/")
        data = p.read_bytes()
        if b"\x00" in data[:4000]:
            continue
        h = hashlib.sha1(data).hexdigest()
        row = con.execute("SELECT file_hash, embedder FROM chunks WHERE path=? LIMIT 1", (rel,)).fetchone()
        if row and row[0] == h and row[1] == emb.name:
            continue
        con.execute("DELETE FROM chunks WHERE path=?", (rel,))
        for c in chunk_file(project_of(root, p), rel, data.decode("utf-8", errors="replace")):
            con.execute("INSERT INTO chunks(project,path,start,end,text,embedder,vector,file_hash) VALUES(?,?,?,?,?,?,?,?)",
                        (c.project, c.path, c.start, c.end, c.text, emb.name, json.dumps(emb.embed(c.text)), h))
            added += 1
        con.commit()
    log(f"indexed {added} new chunk(s) with {emb.name}")
    return added


def search(con: sqlite3.Connection, emb: Embedder, query: str, k: int = 5, project: str | None = None) -> list[dict]:
    q = emb.embed(query)
    sql, args = "SELECT project,path,start,end,text,vector FROM chunks WHERE embedder=?", [emb.name]
    if project:
        sql += " AND project=?"
        args.append(project)
    scored = []
    for proj, path, s, e, text, vec in con.execute(sql, args):
        v = json.loads(vec)
        scored.append((sum(a * b for a, b in zip(q, v)), proj, path, s, e, text))
    scored.sort(key=lambda t: t[0], reverse=True)
    return [{"score": round(sc, 3), "project": p, "path": pa, "lines": f"{s}-{e}", "text": t}
            for sc, p, pa, s, e, t in scored[:k]]


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = ap.add_subparsers(dest="cmd", required=True)
    b = sub.add_parser("build"); b.add_argument("roots", nargs="+")
    s = sub.add_parser("search"); s.add_argument("query"); s.add_argument("-k", type=int, default=5); s.add_argument("--project")
    sub.add_parser("stats")
    a = ap.parse_args(argv)
    con, emb = connect(index_path()), default_embedder()
    if a.cmd == "build":
        for r in a.roots:
            build(Path(r), con, emb)
    elif a.cmd == "search":
        for hit in search(con, emb, a.query, a.k, a.project):
            print(f"[{hit['score']}] {hit['project']}/{hit['path']}:{hit['lines']}\n{hit['text'][:400]}\n")
    else:
        for row in con.execute("SELECT project, COUNT(*), embedder FROM chunks GROUP BY project, embedder ORDER BY 2 DESC"):
            print(*row, sep="\t")
    return 0


if __name__ == "__main__":
    sys.exit(main())
