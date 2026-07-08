import { describe, it, expect, beforeEach } from 'vitest'
import { getToken, setToken, getStoredWard, setStoredWard } from './api'

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
