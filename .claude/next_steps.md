# Next Steps: The Canalave Library

## What you have now

Seven files ready to use:

| File | What it is |
|------|------------|
| `claude_v3.md` | Universal CLAUDE.md — rename to `CLAUDE.md` and place in repo root. Every Claude Code session reads this automatically. |
| `step1_conventions_v3.md` | Instructions for Step 1: establish conventions baseline |
| `step2_axes_v3.md` | Instructions for Step 2: derive the grid axes |
| `step3_classify_v3.md` | Instructions for Step 3: classify every cell in the grid |
| `step4_resolve_v3.md` | Instructions for Step 4: resolve Stage-1 gaps |
| `step5_workplan_v3.md` | Instructions for Step 5: produce the workplan |
| `step6_implement_v3.md` | Instructions for Step 6: build features per workplan |
| `model_selection_guide.md` | Personal reference — portable model-selection principles (not loaded by Claude Code) |
| `canalave_library_spec.md` | The design-history spec (goes in repo root, unchanged) |

## One-time setup (do once before Step 1)

### Place files in your repo

```
# In your repo root:
cp claude_v3.md CLAUDE.md
cp canalave_library_spec.md .   # if not already there

# Create the directories the process will write into:
mkdir -p features
mkdir -p .claude/skills/canalave-conventions
```

Step instruction files do NOT go in the repo — they're prompts you feed to sessions one at a time. Keep them wherever is convenient for you to reference.

### Install and update Claude Code

```
npm install -g @anthropic-ai/claude-code
claude update
claude --version   # needs v2.1.170+
```

### Install the VS Code extension

Open VS Code → Extensions (Ctrl+Shift+X) → search "Claude Code" → install the one from Anthropic. Requires VS Code 1.98.0+. The extension wraps the same CLI — same models, same CLAUDE.md, same skills, same MCP servers.

### Set up Context7 MCP

This gives Claude Code access to live framework documentation instead of relying on training data. Run once:

```
claude mcp add context7 -- npx -y @upstash/context7-mcp
```

Verify with `claude mcp list`. Restart any open Claude Code session after adding it.

To use it during a session, append "use context7" to prompts where you want current docs fetched (Step 1 relies on this heavily).

### Configure permissions

Create `.claude/settings.json` in your repo:

```json
{
  "permissions": {
    "allow": [
      "Read(**)",
      "Glob(**)",
      "Grep(**)",
      "Bash(dotnet build *)",
      "Bash(dotnet test *)",
      "Bash(git status)",
      "Bash(git log *)",
      "Bash(git diff *)",
      "Bash(find *)",
      "Bash(cat *)",
      "Write(features/**)",
      "Write(status.md)",
      "Write(workplan.md)",
      "Write(audit-summary.md)",
      "Write(.claude/skills/**)"
    ],
    "deny": [
      "Write(**.cs)",
      "Write(**.csproj)",
      "Write(**.razor)",
      "Edit(**.cs)",
      "Edit(**.csproj)",
      "Edit(**.razor)"
    ]
  }
}
```

This allows reading everything and writing markdown outputs while blocking source-file modifications. Steps 1–5 are read-only on the codebase; Step 6 (implementation) will need these deny rules relaxed when you get there.

### Check your environment

Make sure `ANTHROPIC_API_KEY` is NOT set in your environment — if it is, Claude Code bills at API rates instead of using your Pro subscription:

```
echo $ANTHROPIC_API_KEY   # should be empty
```

If set, unset it: `unset ANTHROPIC_API_KEY`

### Git safety snapshot

```
git checkout -b pre-audit-snapshot
git add -A && git commit -m "snapshot before audit process"
git checkout main
```

## The six steps

### Step 1 — Conventions baseline
**Tool:** Opus 4.8 in Claude Code (VS Code extension)
**Time:** One session
**Usage note:** Opus is 1× usage. Check `/usage` before starting to ensure a fresh window.

Open your project in VS Code. Open the Claude Code panel. Set model: `/model opus`. Verify with `/status`.

Feed the contents of `step1_conventions_v3.md` as your prompt, or use @-mention: paste a brief framing line and reference the file. Let it research current Blazor/EF Core docs (via Context7 or web search) and produce the skill folder at `.claude/skills/canalave-conventions/`.

**Output:** `.claude/skills/canalave-conventions/SKILL.md` plus supporting reference files.

**Before moving on:** Skim the SKILL.md and reference files. This is the yardstick for everything downstream — if something looks wrong about how it interprets your architectural choices, fix it now. This is the one review checkpoint in the early steps that has the highest leverage.

---

### Step 2 — Derive the grid axes
**Tool:** Sonnet in chat (claude.ai or Claude app, NOT Claude Code)
**Time:** One conversation, iterative

Upload `canalave_library_spec.md` AND the draft `SKILL.md` from Step 1 as context. Work through the layer candidates and feature list interactively, refining until both feel right.

