#!/usr/bin/env bash
# Runs every test suite in this repo, the same way CI does.
set -euo pipefail
cd "$(dirname "$0")/.."
echo "==> dotnet test AgenticEngineeringKit.sln -c Release --nologo"
dotnet test AgenticEngineeringKit.sln -c Release --nologo
echo "==> python3 -m unittest discover -s tests/tools"
python3 -m unittest discover -s tests/tools
echo "==> python3 tools/privacy_guard.py --all"
python3 tools/privacy_guard.py --all
echo 'All tests passed.'
