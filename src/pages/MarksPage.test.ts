import { describe, it, expect } from 'vitest'
import { backendErrorMessage } from './MarksPage'
import { ApiError } from '@/lib/api'

// #160 item 2 — non-403 marks-save failures must surface the backend's actual reason
// (e.g. MarksController's "Marks must not be negative.") instead of a generic
// "Failed to save marks." string, both before and after #158 fixes ApiError.message to
// already be the parsed backend message instead of a raw JSON blob.
describe('backendErrorMessage (TWA-16 / #160 item 2)', () => {
  it('unwraps a raw JSON error blob (current api.ts behavior, pre-#158)', () => {
    const err = new ApiError(400, JSON.stringify({ error: 'invalid_marks', message: 'Marks must not be negative.' }))
    expect(backendErrorMessage(err, 'Failed to save marks.')).toBe('Marks must not be negative.')
  })

  it('falls back to the plain message once #158 makes ApiError.message the parsed string', () => {
    const err = new ApiError(400, 'Marks must not be negative.')
    expect(backendErrorMessage(err, 'Failed to save marks.')).toBe('Marks must not be negative.')
  })

  it('falls back to the generic message for a non-ApiError', () => {
    expect(backendErrorMessage(new Error('network down'), 'Failed to save marks.')).toBe('Failed to save marks.')
  })

  it('falls back to the generic message when the ApiError message is empty', () => {
    const err = new ApiError(500, '')
    expect(backendErrorMessage(err, 'Failed to save marks.')).toBe('Failed to save marks.')
  })
})
