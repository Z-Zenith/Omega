import { test } from 'node:test';
import assert from 'node:assert/strict';

// Real .ts extension so Node's native type-stripping can resolve this import
// directly — see the allowImportingTsExtensions note in tsconfig.json.
import { getOtherPartyId } from '../src/otherParty.ts';
import type { MessageThread } from '../src/types.ts';

const thread: MessageThread = {
  id: 't1',
  studentId: 'student-1',
  teacherId: 'teacher-1',
  createdAt: '2026-01-01T00:00:00.000Z',
};

test('a student viewer sees the teacher as the other party', () => {
  assert.equal(getOtherPartyId(thread, 'student'), 'teacher-1');
});

test('a teacher viewer sees the student as the other party', () => {
  assert.equal(getOtherPartyId(thread, 'teacher'), 'student-1');
});
