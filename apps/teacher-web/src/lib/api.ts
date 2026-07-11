import { extractOutgoingLinks, type SekError } from '@campus/shared-editor-kit'

const TOKEN_KEY = 'campus.token'

export function getToken(): string | null {
  return localStorage.getItem(TOKEN_KEY)
}

export function setToken(token: string | null) {
  if (token) localStorage.setItem(TOKEN_KEY, token)
  else localStorage.removeItem(TOKEN_KEY)
}

class ApiError extends Error {
  status: number

  constructor(status: number, message: string) {
    super(message)
    this.status = status
  }
}

// #158 — every backend controller returns {"error": "...", "message": "human text"} on
// failure; surface that human message instead of the raw JSON blob, falling back to the
// raw text/status if the body isn't the shape we expect (or isn't JSON at all).
async function readErrorMessage(res: Response): Promise<string> {
  const body = await res.text().catch(() => '')
  if (!body) return res.statusText
  try {
    const parsed = JSON.parse(body)
    if (typeof parsed?.message === 'string' && parsed.message) return parsed.message
  } catch {
    // not JSON - fall through to the raw text below
  }
  return body
}

async function request<T>(path: string, options: RequestInit = {}): Promise<T> {
  const token = getToken()
  const res = await fetch(`/api/v1${path}`, {
    ...options,
    headers: {
      'Content-Type': 'application/json',
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
      ...options.headers,
    },
  })
  if (!res.ok) {
    throw new ApiError(res.status, await readErrorMessage(res))
  }
  if (res.status === 204) return undefined as T
  return res.json() as Promise<T>
}

export interface LoginResponse {
  token: string
  userId: string
  sessionId: string
  accountType: string
  fullName: string
}

export function login(identifier: string, password: string, totpCode: string) {
  return request<LoginResponse>('/auth/login', {
    method: 'POST',
    body: JSON.stringify({ identifier, password, totpCode, deviceInfo: navigator.userAgent }),
  })
}

export interface TimetableSlotDto {
  id: string
  dayOfWeek: number
  startTime: string
  endTime: string
  sectionId: string
  sectionName: string
  subjectId: string
  subjectName: string
  teacherId: string
  teacherName: string
  room: string | null
  manuallyEdited: boolean
}

export function getMyTimetable() {
  return request<TimetableSlotDto[]>('/timetable/mine')
}

export function generateTimetable(departmentId?: string) {
  return request<TimetableSlotDto[]>('/timetable/generate', {
    method: 'POST',
    body: JSON.stringify({ departmentId: departmentId ?? null }),
  })
}

export function patchTimetableSlot(
  id: string,
  patch: Partial<{ teacherId: string; dayOfWeek: number; startTime: string; endTime: string; room: string }>,
) {
  return request<TimetableSlotDto>(`/timetable/slots/${id}`, {
    method: 'PATCH',
    body: JSON.stringify(patch),
  })
}

export interface ChangeRequestDto {
  id: string
  description: string
  status: string
  requestedAt: string
}

export function createChangeRequest(description: string) {
  return request<ChangeRequestDto>('/timetable/change-requests', {
    method: 'POST',
    body: JSON.stringify({ description }),
  })
}

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

export interface EventDto {
  id: string
  title: string
  startTime: string
  endTime: string
  isRegistered: boolean
}

export function createEvent(event: {
  title: string
  startTime: string
  endTime: string
  restrictedYears: number[] | null
  restrictedDepartments: string[] | null
}) {
  return request<EventDto>('/events', {
    method: 'POST',
    body: JSON.stringify(event),
  })
}

export interface TeacherReportDto {
  id: string
  teacherId: string
  teacherName: string
  sectionId: string | null
  sectionName: string | null
  studentId: string | null
  studentName: string | null
  content: string
  submittedAt: string
}

