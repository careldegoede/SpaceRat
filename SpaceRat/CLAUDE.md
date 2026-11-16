# Space Rat — Claude Code Project Instructions

> **Purpose:** Develop the Unity game *Space Rat* efficiently, preserve project integrity, and minimise unnecessary Claude token and tool usage.
>
> **Default mode:** BALANCED. **Version control:** The developer uses GitHub Desktop for commits and pushes.

## 1. Working principles

- Work on the **specific requested feature or bug**, not unrelated areas.
- Inspect the smallest relevant set of files, scenes, prefabs, logs and dependencies before editing. Avoid full-project dumps.
- Prefer targeted local source-file edits over Unity commands that echo full scripts (especially `Unity_RunCommand` responses containing `localFixedCode`).
- Batch related edits into one logical change set. Avoid compile/refresh/console cycles after every small edit.
- Keep tool outputs short: relevant errors, changed paths, and concise diffs. Never paste full scripts or large logs unless requested or essential.
- Reuse existing Unity Asset Store assets and project conventions. Do not introduce replacement assets or dependencies without checking what is already available.
- Do not disable or uninstall Unity AI integrations, packages or MCP components merely to make a build pass. Diagnose the actual error first.
- Do not delete assets, reset scenes, rewrite working systems, or make destructive changes without explicit approval.

## 2. Operating modes

### BALANCED (default)
1. Read only the task-relevant context and files.
2. Briefly identify the intended changes; implement them together.
3. Compile once after the logical batch where possible.
4. Inspect only actionable compiler errors and warnings relevant to the change.
5. Fix issues and recompile if needed; do not run broad tests by default.
6. Ask the developer to manually test ordinary gameplay in Unity unless automated testing is clearly valuable.

### ECONOMY (when usage is limited)
- Make the smallest safe changes and minimise Unity round-trips.
- Do not enter Play Mode, take screenshots, or run repeated visual checks unless explicitly asked.
- Prefer static review, existing logs, and one targeted validation step.
- Give a short manual verification checklist and clearly state what was not tested.

### DEBUG (explicitly requested or necessary for a blocking failure)
- Reproduce or isolate the **specific** problem with targeted logs and checks.
- Identify likely cause before making broad changes.
- Validate the fix with the smallest reliable test.
- After **two unsuccessful attempts**, stop repeating the same approach; summarise evidence, change strategy, or ask for guidance.

The developer can override the mode by saying `Economy mode`, `Balanced mode`, or `Debug mode`.

## 3. Unity tool-call budget

- Avoid repeated sequences of `refresh → wait → console` for individual edits.
- Prefer one refresh/compile after multiple related file changes.
- When console output is required, request **errors first**, and return concise messages rather than full logs.
- Avoid Unity Play Mode automation for routine visual/gameplay checks. A full session may involve many calls (enter, wait, act, inspect, stop, reset).
- If Play Mode is necessary, plan a short deterministic sequence and clean up afterward (including `Time.timeScale` and editor state).
- Use reusable Unity Editor menu commands for repeated project operations when justified, such as:
  - `SpaceRat/Rebuild Scene03 Wiring`
  - `SpaceRat/Capture Terminal View`
  - `SpaceRat/Capture HUD Preview`
  - `SpaceRat/Reset Time Scale`
- Create such commands only if their repeat use offsets maintenance complexity; do not build tooling for a one-off task.
- Avoid screenshot loops: capture once, group feedback, and make a batch of changes.

## 4. MCP and model usage

- Keep only MCP servers needed for the current task. Space Rat commonly needs Unity integration; unrelated services such as Comfy, Intent Architect, DevExpress docs, Canva and Claude Docs should be disabled for this project when not needed.
- Use Sonnet for routine code, UI wiring, and straightforward bug fixes; reserve Opus for difficult architecture or debugging when warranted and available.
- Tool definitions, cached context, and billing vary by environment; do **not** assume a fixed token cost per call. The practical objective is fewer unnecessary calls and smaller outputs.

## 5. Session boundaries and persistent context

- Prefer one focused Claude session per feature or bug. Start fresh with `/clear` when context becomes unwieldy, **after** saving the necessary handoff.
- Treat `Plans/TODO.md` as the source of truth for active work, if it exists. Do not invent its contents.
- Maintain a short handoff note when appropriate: feature, changed files, design decisions, current state, known issues, next step.
- Avoid rereading the entire project or all prior notes at the start of every session.
- Keep architecture and decisions in project files rather than relying on long chat history.

## 6. Git and GitHub Desktop policy — IMPORTANT

**The developer owns Git operations through GitHub Desktop.**

- **Never commit, push, pull, fetch, create/switch branches, merge, rebase, reset, revert, stash, discard changes, or rewrite Git history unless explicitly asked.**
- You may inspect `git status` and focused diffs when necessary for safe development.
- Do not automatically stage files (`git add`).
- Do not overwrite uncommitted user changes; inspect and preserve them.
- At task completion, list changed files and provide a short summary suitable for a GitHub Desktop commit message, but **do not commit**.
- If the developer explicitly requests a Git action, carry out only that action and its clearly necessary prerequisites, respecting existing changes.

## 7. Scene, asset, and UI safeguards

- Preserve existing working scenes and serialized Unity references. Avoid unnecessary prefab/scene rewrites.
- Use existing artwork and UI layouts where suitable. For HUD bars, keep fill indicators dynamic in Unity rather than baking values into images.
- Do not regenerate, replace, move or rename assets without checking references and obtaining approval for disruptive changes.
- Avoid large binary asset changes unless the task requires them.
- Treat build failures from Unity AI integration as debugging work, not permission to remove the integration.

## 8. Response format (keep concise)

For normal tasks, report:

1. **Changed:** one or two sentences explaining what was done.
2. **Files:** paths of created/modified files.
3. **Validation:** what was actually compiled or tested; explicitly say if not tested.
4. **Next:** one short manual test or blocker, if any.

Do not provide long progress narration, repeat the prompt, or dump source files unless requested. Ask a question only when a decision is genuinely blocking.

## 9. Start-of-task checklist

- [ ] Confirm requested task and operating mode (default BALANCED).
- [ ] Read only relevant project instructions and source files.
- [ ] Check for existing user modifications before editing.
- [ ] Reuse assets and patterns already present.
- [ ] Plan a single logical batch and minimal validation.
- [ ] Avoid automatic Git operations.

## 10. End-of-task checklist

- [ ] No unrelated files or assets changed.
- [ ] Unity/editor state restored if testing modified it.
- [ ] Validation result stated accurately.
- [ ] Changed files and outstanding issues summarised.
- [ ] Developer can review and commit through GitHub Desktop.

## 11. Example task instruction

> **BALANCED mode.** Implement [feature] in Space Rat. Inspect only relevant files, preserve existing scenes and assets, batch the edits, compile once, report concise errors only, and leave gameplay testing to me unless essential. Do not commit or push; I'll use GitHub Desktop.

---

### Basis for this guide

Adapted from the project's `Usage.md` (2026-11-16), which identified excessive Unity round-trips, full-script echoes, long sessions, unused MCP servers, screenshot iterations, and model selection as major usage drivers. This file turns those observations into day-to-day Claude Code instructions.
