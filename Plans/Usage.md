# Claude ↔ Unity: where the usage goes (and how to save it)

Notes from 2026-11-16, after a long Scene 3 session burned the 5-hour limit quickly.

## Why it costs so much

Every tool call re-sends the **whole conversation so far**. Cost ≈ *conversation length × number of tool calls*.
Late in a long session, each small step (refresh, compile wait, console check) costs as much as a big one.

Biggest drivers, in order:

1. **Many small Unity round-trips.**
   - A Play Mode test is ~10 calls: enter play, wait for the intro, place the pilot, check, act, check, stop, reset `timeScale`.
   - Every code change costs 3 calls: refresh, wait for compile, read console.
2. **Unity echoes scripts back.** Every `Unity_RunCommand` result includes the full script again (`localFixedCode`).
   Each command lands in the conversation twice, and that text is re-sent on every later step.
3. **Long sessions.** Context grows until it has to be summarised; after that, files often get re-read to continue.
4. **Unused MCP servers.** Comfy, Intent Architect, DevExpress docs, Canva and Claude Docs are connected.
   Their tool lists and instructions are part of every request even though Space Rat never uses them.
5. **Screenshot iterations.** Each render is ~1–2k tokens; visual tweaks often take 2–3 rounds.
6. **Model choice.** Opus uses more of the limit than Sonnet for the same work.

## What helps most

- [ ] **One fresh session per feature** (`/clear`). `Plans/TODO.md` and the memory notes carry the context over.
- [ ] **Disconnect unused MCP servers** for this project (`/mcp`, or the Claude.ai connector settings).
- [ ] **Sonnet for routine work** (`/model sonnet`); switch to Opus for tricky design or debugging.
- [ ] **Skip Claude's Play Mode tests** unless something is risky. Test it yourself instead (~10 calls saved per feature).
- [ ] **Editor menu items for repeated scripts**, so each becomes one short `ManageMenuItem` call instead of ~40 lines sent twice. Candidates:
  - `SpaceRat/Rebuild Scene03 Wiring`
  - `SpaceRat/Capture Terminal View`
  - `SpaceRat/Capture HUD Preview`
  - `SpaceRat/Reset Time Scale`
- [ ] **Batch requests.** Several fixes in one message share the compile and test steps.
- [ ] **Accept "good enough" visuals sooner.** Fewer screenshot rounds; fine-tune by hand in the Inspector.
