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

export interface AssignedSection {
  sectionId: string
  sectionName: string
}

/** Distinct sections appearing anywhere in the teacher's timetable (TWA-02: switch target list). */
function computeAssignedSections(slots: TimetableSlotDto[]): AssignedSection[] {
  const bySectionId = new Map<string, string>()
  for (const slot of slots) {
    bySectionId.set(slot.sectionId, slot.sectionName)
  }
  return Array.from(bySectionId, ([sectionId, sectionName]) => ({ sectionId, sectionName }))
}

interface ActiveSectionState {
  /** The full timetable slot currently in session, or null if the teacher isn't in class right now. */
  activeSlot: TimetableSlotDto | null
  sectionId: string | null
  sectionName: string | null
  /** True when `sectionId` reflects a manual switch (TWA-02) rather than the auto-computed slot. */
  isManualOverride: boolean
  /** All sections the teacher is assigned to anywhere in their timetable, for the switcher UI. */
  assignedSections: AssignedSection[]
  /** Switch to any assigned section (TWA-02), taking precedence over the auto-computed one. */
  selectSection: (sectionId: string) => void
  /** Revert to the auto-computed "currently scheduled" section (TWA-01). */
  clearManualSelection: () => void
  isLoading: boolean
  isError: boolean
}

const ActiveSectionContext = createContext<ActiveSectionState | null>(null)

/**
 * Provides the teacher's "currently scheduled section" (TWA-01), computed live from the
 * TWA-10 timetable endpoint, with a manual override (TWA-02) that takes precedence until
 * cleared. Must be nested inside AuthProvider — it only fetches once a session exists, and
 * shares the `['timetable', 'mine']` query cache with TimetablePage.
 */
export function ActiveSectionProvider({ children }: { children: ReactNode }) {
  const { token } = useAuth()
  const [now, setNow] = useState(() => new Date())
  const [manualSectionId, setManualSectionId] = useState<string | null>(null)

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

  const assignedSections = useMemo(
    () => (timetable.data ? computeAssignedSections(timetable.data) : []),
    [timetable.data],
  )

  const manualSection = manualSectionId
    ? assignedSections.find((s) => s.sectionId === manualSectionId) ?? null
    : null

  const sectionId = manualSection?.sectionId ?? activeSlot?.sectionId ?? null
  const sectionName = manualSection?.sectionName ?? activeSlot?.sectionName ?? null

  const value: ActiveSectionState = {
    activeSlot,
    sectionId,
    sectionName,
    isManualOverride: !!manualSection,
    assignedSections,
    selectSection: setManualSectionId,
    clearManualSelection: () => setManualSectionId(null),
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
