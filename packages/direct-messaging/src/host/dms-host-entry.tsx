/**
 * SDA-24 — DMS-01 host entry for the Student Desktop App.
 *
 * Not part of the public DMS interface (see ../index.ts) — this is glue that
 * bootstraps MessageInbox + MessageThreadView inside an Avalonia NativeWebView, the
 * same master-detail layout apps/teacher-web's MessagesPage.tsx (TWA-18) builds by
 * hand for the web. Bundled standalone via `npm run build:host` (esbuild), never
 * imported by TWA/SDA as a module.
 *
 * Bridge protocol: mirrors packages/shared-editor-kit/src/host/notes-host-entry.tsx
 * (SDA-19) exactly — a `{ requestId, method, payload }` message posted to the host (C#)
 * via `window.chrome.webview.postMessage`, resolved by the host calling
 * `window.__dmsHostReceive(json)` back via InvokeScript. Auth/API calls live entirely
 * on the C# side — this file never sees a session token.
 */
import { useState } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { MessageInbox, MessageThreadView } from '../index.js';
import type { DirectMessage, DmsError, MessageThread, Result, ThreadSummary, UserContext } from '../types.js';

type BridgeMethod = 'listThreads' | 'listMessages' | 'sendMessage';

interface HostRequest {
  readonly requestId: string;
  readonly method: BridgeMethod;
  readonly payload: unknown;
}

interface HostResponse {
  readonly requestId: string;
  readonly ok: boolean;
  readonly value?: unknown;
  readonly error?: DmsError;
}

interface MountMessage {
  readonly user: UserContext;
}

declare global {
  interface Window {
    chrome: { webview: { postMessage(message: string): void } };
    __dmsHostReceive?: (json: string) => void;
    __dmsHostMount?: (json: string) => void;
  }
}

let nextRequestId = 0;
const pendingRequests = new Map<string, (response: HostResponse) => void>();

function callHost<TValue>(method: BridgeMethod, payload: unknown): Promise<Result<TValue>> {
  const requestId = `${method}-${++nextRequestId}`;
  return new Promise((resolve) => {
    pendingRequests.set(requestId, (response) => {
      resolve(
        response.ok
          ? { ok: true, value: response.value as TValue }
          : {
              ok: false,
              error: response.error ?? { code: 'network_error', message: 'The host did not return a result.' },
            }
      );
    });
    window.chrome.webview.postMessage(JSON.stringify({ requestId, method, payload } satisfies HostRequest));
  });
}

window.__dmsHostReceive = (json: string) => {
  const response: HostResponse = JSON.parse(json);
  const resolve = pendingRequests.get(response.requestId);
  if (!resolve) {
    return;
  }
  pendingRequests.delete(response.requestId);
  resolve(response);
};

function DmsHostApp({ user }: { user: UserContext }) {
  const [selectedThreadId, setSelectedThreadId] = useState<string | null>(null);
  const [threads, setThreads] = useState<ReadonlyArray<ThreadSummary>>([]);

  const handleListThreads = async () => {
    const result = await callHost<ReadonlyArray<ThreadSummary>>('listThreads', {});
    if (result.ok) setThreads(result.value);
    return result;
  };

  const selectedThread: MessageThread | null = threads.find((t) => t.id === selectedThreadId) ?? null;

  return (
    <div className="dms-host" style={{ display: 'grid', gridTemplateColumns: '280px 1fr', height: '100%' }}>
      <div className="dms-host__inbox" style={{ borderRight: '1px solid #ddd', overflowY: 'auto' }}>
        <MessageInbox
          user={user}
          selectedThreadId={selectedThreadId}
          onSelectThread={setSelectedThreadId}
          onListThreads={handleListThreads}
        />
      </div>
      <div className="dms-host__thread">
        {selectedThread ? (
          <MessageThreadView
            user={user}
            thread={selectedThread}
            onListMessages={(threadId: string) => callHost<ReadonlyArray<DirectMessage>>('listMessages', { threadId })}
            onSendMessage={(threadId: string, content: string) =>
              callHost<DirectMessage>('sendMessage', { threadId, content })
            }
          />
        ) : (
          <p style={{ padding: 16, color: '#666' }}>Select a conversation to view messages.</p>
        )}
      </div>
    </div>
  );
}

const rootElement = document.getElementById('root');
if (!rootElement) {
  throw new Error('DMS host: #root element is missing from host/index.html.');
}
const root: Root = createRoot(rootElement);

window.__dmsHostMount = (json: string) => {
  const { user }: MountMessage = JSON.parse(json);
  root.render(<DmsHostApp user={user} />);
};
