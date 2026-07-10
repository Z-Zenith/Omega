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
    const body = await res.text().catch(() => '')
    throw new ApiError(res.status, body || res.statusText)
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

export interface DepartmentDto {
  id: string
  collegeId: string
  name: string
  hodRoleBindingId: string | null
  hodUserId: string | null
}

export function createDepartment(department: { collegeId: string; name: string }) {
  return request<DepartmentDto>('/departments', {
    method: 'POST',
    body: JSON.stringify(department),
  })
}

export function assignHod(departmentId: string, userId: string) {
  return request<DepartmentDto>(`/departments/${departmentId}/hod`, {
    method: 'POST',
    body: JSON.stringify({ userId }),
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

export interface UserProfileDto {
  id: string
  fullName: string
  identifier: string
  accountType: string
  collegeId: string
  departmentId: string | null
  isActive: boolean
}

export function getUserProfile(id: string) {
  return request<UserProfileDto>(`/users/${id}/profile`)
}

export function resetUserPassword(id: string, newPassword: string) {
  return request<void>(`/users/${id}/reset-password`, {
    method: 'POST',
    body: JSON.stringify(newPassword),
  })
}

export interface RoleBindingDto {
  id: string
  userId: string
  userFullName: string
  roleCode: string
  scopeType: 'Global' | 'Department'
  departmentId: string | null
  grantedAt: string
}

export function listRoleBindings() {
  return request<RoleBindingDto[]>('/role-bindings')
}

export function createRoleBinding(binding: {
  userId: string
  roleCode: string
  scopeType: 'Global' | 'Department'
  departmentId: string | null
}) {
  return request<RoleBindingDto>('/role-bindings', {
    method: 'POST',
    body: JSON.stringify(binding),
  })
}

export interface PermissionGrantDto {
  id: string
  userId: string
  userFullName: string
  permissionCode: string
  granted: boolean
  expiresAt: string | null
  grantedBy: string
  createdAt: string
}

export function listPermissionGrants() {
  return request<PermissionGrantDto[]>('/permission-grants')
}

export function createPermissionGrant(grant: {
  userId: string
  permissionCode: string
  granted: boolean
  expiresAt: string | null
}) {
  return request<PermissionGrantDto>('/permission-grants', {
    method: 'POST',
    body: JSON.stringify(grant),
  })
}

export function deletePermissionGrant(id: string) {
  return request<void>(`/permission-grants/${id}`, { method: 'DELETE' })
}

export type AccountType = 'Student' | 'Teacher'

export interface CreateUserRequest {
  collegeId: string
  accountType: AccountType
  identifier: string
  initialPassword: string
  fullName: string
  departmentId: string | null
}

export interface CreateUserResponse {
  userId: string
  totpProvisioningUri: string
  totpSecret: string
}

export function createUser(user: CreateUserRequest) {
  return request<CreateUserResponse>('/users', {
    method: 'POST',
    body: JSON.stringify(user),
  })
}

// AWA-12 — Admin can create community groups directly (in addition to viewing all
// existing groups, AWA-06). Same POST /groups endpoint TWA-05 uses, gated by the
// `create_group` permission (Lecturer/HoD/Admin by default — see services/authz/model.fga
// and db/init/02_seed_roles_and_permissions.sql), so a group created here is
// indistinguishable in structure from one a teacher creates.
export type GroupType = 'SubjectSection' | 'Club' | 'TeacherOnly'

export interface CreateGroupRequest {
  name: string
  type: GroupType
  sectionId: string | null
}

export interface GroupDto {
  id: string
  name: string
  type: string
  sectionId: string | null
}

export function createGroup(group: CreateGroupRequest) {
  return request<GroupDto>('/groups', {
    method: 'POST',
    body: JSON.stringify(group),
  })
}

// AWA-07
export interface TeacherRemarkDto {
  id: string
  teacherId: string
  teacherName: string
  content: string
  submittedAt: string
}

export function getReports() {
  return request<TeacherReportDto[]>('/reports')
}

export interface BrowsingSummaryReportDto {
  id: string
  summaryText: string
  generatedAt: string
}

export interface SuspiciousFlagReportDto {
  id: string
  confidenceScore: number
  flaggedAt: string
  assignmentId: string | null
  classSessionId: string | null
}

// AWA-08 — same shapes SDA-15's MyMarksResponse uses, reused here so the Admin view
// can't drift from what the student sees.
export interface InternalMarkDto {
  subjectId: string
  subjectName: string
  marks: number
  publishedAt: string | null
}

export interface ExternalMarkDto {
  subjectId: string
  subjectName: string
  grade: string
  approvedAt: string | null
}

export interface StudentRecordDto {
  id: string
  fullName: string
  identifier: string
  accountType: string
  collegeId: string
  departmentId: string | null
  isActive: boolean
  remarks: TeacherRemarkDto[]
  browsingSummaries: BrowsingSummaryReportDto[]
  suspiciousFlags: SuspiciousFlagReportDto[]
  internalMarks: InternalMarkDto[]
  externalMarks: ExternalMarkDto[]
}

export function getStudentRecord(userId: string) {
  return request<StudentRecordDto>(`/users/${userId}/profile`)
}

// AWA-06 — institution-wide group visibility. Backend: CommunityController.AllGroups
// (already on main), gated by view_all_groups (Lecturer/HoD/Admin by default). Reuses
// the GroupDto declared above for AWA-12's createGroup.
export function listAllGroups() {
  return request<{ groups: GroupDto[] }>('/groups')
}

// AWA-04 — fee payment links. Backend: FeesController.CreateLink (already on main),
// gated by the manage_fees permission (Finance/Admin by default — services/authz/model.fga).
export interface FeeLinkResponse {
  feeRecordId: string
  paymentLink: string
  amount: number
  dueDate: string
  status: string
}

export function createFeeLink(link: { studentId: string; amount: number; dueDate: string }) {
  return request<FeeLinkResponse>('/fees/links', {
    method: 'POST',
    body: JSON.stringify(link),
  })
}

export { ApiError }
