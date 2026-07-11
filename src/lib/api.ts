// Only the core HTTP client (token storage, fetch wrapper, error type) is
// genuinely shared with apps/teacher-web and apps/admin-web — see
// packages/api-client (issue #87). PRT's auth flow (ward login, not staff
// login) and everything below are Parent-Portal-specific and stay local.
export { getToken, setToken, ApiError } from '@campus/api-client'

import { request } from '@campus/api-client'

const WARD_KEY = 'campus.ward'

export interface StoredWard {
  wardStudentId: string
  wardFullName: string
}

export function getStoredWard(): StoredWard | null {
  const raw = localStorage.getItem(WARD_KEY)
  if (!raw) return null
  try {
    return JSON.parse(raw) as StoredWard
  } catch {
    return null
  }
}

export function setStoredWard(ward: StoredWard | null) {
  if (ward) localStorage.setItem(WARD_KEY, JSON.stringify(ward))
  else localStorage.removeItem(WARD_KEY)
}

export interface ParentLoginResponse {
  token: string
  parentUserId: string
  sessionId: string
  wardStudentId: string
  wardFullName: string
  wardIdentifier: string
}

export function parentLogin(rollNumber: string, dateOfBirth: string) {
  return request<ParentLoginResponse>('/parent/login', {
    method: 'POST',
    body: JSON.stringify({ rollNumber, dateOfBirth, deviceInfo: navigator.userAgent }),
  })
}

export interface AttendanceRecordDto {
  sessionDate: string
  subjectId: string
  subjectName: string
  status: string
}

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

export interface WardRecordResponse {
  studentId: string
  studentFullName: string
  attendance: AttendanceRecordDto[]
  internalMarks: InternalMarkDto[]
  externalMarks: ExternalMarkDto[]
}

export function getWardRecord(studentId: string) {
  return request<WardRecordResponse>(`/marks/ward/${studentId}`)
}

export interface WardFeeDto {
  id: string
  amount: number
  dueDate: string
  status: string
  paidAt: string | null
}

export function getWardFees(studentId: string) {
  return request<WardFeeDto[]>(`/fees/ward/${studentId}`)
}

export interface PayFeeResponse {
  feeRecordId: string
  status: string
  processedAt: string
  gatewayTxnId: string
}

export function payFee(feeId: string) {
  return request<PayFeeResponse>(`/fees/${feeId}/pay`, { method: 'POST' })
}
