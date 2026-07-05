/**
 * DMS-01 — a single thread's conversation plus compose box (SDA-24:
 * "composing and reading messages to/from teachers").
 */

import { forwardRef, useEffect, useImperativeHandle, useState } from 'react';
import type {
  DirectMessage,
  DmsError,
  MessageThreadViewApi,
  MessageThreadViewProps,
} from './types.js';

export const MessageThreadView = forwardRef<MessageThreadViewApi, MessageThreadViewProps>(
  function MessageThreadView({ user, thread, onListMessages, onSendMessage }, ref) {
    const [messages, setMessages] = useState<ReadonlyArray<DirectMessage>>([]);
    const [draft, setDraft] = useState('');
    const [error, setError] = useState<DmsError | null>(null);
    const [sending, setSending] = useState(false);

    const load = async () => {
      const result = await onListMessages(thread.id);
      if (result.ok) {
        setMessages(result.value);
        setError(null);
      } else {
        setError(result.error);
      }
    };

    useEffect(() => {
      load();
      // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [thread.id, onListMessages]);

    useImperativeHandle(ref, () => ({ reload: load }), [thread.id, onListMessages]);

    const handleSend = async () => {
      const content = draft.trim();
      // `sending` guards against a double-click/double-Enter firing two sends
      // for one user action, which would otherwise persist the message twice.
      if (!content || sending) return;

      setSending(true);
      const result = await onSendMessage(thread.id, content);
      if (result.ok) {
        setMessages((prev) => [...prev, result.value]);
        setDraft('');
        setError(null);
      } else {
        setError(result.error);
      }
      setSending(false);
    };

    return (
      <div className="dms-thread">
        {error && (
          <div className="dms-thread__error" role="alert">
            {error.message}
          </div>
        )}
        <ul className="dms-thread__messages">
          {messages.map((message) => (
            <li
              key={message.id}
              className={
                message.senderId === user.userId
                  ? 'dms-thread__message dms-thread__message--mine'
                  : 'dms-thread__message'
              }
            >
              {message.content}
            </li>
          ))}
        </ul>
        <div className="dms-thread__compose">
          <textarea
            className="dms-thread__input"
            value={draft}
            onChange={(e) => setDraft(e.target.value)}
            disabled={sending}
          />
          <button type="button" onClick={handleSend} disabled={sending || !draft.trim()}>
            Send
          </button>
        </div>
      </div>
    );
  }
);
