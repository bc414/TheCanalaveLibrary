# To do, owner: what only Brian or an outside party can decide or do

Brian, to do. Each entry is a decision only Brian (or an outside party he engages) can make, a
check only he can run, or a task only he can take. Claude never decides or does these; it records
them, says what holds in the meantime, and works around them.

The other three cells: Brian's rulings are in `.claude/rules/`; Claude's built defaults waiting for
his ruling are in `DEFAULTS.md`; what Claude builds next is `TODO-AGENT.md`.

Nothing here is for Brian's hands-on pass of the site: that pass is free use. Entries are due
before beta or before launch.

## Kinds

- **`decide`**: a choice only Brian or an outside party can make (a vendor, legal terms, a licence).
  `meanwhile:` says what holds until then.
- **`check`**: a manual pass only Brian can run (keyboard and screen reader, authenticator logins).
- **`task`**: an action only Brian or an outside party can take (a licence application, DNS records,
  counsel).

## When

- **`beta`**: before anyone else uses the site.
- **`launch`**: before the site goes live.

when values: beta, launch

## How an entry lives

- **Added** at any point in a run: when work is found that only Brian or an outside party can do.
- **Removed** when it is done or decided. A decision becomes a ruling: the commit quotes Brian's
  typed words in its `Brian:` section, any part code cannot show goes into `.claude/rules/`, and
  anything it requires Claude to build becomes a `TODO-AGENT.md` entry.
- **Ids are never reused.** Take the id from `next id:` below and raise `next id:` by one in the
  same edit. Never lower it.

## Fields

```
### TO-NNNN · decide|check|task · beta|launch · <title, in plain words>
- meanwhile: <what holds until then>    (required for decide)
- why: <one line, ending with any old ids in parentheses>
- code: <repo-relative paths>           (optional)
- added: YYYY-MM-DD
```

## Grep recipes

| Question | Command |
|---|---|
| Everything, one per row | `grep -n '^### TO-[0-9]' TODO-OWNER.md` |
| What blocks beta | `grep -n '^### TO-[0-9].* · beta · ' TODO-OWNER.md` |
| What blocks launch | `grep -n '^### TO-[0-9].* · launch · ' TODO-OWNER.md` |
| An entry's history after it is done | `git log --grep 'TO-0042'` |

The plugin's list check (its command is in each session's plugin context, run from the repo root) checks this file's grammar, ids and paths with the other two lists.

next id: TO-0001

## Entries
