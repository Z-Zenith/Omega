import { describe, it, expect } from 'vitest'
import { computeActiveSlot } from './activeSection'
import type { TimetableSlotDto } from './api'

function slot(overrides: Partial<TimetableSlotDto>): TimetableSlotDto {
  return {
    id: 'slot-1',
    dayOfWeek: 1,
    startTime: '09:00:00',
    endTime: '10:00:00',
    sectionId: 'section-1',
    sectionName: '10-A',
    subjectId: 'subject-1',
    subjectName: 'Mathematics',
    teacherId: 'teacher-1',
    teacherName: 'Ms. Rao',
    room: 'R101',
    manuallyEdited: false,
    ...overrides,
  }
}

describe('computeActiveSlot (TWA-01)', () => {
  it('returns the slot when now falls inside its [startTime, endTime) window', () => {
    const monday0930 = new Date(2026, 6, 6, 9, 30, 0) // 2026-07-06 is a Monday
    const slots = [slot({ dayOfWeek: 1, startTime: '09:00:00', endTime: '10:00:00' })]
    expect(computeActiveSlot(slots, monday0930)?.sectionName).toBe('10-A')
  })

  it('returns null before the slot starts', () => {
    const monday0859 = new Date(2026, 6, 6, 8, 59, 59)
    const slots = [slot({ dayOfWeek: 1, startTime: '09:00:00', endTime: '10:00:00' })]
    expect(computeActiveSlot(slots, monday0859)).toBeNull()
  })

  it('returns null exactly at the slot end time (end is exclusive)', () => {
    const monday1000 = new Date(2026, 6, 6, 10, 0, 0)
    const slots = [slot({ dayOfWeek: 1, startTime: '09:00:00', endTime: '10:00:00' })]
    expect(computeActiveSlot(slots, monday1000)).toBeNull()
  })

  it('picks the correct slot among multiple, based on live time — not list order', () => {
    const tuesday1415 = new Date(2026, 6, 7, 14, 15, 0) // Tuesday
    const slots = [
      slot({ id: 's1', dayOfWeek: 2, startTime: '09:00:00', endTime: '10:00:00', sectionName: '9-B' }),
      slot({ id: 's2', dayOfWeek: 2, startTime: '14:00:00', endTime: '15:00:00', sectionName: '11-C' }),
    ]
    expect(computeActiveSlot(slots, tuesday1415)?.sectionName).toBe('11-C')
  })

  it('does not match a slot on a different day of week', () => {
    const wednesday0930 = new Date(2026, 6, 8, 9, 30, 0) // Wednesday
    const slots = [slot({ dayOfWeek: 1, startTime: '09:00:00', endTime: '10:00:00' })]
    expect(computeActiveSlot(slots, wednesday0930)).toBeNull()
  })

  it('returns null when there are no scheduled slots', () => {
    expect(computeActiveSlot([], new Date(2026, 6, 6, 9, 30, 0))).toBeNull()
  })
})
