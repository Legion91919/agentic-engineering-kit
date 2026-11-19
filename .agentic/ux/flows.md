# User flows

The kit's "UI" is the agent conversation and the CLI.

1. **New project**: `tools/new_project.py` → open in Claude Code → `/baton` → frame → map → decide (ADR-0001) → sketch → first slice.
2. **Retrofit an existing repo**: `arch-advisor stack <path>` → `project-context` skill → `.agentic/` filled from the code.
3. **Before handover**: `arch-advisor check <path> --min 80` → `handover` skill.
4. **Before going public**: `python tools/privacy_guard.py --all` → fix → publish.
