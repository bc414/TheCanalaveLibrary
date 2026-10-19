// Known-bad fixtures for check-ledger.mjs: each rule has a fixture that must fail, and a valid
// ledger must pass. Run: node --test scripts/check-ledger.test.mjs
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { checkLedger } from './check-ledger.mjs';

const EXISTING = new Set(['src/Polls/PollService.cs', 'src/Polls']);
const exists = (p) => EXISTING.has(p);

const HEADER = '# Ledger\n\nnext id: L-0010\n\n## Entries\n';
const GOOD_DEFAULT = [
  '### L-0001 · default · invisible · Can a poll\'s anonymity change once votes exist?',
  '- default: Anonymity is frozen at the database once a vote exists.',
  '- why: Strictest security. Changing it after a vote could name a voter.',
  '- code: src/Polls/PollService.cs, src/Polls',
  '- taken: 2026-10-19',
].join('\n');
const GOOD_EXTERNAL = [
  '### L-0002 · external · ImageSharp licensing: apply for a key, swap libraries, or accept the risk?',
  '- default: Held at 3.1.12 as accepted risk.',
  '- why: Every patched version needs a license key.',
  '- taken: 2026-10-19',
].join('\n');

const ledger = (...entries) => HEADER + '\n' + entries.join('\n\n') + '\n';
const fails = (text, fragment) => {
  const errors = checkLedger(text, exists);
  assert.ok(errors.some((e) => e.includes(fragment)), `expected an error containing "${fragment}", got:\n${errors.join('\n')}`);
};

test('a valid ledger passes', () => {
  assert.deepEqual(checkLedger(ledger(GOOD_DEFAULT, GOOD_EXTERNAL), exists), []);
});

test('an empty ledger (header only) passes', () => {
  assert.deepEqual(checkLedger(HEADER, exists), []);
});

test('CRLF line endings pass', () => {
  assert.deepEqual(checkLedger(ledger(GOOD_DEFAULT).replace(/\n/g, '\r\n'), exists), []);
});

test('missing ## Entries fails', () => fails('# Ledger\nnext id: L-0001\n', '## Entries'));
test('missing next id fails', () => fails('# Ledger\n\n## Entries\n', 'next id'));
test('a second next id fails', () => fails('next id: L-0002\nnext id: L-0003\n## Entries\n', 'second "next id:"'));
test('a heading above ## Entries fails', () => fails('next id: L-0002\n### L-0001 · x\n## Entries\n', 'above "## Entries"'));
test('the header field template is not an entry', () => {
  assert.deepEqual(checkLedger('next id: L-0001\n```\n### L-NNNN · default · visible · <question>\n```\n## Entries\n', exists), []);
});

test('a malformed id fails', () => fails(ledger(GOOD_DEFAULT.replace('L-0001', 'L-1')), 'heading must start'));
test('a duplicate id fails', () => fails(ledger(GOOD_DEFAULT, GOOD_DEFAULT), 'used twice'));
test('an id not below next id fails', () => fails(ledger(GOOD_DEFAULT.replace('L-0001', 'L-0010')), 'not below'));
test('a missing separator fails', () => fails(ledger(GOOD_DEFAULT.replace('L-0001 · ', 'L-0001 ')), 'expected " · "'));
test('an unknown kind fails', () => fails(ledger(GOOD_DEFAULT.replace('· default ·', '· manual ·')), 'kind must be'));
test('a default line without visibility fails', () => fails(ledger(GOOD_DEFAULT.replace('· invisible ', '')), 'needs visible or invisible'));
test('visibility on an external line fails', () => fails(ledger(GOOD_EXTERNAL.replace('· external ·', '· external · visible ·')), 'only on default lines'));
test('an empty question fails', () => fails(ledger('### L-0003 · external · \n- default: x\n- why: y\n- taken: 2026-10-19'), 'question is empty'));

test('a missing required field fails', () => fails(ledger(GOOD_DEFAULT.replace(/\n- why: .*/, '')), 'missing "- why:"'));
test('a default line without code fails', () => fails(ledger(GOOD_DEFAULT.replace(/\n- code: .*/, '')), 'missing "- code:"'));
test('an unknown field fails', () => fails(ledger(GOOD_DEFAULT + '\n- basis: current'), 'unknown field "basis"'));
test('a duplicate field fails', () => fails(ledger(GOOD_DEFAULT + '\n- why: again'), '"why" appears twice'));
test('an empty field fails', () => fails(ledger(GOOD_DEFAULT.replace(/- why: .*/, '- why: ')), '"why" is empty'));
test('an impossible date fails', () => fails(ledger(GOOD_DEFAULT.replace('2026-10-19', '2026-02-30')), 'real date'));
test('a malformed date fails', () => fails(ledger(GOOD_DEFAULT.replace('2026-10-19', 'Oct 19')), 'real date'));

test('a code path that does not exist fails', () => fails(ledger(GOOD_DEFAULT.replace('src/Polls/PollService.cs', 'src/Gone.cs')), 'does not exist'));
test('an absolute code path fails', () => fails(ledger(GOOD_DEFAULT.replace('src/Polls/PollService.cs', 'C:/repo/x.cs')), 'repo-relative'));
test('a backslash code path fails', () => fails(ledger(GOOD_DEFAULT.replace('src/Polls/PollService.cs', 'src\\Polls\\PollService.cs')), 'forward slashes'));
test('a parent-directory code path fails', () => fails(ledger(GOOD_DEFAULT.replace('src/Polls/PollService.cs', '../x.cs')), 'repo-relative'));

test('prose inside the entries section fails', () => fails(ledger(GOOD_DEFAULT, 'Some loose note.'), 'not part of an entry'));
test('a field separated from its heading by a blank line fails', () => fails(ledger(GOOD_DEFAULT.replace('\n- taken', '\n\n- taken')), 'not part of an entry'));
