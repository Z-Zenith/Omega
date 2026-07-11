// Core HTTP client, auth, timetable, events, and reports are genuinely shared
// with apps/admin-web (and, for the HTTP client only, apps/parent-portal) —
// see packages/api-client (issue #87). Everything below stays app-local
// because it's TWA-specific (attendance, marks, roster, feedback, DMS adapters).
export {
  getToken,
  setToken,
  ApiError,
  login,
  getMyTimetable,
  generateTimetable,
  patchTimetableSlot,
  createChangeRequest,
  createEvent,
  createReport,
} from '@campus/api-client'
export type {
  LoginResponse,
  TimetableSlotDto,
  ChangeRequestDto,
  EventDto,
  TeacherReportDto,
} from '@campus/api-client'

import { request, ApiError } from '@campus/api-client'

export interface RosterStudentDto {
  studentId: string
  fullName: string
}

export function getSectionRoster(timetableSlotId: string) {
  return request<RosterStudentDto[]>(`/timetable/slots/${timetableSlotId}/roster`)
}

export type AttendanceStatus = 'Present' | 'Absent' | 'Late'

export interface MarkedAttendanceDto {
  studentId: string
  studentName: string
  status: string
}

export interface MarkAttendanceResponse {
  classSessionId: string
  sessionDate: string
  sectionId: string
  records: MarkedAttendanceDto[]
}

export interface AttendanceAlertDto {
  studentId: string
  studentName: string
  sectionId: string
  sectionName: string
  attendancePercentage: number
}

export function getAttendanceAlerts() {
  return request<AttendanceAlertDto[]>('/attendance/alerts')
}

export function markAttendance(
  timetableSlotId: string,
  entries: { studentId: string; status: AttendanceStatus }[],
  sessionDate?: string,
) {
  return request<MarkAttendanceResponse>('/attendance', {
    method: 'POST',
    body: JSON.stringify({ timetableSlotId, sessionDate: sessionDate ?? null, entries }),
  })
}

export interface SectionFeedbackDto {
  id: string
  sectionId: string
  sectionName: string
  rating: number
  comments: string | null
  submittedAt: string
}

export function submitSectionFeedback(sectionId: string, rating: number, comments?: string) {
  return request<SectionFeedbackDto>(`/timetable/sections/${sectionId}/feedback`, {
    method: 'POST',
    body: JSON.stringify({ rating, comments: comments ?? null }),
  })
}

export interface StudentAttendanceDto {
  studentId: string
  studentName: string
  attendancePercentage: number | null
}

export interface SubjectMarksSummaryDto {
  subjectId: string
  subjectName: string
  averageMarks: number | null
  studentsGraded: number
}

export interface SectionPerformanceSummaryDto {
  sectionId: string
  sectionName: string
  overallAttendancePercentage: number | null
  studentAttendance: StudentAttendanceDto[]
  marksBySubject: SubjectMarksSummaryDto[]
}

export function getSectionPerformanceSummary(sectionId: string) {
  return request<SectionPerformanceSummaryDto>(`/timetable/sections/${sectionId}/performance-summary`)
}

export interface ExternalMarksPermissionStatus {
  granted: boolean
  expiresAt: string | null
}

export function getExternalMarksPermissionStatus() {
  return request<ExternalMarksPermissionStatus>('/marks/external/permission-status')
}

export interface ExternalMarkSubmission {
  id: string
  studentId: string
  subjectId: string
  grade: string
  status: string
  submittedAt: string
}

export function submitExternalMark(mark: { studentId: string; subjectId: string; grade: string }) {
  return request<ExternalMarkSubmission>('/marks/external', {
    method: 'POST',
    body: JSON.stringify(mark),
  })
}

// TWA-20
export interface PendingExternalMarkDto {
  id: string
  studentId: string
  studentFullName: string
  subjectId: string
  subjectName: string
  grade: string
  submittedBy: string
  submittedByFullName: string
  submittedAt: string
}

export function getPendingExternalMarks() {
  return request<PendingExternalMarkDto[]>('/marks/external/pending')
}

export interface ApproveExternalMarkResponse {
  id: string
  approvedBy: string
  approvedAt: string
}

export function approveExternalMark(id: string) {
  return request<ApproveExternalMarkResponse>(`/marks/external/${id}/approve`, {
    method: 'POST',
  })
}

export interface InternalMarksRosterEntry {
  studentId: string
  studentName: string
  marks: number | null
  published: boolean
  publishedAt: string | null
}

export function getInternalMarksRoster(subjectId: string, assignmentId?: string) {
  const params = new URLSearchParams({ subjectId })
  if (assignmentId) params.set('assignmentId', assignmentId)
  return request<InternalMarksRosterEntry[]>(`/marks/internal/roster?${params.toString()}`)
}

export interface InternalMarkRecord {
  id: string
  studentId: string
  subjectId: string
  assignmentId: string | null
  marks: number
  published: boolean
  publishedAt: string | null
}

export function submitInternalMark(mark: {
  studentId: string
  subjectId: string
  assignmentId?: string | null
  marks: number
  publish?: boolean
}) {
  return request<InternalMarkRecord>('/marks/internal', {
    method: 'POST',
    body: JSON.stringify({
      studentId: mark.studentId,
      subjectId: mark.subjectId,
      assignmentId: mark.assignmentId ?? null,
      marks: mark.marks,
      publish: mark.publish ?? false,
    }),
  })
}

// DMS-01 / TWA-18 — thin adapters from the shared Direct Messaging package's
// embedder callbacks (Result<T, DmsError>) onto this app's fetch client
// (which throws ApiError). DMS owns no persistence or auth of its own; this
// is the only messaging logic that lives in teacher-web.
export interface ThreadSummaryDto {
  id: string
  studentId: string
  teacherId: string
  createdAt: string
  lastMessage: MessageDto | null
}

export interface MessageDto {
  id: string
  threadId: string
  senderId: string
  content: string
  sentAt: string
  readAt: string | null
}

function toDmsError(err: unknown): { code: 'unauthorized' | 'network_error'; message: string } {
  if (err instanceof ApiError) {
    return {
      code: err.status === 401 || err.status === 403 ? 'unauthorized' : 'network_error',
      message: err.message || 'Something went wrong.',
    }
  }
  return { code: 'network_error', message: 'Could not reach the server.' }
}

export async function dmsListThreads() {
  try {
    const threads = await request<ThreadSummaryDto[]>('/messages/threads')
    return { ok: true as const, value: threads }
  } catch (err) {
    return { ok: false as const, error: toDmsError(err) }
  }
}

export async function dmsListMessages(threadId: string) {
  try {
    const messages = await request<MessageDto[]>(`/messages/threads/${threadId}/messages`)
    return { ok: true as const, value: messages }
  } catch (err) {
    return { ok: false as const, error: toDmsError(err) }
  }
}

export async function dmsSendMessage(threadId: string, content: string) {
  try {
    const message = await request<MessageDto>(`/messages/threads/${threadId}/messages`, {
      method: 'POST',
      body: JSON.stringify({ content }),
    })
    return { ok: true as const, value: message }
  } catch (err) {
    return { ok: false as const, error: toDmsError(err) }
  }
}
