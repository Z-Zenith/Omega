import type { MessageThread, UserContext } from './types.js';

/**
 * The id of the person on the other side of a thread, from the viewer's
 * perspective — a student viewing their own inbox sees the teacher's id,
 * and vice versa.
 */
export function getOtherPartyId(thread: MessageThread, viewerRole: UserContext['role']): string {
  return viewerRole === 'student' ? thread.teacherId : thread.studentId;
}
