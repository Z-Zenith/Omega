import '@testing-library/jest-dom/vitest'
import { describe, expect, it, vi, beforeEach } from 'vitest'
import { render, screen, waitFor, fireEvent } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { AttendancePage } from './AttendancePage'
import * as api from '@/lib/api'

// Regression test for #150: submitting attendance for session B must not include
// leftover student IDs accumulated while session A's roster was displayed.
vi.mock('@/lib/api', () => ({
  getMyTimetable: vi.fn(),
  getSectionRoster: vi.fn(),
  getAttendanceAlerts: vi.fn(),
  markAttendance: vi.fn(),
  ApiError: class ApiError extends Error {},
}))

const slotA = {
  id: 'slot-a',
  dayOfWeek: new Date().getDay() === 0 ? 7 : new Date().getDay(),
  startTime: '09:00',
  endTime: '10:00',
  sectionId: 'section-a',
  sectionName: 'Section A',
  subjectId: 'subj-a',
  subjectName: 'Maths',
  teacherId: 't1',
  teacherName: 'Teacher',
  room: null,
  manuallyEdited: false,
}

const slotB = { ...slotA, id: 'slot-b', sectionId: 'section-b', sectionName: 'Section B', subjectId: 'subj-b', subjectName: 'Physics' }

const rosterA = [{ studentId: 'student-a1', fullName: 'Alice' }]
const rosterB = [{ studentId: 'student-b1', fullName: 'Bob' }]

function renderPage() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={client}>
      <AttendancePage />
    </QueryClientProvider>,
  )
}

describe('AttendancePage (#150)', () => {
  beforeEach(() => {
    vi.mocked(api.getMyTimetable).mockResolvedValue([slotA, slotB])
    vi.mocked(api.getAttendanceAlerts).mockResolvedValue([])
    vi.mocked(api.getSectionRoster).mockImplementation((slotId: string) =>
      Promise.resolve(slotId === 'slot-a' ? rosterA : rosterB),
    )
    vi.mocked(api.markAttendance).mockResolvedValue({
      classSessionId: 'cs-1',
      sessionDate: '2026-01-01',
      sectionId: 'section-b',
      records: [],
    })
  })

  it('only submits the currently selected session roster, not students from a previously viewed session', async () => {
    renderPage()

    // Session A loads by default (first today's slot) and its roster appears.
    await screen.findByText('Alice')

    // Switch to session B (the session selector is the first combobox on the page).
    const select = screen.getAllByRole('combobox')[0]
    fireEvent.change(select, { target: { value: 'slot-b' } })

    await screen.findByText('Bob')
    expect(screen.queryByText('Alice')).not.toBeInTheDocument()

    // Wait for the "every enrolled student has a status" effect to settle for the
    // new roster before submitting — this is what the button's disabled state gates
    // in the real app too, so it's the correct signal to wait on, not an arbitrary delay.
    const submitButton = screen.getByRole('button', { name: 'Submit attendance' })
    await waitFor(() => expect(submitButton).not.toBeDisabled())

    fireEvent.click(submitButton)

    await waitFor(() => expect(api.markAttendance).toHaveBeenCalledTimes(1))
    const [slotId, entries] = vi.mocked(api.markAttendance).mock.calls[0]
    expect(slotId).toBe('slot-b')
    expect(entries).toEqual([{ studentId: 'student-b1', status: 'Present' }])
  })
})
