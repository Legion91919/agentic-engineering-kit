#!/usr/bin/env python3
"""Create a new agent-ready project from the kit.

  python tools/new_project.py D:/dev/agentic/showcase/termsheet-reader "Termsheet Reader" \
      "Analysts at a credit fund cannot ... because ..." --traits DocumentIngestion RegulatoryAudit SmallTeam

What it does (idempotent, never overwrites):
  1. creates the folder and `git init`
  2. hidden .agentic/ context + short AGENTS.md / CLAUDE.md   (via arch-advisor, or a built-in fallback)
  3. copies the kit's skills, subagents, hooks and .mcp.json  (so the client owns the agent setup too)
  4. adds .gitignore, .privacy-allow, CI workflow, tests/ and README
  5. if --traits is given: writes the architecture recommendation into .agentic/architecture.md
  6. installs the privacy-guard pre-commit hook
"""
from __future__ import annotations

import argparse
import shutil
import subprocess
import sys
from pathlib import Path

KIT = Path(__file__).resolve().parents[1]
COPY_DIRS = [".claude/skills", ".claude/agents"]
COPY_FILES = {
    ".claude/settings.json": ".claude/settings.json",
    "tools/privacy_guard.py": "tools/privacy_guard.py",
    "tools/hooks/guard_commit.py": "tools/hooks/guard_commit.py",
    "templates/gitignore": ".gitignore",
    "templates/gitattributes": ".gitattributes",
    "templates/privacy-allow": ".privacy-allow",
    "templates/ci/dotnet-ci.yml": ".github/workflows/ci.yml",
    "templates/mcp.json": ".mcp.json",
    "templates/copilot-instructions.md": ".github/copilot-instructions.md",
}


def advisor_cmd() -> list[str] | None:
    builds = [KIT / "src" / "ArchAdvisor.Mcp" / "bin" / cfg / "net8.0" / "arch-advisor.dll" for cfg in ("Release", "Debug")]
    builds = sorted((d for d in builds if d.is_file()), key=lambda d: max(f.stat().st_mtime for f in d.parent.glob("*.dll")), reverse=True)
    if builds and shutil.which("dotnet"):
        return ["dotnet", str(builds[0])]  # newest local build wins over a stale one
    return ["arch-advisor"] if shutil.which("arch-advisor") else None


def run(cmd: list[str], cwd: Path | None = None) -> str:
    return subprocess.run(cmd, cwd=cwd, check=True, capture_output=True, text=True).stdout


def copy_if_missing(src: Path, dst: Path, created: list[str], root: Path) -> None:
    if src.is_dir():
        for f in src.rglob("*"):
            if f.is_file():
                copy_if_missing(f, dst / f.relative_to(src), created, root)
        return
    if dst.exists():
        return
    dst.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(src, dst)
    created.append(str(dst.relative_to(root)).replace("\\", "/"))


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("path")
    ap.add_argument("name")
    ap.add_argument("purpose", nargs="?", default="")
    ap.add_argument("--traits", nargs="*", default=[])
    ap.add_argument("--no-git", action="store_true")
    a = ap.parse_args(argv)

    root = Path(a.path).resolve()
    root.mkdir(parents=True, exist_ok=True)
    created: list[str] = []

    if not a.no_git and not (root / ".git").exists() and shutil.which("git"):
        run(["git", "init", "-q", "-b", "main"], root)

    adv = advisor_cmd()
    if adv is None:
        print("arch-advisor not found: build it (dotnet build src/ArchAdvisor.Mcp) or install the global tool.", file=sys.stderr)
        return 2
    print(run(adv + ["scaffold", str(root), a.name, a.purpose]).strip())

    for d in COPY_DIRS:
        copy_if_missing(KIT / d, root / d, created, root)
    for src, dst in COPY_FILES.items():
        copy_if_missing(KIT / src, root / dst, created, root)

    tests = root / "tests"
    if not tests.exists():
        tests.mkdir()
        (tests / ".gitkeep").write_text("", encoding="utf-8")
        created.append("tests/.gitkeep")

    readme = root / "README.md"
    if not readme.exists():
        readme.write_text(f"# {a.name}\n\n{a.purpose or 'TODO: one sentence.'}\n\n"
                          "## Run locally\nSee `.agentic/runbook.md`.\n\n"
                          "## How this project is organised\nProject knowledge lives in the hidden `.agentic/` folder "
                          "(start with `.agentic/README.md`). AI agents read `AGENTS.md` first.\n\n"
                          "Built with the Baton model from the Agentic Engineering Kit: "
                          "frame → map → decide → sketch → slice loop → pass the baton.\n", encoding="utf-8")
        created.append("README.md")

    if a.traits:
        rec = run(adv + ["recommend", *a.traits])
        arch = root / ".agentic" / "architecture.md"
        text = arch.read_text(encoding="utf-8")
        marker = "TODO: output of recommend_architecture, plus our judgement."
        if marker in text:
            arch.write_text(text.replace(marker, "Advisor output (traits: " + ", ".join(a.traits) + "):\n\n"
                                         + rec.replace("\n## ", "\n#### ").replace("# Recommendation", "### Recommendation").strip()
                                         + "\n\nTODO(architect): add judgement, then record ADR-0001 with write_adr."),
                            encoding="utf-8")
            created.append(".agentic/architecture.md (recommendation)")

    # Security baseline: Dependabot, CodeQL for the detected languages, SECURITY.md (never overwrites).
    subprocess.run([sys.executable, str(KIT / "tools" / "add_security_baseline.py"), str(root)], check=False, capture_output=True)

    if (root / ".git").exists():
        subprocess.run([sys.executable, str(root / "tools" / "privacy_guard.py"), "--install", "--repo", str(root)],
                       check=False, capture_output=True)

    print(f"Added {len(created)} kit file(s). Next: open the folder in Claude Code and run the `baton` skill.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