export function createReport(report: { sectionId?: string | null; studentId?: string | null; content: string }) {
  return request<TeacherReportDto>('/reports', {
    method: 'POST',
    body: JSON.stringify({
      sectionId: report.sectionId ?? null,
      studentId: report.studentId ?? null,
      content: report.content,
    }),
  })
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

// TWA-07 — assignment creation. Backend: AssignmentsController.Create (already on main).
export type AssignmentType = 'Code' | 'Quiz' | 'Essay' | 'FileUpload'

export interface AssignmentDto {
  id: string
  subjectId: string
  title: string
  description: string | null
  type: string
  dueDate: string
  submissionWindowStart: string
  submissionWindowEnd: string
  typeSpecificSettings: string | null
}

export function createAssignment(assignment: {
  subjectId: string
  title: string
  description: string | null
  type: AssignmentType
  dueDate: string
  submissionWindowStart: string
  submissionWindowEnd: string
}) {
  return request<AssignmentDto>('/assignments', {
    method: 'POST',
    body: JSON.stringify(assignment),
  })
}

// TWA-06 — material upload. Backend: CommunityController.UploadMaterial (already on main).
export interface MaterialDto {
  id: string
  title: string
  fileUrl: string
  subjectId: string | null
  groupId: string | null
  uploadedBy: string
  uploadedAt: string
}

export function uploadMaterial(material: { title: string; fileUrl: string; subjectId: string | null; groupId: string | null }) {
  return request<MaterialDto>('/materials', {
    method: 'POST',
    body: JSON.stringify(material),
  })
}

// TWA-05 — community groups: create a group, list the groups you belong to, and
// view/post within one. Backend: services/backend-api/Controllers/CommunityController.cs.
export type GroupType = 'SubjectSection' | 'Club' | 'TeacherOnly'

export interface GroupDto {
  id: string
  name: string
  type: string
  sectionId: string | null
}

export interface GroupPostDto {
  id: string
  groupId: string
  authorId: string
  content: string
  createdAt: string
}

export function createGroup(group: { name: string; type: GroupType; sectionId: string | null }) {
  return request<GroupDto>('/groups', {
    method: 'POST',
    body: JSON.stringify(group),
  })
}

export function listMyGroups() {
  return request<{ groups: GroupDto[] }>('/groups/mine')
}

export function listGroupPosts(groupId: string) {
  return request<GroupPostDto[]>(`/groups/${groupId}/posts`)
}

export function createGroupPost(groupId: string, content: string) {
  return request<GroupPostDto>(`/groups/${groupId}/posts`, {
    method: 'POST',
    body: JSON.stringify({ content }),
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

// TWA-14 — thin adapters from the Shared Editor Kit's NotesEditor (SEK-03) embedder
// callbacks (Result<T, SekError>) onto this app's fetch client. Same /notes/* endpoints
// SDA-08/SDA-19 already built for the Student Desktop App — Notes storage isn't
// SDA-specific, ownership is just scoped to whichever user is signed in.
export interface NoteSummaryDto {
  id: string
  title: string
  updatedAt: string
}

interface NoteDto {
  id: string
  title: string
  contentMarkdown: string
  createdAt: string
  updatedAt: string
}

interface NoteLinkInput {
  toNoteId: string
  anchor: string
}

function toSekError(err: unknown): SekError {
  if (err instanceof ApiError) {
    if (err.status === 404) return { code: 'note_not_found', message: 'Note not found.' }
    if (err.status === 403) return { code: 'unauthorized', message: "You don't have access to this note." }
    if (err.status === 400) return { code: 'validation_error', message: err.message || 'Invalid request.' }
    return { code: 'network_error', message: err.message || 'Something went wrong.' }
  }
  return { code: 'network_error', message: 'Could not reach the server.' }
}

// NoteDto has no ownerId (every note the API returns already belongs to the caller);
// SEK's Note type requires one, so it's synthesized from the signed-in user here —
// same approach SDA-19's SekBridge takes on the desktop side.
function toSekNote(dto: NoteDto, ownerId: string) {
  return { id: dto.id, ownerId, title: dto.title, contentMarkdown: dto.contentMarkdown, createdAt: dto.createdAt, updatedAt: dto.updatedAt }
}

export async function notesListMine() {
  try {
    return { ok: true as const, value: await request<NoteSummaryDto[]>('/notes/mine') }
  } catch (err) {
    return { ok: false as const, error: toSekError(err) }
  }
}

export async function notesGet(id: string, ownerId: string) {
  try {
    return { ok: true as const, value: toSekNote(await request<NoteDto>(`/notes/${id}`), ownerId) }
  } catch (err) {
    return { ok: false as const, error: toSekError(err) }
  }
}

// Upsert: SEK-03's NotesEditor always generates a note's Id client-side before its
// first save and expects one onSave callback, not a create/update split — try PATCH
// first, fall back to POST (with that Id) only if the note doesn't exist yet.
export async function notesSave(note: { id: string; ownerId: string; title: string; contentMarkdown: string }) {
  const links: NoteLinkInput[] = extractOutgoingLinks(note.contentMarkdown).map((link) => ({
    toNoteId: link.toNoteId,
    anchor: link.anchor,
  }))
  try {
    const dto = await request<NoteDto>(`/notes/${note.id}`, {
      method: 'PATCH',
      body: JSON.stringify({ title: note.title, contentMarkdown: note.contentMarkdown, links }),
    })
    return { ok: true as const, value: toSekNote(dto, note.ownerId) }
  } catch (err) {
    if (!(err instanceof ApiError) || err.status !== 404) {
      return { ok: false as const, error: toSekError(err) }
    }
    try {
      const dto = await request<NoteDto>('/notes', {
        method: 'POST',
        body: JSON.stringify({ title: note.title, contentMarkdown: note.contentMarkdown, id: note.id, links }),
      })
      return { ok: true as const, value: toSekNote(dto, note.ownerId) }
    } catch (err2) {
      return { ok: false as const, error: toSekError(err2) }
    }
  }
}

export async function notesDelete(id: string) {
  try {
    await request<void>(`/notes/${id}`, { method: 'DELETE' })
    return { ok: true as const, value: undefined }
  } catch (err) {
    return { ok: false as const, error: toSekError(err) }
  }
}

export async function notesBacklinks(id: string, ownerId: string) {
  try {
    const dtos = await request<NoteDto[]>(`/notes/${id}/backlinks`)
    return { ok: true as const, value: dtos.map((dto) => toSekNote(dto, ownerId)) }
  } catch (err) {
    return { ok: false as const, error: toSekError(err) }
  }
}

export { ApiError }
