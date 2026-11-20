import contextlib
import io
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "tools"))
import add_security_baseline as asb  # noqa: E402


def make_repo(root: Path, files: dict[str, str]) -> Path:
    for rel, text in files.items():
        p = root / rel
        p.parent.mkdir(parents=True, exist_ok=True)
        p.write_text(text, encoding="utf-8")
    return root


class DetectionTests(unittest.TestCase):
    def detect(self, files):
        with tempfile.TemporaryDirectory() as d:
            return asb.detect(make_repo(Path(d), files))

    def test_csproj_means_csharp(self):
        self.assertEqual(["csharp"], self.detect({"src/Api/Api.csproj": "<Project/>"}).languages)

    def test_package_json_means_javascript_typescript(self):
        self.assertEqual(["javascript-typescript"], self.detect({"web/package.json": "{}"}).languages)

    def test_pyproject_or_requirements_means_python(self):
        self.assertEqual(["python"], self.detect({"pyproject.toml": ""}).languages)
        self.assertEqual(["python"], self.detect({"svc/requirements-dev.txt": ""}).languages)

    def test_mixed_repo_finds_all_three_in_fixed_order(self):
        det = self.detect({"web/package.json": "{}", "requirements.txt": "", "src/A/A.csproj": ""})
        self.assertEqual(["csharp", "javascript-typescript", "python"], det.languages)

    def test_empty_repo_finds_nothing(self):
        self.assertEqual([], self.detect({"README.md": "# hi"}).languages)

    def test_dependency_build_and_hidden_folders_are_ignored(self):
        det = self.detect({
            "web/node_modules/left-pad/package.json": "{}",
            "src/A/bin/Debug/x.csproj": "",
            "web/.angular/cache/package.json": "{}",
            ".venv/lib/requirements.txt": "",
        })
        self.assertEqual([], det.languages)

    def test_monorepo_packages_folder_is_scanned(self):
        det = self.detect({"packages/ui/package.json": "{}", "packages/api/package.json": "{}"})
        self.assertEqual({"/packages/api", "/packages/ui"}, det.dirs["javascript-typescript"])

    def test_solution_folder_is_used_for_nuget_when_present(self):
        det = self.detect({"App.sln": "", "src/A/A.csproj": "", "src/B/B.csproj": ""})
        self.assertEqual({"/"}, det.dirs["csharp"])

    def test_project_folders_are_used_without_a_solution(self):
        det = self.detect({"src/A/A.csproj": "", "src/B/B.csproj": ""})
        self.assertEqual({"/src/A", "/src/B"}, det.dirs["csharp"])


class RenderTests(unittest.TestCase):
    def setUp(self):
        self.codeql = (asb.TEMPLATES / "github" / "codeql.yml").read_text(encoding="utf-8")
        self.dependabot = (asb.TEMPLATES / "github" / "dependabot.yml").read_text(encoding="utf-8")

    def test_codeql_keeps_only_detected_languages(self):
        out = asb.render_codeql(self.codeql, ["csharp", "python"])
        self.assertIn("- language: csharp", out)
        self.assertIn("- language: python", out)
        self.assertNotIn("javascript-typescript", out)
        self.assertEqual(2, out.count("build-mode: none"))
        self.assertIn("security-events: write", out)  # rest of the workflow is intact
        self.assertIn("github/codeql-action/analyze", out)

    def test_dependabot_keeps_detected_ecosystems_plus_actions_with_their_folders(self):
        det = asb.Detection({"javascript-typescript": {"/web", "/demo"}})
        out = asb.render_dependabot(self.dependabot, det)
        self.assertIn('package-ecosystem: "npm"', out)
        self.assertIn('package-ecosystem: "github-actions"', out)
        self.assertNotIn('"nuget"', out)
        self.assertNotIn('"pip"', out)
        self.assertIn('      - "/demo"\n      - "/web"\n', out)


class NoOverwriteTests(unittest.TestCase):
    def test_adds_all_three_files_to_a_bare_repo(self):
        with tempfile.TemporaryDirectory() as d:
            repo = make_repo(Path(d), {"pyproject.toml": ""})
            added, skipped = asb.apply(repo)
            self.assertEqual({".github/dependabot.yml", ".github/workflows/codeql.yml", "SECURITY.md"}, set(added))
            self.assertEqual([], skipped)
            self.assertIn("- language: python", (repo / ".github/workflows/codeql.yml").read_text(encoding="utf-8"))

    def test_existing_files_are_kept_byte_for_byte(self):
        with tempfile.TemporaryDirectory() as d:
            repo = make_repo(Path(d), {"package.json": "{}", "SECURITY.md": "ours\n",
                                       ".github/dependabot.yml": "version: 2 # ours\n"})
            added, skipped = asb.apply(repo)
            self.assertEqual([".github/workflows/codeql.yml"], added)
            self.assertEqual({"SECURITY.md", ".github/dependabot.yml"}, set(skipped))
            self.assertEqual("ours\n", (repo / "SECURITY.md").read_text(encoding="utf-8"))
            self.assertEqual("version: 2 # ours\n", (repo / ".github/dependabot.yml").read_text(encoding="utf-8"))

    def test_dry_run_writes_nothing(self):
        with tempfile.TemporaryDirectory() as d:
            repo = make_repo(Path(d), {"src/A/A.csproj": ""})
            added, _ = asb.apply(repo, dry_run=True)
            self.assertEqual(3, len(added))
            self.assertFalse((repo / ".github").exists())
            self.assertFalse((repo / "SECURITY.md").exists())

    def test_second_run_adds_nothing(self):
        with tempfile.TemporaryDirectory() as d:
            repo = make_repo(Path(d), {"requirements.txt": ""})
            asb.apply(repo)
            added, skipped = asb.apply(repo)
            self.assertEqual([], added)
            self.assertEqual(3, len(skipped))

    def test_no_languages_means_no_codeql_workflow(self):
        with tempfile.TemporaryDirectory() as d:
            repo = make_repo(Path(d), {"README.md": ""})
            added, _ = asb.apply(repo, dry_run=True)
            self.assertEqual([".github/dependabot.yml", "SECURITY.md"], added)

    def test_cli_reports_missing_folder(self):
        with contextlib.redirect_stderr(io.StringIO()):
            self.assertEqual(2, asb.main(["/definitely/not/here"]))


if __name__ == "__main__":
    unittest.main()
