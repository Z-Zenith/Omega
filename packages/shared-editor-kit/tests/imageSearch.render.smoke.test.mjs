/**
 * SEK-04 — runtime render smoke tests for ImageSearchPanel and its integration
 * into NotesEditor.
 *
 * Imports the *compiled* dist/ output (not src/*.tsx directly) — same reason
 * as packages/direct-messaging's render.smoke.test.mjs: Node's native
 * TypeScript type-stripping does not perform a JSX transform, so these run
 * against tsc's "jsx": "react-jsx" output instead ("pretest" builds it first).
 *
 * react-dom/server's renderToStaticMarkup is synchronous and does not run
 * effects, so these only exercise each component's initial render — they
 * catch crashes (bad prop access, hook misuse) but not the "results loaded"
 * visual output.
 */

import { test } from 'node:test';
import assert from 'node:assert/strict';
import React, { createRef } from 'react';
import { renderToStaticMarkup } from 'react-dom/server';

import { ImageSearchPanel, NotesEditor } from '../dist/index.js';

const user = { userId: 'student-1', sessionToken: 'tok', role: 'student' };

test('ImageSearchPanel renders nothing when disabled', () => {
  const html = renderToStaticMarkup(
    React.createElement(ImageSearchPanel, {
      user,
      enabled: false,
      onSearch: async () => ({ ok: true, value: { query: '', results: [], degraded: false } }),
      onUploadImage: async () => ({ ok: true, value: { embeddedUrl: '', altText: '', width: 0, height: 0, attribution: '' } }),
      onInsert: () => {},
    })
  );
  assert.equal(html, '');
});

test('ImageSearchPanel renders the search box when enabled', () => {
  const html = renderToStaticMarkup(
    React.createElement(ImageSearchPanel, {
      user,
      enabled: true,
      onSearch: async () => ({ ok: true, value: { query: 'cats', results: [], degraded: false } }),
      onUploadImage: async () => ({ ok: true, value: { embeddedUrl: '', altText: '', width: 0, height: 0, attribution: '' } }),
      onInsert: () => {},
    })
  );
  assert.match(html, /sek-image-search/);
});

test('NotesEditor renders without crashing when imageSearch is wired in', () => {
  assert.doesNotThrow(() => {
    renderToStaticMarkup(
      React.createElement(NotesEditor, {
        ref: createRef(),
        user,
        currentNote: null,
        canEdit: true,
        onSave: async (note) => ({ ok: true, value: note }),
        onDelete: async () => ({ ok: true, value: undefined }),
        onResolveLink: async () => ({ ok: false, error: { code: 'note_not_found', message: 'not found' } }),
        onListBacklinks: async () => ({ ok: true, value: [] }),
        imageSearch: {
          enabled: true,
          onSearch: async () => ({ ok: true, value: { query: '', results: [], degraded: false } }),
          onUploadImage: async () => ({
            ok: true,
            value: { embeddedUrl: 'content://img/1', altText: 'a cat', width: 10, height: 10, attribution: 'CC0' },
          }),
        },
      })
    );
  });
});

test('NotesEditor renders without crashing when imageSearch is omitted', () => {
  assert.doesNotThrow(() => {
    renderToStaticMarkup(
      React.createElement(NotesEditor, {
        ref: createRef(),
        user,
        currentNote: null,
        canEdit: true,
        onSave: async (note) => ({ ok: true, value: note }),
        onDelete: async () => ({ ok: true, value: undefined }),
        onResolveLink: async () => ({ ok: false, error: { code: 'note_not_found', message: 'not found' } }),
        onListBacklinks: async () => ({ ok: true, value: [] }),
      })
    );
  });
});
