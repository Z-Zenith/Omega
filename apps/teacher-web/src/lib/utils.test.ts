import { describe, it, expect } from 'vitest'
import { cn } from './utils'

describe('cn', () => {
  it('joins truthy class names', () => {
    expect(cn('a', 'b')).toBe('a b')
  })

  it('drops falsy values', () => {
    const isEnabled = false
    expect(cn('a', isEnabled && 'b', undefined, null, 'c')).toBe('a c')
  })

  it('merges conflicting tailwind classes, keeping the last one', () => {
    expect(cn('p-2', 'p-4')).toBe('p-4')
  })
})
