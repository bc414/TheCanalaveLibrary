// check-ledger.mjs: the mechanical gate for LEDGER.md. Version 1 (2026-10-19).
//
// One script shared by TheCanalaveLibrary and StoryPlanner3. Copies are kept identical by hand;
// bump the version line when changing it so a drifted copy shows at a glance.
//
// Scope is fixed: one file (LEDGER.md), its line grammar, and that its `code:` paths exist. It does
// not judge meaning. Growing the scope (other docs, meaning, git history) is a decision for Brian:
// a run that wants more writes a ledger line saying so instead of building it (Brian, 2026-10-19:
// "Agreed it stays as a script until scope growth tripwire").
//
//   node scripts/check-ledger.mjs [path/to/LEDGER.md]     exit 0 = clean, 1 = violations listed
//   node --test scripts/check-ledger.test.mjs              the known-bad fixtures

import { existsSync, readFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const SEP = ' · ';
const KINDS = ['default', 'external'];
const VISIBILITIES = ['visible', 'invisible'];
const FIELDS = {
  default: { required: ['default', 'why', 'code', 'taken'], optional: [] },
  external: { required: ['default', 'why', 'taken'], optional: ['code'] },
};
const ALL_KEYS = ['default', 'why', 'code', 'taken'];

// Returns a list of violation strings; empty means clean. `pathExists(relPath)` answers whether a
// repo-relative path exists, so tests can run without a filesystem.
export function checkLedger(text, pathExists) {
  const errors = [];
  const lines = text.replace(/\r\n/g, '\n').split('\n');
  const at = (i, msg) => errors.push(`line ${i + 1}: ${msg}`);

  const entriesAt = lines.findIndex((l) => l.trim() === '## Entries');
  if (entriesAt < 0) {
    errors.push('missing the "## Entries" line that separates the header from the entries');
    return errors;
  }

  let nextId = null;
  for (let i = 0; i < entriesAt; i++) {
    const m = lines[i].match(/^next id: L-(\d{4})$/);
    if (m) {
      if (nextId !== null) at(i, 'a second "next id:" line');
      nextId = Number(m[1]);
    }
    // Only a real id counts: the header's own field template ("### L-NNNN · ...") must not trip this.
    if (/^### L-\d{4}/.test(lines[i])) at(i, 'an entry heading above "## Entries"');
  }
  if (nextId === null) errors.push('missing the header line "next id: L-NNNN"');

  const seen = new Map();
  let entry = null;
  let afterBlank = true;

  const finish = () => {
    if (!entry || !entry.kind) return;
    const spec = FIELDS[entry.kind];
    for (const key of spec.required) {
      if (!entry.fields.has(key)) at(entry.line, `${entry.id} is missing "- ${key}:"`);
    }
    for (const [key, { value, line }] of entry.fields) {
      if (!spec.required.includes(key) && !spec.optional.includes(key)) {
        at(line, `${entry.id}: "${key}" is not allowed on a ${entry.kind} line`);
      }
      if (key === 'taken' && !isDate(value)) at(line, `${entry.id}: taken must be a real date, YYYY-MM-DD`);
      if (key === 'code') {
        for (const p of value.split(', ')) {
          if (!p || p.startsWith('/') || p.includes('\\') || p.split('/').includes('..') || /^[A-Za-z]:/.test(p)) {
            at(line, `${entry.id}: code path "${p}" must be repo-relative with forward slashes`);
          } else if (!pathExists(p)) {
            at(line, `${entry.id}: code path "${p}" does not exist`);
          }
        }
      }
    }
  };

  for (let i = entriesAt + 1; i < lines.length; i++) {
    const line = lines[i];
    if (line.trim() === '') {
      finish();
      entry = null;
      afterBlank = true;
      continue;
    }

    if (line.startsWith('### ')) {
      finish();
      afterBlank = false;
      entry = { line: i, fields: new Map() };
      const m = line.match(/^### (L-(\d{4}))(.*)$/);
      if (!m) { at(i, 'heading must start "### L-NNNN · "'); entry = null; continue; }
      entry.id = m[1];
      const n = Number(m[2]);
      if (seen.has(entry.id)) at(i, `${entry.id} is used twice (first at line ${seen.get(entry.id) + 1})`);
      else seen.set(entry.id, i);
      if (nextId !== null && n >= nextId) at(i, `${entry.id} is not below "next id: L-${String(nextId).padStart(4, '0')}"`);

      if (!m[3].startsWith(SEP)) { at(i, `${entry.id}: expected " · " after the id`); entry.kind = null; continue; }
      const parts = m[3].slice(SEP.length).split(SEP);
      const kind = parts.shift();
      if (!KINDS.includes(kind)) { at(i, `${entry.id}: kind must be one of ${KINDS.join(', ')}`); entry.kind = null; continue; }
      entry.kind = kind;
      if (kind === 'default') {
        const vis = parts.shift();
        if (!VISIBILITIES.includes(vis)) at(i, `${entry.id}: a default line needs visible or invisible after its kind`);
      } else if (VISIBILITIES.includes(parts[0])) {
        at(i, `${entry.id}: visibility belongs only on default lines`);
        parts.shift();
      }
      if (parts.join(SEP).trim() === '') at(i, `${entry.id}: the question is empty`);
      continue;
    }

    const field = line.match(/^- ([a-z]+): (.*)$/);
    if (field && entry && !afterBlank) {
      const [, key, value] = field;
      if (!ALL_KEYS.includes(key)) at(i, `${entry.id ?? 'entry'}: unknown field "${key}"`);
      else if (entry.fields.has(key)) at(i, `${entry.id ?? 'entry'}: "${key}" appears twice`);
      else if (value.trim() === '') at(i, `${entry.id ?? 'entry'}: "${key}" is empty`);
      else entry.fields.set(key, { value: value.trim(), line: i });
      continue;
    }

    at(i, 'not part of an entry: entries are a "### L-" heading followed directly by "- field: value" lines');
  }
  finish();
  return errors;
}

function isDate(s) {
  const m = s.match(/^(\d{4})-(\d{2})-(\d{2})$/);
  if (!m) return false;
  const d = new Date(Date.UTC(+m[1], +m[2] - 1, +m[3]));
  return d.getUTCFullYear() === +m[1] && d.getUTCMonth() === +m[2] - 1 && d.getUTCDate() === +m[3];
}

if (import.meta.url === pathToFileURL(process.argv[1] ?? '').href) {
  const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
  const ledgerPath = process.argv[2] ? resolve(process.argv[2]) : join(root, 'LEDGER.md');
  if (!existsSync(ledgerPath)) {
    console.error(`check-ledger: ${ledgerPath} not found`);
    process.exit(1);
  }
  const errors = checkLedger(readFileSync(ledgerPath, 'utf8'), (p) => existsSync(join(root, p)));
  if (errors.length) {
    console.error(`check-ledger: ${errors.length} violation(s) in ${ledgerPath}:`);
    for (const e of errors) console.error(`  ${e}`);
    process.exit(1);
  }
  console.log(`check-ledger: clean (${ledgerPath}).`);
}
