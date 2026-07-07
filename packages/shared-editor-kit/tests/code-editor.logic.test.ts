/**
 * SEK-01 — runtime tests for the code-editor's pure logic helpers.
 *
 * Uses Node's built-in test runner directly against TypeScript sources
 * (Node 22+ type stripping) rather than adding a new test-framework
 * dependency — same rationale as tests/notes.linkExtraction.test.ts.
 */

import { test } from 'node:test';
import assert from 'node:assert/strict';

import {
  buildCodeSource,
  isSupportedLanguage,
  unsupportedLanguageError,
} from '../src/code-editor/logic.ts';

test('isSupportedLanguage accepts every launch-list language', () => {
  const launchList = [
    'c',
    'cpp',
    'python',
    'java',
    'dotnet',
    'html',
    'css',
    'javascript',
    'typescript',
    'nodejs',
    'sql',
    'json',
    'yaml',
  ];
  for (const language of launchList) {
    assert.equal(isSupportedLanguage(language), true, language);
  }
});

test('isSupportedLanguage rejects a foreign or stale language value', () => {
  assert.equal(isSupportedLanguage('ruby'), false);
  assert.equal(isSupportedLanguage(''), false);
  assert.equal(isSupportedLanguage('PYTHON'), false); // case-sensitive
});

test('buildCodeSource omits stdin/filename when blank, rather than setting undefined', () => {
  const source = buildCodeSource('python', 'print(1)', '', undefined);
  assert.deepEqual(source, { language: 'python', content: 'print(1)' });
  assert.equal('stdin' in source, false);
  assert.equal('filename' in source, false);
});

test('buildCodeSource includes stdin and filename when provided', () => {
  const source = buildCodeSource('python', 'print(1)', '5\n', 'main.py');
  assert.deepEqual(source, {
    language: 'python',
    content: 'print(1)',
    stdin: '5\n',
    filename: 'main.py',
  });
});

test('unsupportedLanguageError returns the canonical code and names the rejected value', () => {
  const err = unsupportedLanguageError('ruby');
  assert.equal(err.code, 'unsupported_language');
  assert.match(err.message, /ruby/);
});
