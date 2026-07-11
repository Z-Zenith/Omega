import { describe, it, expect } from 'vitest'
import { computeHourRange } from './CalendarGrid'
import type { TimetableSlotDto } from '@/lib/api'

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

// #160 item 5 — CalendarGrid must never silently drop a slot that falls outside the
// generator's default 9-15 window (the slot-edit endpoint allows arbitrary start/end times
// with no server-side range validation).
describe('computeHourRange (TWA-10/AWA-01-03)', () => {
  it('defaults to the 9-14 window (9am-3pm) when there are no slots, or all slots fit inside it', () => {
    expect(computeHourRange([])).toEqual([9, 10, 11, 12, 13, 14])
    expect(computeHourRange([slot({ startTime: '11:00:00' })])).toEqual([9, 10, 11, 12, 13, 14])
  })

  it('expands the upper bound to include a slot manually rescheduled after 3pm', () => {
    expect(computeHourRange([slot({ startTime: '16:00:00' })])).toEqual([9, 10, 11, 12, 13, 14, 15, 16])
  })

  it('expands the lower bound to include a slot manually rescheduled before 9am', () => {
    expect(computeHourRange([slot({ startTime: '07:00:00' })])).toEqual([7, 8, 9, 10, 11, 12, 13, 14])
  })

  it('expands both bounds when slots span outside the window on either side', () => {
    expect(
      computeHourRange([slot({ startTime: '06:00:00' }), slot({ startTime: '20:00:00' })]),
    ).toEqual([6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20])
  })
})
