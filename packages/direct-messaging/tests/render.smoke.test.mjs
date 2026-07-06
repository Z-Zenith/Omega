/**
 * DMS-01 — runtime render smoke tests.
 *
 * These import the *compiled* dist/ output (not src/*.tsx directly) because
 * Node's native TypeScript type-stripping erases types but does not perform
 * a JSX transform — `npm run build` (wired as "pretest") does that via tsc's
 * "jsx": "react-jsx" first.
 *
 * Scope note: react-dom/server's renderToStaticMarkup is synchronous and
 * does not run effects, so these only exercise each component's initial,
 * pre-fetch render (loading/empty state) — they catch crashes (bad prop
 * access, hook misuse, JSX errors) but do not verify the "data loaded"
 * visual output. That would need jsdom + act(), which this package doesn't
 * currently depend on.
 */

import { test } from 'node:test';
import assert from 'node:assert/strict';
import React, { createRef } from 'react';
import { renderToStaticMarkup } from 'react-dom/server';

import { MessageInbox, MessageThreadView } from '../dist/index.js';

const studentUser = { userId: 'student-1', sessionToken: 'tok', role: 'student' };
const teacherUser = { userId: 'teacher-1', sessionToken: 'tok', role: 'teacher' };

const thread = {
  id: 'thread-1',
  studentId: 'student-1',
  teacherId: 'teacher-1',
  createdAt: '2026-01-01T00:00:00.000Z',
};

test('MessageInbox renders without crashing for a student viewer', () => {
  const html = renderToStaticMarkup(
    React.createElement(MessageInbox, {
      ref: createRef(),
      user: studentUser,
      selectedThreadId: null,
      onSelectThread: () => {},
      onListThreads: async () => ({
        ok: true,
        value: [{ ...thread, lastMessage: null }],
      }),
    })
  );
  assert.match(html, /dms-inbox/);
});

test('MessageInbox renders without crashing when a thread is selected', () => {
  assert.doesNotThrow(() => {
    renderToStaticMarkup(
      React.createElement(MessageInbox, {
        ref: createRef(),
        user: teacherUser,
        selectedThreadId: thread.id,
        onSelectThread: () => {},
        onListThreads: async () => ({ ok: false, error: { code: 'network_error', message: 'offline' } }),
      })
    );
  });
});

test('MessageThreadView renders without crashing for an existing thread', () => {
  const html = renderToStaticMarkup(
    React.createElement(MessageThreadView, {
      ref: createRef(),
      user: studentUser,
      thread,
      onListMessages: async () => ({ ok: true, value: [] }),
      onSendMessage: async (_threadId, content) => ({
        ok: true,
        value: {
          id: 'm1',
          threadId: thread.id,
          senderId: studentUser.userId,
          content,
          sentAt: new Date().toISOString(),
          readAt: null,
        },
      }),
    })
  );
  assert.match(html, /dms-thread/);
});
