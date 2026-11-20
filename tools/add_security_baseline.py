#!/usr/bin/env python3
"""Add a security baseline to a repo: Dependabot, CodeQL and SECURITY.md.

Usage:
  python tools/add_security_baseline.py <repo>              add the files that don't exist yet
  python tools/add_security_baseline.py <repo> --dry-run    show what would be added, write nothing
  python tools/add_security_baseline.py <repo> --dry-run --print   also print the file contents

It looks at the repo's files to choose languages and ecosystems:
  *.csproj / *.sln                  -> CodeQL csharp,                Dependabot nuget
  package.json                      -> CodeQL javascript-typescript, Dependabot npm
  pyproject.toml / requirements*    -> CodeQL python,                Dependabot pip
github-actions updates are always included. Existing files are never overwritten.
Only the standard library is used.
"""
from __future__ import annotations

import argparse
import os
import re
import sys
from dataclasses import dataclass, field
from pathlib import Path

KIT_ROOT = Path(__file__).resolve().parents[1]
TEMPLATES = KIT_ROOT / "templates"

SKIP_DIRS = {"node_modules", "bin", "obj", "dist", "build", "out", "venv", "site-packages",
             "__pycache__", "coverage", "TestResults", "vendor"}

LANGUAGES = ("csharp", "javascript-typescript", "python")
ECOSYSTEM = {"csharp": "nuget", "javascript-typescript": "npm", "python": "pip"}


@dataclass
class Detection:
    """Folders (relative, '/'-style) holding each language's manifests."""
    dirs: dict[str, set[str]] = field(default_factory=dict)

    @property
    def languages(self) -> list[str]:
        return [lang for lang in LANGUAGES if self.dirs.get(lang)]


def _is_python_manifest(name: str) -> bool:
    return name in ("pyproject.toml", "setup.py", "setup.cfg", "Pipfile") or (
        name.startswith("requirements") and name.endswith((".txt", ".in", ".lock")))


def _rel(root: Path, folder: str) -> str:
    rel = Path(folder).relative_to(root).as_posix()
    return "/" if rel == "." else "/" + rel


def detect(repo: Path) -> Detection:
    """Walk the repo (skipping build output, dependencies and hidden folders) and find manifests."""
    root = repo.resolve()
    csproj: set[str] = set()
    sln: set[str] = set()
    found: dict[str, set[str]] = {lang: set() for lang in LANGUAGES}
    for folder, dirnames, filenames in os.walk(root):
        dirnames[:] = sorted(d for d in dirnames if d not in SKIP_DIRS and not d.startswith("."))
        rel = _rel(root, folder)
        for name in filenames:
            if name.endswith((".csproj", ".fsproj")):
                csproj.add(rel)
            elif name.endswith(".sln") or name.endswith(".slnx"):
                sln.add(rel)
            elif name == "package.json":
                found["javascript-typescript"].add(rel)
            elif _is_python_manifest(name):
                found["python"].add(rel)
    if csproj:
        # Dependabot finds every project from the solution; fall back to project folders.
        found["csharp"] = sln or csproj
    return Detection({lang: dirs for lang, dirs in found.items() if dirs})


def render_codeql(template: str, languages: list[str]) -> str:
    """Keep only the matrix entries (`- language: x` plus their indented lines) for the given languages."""
    out: list[str] = []
    skipping_indent: int | None = None
    for line in template.splitlines(keepends=True):
        m = re.match(r"^(\s*)- language: (\S+)", line)
        if m:
            skipping_indent = None if m.group(2) in languages else len(m.group(1))
            if skipping_indent is None:
                out.append(line)
            continue
        if skipping_indent is not None:
            indent = len(line) - len(line.lstrip())
            if line.strip() and indent > skipping_indent:
                continue
            skipping_indent = None
        out.append(line)
    return "".join(out)


def render_dependabot(template: str, detection: Detection) -> str:
    """Keep the ecosystems the repo uses and point each at the folders holding its manifests."""
    header, *blocks = re.split(r"(?m)^(?=  - package-ecosystem:)", template)
    wanted = {ECOSYSTEM[lang]: sorted(dirs) for lang, dirs in detection.dirs.items()}
    wanted["github-actions"] = ["/"]
    kept: list[str] = []
    for block in blocks:
        eco = re.match(r'  - package-ecosystem: "([^"]+)"', block)
        if not eco or eco.group(1) not in wanted:
            continue
        dirs = "".join(f'      - "{d}"\n' for d in wanted[eco.group(1)])
        kept.append(re.sub(r'(?m)^    directories:\n(?:      - "[^"]*"\n)+', "    directories:\n" + dirs, block))
    body = "".join(kept).rstrip("\n") + "\n"
    return header + body


def plan(repo: Path, templates: Path = TEMPLATES) -> list[tuple[str, str]]:
    """Return (relative path, content) for every baseline file, rendered for this repo."""
    detection = detect(repo)
    files = [(".github/dependabot.yml",
              render_dependabot((templates / "github" / "dependabot.yml").read_text(encoding="utf-8"), detection))]
    if detection.languages:
        files.append((".github/workflows/codeql.yml",
                      render_codeql((templates / "github" / "codeql.yml").read_text(encoding="utf-8"),
                                    detection.languages)))
    files.append(("SECURITY.md", (templates / "SECURITY.md").read_text(encoding="utf-8")))
    return files


def apply(repo: Path, dry_run: bool = False, templates: Path = TEMPLATES) -> tuple[list[str], list[str]]:
    """Write files that don't exist yet. Returns (added, skipped). Never overwrites."""
    added: list[str] = []
    skipped: list[str] = []
    for rel, content in plan(repo, templates):
        target = repo / rel
        if target.exists():
            skipped.append(rel)
            continue
        added.append(rel)
        if not dry_run:
            target.parent.mkdir(parents=True, exist_ok=True)
            with open(target, "x", encoding="utf-8", newline="\n") as fh:  # "x" refuses to overwrite
                fh.write(content)
    return added, skipped


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="Add Dependabot, CodeQL and SECURITY.md to a repo.")
    parser.add_argument("repo", type=Path)
    parser.add_argument("--dry-run", action="store_true", help="show what would be added, write nothing")
    parser.add_argument("--print", dest="show", action="store_true", help="print the content of files to add")
    args = parser.parse_args(argv)
    repo: Path = args.repo
    if not repo.is_dir():
        print(f"not a folder: {repo}", file=sys.stderr)
        return 2

    detection = detect(repo)
    print(f"repo: {repo}")
    for lang in detection.languages:
        print(f"  {lang:<22} {ECOSYSTEM[lang]:<6} {', '.join(sorted(detection.dirs[lang]))}")
    if not detection.languages:
        print("  no csharp, javascript-typescript or python manifests found; CodeQL skipped")

    contents = dict(plan(repo))
    added, skipped = apply(repo, dry_run=args.dry_run)
    verb = "would add" if args.dry_run else "added"
    for rel in added:
        print(f"  {verb}: {rel}")
        if args.show:
            print("  " + "-" * 60)
            print("".join("    " + line for line in contents[rel].splitlines(keepends=True)).rstrip())
            print("  " + "-" * 60)
    for rel in skipped:
        print(f"  kept existing: {rel}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
