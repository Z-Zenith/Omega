import { describe, it, expect } from 'vitest'
import { generateTempPassword } from './generatePassword'

describe('AWA-10 generateTempPassword', () => {
  it('defaults to a 16-character password', () => {
    expect(generateTempPassword()).toHaveLength(16)
  })

  it('respects a custom length', () => {
    expect(generateTempPassword(24)).toHaveLength(24)
  })

  it('generates different passwords on successive calls', () => {
    const a = generateTempPassword()
    const b = generateTempPassword()
    expect(a).not.toBe(b)
  })

  it('only uses characters from the allowed charset', () => {
    const password = generateTempPassword(64)
    expect(password).toMatch(/^[A-Za-z0-9!@#$%]+$/)
  })
})
