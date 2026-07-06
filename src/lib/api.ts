const TOKEN_KEY = 'campus.token'
const WARD_KEY = 'campus.ward'

export function getToken(): string | null {
  return localStorage.getItem(TOKEN_KEY)
}

export function setToken(token: string | null) {
  if (token) localStorage.setItem(TOKEN_KEY, token)
  else localStorage.removeItem(TOKEN_KEY)
}

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

export class ApiError extends Error {
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
