/**
 * Direct Messaging (DMS) — public types.
 *
 * Spec: docs/Campus platform architecture.md, Section 3 (DMS-01).
 *   "One-to-one message thread between a student and a teacher."
 *
 * Acceptance criterion this component must not violate: "Each thread is
 * scoped to exactly one student-teacher pair." That invariant is enforced
 * by the Backend API (a unique index on student_id/teacher_id — see
 * services/backend-api/Controllers/MessagingController.cs) — this
 * component only renders what it's given and never fabricates a thread.
 *
 * Same design rules as the Shared Editor Kit (packages/shared-editor-kit):
 * this component owns no persistence and no auth — every read/write goes
 * through an embedder-supplied callback, and every call carries a
 * UserContext the embedder controls.
 */

/** Opaque, embedder-supplied authentication context. DMS never opens a session itself. */
export interface UserContext {
  readonly userId: string;
  readonly sessionToken: string;
  readonly role: 'student' | 'teacher';
}

/** Standard Result envelope so DMS callers can handle errors without try/catch. */
export type Result<TValue, TError = DmsError> =
  | { readonly ok: true; readonly value: TValue }
  | { readonly ok: false; readonly error: TError };

/** Canonical error shape returned by every DMS API call. */
export interface DmsError {
  readonly code: DmsErrorCode;
  readonly message: string;
  readonly cause?: unknown;
}

export type DmsErrorCode =
  | 'thread_not_found'
  | 'not_a_participant'
  | 'validation_error'
  | 'network_error'
  | 'unauthorized';

/** A single message within a thread. Mirrors services/backend-api's MessageResponse. */
export interface DirectMessage {
  readonly id: string;
  readonly threadId: string;
  readonly senderId: string;
  readonly content: string;
  readonly sentAt: string; // ISO 8601
  readonly readAt: string | null;
}

/** A one-to-one student-teacher thread. Mirrors MessageThreadResponse. */
export interface MessageThread {
  readonly id: string;
  readonly studentId: string;
  readonly teacherId: string;
  readonly createdAt: string;
}

/** A thread plus enough to render an inbox row without a second round trip. */
export interface ThreadSummary extends MessageThread {
  readonly lastMessage: DirectMessage | null;
}

export interface MessageInboxProps {
  readonly user: UserContext;
  /** Which thread (if any) is currently open — highlights the matching row. */
  readonly selectedThreadId: string | null;
  readonly onSelectThread: (threadId: string) => void;
  /** Fetch every thread the current user participates in, newest activity first. */
  readonly onListThreads: () => Promise<Result<ReadonlyArray<ThreadSummary>>>;
}

export interface MessageInboxApi {
  /** Re-fetch the thread list, e.g. after a notification says something changed. */
  reload(): Promise<void>;
}

export interface MessageThreadViewProps {
  readonly user: UserContext;
  readonly thread: MessageThread;
  /** Fetch this thread's messages in chronological order. */
  readonly onListMessages: (threadId: string) => Promise<Result<ReadonlyArray<DirectMessage>>>;
  /** Send a message into this thread. */
  readonly onSendMessage: (threadId: string, content: string) => Promise<Result<DirectMessage>>;
}

export interface MessageThreadViewApi {
  /** Re-fetch this thread's messages, e.g. after a "new message" notification. */
  reload(): Promise<void>;
}
