/**
 * DMS-01 — inbox of a user's message threads (TWA-18: "inbox of student
 * messages"; also the thread-picker half of SDA-24 on the student side).
 */

import { forwardRef, useEffect, useImperativeHandle, useState } from 'react';
import type { DmsError, MessageInboxApi, MessageInboxProps, ThreadSummary } from './types.js';
import { getOtherPartyId } from './otherParty.js';

export const MessageInbox = forwardRef<MessageInboxApi, MessageInboxProps>(
  function MessageInbox({ user, selectedThreadId, onSelectThread, onListThreads }, ref) {
    const [threads, setThreads] = useState<ReadonlyArray<ThreadSummary>>([]);
    const [error, setError] = useState<DmsError | null>(null);
    const [loading, setLoading] = useState(true);

    const load = async () => {
      setLoading(true);
      const result = await onListThreads();
      if (result.ok) {
        setThreads(result.value);
        setError(null);
      } else {
        setError(result.error);
      }
      setLoading(false);
    };

    useEffect(() => {
      load();
      // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [onListThreads]);

    useImperativeHandle(ref, () => ({ reload: load }), [onListThreads]);

    return (
      <div className="dms-inbox">
        {error && (
          <div className="dms-inbox__error" role="alert">
            {error.message}
          </div>
        )}
        {loading && threads.length === 0 && <div className="dms-inbox__loading">Loading…</div>}
        <ul className="dms-inbox__threads">
          {threads.map((thread) => (
            <li
              key={thread.id}
              className={
                thread.id === selectedThreadId
                  ? 'dms-inbox__thread dms-inbox__thread--selected'
                  : 'dms-inbox__thread'
              }
              onClick={() => onSelectThread(thread.id)}
            >
              <span className="dms-inbox__thread-party">{getOtherPartyId(thread, user.role)}</span>
              {thread.lastMessage && (
                <span className="dms-inbox__thread-preview">{thread.lastMessage.content}</span>
              )}
            </li>
          ))}
        </ul>
      </div>
    );
  }
);
