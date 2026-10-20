# To do, agent: what Claude builds next

Claude, pending. Each entry is work Claude can do entirely on its own. Anything that needs Brian or an
outside party first belongs in `TODO-OWNER.md` instead.

The other three cells: Brian's rulings are in `.claude/rules/`; Claude's built defaults waiting for
his ruling are in `DEFAULTS.md`; what only Brian or an outside party can do is `TODO-OWNER.md`.

## Kinds, in the order they are worked

1. **`bug`**: the code does something wrong and the right answer is clear.
2. **`ruling`**: build something Brian ruled. `source:` says where the ruling lives.
3. **`feature`**: new behavior Claude chose to build (a policy default the code does not do yet,
   or an edge case met mid-task whose build belongs in later work). Once built, the choice in it
   becomes a `DEFAULTS.md` line.
4. **`polish`**: small quality work, including tests and accessibility fixes.

Within a kind, lower ids first.

## How an entry lives

- **Added** at any point in a run: when work is found that is not done in the current task.
- **Removed** in the commit that does the work. That commit names the id. If the work involved a
  choice, the same commit writes its `DEFAULTS.md` line and re-points any code comment citing the
  entry's id.
- **`blocked:`** is written when an entry fails twice, so later work knows why; the run then moves
  on. Removing the reason means it is worth trying again.
- **Ids are never reused.** Take the id from `next id:` below and raise `next id:` by one in the
  same edit. Never lower it.

## Fields

```
### TA-NNNN · bug|ruling|feature|polish · <title, in plain words>
- do: <the behavior to build, stated as what the code must do>
- why: <one line, ending with any old ids in parentheses>
- code: <repo-relative paths>              (not required for polish)
- source: <rules-file path or commit hash> (ruling only)
- blocked: <reason, with its date>         (optional)
- added: YYYY-MM-DD
```

## Grep recipes

| Question | Command |
|---|---|
| Everything, one per row | `grep -n '^### TA-[0-9]' TODO-AGENT.md` |
| One kind (bugs here) | `grep -n '^### TA-[0-9].* · bug · ' TODO-AGENT.md` |
| Blocked entries | `grep -n -B5 '^- blocked:' TODO-AGENT.md` |
| Entries touching a file | `grep -n -B3 'path/to/File.cs' TODO-AGENT.md` |
| An entry's history after it is done | `git log --grep 'TA-0042'` |

The plugin's list check (its command is in each session's plugin context, run from the repo root) checks this file's grammar, ids and paths with the other two lists.

next id: TA-0001

## Entries
