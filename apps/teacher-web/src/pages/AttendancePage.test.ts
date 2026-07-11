import { describe, it, expect } from 'vitest'
import { todayDayOfWeek } from './AttendancePage'

// TWA-08 / #160 item 7 — todayDayOfWeek() must use the same Mon=1..Fri=5, Sun=0 convention
// as CalendarGrid and activeSection.tsx's computeActiveSlot (plain JS getDay(), no remapping
// of Sunday to 7). A previous version mapped Sunday to 7, which was inconsistent with how
// TimetableSlotDto.dayOfWeek is actually populated (TimetableController's Grid seeds 1..5).
describe('todayDayOfWeek (TWA-08)', () => {
  it('returns 0 for Sunday, matching plain getDay() (not a 7-for-Sunday remap)', () => {
    const sunday = new Date(2026, 6, 12) // 2026-07-12 is a Sunday
    expect(todayDayOfWeek(sunday)).toBe(0)
  })

  it('returns 1 for Monday, matching the backend timetable grid (Day=1..5, Mon..Fri)', () => {
    const monday = new Date(2026, 6, 6) // 2026-07-06 is a Monday
    expect(todayDayOfWeek(monday)).toBe(1)
  })

  it('returns 5 for Friday', () => {
    const friday = new Date(2026, 6, 10) // 2026-07-10 is a Friday
    expect(todayDayOfWeek(friday)).toBe(5)
  })

  it('returns 6 for Saturday', () => {
    const saturday = new Date(2026, 6, 11) // 2026-07-11 is a Saturday
    expect(todayDayOfWeek(saturday)).toBe(6)
  })
})
