import { useEffect, useMemo, useState } from 'react'
import { useQuery, useMutation } from '@tanstack/react-query'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card'
import {
  getMyTimetable,
  getSectionRoster,
  markAttendance,
  ApiError,
  type AttendanceStatus,
  type TimetableSlotDto,
} from '@/lib/api'

const STATUS_OPTIONS: AttendanceStatus[] = ['Present', 'Absent', 'Late']

function todayIso(): string {
  return new Date().toISOString().slice(0, 10)
}

// JS getDay(): 0=Sunday..6=Saturday. Backend's timetable grid runs Monday(1)..Friday(5).
function todayDayOfWeek(): number {
  const day = new Date().getDay()
  return day === 0 ? 7 : day
}

export function AttendancePage() {
  const [selectedSlotId, setSelectedSlotId] = useState<string | null>(null)
  const [statuses, setStatuses] = useState<Record<string, AttendanceStatus>>({})
  const [message, setMessage] = useState<string | null>(null)

  const timetable = useQuery({ queryKey: ['timetable', 'mine'], queryFn: getMyTimetable })

  const todaysSlots = useMemo<TimetableSlotDto[]>(() => {
    if (!timetable.data) return []
    const dow = todayDayOfWeek()
    return timetable.data
      .filter((s) => s.dayOfWeek === dow)
      .sort((a, b) => a.startTime.localeCompare(b.startTime))
  }, [timetable.data])

  useEffect(() => {
    if (!selectedSlotId && todaysSlots.length > 0) {
      setSelectedSlotId(todaysSlots[0].id)
    }
  }, [todaysSlots, selectedSlotId])

  const roster = useQuery({
    queryKey: ['attendance', 'roster', selectedSlotId],
    queryFn: () => getSectionRoster(selectedSlotId!),
    enabled: !!selectedSlotId,
  })

  // Every enrolled student defaults to Present as soon as the roster loads, so the AC
  // ("every enrolled student must have a status set") holds before the teacher touches
  // anything — they only need to change the exceptions.
  useEffect(() => {
    if (!roster.data) return
    setStatuses((prev) => {
      const next: Record<string, AttendanceStatus> = {}
      for (const student of roster.data) {
        next[student.studentId] = prev[student.studentId] ?? 'Present'
      }
      return next
    })
  }, [roster.data])

  const markMutation = useMutation({
    mutationFn: () =>
      markAttendance(
        selectedSlotId!,
        Object.entries(statuses).map(([studentId, status]) => ({ studentId, status })),
        todayIso(),
      ),
    onSuccess: (response) => setMessage(`Attendance saved for ${response.records.length} student(s).`),
    onError: (err) =>
      setMessage(
        err instanceof ApiError
          ? `Could not save attendance: ${err.message}`
          : 'Could not save attendance.',
      ),
  })

  const selectedSlot = todaysSlots.find((s) => s.id === selectedSlotId)
  const allMarked = !!roster.data && roster.data.every((s) => statuses[s.studentId])

  return (
    <div className="mx-auto flex max-w-3xl flex-col gap-6 p-8">
      <Card>
        <CardHeader>
          <CardTitle>Mark attendance</CardTitle>
          <CardDescription>Mark attendance per session for the active section (TWA-08).</CardDescription>
        </CardHeader>
        <CardContent className="flex flex-col gap-4">
          {timetable.isLoading && <p>Loading today's sessions…</p>}
          {timetable.isError && <p className="text-destructive">Could not load your timetable.</p>}

          {timetable.data && todaysSlots.length === 0 && (
            <p className="text-sm text-muted-foreground">No sessions scheduled for you today.</p>
          )}

          {todaysSlots.length > 0 && (
            <div className="flex flex-col gap-2">
              <label className="text-sm text-muted-foreground">Session</label>
              <select
                className="rounded-md border px-3 py-2 text-sm"
                value={selectedSlotId ?? ''}
                onChange={(e) => setSelectedSlotId(e.target.value)}
              >
                {todaysSlots.map((slot) => (
                  <option key={slot.id} value={slot.id}>
                    {slot.startTime}–{slot.endTime} · {slot.sectionName} · {slot.subjectName}
                  </option>
                ))}
              </select>
            </div>
          )}

          {selectedSlot && roster.isLoading && <p>Loading roster…</p>}
          {selectedSlot && roster.isError && <p className="text-destructive">Could not load section roster.</p>}

          {selectedSlot && roster.data && roster.data.length === 0 && (
            <p className="text-sm text-muted-foreground">No students are enrolled in this section.</p>
          )}

          {selectedSlot && roster.data && roster.data.length > 0 && (
            <div className="flex flex-col divide-y rounded-md border">
              {roster.data.map((student) => (
                <div key={student.studentId} className="flex items-center justify-between gap-4 px-4 py-2">
                  <span className="text-sm">{student.fullName}</span>
                  <select
                    className="rounded-md border px-2 py-1 text-sm"
                    value={statuses[student.studentId] ?? 'Present'}
                    onChange={(e) =>
                      setStatuses((prev) => ({
                        ...prev,
                        [student.studentId]: e.target.value as AttendanceStatus,
                      }))
                    }
                  >
                    {STATUS_OPTIONS.map((status) => (
                      <option key={status} value={status}>
                        {status}
                      </option>
                    ))}
                  </select>
                </div>
              ))}
            </div>
          )}

          {selectedSlot && roster.data && roster.data.length > 0 && (
            <Button onClick={() => markMutation.mutate()} disabled={!allMarked || markMutation.isPending}>
              Submit attendance
            </Button>
          )}

          {message && <p className="text-sm">{message}</p>}
        </CardContent>
      </Card>
    </div>
  )
}
