# Defaults: Claude's standing choices, waiting for Brian

Claude, standing. Each line is a default Claude reasoned out and built into the code because
nothing Brian had ruled covered it. It binds later work until Brian rules on it; it never binds
Brian. He rules on these after his hands-on pass of the running site, or in the abstract, whichever
suits the line.

The other three cells: Brian's rulings are in `.claude/rules/`; what Claude builds next is
`TODO-AGENT.md`; what only Brian or an outside party can decide or do is `TODO-OWNER.md`.

## Who reads it

- **Agents in later work.** Before changing behavior, grep this file for the paths you are
  touching and stay consistent with the lines you find. Two work units must never answer the same
  question differently.
- **Claude in Brian's review sessions.** When a request overturns a line, say so, then do what he
  asked, and remove the line in the same commit.
- **Brian, at reconcile:** he ratifies or overturns each remaining line.

## What gets a line

A default Claude built gets a line if any of these hold:

1. It changes what a user can do, see or is told, on a path Brian might not walk in normal use
   (refusals, limits, edge states, notification rules, privacy, moderation outcomes).
2. It has security, privacy or data consequences that do not show in use.
3. Later work must stay consistent with it: other code will follow it as a rule.

A spec deviation also gets a line: Claude-built code that differs from the spec with no recorded
ruling covering it. The built behavior is kept until Brian rules.

Not here:
- **Implementation choices** (names, structure, internal library use) and styling seen on every
  page that the conventions already govern: the commit message's `Defaults:` section only.
- **Anything a recorded ruling of Brian's covers:** that is his, in `.claude/rules/`.
- **Work not yet built:** `TODO-AGENT.md`. It gets a line here when it is built.
- **Anything needing Brian or an outside party:** `TODO-OWNER.md`.

Consolidation changes no behavior, so it writes no lines; a consolidation that wants one has
changed behavior.

## How a line lives

- **Added** in the commit that builds its default, at any point in a run, including for an edge
  case met mid-task. `default:` says what the code does.
- **Changed** in the same commit as any change to the behavior it describes, including moved or
  renamed `code:` paths. A later choice by Claude on the same question replaces the line, with a
  new `taken:` date.
- **Removed** in the commit that records Brian's ruling, ratified or overturned. That commit names
  the id and quotes his typed words in its `Brian:` section; a ruling code cannot show (a rejection,
  a reason) moves into `.claude/rules/`. Silence is not a ruling: a line stays until he rules.
- **Ids are never reused.** Take the id from `next id:` below and raise `next id:` by one in the
  same edit. Never lower it.

## Fields

```
### DF-NNNN · visible|invisible · <the question, in plain words>
- default: <what the site does, as Claude chose it>
- why: <one line, ending with any old ids in parentheses>
- code: <repo-relative path>, <another path>
- taken: YYYY-MM-DD
```

- `visible`: ordinary use of the feature would plausibly show it, so Brian's hands-on pass can
  settle it.
- `invisible`: it needs deliberate setup to see (an edge state, a race, an API call) or does not
  show at all (security, privacy, retention); it is settled at reconcile.
- One line per question, however many paths it touches. Paths only, no line numbers.

## Grep recipes

| Question | Command |
|---|---|
| Every line, one per row | `grep -n '^### DF-[0-9]' DEFAULTS.md` |
| The reconcile pile | `grep -n '^### DF-[0-9].* · invisible · ' DEFAULTS.md` |
| Lines pointing at a file you are changing | `grep -n -B3 'path/to/File.cs' DEFAULTS.md` |
| A line's history after it is gone | `git log --grep 'DF-0042'` |

The plugin's list check (its command is in each session's plugin context, run from the repo root) checks this file's grammar, ids and paths with the other two lists.

next id: DF-0001

## Entries
