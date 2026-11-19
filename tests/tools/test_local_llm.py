import sqlite3
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "local-llm"))
import my_code_index as idx  # noqa: E402
import my_code_mcp as mcp  # noqa: E402


def make_workspace(root: Path):
    (root / "GameA" / "Tools").mkdir(parents=True)
    (root / "GameA" / "Tools" / "slice_sheet.py").write_text(
        "def slice_sprite_sheet(image, columns, rows):\n    \"\"\"Cut a sprite sheet into frames.\"\"\"\n    return frames\n", encoding="utf-8")
    (root / "ApiB").mkdir()
    (root / "ApiB" / "Ledger.cs").write_text(
        "public sealed class Ledger { public void Post(decimal amount, string idempotencyKey) { } }\n", encoding="utf-8")
    (root / "ApiB" / "node_modules").mkdir()
    (root / "ApiB" / "node_modules" / "junk.js").write_text("sprite sheet junk", encoding="utf-8")
    (root / "ApiB" / ".env").write_text("SECRET=1", encoding="utf-8")


class IndexTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.root = Path(self.tmp.name)
        make_workspace(self.root)
        self.con = idx.connect(Path(self.tmp.name) / "i.db")
        self.emb = idx.HashEmbedder()
        idx.build(self.root, self.con, self.emb, log=lambda *_: None)

    def tearDown(self):
        self.con.close()
        self.tmp.cleanup()

    def test_skips_dependencies_and_secret_files(self):
        paths = {r[0] for r in self.con.execute("SELECT DISTINCT path FROM chunks")}
        self.assertIn("GameA/Tools/slice_sheet.py", paths)
        self.assertFalse(any("node_modules" in p or p.endswith(".env") for p in paths))

    def test_search_finds_the_right_project(self):
        self.assertEqual("GameA", idx.search(self.con, self.emb, "slice sprite sheet frames", 1)[0]["project"])
        self.assertEqual("ApiB", idx.search(self.con, self.emb, "ledger idempotency key", 1)[0]["project"])

    def test_rebuild_skips_unchanged_files(self):
        self.assertEqual(0, idx.build(self.root, self.con, self.emb, log=lambda *_: None))

    def test_chunks_overlap_and_cover_the_file(self):
        text = "\n".join(f"line {i}" for i in range(1, 131))
        chunks = idx.chunk_file("p", "f.py", text)
        self.assertEqual(1, chunks[0].start)
        self.assertEqual(130, chunks[-1].end)
        self.assertLess(chunks[1].start, chunks[0].end)

    def test_mcp_handshake_list_and_call(self):
        init = mcp.handle({"jsonrpc": "2.0", "id": 1, "method": "initialize", "params": {"protocolVersion": "2025-06-18"}}, self.con, self.emb)
        self.assertEqual("my-code", init["result"]["serverInfo"]["name"])
        self.assertIsNone(mcp.handle({"jsonrpc": "2.0", "method": "notifications/initialized"}, self.con, self.emb))
        names = [t["name"] for t in mcp.handle({"jsonrpc": "2.0", "id": 2, "method": "tools/list"}, self.con, self.emb)["result"]["tools"]]
        self.assertEqual(["search_my_code", "ask_my_code"], names)
        res = mcp.handle({"jsonrpc": "2.0", "id": 3, "method": "tools/call",
                          "params": {"name": "search_my_code", "arguments": {"query": "sprite sheet", "k": 1}}}, self.con, self.emb)
        self.assertIn("slice_sheet.py", res["result"]["content"][0]["text"])

    def test_ask_uses_the_local_model_and_cites_sources(self):
        out = mcp.call_tool("ask_my_code", {"question": "how did I post to the ledger?"}, self.con, self.emb,
                            generate=lambda prompt: "Use Ledger.Post with an idempotency key [1].")
        self.assertIn("Sources:", out)
        self.assertIn("ApiB/Ledger.cs", out)

    def test_unknown_method_is_a_json_rpc_error(self):
        self.assertEqual(-32601, mcp.handle({"jsonrpc": "2.0", "id": 9, "method": "nope"}, self.con, self.emb)["error"]["code"])


if __name__ == "__main__":
    unittest.main()
