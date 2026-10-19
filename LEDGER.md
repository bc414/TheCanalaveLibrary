# Ledger: Claude's defaults, waiting for Brian

The one list of choices Claude made on its own while building, plus the decisions only someone
outside the code can make (licences, legal, vendors, launch). Every line is Claude's and
unratified until Brian rules on it. Brian reads it after his hands-on pass of the running site;
until then it keeps later work consistent.

## Who reads it

- **Agents in later work.** Before changing behavior, grep this file for the paths you are
  touching and stay consistent with the lines you find. Two work units must never answer the same
  question differently.
- **Claude in Brian's review sessions.** When a request overturns a line, say so, then do what he
  asked.
- **Brian, at reconcile:** after the hands-on pass, he ratifies or overturns each remaining line.

## What gets a line

A choice gets a line if any of these hold:

1. It changes what a user can do, see or is told, on a path Brian might not walk in normal use
   (refusals, limits, edge states, notification rules, privacy, moderation outcomes).
2. It is invisible in use but has security, privacy or data-loss consequences.
3. Later work must stay consistent with it: other code will follow it as a rule.
4. It needs someone outside the code (licence, legal, vendor, launch). Recorded as `external`,
   never decided by Claude.

A spec deviation found during a task (Claude-built code that differs from the spec, with no
recorded ruling covering it) also gets a line: the current behavior is kept until Brian rules.

Everything else goes only in the commit message's `Defaults:` section: implementation choices
(names, structure, internal library use) and styling seen on every page that the conventions
already govern. Consolidation changes no behavior, so it writes no lines; a consolidation that
wants one has changed behavior.

## How a line lives

- **Added** in the commit that builds its default. `default:` says what the code does at the
  time of writing, so a default is written here only once it is built. External lines are added
  when the need is found.
- **Changed** in the same commit as any change to the behavior it describes, including moved or
  renamed `code:` paths. A later choice by Claude on the same question replaces the line, with a
  new `taken:` date.
- **Removed** in the commit that records Brian's ruling, ratified or overturned. That commit names
  the id and quotes his typed words in its `Brian:` section. Silence is not a ruling: a line stays
  until he rules on it.
- **Ids are never reused.** Take the id from `next id:` below and raise `next id:` by one in the
  same edit. Never lower it.

Brian's statements outrank every line, and a line that contradicts a recorded ruling of his is
wrong. When code contradicts a line, the line is stale: fix whichever the commit intends.

## Fields

```
### L-NNNN · default · visible|invisible · <the question, in plain words>
- default: <what the site does, as Claude chose it>
- why: <one line>
- code: <repo-relative path>, <another path>
- taken: YYYY-MM-DD

### L-NNNN · external · <the question, in plain words>
- default: <what holds in the meantime>
- why: <one line>
- code: <optional, repo-relative paths>
- taken: YYYY-MM-DD
```

- `visible`: Brian would meet it while using the site; his hands-on pass can settle it.
- `invisible`: it does not show in use (security, privacy, data retention); it is settled at
  reconcile, after the pass.
- One line per question, however many code paths it touches. Paths only, no line numbers.

## Grep recipes

| Question | Command |
|---|---|
| Every open line, one per row | `grep -n '^### L-' LEDGER.md` |
| Invisible defaults | `grep -n '^### L-.* · invisible · ' LEDGER.md` |
| External items | `grep -n '^### L-.* · external · ' LEDGER.md` |
| Lines pointing at a file you are changing | `grep -n -B3 'path/to/File.cs' LEDGER.md` |
| A line's history after it is gone | `git log --grep 'L-0042'` |

## The gate

`node scripts/check-ledger.mjs` checks the grammar above, that ids are unique and below
`next id:`, and that every `code:` path exists. `node --test scripts/check-ledger.test.mjs` holds
a failing fixture for each rule. The gate runs with the other checks before every commit.

next id: L-0001

## Entries
