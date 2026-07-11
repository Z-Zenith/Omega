import { describe, it, expect } from 'vitest'
import { parseRestrictedYears } from './EventsPage'

describe('parseRestrictedYears (#156)', () => {
  it('returns null for a blank input, meaning "everyone"', () => {
    expect(parseRestrictedYears('')).toBeNull()
  })

  it('returns null for whitespace-only input', () => {
    expect(parseRestrictedYears('   ')).toBeNull()
  })

  it('does not inject a spurious 0 for a trailing comma', () => {
    expect(parseRestrictedYears('1,2,')).toEqual([1, 2])
  })

  it('parses a normal comma-separated list', () => {
    expect(parseRestrictedYears('1, 2, 3')).toEqual([1, 2, 3])
  })

  it('ignores genuinely non-numeric segments', () => {
    expect(parseRestrictedYears('1,abc,3')).toEqual([1, 3])
  })
})
