import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest'
import { getToken, setToken, getStoredWard, setStoredWard, ApiError } from './api'
import * as api from './api'

describe('token storage', () => {
  beforeEach(() => {
    localStorage.clear()
  })

  it('returns null when no token is stored', () => {
    expect(getToken()).toBeNull()
  })

  it('round-trips a token through localStorage', () => {
    setToken('abc.def.ghi')
    expect(getToken()).toBe('abc.def.ghi')
  })

  it('clears the token when set to null', () => {
    setToken('abc.def.ghi')
    setToken(null)
    expect(getToken()).toBeNull()
  })
})

describe('ward storage', () => {
  beforeEach(() => {
    localStorage.clear()
  })

  it('returns null when no ward is stored', () => {
    expect(getStoredWard()).toBeNull()
  })

  it('round-trips a ward through localStorage', () => {
    setStoredWard({ wardStudentId: 'stu-1', wardFullName: 'Jane Doe' })
    expect(getStoredWard()).toEqual({ wardStudentId: 'stu-1', wardFullName: 'Jane Doe' })
  })

  it('clears the ward when set to null', () => {
    setStoredWard({ wardStudentId: 'stu-1', wardFullName: 'Jane Doe' })
    setStoredWard(null)
    expect(getStoredWard()).toBeNull()
  })

  it('returns null instead of throwing when stored ward is malformed JSON', () => {
    localStorage.setItem('campus.ward', '{not-json')
    expect(getStoredWard()).toBeNull()
  })
})

// Regression test for #158: raw JSON error blobs must not be surfaced to users - the
// backend's {"error": "...", "message": "human text"} shape should be parsed and only
// `.message` shown, falling back to raw text/status if the body isn't that shape.
describe('request() error handling (#158)', () => {
  const fetchMock = vi.fn()

  beforeEach(() => {
    vi.stubGlobal('fetch', fetchMock)
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('surfaces the backend message instead of the raw JSON body', async () => {
    fetchMock.mockResolvedValue(
      new Response(JSON.stringify({ error: 'unknown_student', message: 'No fee records for this ward.' }), { status: 404 }),
    )

    await expect(api.getWardFees('student-1')).rejects.toMatchObject({
      message: 'No fee records for this ward.',
      status: 404,
    })
  })

  it('falls back to the raw body when the response is not JSON', async () => {
    fetchMock.mockResolvedValue(new Response('Internal Server Error', { status: 500, statusText: 'Internal Server Error' }))

    await expect(api.getWardFees('student-1')).rejects.toMatchObject({ message: 'Internal Server Error' })
  })

  it('falls back to status text when the body is empty', async () => {
    fetchMock.mockResolvedValue(new Response('', { status: 500, statusText: 'Server Error' }))

    await expect(api.getWardFees('student-1')).rejects.toBeInstanceOf(ApiError)
  })
})
