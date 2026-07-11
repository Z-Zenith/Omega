/**
 * DMS-01 — regression coverage for #154 / #155.
 *
 * render.smoke.test.mjs only exercises MessageThreadView's *initial* render via
 * renderToStaticMarkup, which never runs effects and never re-renders an existing
 * instance with a changed `thread` prop — so it could not have caught either bug:
 *
 *   #154: switching threads left the previous thread's unsent draft in the
 *         compose box; sending it delivered the old text to the *new* thread.id.
 *   #155: switching threads left the previous thread's messages on screen until
 *         the new thread's fetch resolved (or forever, if it failed).
 *
 * Catching this requires a real client render that keeps one component instance
 * alive across a prop change and actually runs its effects, so this file pulls in
 * jsdom + react-dom/client + React's `act` instead of renderToStaticMarkup. That's
 * a step up from this package's existing smoke-test convention, called out as the
 * missing piece in the smoke test's own doc comment.
 */

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { JSDOM } from 'jsdom';

const dom = new JSDOM('<!doctype html><html><body></body></html>');
globalThis.window = dom.window;
globalThis.document = dom.window.document;
globalThis.HTMLElement = dom.window.HTMLElement;
globalThis.Node = dom.window.Node;
// Node 21+ ships its own global `navigator` as a getter-only property, so a plain
// assignment throws — redefine it instead so React/jsdom see the jsdom navigator.
Object.defineProperty(globalThis, 'navigator', {
  value: dom.window.navigator,
  configurable: true,
  writable: true,
});
globalThis.IS_REACT_ACT_ENVIRONMENT = true;

// Must be dynamic: react-dom/client resolves against the jsdom globals set up above,
// and static ESM imports are hoisted ahead of any top-level statements in this file.
const React = (await import('react')).default;
const { createRef, act } = await import('react');
const { createRoot } = await import('react-dom/client');
const { MessageThreadView } = await import('../dist/index.js');

const studentUser = { userId: 'student-1', sessionToken: 'tok', role: 'student' };

const threadA = {
  id: 'thread-A',
  studentId: 'student-1',
  teacherId: 'teacher-1',
  createdAt: '2026-01-01T00:00:00.000Z',
};
const threadB = {
  id: 'thread-B',
  studentId: 'student-1',
  teacherId: 'teacher-2',
  createdAt: '2026-01-02T00:00:00.000Z',
};

const messageA = {
  id: 'a1',
  threadId: threadA.id,
  senderId: 'teacher-1',
  content: 'Hello from thread A',
  sentAt: '2026-01-01T00:01:00.000Z',
  readAt: null,
};
const messageB = {
  id: 'b1',
  threadId: threadB.id,
  senderId: 'teacher-2',
  content: 'Hello from thread B',
  sentAt: '2026-01-02T00:01:00.000Z',
  readAt: null,
};

function flushMicrotasks() {
  return new Promise((resolve) => setTimeout(resolve, 0));
}

function setTextareaValue(textarea, value) {
  // React tracks the native input value setter to detect controlled-input changes;
  // assigning `.value` directly doesn't trigger it, so go through the prototype
  // setter the same way user-event / RTL do.
  const nativeSetter = Object.getOwnPropertyDescriptor(
    dom.window.HTMLTextAreaElement.prototype,
    'value'
  ).set;
  nativeSetter.call(textarea, value);
  textarea.dispatchEvent(new dom.window.Event('input', { bubbles: true }));
}

test('DMS-01 (#154/#155): switching thread.id clears the old draft and old messages before the new thread loads, and Send targets the new thread', async () => {
  const container = document.createElement('div');
  document.body.appendChild(container);
  const root = createRoot(container);

  const sentMessages = [];

  // Thread B's fetch is held open under our control so we can assert on the
  // in-between state — old thread cleared, new thread not yet loaded — which is
  // exactly the window #155 leaked stale messages into.
  let resolveBFetch;
  const bFetchGate = new Promise((resolve) => {
    resolveBFetch = resolve;
  });

  const onListMessages = async (threadId) => {
    if (threadId === threadA.id) return { ok: true, value: [messageA] };
    if (threadId === threadB.id) {
      await bFetchGate;
      return { ok: true, value: [messageB] };
    }
    throw new Error(`unexpected threadId ${threadId}`);
  };

  const onSendMessage = async (threadId, content) => {
    sentMessages.push({ threadId, content });
    return {
      ok: true,
      value: {
        id: `sent-${sentMessages.length}`,
        threadId,
        senderId: studentUser.userId,
        content,
        sentAt: new Date().toISOString(),
        readAt: null,
      },
    };
  };

  const ref = createRef();

  // --- Open thread A, let its fetch resolve, type an unsent draft. ---
  await act(async () => {
    root.render(
      React.createElement(MessageThreadView, {
        ref,
        user: studentUser,
        thread: threadA,
        onListMessages,
        onSendMessage,
      })
    );
    await flushMicrotasks();
  });

  assert.match(container.textContent, /Hello from thread A/, 'thread A message should be visible');

  const textarea = container.querySelector('.dms-thread__input');
  assert.ok(textarea, 'compose textarea should be present');

  await act(async () => {
    setTextareaValue(textarea, 'unsent reply meant for A');
  });
  assert.equal(textarea.value, 'unsent reply meant for A');

  // --- Switch to thread B without sending. Same component instance (no remount). ---
  await act(async () => {
    root.render(
      React.createElement(MessageThreadView, {
        ref,
        user: studentUser,
        thread: threadB,
        onListMessages,
        onSendMessage,
      })
    );
    await flushMicrotasks();
  });

  // #154: the draft must not leak across the switch, even before B's fetch resolves.
  assert.equal(
    textarea.value,
    '',
    'draft from thread A must be cleared immediately on switching to thread B'
  );

  // #155: thread A's messages must not still be on screen while B's fetch is pending.
  assert.doesNotMatch(
    container.textContent,
    /Hello from thread A/,
    'thread A messages must be cleared before thread B has loaded, not left stale on screen'
  );
  assert.doesNotMatch(
    container.textContent,
    /Hello from thread B/,
    "thread B's fetch hasn't resolved yet, so its messages shouldn't be showing either"
  );

  // --- Let thread B's fetch resolve. ---
  await act(async () => {
    resolveBFetch();
    await flushMicrotasks();
  });

  assert.match(container.textContent, /Hello from thread B/, 'thread B messages should now be visible');
  assert.doesNotMatch(container.textContent, /Hello from thread A/, 'thread A messages must stay gone');

  // --- Type a fresh draft for B and send it. ---
  await act(async () => {
    setTextareaValue(textarea, 'reply meant for B');
  });

  const sendButton = Array.from(container.querySelectorAll('button')).find(
    (button) => button.textContent === 'Send'
  );
  assert.ok(sendButton, 'Send button should be present');

  await act(async () => {
    sendButton.dispatchEvent(new dom.window.MouseEvent('click', { bubbles: true }));
    await flushMicrotasks();
  });

  // The regression: this must be thread B's id with only B's draft content — never
  // thread A's id, and never A's leaked draft prepended/mixed in.
  assert.deepEqual(sentMessages, [{ threadId: threadB.id, content: 'reply meant for B' }]);

  await act(async () => {
    root.unmount();
  });
  container.remove();
});
