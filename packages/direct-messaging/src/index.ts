/**
 * Direct Messaging (DMS) — public surface.
 *
 * Feature ID reference: docs/Campus platform architecture.md, Section 3 (DMS-01).
 * Consumers: apps/teacher-web (TWA-18), apps/student-desktop (SDA-24, binding deferred
 * the same way Shared Editor Kit's SDA binding is — see packages/shared-editor-kit/README.md).
 */

export type {
  UserContext,
  Result,
  DmsError,
  DmsErrorCode,
  DirectMessage,
  MessageThread,
  ThreadSummary,
  MessageInboxProps,
  MessageInboxApi,
  MessageThreadViewProps,
  MessageThreadViewApi,
} from './types.js';

export { MessageInbox } from './MessageInbox.js';
export { MessageThreadView } from './MessageThreadView.js';
export { getOtherPartyId } from './otherParty.js';
