// Core HTTP client, auth, timetable, events, and reports are genuinely shared
// with apps/teacher-web (and, for the HTTP client only, apps/parent-portal) —
// see packages/api-client (issue #87). Everything below stays app-local
// because it's AWA-specific (departments, users, roles, permissions, fees).
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
  getReports,
} from '@campus/api-client'
export type {
  LoginResponse,
  TimetableSlotDto,
  ChangeRequestDto,
  EventDto,
  TeacherReportDto,
} from '@campus/api-client'

import { request } from '@campus/api-client'

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

// AWA-07
export interface TeacherRemarkDto {
  id: string
  teacherId: string
  teacherName: string
  content: string
  submittedAt: string
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
}

export function getStudentRecord(userId: string) {
  return request<StudentRecordDto>(`/users/${userId}/profile`)
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
