---
name: supply-chain-security
description: Secure what the code depends on and how it is built - pinned dependencies and lock files, Dependabot, vulnerable-package checks (dotnet, npm, pip-audit), CycloneDX SBOMs, signed tags, hardened GitHub Actions (actions pinned by SHA, least-privilege permissions, no secrets for fork PRs), secret scanning with gitleaks and the privacy guard. Use when setting up a repo or CI, adding a dependency or a workflow, before a release, or at hand-off.
---

# Supply chain security

Most of the code you ship was written by someone else. Know what it is, pin it, scan it and rebuild it the same way every time.

Quick start, from the kit: `python3 tools/add_security_baseline.py <repo>` copies `dependabot.yml`, a CodeQL workflow for the languages it finds, and `SECURITY.md` into a repo without overwriting anything.

## 1. Pin and lock
| Stack | Lock | Check |
|---|---|---|
| .NET | `<RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>` in `Directory.Build.props`; commit `packages.lock.json` | CI restores with `dotnet restore --locked-mode` |
| npm | commit `package-lock.json` | CI uses `npm ci`, never `npm install` |
| Python | `requirements.lock` / `uv.lock` / `poetry.lock` with hashes | CI uses `pip install --require-hashes -r requirements.lock` or `uv sync --frozen` |

Also: central package versions (`Directory.Packages.props`), no floating versions (`grep -rnE 'Version="[^"]*\*' --include=*.csproj .` is empty), and a `nuget.config` with `<clear/>` and only the feeds you trust (with package source mapping) to block dependency confusion.

## 2. Keep it current: Dependabot
Copy `templates/github/dependabot.yml` to `.github/dependabot.yml`. It checks nuget, npm, pip and github-actions weekly and groups minor and patch updates into one PR per ecosystem, so reviews stay small. Majors come alone. Delete the ecosystems the repo doesn't use. Turn on Dependabot alerts and security updates in the repo settings.

## 3. Scan for known vulnerabilities (fail CI on findings)
```bash
dotnet list package --vulnerable --include-transitive | tee vuln.txt
! grep -q "has the following vulnerable packages" vuln.txt
npm audit --audit-level=high --omit=dev
pip-audit -r requirements.lock --strict          # or: pip-audit (inside the venv)
```
A finding you can't fix yet gets an entry in `docs/THREAT-MODEL.md` (accepted risk, owner, review date), not a silenced gate.

## 4. Static analysis: CodeQL
Copy `templates/github/codeql.yml` to `.github/workflows/codeql.yml` and keep only the languages in the repo (the baseline script does this). It runs on PRs, on `main` and weekly.

## 5. SBOM (CycloneDX)
Produce one per release and attach it to the release:
```bash
dotnet tool install --global CycloneDX
dotnet CycloneDX MySolution.sln -o sbom -fn sbom-dotnet.json -j      # flags vary by version: dotnet CycloneDX --help
npx @cyclonedx/cyclonedx-npm --output-file sbom/sbom-npm.json
pip install cyclonedx-bom && cyclonedx-py requirements requirements.lock -o sbom/sbom-python.json
```
Check: the SBOM lists the same top-level packages as the lock files.

## 6. Signed tags and protected branches
- Sign release tags: `git tag -s v1.2.0 -m "v1.2.0"` (GPG or SSH signing: `git config gpg.format ssh`). Verify with `git tag -v v1.2.0`.
- Protect `main`: PRs required, status checks required, no force push, signed commits if the team can support it.

## 7. Harden GitHub Actions
| Rule | Proof |
|---|---|
| Pin third-party actions to a full commit SHA, with the tag in a comment: `uses: actions/checkout@<40-char-sha> # v4.2.2` | `grep -rnE "uses: [^ ]+@(v[0-9]+|main|master)\b" .github/workflows/` is empty (Dependabot's github-actions ecosystem keeps SHAs current) |
| Least-privilege token: `permissions: contents: read` at the top, wider scopes only on the job that needs them | `grep -L "^permissions:" .github/workflows/*.yml` is empty |
| No secrets for code from forks: never use `pull_request_target` with a checkout of the PR head; secrets only in `push`, `workflow_dispatch` or protected environments | `grep -rn "pull_request_target" .github/workflows/` is empty or justified |
| No script injection: never put `${{ github.event.* }}` (titles, branch names, bodies) straight into `run:`; pass through `env:` | `grep -rnE "run:.*\\$\{\{ *github\.event\." .github/workflows/` is empty |
| Deploy with OIDC (`id-token: write`) instead of long-lived cloud keys | no cloud keys in repo secrets |
| `persist-credentials: false` on checkout when later steps don't push | read each checkout step |

## 8. Secrets and private names
- `gitleaks detect --no-banner` locally and `gitleaks/gitleaks-action` in CI (already in `templates/ci/dotnet-ci.yml`).
- The privacy guard as a pre-commit hook: `python3 tools/privacy_guard.py --install`; in CI `python3 tools/privacy_guard.py --all`. See the `privacy-guard` skill.
- Turn on GitHub secret scanning and push protection.
- A leaked secret is rotated first, then removed from history. Removing it alone is not enough.

## Done when
Lock files are committed and enforced, Dependabot and CodeQL are on, the three audit commands pass in CI, actions are pinned by SHA with `permissions:` set, gitleaks and the privacy guard are green, and the release has an SBOM and a signed tag.
