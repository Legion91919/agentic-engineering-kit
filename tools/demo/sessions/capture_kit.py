#!/usr/bin/env python3
"""Run the kit demo commands for real and save their output as a terminal session (kit.json).

Usage (from the repo root, after `dotnet build`):  python3 tools/demo/sessions/capture_kit.py
Then:  cd tools/demo && node terminal.mjs sessions/kit.json ../../docs/media kit-demo
The privacy-guard part runs in a throwaway git repo under /tmp with an invented, fake secret.
"""
import json, os, pathlib, shutil, subprocess, tempfile

ROOT = pathlib.Path(__file__).resolve().parents[3]
DLL = "src/ArchAdvisor.Mcp/bin/Debug/net8.0/arch-advisor.dll"


def run(cmd: str, cwd: pathlib.Path, env=None) -> str:
    r = subprocess.run(cmd, shell=True, cwd=cwd, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True, env=env)
    return r.stdout.rstrip()


steps = []
adv = f"dotnet {DLL}"
for caption, traits, lines in [
    ("Compare ten designs for a small internal tool", "InternalUsersOnly SmallTeam", 16),
    ("Same advisor, a regulated money platform at global scale", "MoneyMovement RegulatoryAudit SeveralBusinessAreas GlobalScale", 16),
]:
    cmd = f"arch-advisor compare {traits} | head -{lines}"
    steps.append({"caption": caption, "cmd": cmd, "out": run(f"{adv} compare {traits} | head -{lines}", ROOT), "clear": True, "hold": 4200})

traits = "MoneyMovement RegulatoryAudit PersonalData SmallTeam"
steps.append({"caption": "Recommend: style, patterns, reference systems, risks, when to evolve",
              "cmd": f"arch-advisor recommend {traits}", "out": run(f"{adv} recommend {traits}", ROOT), "clear": True, "hold": 5200})

# Privacy guard in a throwaway repo.
tmp = pathlib.Path(tempfile.mkdtemp(prefix="demo-shop-"))
try:
    env = {**os.environ, "AGENTIC_DENYLIST": str(tmp / ".no-denylist"), "GIT_AUTHOR_NAME": "Demo", "GIT_AUTHOR_EMAIL": "demo@example.invalid",  # invented demo identity / fake key, privacy-guard: ignore
           "GIT_COMMITTER_NAME": "Demo", "GIT_COMMITTER_EMAIL": "demo@example.invalid"}  # invented demo identity / fake key, privacy-guard: ignore
    (tmp / ".no-denylist").write_text("")
    (tmp / "tools").mkdir()
    shutil.copy(ROOT / "tools/privacy_guard.py", tmp / "tools/privacy_guard.py")
    run("git init -q -b main && git add tools && git commit -qm init", tmp, env)
    priv = [
        ("Install the privacy guard as a pre-commit hook (throwaway repo)", "python3 tools/privacy_guard.py --install"),
        (None, "printf 'AWS_ACCESS_KEY_ID=AKIAFAKEDEMO0000KEY1\\n' > config.env"),  # invented demo identity / fake key, privacy-guard: ignore
        (None, "git add config.env && git commit -m 'add config'; echo \"commit exit code: $?\""),
        ("Blocked: the fake key never reaches history", "git log --oneline"),
    ]
    first = True
    for caption, cmd in priv:
        step = {"cmd": cmd, "out": run(cmd, tmp, env), "hold": 1200, "prompt": "/tmp/demo-shop $"}
        if caption: step["caption"] = caption
        if first: step["clear"] = True; first = False
        steps.append(step)
    steps[-2]["hold"] = 3600
    steps[-1]["hold"] = 3200
finally:
    shutil.rmtree(tmp, ignore_errors=True)

session = {
    "title": "Agentic Engineering Kit",
    "prompt": "~/agentic-kit $",
    "width": 1180, "height": 720, "fontSize": 14,
    "highlight": [
        [r"^#", "#7ec4ff"], [r"no private denylist", "#7c8699"], [r"exit code: 1", "#f28b82"], [r"Recommended|Good alternative", "#7fd4a8"], [r"Poor fit", "#f28b82"],
        [r"BLOCK|block|secret|AWS access key|refus", "#f28b82"], [r"^- \*\*", "#e8d48b"], [r"installed|^[0-9a-f]{7} ", "#7fd4a8"],
    ],
    "steps": steps,
}
out = pathlib.Path(__file__).with_name("kit.json")
out.write_text(json.dumps(session, indent=1))
for st in steps:
    print("$", st["cmd"]); print(st["out"]); print()
