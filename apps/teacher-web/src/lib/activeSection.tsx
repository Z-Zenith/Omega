import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from 'react'
import { useQuery } from '@tanstack/react-query'
import { getMyTimetable, type TimetableSlotDto } from './api'
import { useAuth } from './auth'

// How often we re-evaluate "now" against the timetable. Keeps the selection live across
// a scheduled period (e.g. correctly flips from "no active section" to the next class the
// moment it starts, and clears the moment it ends) without requiring a page reload.
const RECHECK_INTERVAL_MS = 30_000

/**
 * Converts a TimeOnly string ("HH:mm:ss" or "HH:mm") to seconds-since-midnight.
 */
function toSecondsSinceMidnight(time: string): number {
  const [h, m, s] = time.split(':').map(Number)
  return (h || 0) * 3600 + (m || 0) * 60 + (s || 0)
}

/**
 * Finds the timetable slot the teacher is scheduled to be in at `now`, if any.
 *
 * `TimetableSlotDto.dayOfWeek` uses 1=Mon .. 5=Fri (see CalendarGrid), which matches
 * JS `Date#getDay()` for weekdays — Sunday (0) and Saturday (6) simply never match since
 * no slot is ever scheduled on those days. The window is [startTime, endTime).
 *
 * This is a pure function (no Date.now() side effects) so it's trivially unit-testable
 * and can be re-run on every tick/re-render without re-fetching anything.
 */
export function computeActiveSlot(slots: TimetableSlotDto[], now: Date): TimetableSlotDto | null {
  const dayOfWeek = now.getDay()
  const nowSeconds = now.getHours() * 3600 + now.getMinutes() * 60 + now.getSeconds()

  return (
    slots.find((slot) => {
      if (slot.dayOfWeek !== dayOfWeek) return false
      const start = toSecondsSinceMidnight(slot.startTime)
      const end = toSecondsSinceMidnight(slot.endTime)
      return nowSeconds >= start && nowSeconds < end
    }) ?? null
  )
}

interface ActiveSectionState {
  /** The full timetable slot currently in session, or null if the teacher isn't in class right now. */
  activeSlot: TimetableSlotDto | null
  sectionId: string | null
  sectionName: string | null
  isLoading: boolean
  isError: boolean
}

const ActiveSectionContext = createContext<ActiveSectionState | null>(null)

/**
 * Provides the teacher's "currently scheduled section" (TWA-01), computed live from the
 * TWA-10 timetable endpoint. Must be nested inside AuthProvider — it only fetches once a
 * session exists, and shares the `['timetable', 'mine']` query cache with TimetablePage.
 */
export function ActiveSectionProvider({ children }: { children: ReactNode }) {
  const { token } = useAuth()
  const [now, setNow] = useState(() => new Date())

  useEffect(() => {
    const id = setInterval(() => setNow(new Date()), RECHECK_INTERVAL_MS)
    return () => clearInterval(id)
  }, [])

  const timetable = useQuery({
    queryKey: ['timetable', 'mine'],
    queryFn: getMyTimetable,
    enabled: !!token,
  })

  const activeSlot = useMemo(
    () => (timetable.data ? computeActiveSlot(timetable.data, now) : null),
    [timetable.data, now],
  )

  const value: ActiveSectionState = {
    activeSlot,
    sectionId: activeSlot?.sectionId ?? null,
    sectionName: activeSlot?.sectionName ?? null,
    isLoading: timetable.isLoading,
    isError: timetable.isError,
  }

  return <ActiveSectionContext.Provider value={value}>{children}</ActiveSectionContext.Provider>
}

/**
 * Reusable hook exposing the teacher's live "currently scheduled section" (TWA-01).
 * Consumers (attendance marking TWA-08, materials TWA-04/06, etc.) can call this to
 * pre-select the section a teacher is currently in, instead of defaulting to "first in list".
 */
export function useActiveSection() {
  const ctx = useContext(ActiveSectionContext)
  if (!ctx) throw new Error('useActiveSection must be used within ActiveSectionProvider')
  return ctx
}
