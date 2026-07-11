/**
 * Timetable endpoints shared by Teacher Web (TWA-01/02/10/12/13) and Admin
 * Web (AWA-02/03) — reading a section's timetable, generating/patching
 * slots, and filing a change request. Both apps hit the same DTOs and
 * routes; which functions each app actually calls differs (e.g. AWA drives
 * generation/patch, TWA mostly reads + files change requests) but that's a
 * per-page concern, not a reason to fork the client.
 */

import { request } from './http.js';

export interface TimetableSlotDto {
  id: string;
  dayOfWeek: number;
  startTime: string;
  endTime: string;
  sectionId: string;
  sectionName: string;
  subjectId: string;
  subjectName: string;
  teacherId: string;
  teacherName: string;
  room: string | null;
  manuallyEdited: boolean;
}

export function getMyTimetable() {
  return request<TimetableSlotDto[]>('/timetable/mine');
}

export function generateTimetable(departmentId?: string) {
  return request<TimetableSlotDto[]>('/timetable/generate', {
    method: 'POST',
    body: JSON.stringify({ departmentId: departmentId ?? null }),
  });
}

export function patchTimetableSlot(
  id: string,
  patch: Partial<{ teacherId: string; dayOfWeek: number; startTime: string; endTime: string; room: string }>,
) {
  return request<TimetableSlotDto>(`/timetable/slots/${id}`, {
    method: 'PATCH',
    body: JSON.stringify(patch),
  });
}

export interface ChangeRequestDto {
  id: string;
  description: string;
  status: string;
  requestedAt: string;
}

export function createChangeRequest(description: string) {
  return request<ChangeRequestDto>('/timetable/change-requests', {
    method: 'POST',
    body: JSON.stringify({ description }),
  });
}
