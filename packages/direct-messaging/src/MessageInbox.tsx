/**
 * DMS-01 — inbox of a user's message threads (TWA-18: "inbox of student
 * messages"; also the thread-picker half of SDA-24 on the student side).
 */

import { forwardRef, useEffect, useImperativeHandle, useRef, useState } from 'react';
import type { DmsError, MessageInboxApi, MessageInboxProps, ThreadSummary } from './types.js';
import { getOtherPartyId } from './otherParty.js';

export const MessageInbox = forwardRef<MessageInboxApi, MessageInboxProps>(
  function MessageInbox({ user, selectedThreadId, onSelectThread, onListThreads }, ref) {
    const [threads, setThreads] = useState<ReadonlyArray<ThreadSummary>>([]);
    const [error, setError] = useState<DmsError | null>(null);
    const [loading, setLoading] = useState(true);

    // Read via a ref rather than an effect dependency: an embedder-supplied inline
    // callback gets a fresh identity every parent render, and depending on it directly
    // would refetch on every render instead of only when the viewer changes.
    const onListThreadsRef = useRef(onListThreads);
    onListThreadsRef.current = onListThreads;

    // Bumped on every load; a call whose generation has been superseded by a newer
    // one (component unmounted, or reload() fired again before the first resolved)
    // discards its result instead of clobbering fresher state.
    const generationRef = useRef(0);

    const load = async () => {
      const generation = ++generationRef.current;
      setLoading(true);
      const result = await onListThreadsRef.current();
      if (generationRef.current !== generation) return;
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
      return () => {
        generationRef.current++;
      };
      // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [user.userId]);

    useImperativeHandle(ref, () => ({ reload: load }), []);

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
