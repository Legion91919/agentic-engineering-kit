---
name: visual-demo
description: Record how a product looks and feels before anyone runs it - a captioned walkthrough video (MP4) and autoplaying GIF of the main flow, desktop and mobile screenshots, and a "See it in action" README section, using the kit's Playwright recorder. Use when a slice is demoable, before publishing a repo, or when asked for screenshots, videos or visuals of a project.
---

# Visual demo

A reviewer decides in ten seconds whether to look further. Show the running product at the top of the README.

## What to produce (in `docs/media/`)
- `demo.gif`: 15–30 s, the main flow with one caption per step. Under ~8 MB so GitHub autoplays it.
- `demo.mp4`: the same recording at full quality.
- 3–5 screenshots of the key states (named by what they show, e.g. `group.png`, `contribute.png`), plus `mobile.png`.

## Setup (once per repo)
1. Copy `tools/demo/record.mjs` from the kit into `demo/record.mjs`; add `demo/package.json` with `"playwright": "1.63.0"` (match the installed browsers) and a `record` script (`node record.mjs ..`).
2. `demo/demo.config.json`: the commands that start the app, a `ready` URL for each, and the `url` to open. Use the same ports as the runbook.
3. `demo/scenario.mjs`: `export default async ({ page, step, shot }) => { ... }`, plus optional `export async function mobile({ page, shot })`.
4. Git-ignore `demo/node_modules/` and `docs/media/.video/`.

## Writing the scenario
- **Tell the story of slice 1** in 4–7 steps. Each `step("caption", fn)` caption says what the viewer is seeing and why it matters ("A retry with the same key creates one ledger entry"), not which button is pressed.
- Include **one proof moment** that shows the engineering, not just the UI: a refused non-member, a deduplicated retry, a quarantined file, a live update arriving.
- Use accessible locators (`getByRole`, `getByLabel`, visible text); never CSS classes that might change.
- Use invented demo data only. Never record real accounts, real names or secrets.

## Run and review
```
cd demo && npm install && npm run record
```
Then review it like a designer: extract frames (`ffmpeg -i docs/media/demo.mp4 -vf "fps=1/3,scale=480:-1,tile=4x3" -frames:v 1 frames.jpg`) and look at them. Fix captions that cover the important area, steps that move too fast, and empty or error states that appear by accident.

## README
Put `## See it in action` right after the intro paragraph: the GIF (with descriptive alt text), a small table of 3 screenshots, and a link to the MP4 and to how it was recorded.

## Projects without a UI
Show the system instead: render the Mermaid diagrams from `docs/DESIGN.md` to PNG/SVG, or record a terminal session of the CLI or tests as a GIF.
For a terminal session, capture the commands' real output to a session JSON, then replay it with `tools/demo/terminal.mjs` (`node terminal.mjs <session.json> docs/media <name>` for MP4 + GIF, `--still` for one PNG). Never type invented output; secrets in a demo must be obviously fake and in a throwaway repo.
