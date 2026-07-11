/**
 * SEK-03 — runtime tests for the Markdown link-extraction logic.
 *
 * Uses Node's built-in test runner directly against TypeScript sources
 * (Node 22+ type stripping) rather than adding a new test-framework
 * dependency to a package that doesn't have one yet.
 */

import { test } from 'node:test';
import assert from 'node:assert/strict';

// Real .ts extension (not .js) so Node's native type-stripping can resolve
// this import directly — see the allowImportingTsExtensions note in tsconfig.json.
import { extractOutgoingLinks, uniqueLinkTargetsKey } from '../src/notes/linkExtraction.ts';

test('returns nothing for markdown with no links', () => {
  assert.deepEqual(extractOutgoingLinks('just plain text, no links here'), []);
});

test('returns nothing for empty markdown', () => {
  assert.deepEqual(extractOutgoingLinks(''), []);
});

test('extracts a bare wikilink, using the target as its own anchor', () => {
  const links = extractOutgoingLinks('see [[note-2]] for details');
  assert.deepEqual(links, [{ toNoteId: 'note-2', anchor: 'note-2' }]);
});

test('extracts an aliased wikilink, using the alias as the anchor', () => {
  const links = extractOutgoingLinks('see [[note-2|the other note]] for details');
  assert.deepEqual(links, [{ toNoteId: 'note-2', anchor: 'the other note' }]);
});

test('extracts a markdown id-link', () => {
  const links = extractOutgoingLinks('see [the other note](id:note-2) for details');
  assert.deepEqual(links, [{ toNoteId: 'note-2', anchor: 'the other note' }]);
});

test('extracts multiple links of mixed syntax, in source order', () => {
  const markdown = '[[note-1]] then [alias](id:note-2) then [[note-3|third]]';
  const links = extractOutgoingLinks(markdown);
  assert.deepEqual(links, [
    { toNoteId: 'note-1', anchor: 'note-1' },
    { toNoteId: 'note-2', anchor: 'alias' },
    { toNoteId: 'note-3', anchor: 'third' },
  ]);
});

test('trims whitespace around wikilink targets and aliases', () => {
  const links = extractOutgoingLinks('[[  note-2  |  spaced alias  ]]');
  assert.deepEqual(links, [{ toNoteId: 'note-2', anchor: 'spaced alias' }]);
});

test('skips a wikilink with an empty target rather than throwing', () => {
  assert.doesNotThrow(() => extractOutgoingLinks('[[]] and [[|alias only]]'));
  assert.deepEqual(extractOutgoingLinks('[[]] and [[|alias only]]'), []);
});

test('skips a markdown id-link with an empty target rather than throwing', () => {
  assert.doesNotThrow(() => extractOutgoingLinks('[anchor](id:)'));
  assert.deepEqual(extractOutgoingLinks('[anchor](id:)'), []);
});

test('does not match a normal markdown link without the id: scheme', () => {
  assert.deepEqual(extractOutgoingLinks('[external](https://example.com)'), []);
});

test('repeated links to the same target are each returned', () => {
  const links = extractOutgoingLinks('[[note-2]] again [[note-2]]');
  assert.deepEqual(links, [
    { toNoteId: 'note-2', anchor: 'note-2' },
    { toNoteId: 'note-2', anchor: 'note-2' },
  ]);
});

// #161 — NotesEditor's link-resolution effect depends on this key instead of the
// outgoingLinks array reference, so keystrokes that don't change the target set must
// produce an identical key even though extractOutgoingLinks() itself returns a new array.
test('uniqueLinkTargetsKey: identical for two different arrays with the same target set', () => {
  const before = extractOutgoingLinks('see [[note-2]] and [[note-3]]');
  const after = extractOutgoingLinks('see [[note-2]] and [[note-3]] plus more text');
  assert.notEqual(before, after); // different array references, as extractOutgoingLinks always returns
  assert.equal(uniqueLinkTargetsKey(before), uniqueLinkTargetsKey(after));
});

test('uniqueLinkTargetsKey: differs once a link target is actually added', () => {
  const before = uniqueLinkTargetsKey(extractOutgoingLinks('[[note-2]]'));
  const after = uniqueLinkTargetsKey(extractOutgoingLinks('[[note-2]] [[note-3]]'));
  assert.notEqual(before, after);
});

test('uniqueLinkTargetsKey: order-independent and de-duplicates repeated targets', () => {
  const key1 = uniqueLinkTargetsKey(extractOutgoingLinks('[[note-2]] [[note-3]]'));
  const key2 = uniqueLinkTargetsKey(extractOutgoingLinks('[[note-3]] [[note-2]] [[note-2]]'));
  assert.equal(key1, key2);
});

test('uniqueLinkTargetsKey: empty for no links', () => {
  assert.equal(uniqueLinkTargetsKey([]), '');
});
