import contextlib
import io
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "tools"))
import privacy_guard as pg  # noqa: E402


def scan(text, terms=(), allow=()):
    return pg.scan_text("f.md", text, pg.compile_terms(list(terms)), set(allow))


class DenylistTests(unittest.TestCase):
    def test_private_term_is_found_case_insensitively(self):
        f = scan("We reused the Secret Tower shop code", terms=["secret tower"])
        self.assertEqual(1, len(f))
        self.assertEqual("private term from denylist", f[0].reason)

    def test_term_inside_a_longer_word_is_not_a_match(self):
        self.assertEqual([], scan("towering", terms=["tower"]))

    def test_ignore_marker_skips_the_line(self):
        self.assertEqual([], scan("Secret Tower  <!-- privacy-guard: ignore -->", terms=["secret tower"]))

    def test_short_terms_are_dropped_to_avoid_noise(self):
        with tempfile.TemporaryDirectory() as d:
            p = Path(d) / "deny.txt"
            p.write_text("ab\n# comment\nProject Falcon  # codename\n", encoding="utf-8")
            self.assertEqual(["Project Falcon"], pg.load_terms(p))


class HashedDenylistTests(unittest.TestCase):
    def setUp(self):
        self.hashes = frozenset([pg.hash_term("Acme Widgets")])

    def hits(self, text):
        return pg.scan_text("f.md", text, None, set(), self.hashes)

    def test_hash_matches_spacing_case_and_punctuation_variants(self):
        for text in ["Built for Acme Widgets.", "acmewidgets repo", "the ACME-WIDGETS brief", "AcmeWidgets's diagram"]:
            self.assertEqual(1, len(self.hits(text)), text)

    def test_hash_does_not_match_unrelated_words(self):
        self.assertEqual([], self.hits("Acme sells widgets"))

    def test_hash_entry_never_contains_the_plain_term(self):
        entry = pg.hash_term("Acme Widgets")
        self.assertTrue(entry.startswith("sha256:"))
        self.assertNotIn("acme", entry)

    def test_split_terms_separates_plain_and_hashed(self):
        plain, hashed = pg.split_terms(["Project Falcon", pg.hash_term("Acme Widgets")])
        self.assertEqual(["Project Falcon"], plain)
        self.assertEqual(self.hashes, hashed)


class SecretTests(unittest.TestCase):
    def test_keys_are_detected_and_masked(self):
        f = scan("key = AKIAABCDEFGHIJKLMNOP")  # synthetic test data, privacy-guard: ignore
        self.assertEqual("AWS access key", f[0].reason)
        self.assertNotIn("ABCDEFGHIJKLMNOP", f[0].excerpt)

    def test_private_key_header(self):
        self.assertTrue(scan("-----BEGIN OPENSSH PRIVATE KEY-----"))  # synthetic test data, privacy-guard: ignore

    def test_connection_string_password_but_not_placeholders(self):
        self.assertTrue(scan("Server=db;User Id=sa;Password=Sup3rS3cret;"))  # synthetic test data, privacy-guard: ignore
        self.assertEqual([], scan("Server=db;Password=<your-password>;"))
        self.assertEqual([], scan("Password=changeme"))


class PersonalDataTests(unittest.TestCase):
    def test_real_looking_email_is_flagged_but_example_domains_are_not(self):
        self.assertTrue(scan("contact jane.doe@gmail.com"))  # synthetic test data, privacy-guard: ignore
        self.assertEqual([], scan("contact demo@example.com or noreply@anthropic.com"))

    def test_allow_list_permits_an_email(self):
        self.assertEqual([], scan("me@mydomain.dev", allow=["me@mydomain.dev"]))  # privacy-guard: ignore

    def test_sa_id_requires_valid_checksum(self):
        valid = "8001015009087"  # synthetic test data, privacy-guard: ignore
        self.assertTrue(pg.luhn_ok(valid))
        self.assertTrue(scan(f"id {valid}"))
        self.assertEqual([], scan("id 8001015009088"))  # synthetic test data, privacy-guard: ignore


class InstallTests(unittest.TestCase):
    def test_hook_uses_lf_line_endings(self):
        with tempfile.TemporaryDirectory() as d:
            repo = Path(d)
            subprocess.run(["git", "init", "-q"], cwd=repo, check=True)
            (repo / "tools").mkdir()
            guard = repo / "tools" / "privacy_guard.py"
            guard.write_text(Path(pg.__file__).read_text(encoding="utf-8"), encoding="utf-8")
            subprocess.run([sys.executable, str(guard), "--install", "--repo", str(repo)], check=True, capture_output=True)
            data = (repo / ".git" / "hooks" / "pre-commit").read_bytes()
            self.assertTrue(data.startswith(b"#!/bin/sh\n"))
            self.assertNotIn(b"\r", data)


class StagedModeTests(unittest.TestCase):
    def test_pre_commit_blocks_a_staged_private_term(self):
        with tempfile.TemporaryDirectory() as d:
            repo = Path(d)
            subprocess.run(["git", "init", "-q"], cwd=repo, check=True)
            (repo / "notes.md").write_text("Lessons from Project Falcon\n", encoding="utf-8")
            subprocess.run(["git", "add", "notes.md"], cwd=repo, check=True)
            deny = repo / "deny.txt"
            deny.write_text("Project Falcon\n", encoding="utf-8")
            with contextlib.redirect_stdout(io.StringIO()), contextlib.redirect_stderr(io.StringIO()):
                self.assertEqual(1, pg.main(["--staged", "--repo", str(repo), "--denylist", str(deny)]))
            (repo / "notes.md").write_text("Generic lesson: anchor UI to edges\n", encoding="utf-8")
            subprocess.run(["git", "add", "notes.md"], cwd=repo, check=True)
            with contextlib.redirect_stdout(io.StringIO()), contextlib.redirect_stderr(io.StringIO()):
                self.assertEqual(0, pg.main(["--staged", "--repo", str(repo), "--denylist", str(deny)]))


if __name__ == "__main__":
    unittest.main()