**Output:** A layer list and dependency-ordered feature list. Can be a simple markdown table or list — this is the grid's shape that Step 3 populates.

**Before moving on:** You should feel confident in the axes. Every cell in the grid comes from this. If you're unsure about a layer candidate, that's the moment to push on it — not after 30 features have been classified against it.

---

### Step 3 — Classify the grid
**Tool:** Opus 4.8 in Claude Code (VS Code extension)
**Time:** One session, potentially long
**Usage note:** This is the most token-intensive step — Opus reading the full codebase + spec + conventions. Check `/usage` for a fresh window. Monitor periodically.

Open a new Claude Code session (fresh, not continuing Step 1's conversation). CLAUDE.md is loaded automatically. The conventions skill is available.

Feed `step3_classify_v3.md` as the prompt, along with the axes output from Step 2 (paste it or save it as a file and @-mention it).

**Output:** `status.md`, `features/<name>.md` (one per feature), `audit-summary.md`. May also refine the conventions skill.

**If the session ends early:** Whatever's been written to disk IS the progress. Start a new session, point it at what exists, and ask it to continue covering the features/layers not yet addressed.

**Before moving on:** Read `audit-summary.md` first — it's the human-facing overview. Check the Stage-1 landscape (section 4) to understand what Step 4 needs to tackle. Scan Stage-4 diagnoses in the relevant `features/<name>.md` files for anything that seems wrong.

---

### Step 4 — Resolve Stage-1 cells
**Tool:** Sonnet in chat (conceptual gaps) or Claude Code (code-relationship gaps) + your own editing
**Time:** Multiple sessions, iterative, human-driven

This is not one prompt — it's you working through the Stage-1 backlog. The audit-summary's Stage-1 landscape tells you what needs resolving and how gaps cluster.

For each conceptual gap: open a Sonnet chat, upload `conventions.md` (the SKILL.md) as context, explore the decision, settle on an answer, then edit the relevant `features/<name>.md` and `status.md` yourself.

For code-relationship gaps: open Claude Code, investigate, update the files.

**Output:** Updated `status.md` and `features/<name>.md` files with cells advanced past Stage 1.

**"Done enough" means:** Foundational and mid-dependency-chain cells are resolved. Leaf-node cells that nothing depends on can wait.

---

### Step 5 — Produce the workplan
**Tool:** Opus 4.8 in Claude Code
**Time:** One session

Feed `step5_workplan_v3.md` as the prompt. Opus reads the post-Step-4 state of `status.md` and all `features/<name>.md` files, produces `workplan.md` with sequenced entries.

**Output:** `workplan.md` with a preamble explaining the ordering rationale.

**Before moving on:** Read the workplan. Each entry names cells, tool, and pointer to the feature file. The ordering should make sense — foundational work first, composed features later, nothing depending on something that hasn't been done yet.

---

### Step 6 — Implementation
**Tool:** Per entry — opusplan for Stage-2 cells, Sonnet for Stage-3 cells, Opus for Stage-4 reconciliation
**Time:** Ongoing, one work-unit at a time

**Before starting Step 6:** Relax the permissions in `.claude/settings.json` — remove or comment out the deny rules for `.cs`, `.csproj`, and `.razor` files, since this step writes code.

Pick the first entry from `workplan.md`. Read its `features/<name>.md` pointer. Invoke the right tool per CLAUDE.md's per-stage guidance. After completion, update `status.md` and `workplan.md`.

Repeat.

## Usage budget awareness

All Claude surfaces (claude.ai chat, Claude Code, desktop app) share the same 5-hour rolling usage window on Pro. Practical implications:

- Don't do heavy chat exploration in claude.ai right before a Claude Code session.
- Opus is 1× usage. Steps 1, 3, and 5 all use Opus in Claude Code — space them if your window is tight.
- Step 2 and Step 4 use Sonnet in chat, which is much lighter on the budget.
- `/usage` in Claude Code shows your current session and weekly usage with time remaining.
- The window is rolling, not midnight-reset — usage from 5 hours ago has already fallen off.

## If something goes wrong

**Session ends mid-step:** On-disk artifacts (whatever status.md, features/*.md, conventions skill files exist) are the checkpoint. Open a fresh session, point it at what's on disk, continue.

**Axes feel wrong after Step 3 starts:** If classification reveals a layer or feature the axes missed, the step instructions tell Opus to flag it in audit-summary.md rather than silently expanding the grid. You return briefly to Step 2 to adjust, then continue Step 3.

**Conventions need updating during Step 6:** Implementation experience may reveal a convention that should change. Update the relevant reference file in the conventions skill rather than silently diverging — the skill is a living document after Step 1, not frozen.

**A cell's Stage turns out wrong:** The per-stage process guidance in CLAUDE.md handles this — "stop and flag" rather than improvise. A misclassified cell gets reclassified and routed to the right tool. This is normal, not a failure.
