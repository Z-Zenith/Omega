import { useMemo, useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card'
import {
  ApiError,
  getInternalMarksRoster,
  getMyTimetable,
  submitInternalMark,
  type InternalMarksRosterEntry,
} from '@/lib/api'

// MarksController.CreateInternal (backend) returns BadRequest({ error, message }) for a
// non-403 failure such as negative marks. #158 tracks teaching api.ts's request() to parse
// that JSON body into ApiError.message directly instead of the raw response text; until
// that lands, err.message here is the raw JSON blob, so unwrap it ourselves — this still
// works unchanged once #158 merges (JSON.parse on an already-plain message just fails and
// falls through to the plain-string branch below).
export function backendErrorMessage(err: unknown, fallback: string): string {
  if (!(err instanceof ApiError)) return fallback
  try {
    const parsed = JSON.parse(err.message)
    if (parsed && typeof parsed.message === 'string' && parsed.message) return parsed.message
  } catch {
    // Not JSON — err.message is already the plain message (e.g. once #158 lands).
  }
  return err.message || fallback
}

export function MarksPage() {
  const [subjectId, setSubjectId] = useState('')
  const [assignmentId, setAssignmentId] = useState('')
  const [draftMarks, setDraftMarks] = useState<Record<string, string>>({})
  const [message, setMessage] = useState<string | null>(null)
  const queryClient = useQueryClient()

  const timetable = useQuery({ queryKey: ['timetable', 'mine'], queryFn: getMyTimetable })

  // Subjects the teacher actually teaches — derived from the timetable rather than a
  // separate lookup, since TimetableSlotDto already carries subjectId/subjectName.
  const subjects = useMemo(() => {
    const bySubject = new Map<string, string>()
    for (const slot of timetable.data ?? []) {
      bySubject.set(slot.subjectId, slot.subjectName)
    }
    return Array.from(bySubject, ([id, name]) => ({ id, name }))
  }, [timetable.data])

  const trimmedAssignmentId = assignmentId.trim() || undefined

  const roster = useQuery({
    queryKey: ['marks', 'internal', 'roster', subjectId, trimmedAssignmentId],
    queryFn: () => getInternalMarksRoster(subjectId, trimmedAssignmentId),
    enabled: !!subjectId,
  })

  const marksMutation = useMutation({
    mutationFn: submitInternalMark,
    onSuccess: (record) => {
      setMessage(
        record.published
          ? 'Marks published — now visible to the student (TWA-16).'
          : 'Marks saved — still hidden from the student until published.',
      )
      queryClient.invalidateQueries({ queryKey: ['marks', 'internal', 'roster', subjectId, trimmedAssignmentId] })
    },
    onError: (err) =>
      setMessage(
        err instanceof ApiError && err.status === 403
          ? "You don't have permission to enter marks for this subject/section."
          : backendErrorMessage(err, 'Failed to save marks.'),
      ),
  })

  const valueFor = (entry: InternalMarksRosterEntry) =>
    draftMarks[entry.studentId] ?? (entry.marks !== null ? String(entry.marks) : '')

  const handleSave = (entry: InternalMarksRosterEntry, publish: boolean) => {
    const raw = valueFor(entry)
    const marks = Number(raw)
    if (raw.trim() === '' || Number.isNaN(marks)) {
      setMessage('Enter a numeric mark before saving.')
      return
    }
    // Mirrors MarksController.CreateInternal's `if (request.Marks < 0) return BadRequest(...)`
    // (services/backend-api/Controllers/MarksController.cs:30-33) — reject client-side with
    // the same rule instead of round-tripping to the server just to learn it's invalid.
    if (marks < 0) {
      setMessage('Marks must not be negative.')
      return
    }
    marksMutation.mutate({
      studentId: entry.studentId,
      subjectId,
      assignmentId: trimmedAssignmentId,
      marks,
      publish,
    })
  }

  return (
    <div className="mx-auto flex max-w-3xl flex-col gap-6 p-8">
      <Card>
        <CardHeader>
          <CardTitle>Publish internal marks</CardTitle>
          <CardDescription>
            Marks stay hidden from the student until you explicitly publish them (TWA-16).
          </CardDescription>
        </CardHeader>
        <CardContent className="flex flex-col gap-3">
          <label className="text-sm text-muted-foreground">Subject</label>
          <select
            className="rounded-md border px-3 py-2 text-sm"
            value={subjectId}
            onChange={(e) => setSubjectId(e.target.value)}
          >
            <option value="">Select a subject…</option>
            {subjects.map((s) => (
              <option key={s.id} value={s.id}>
                {s.name}
              </option>
            ))}
          </select>

          <label className="text-sm text-muted-foreground">Assignment ID (optional — leave blank for a subject-level mark)</label>
          <input
            className="rounded-md border px-3 py-2 text-sm"
            placeholder="Assignment ID"
            value={assignmentId}
            onChange={(e) => setAssignmentId(e.target.value)}
          />
        </CardContent>
      </Card>

      {subjectId && (
        <Card>
          <CardHeader>
            <CardTitle>Enter marks</CardTitle>
            <CardDescription>Only students in a section you teach this subject to are listed.</CardDescription>
          </CardHeader>
          <CardContent className="flex flex-col gap-3">
            {roster.isLoading && <p>Loading roster…</p>}
            {roster.isError && (
              <p className="text-destructive">
                {roster.error instanceof ApiError && roster.error.status === 403
                  ? "You don't have permission to enter marks for this subject."
                  : 'Could not load roster.'}
              </p>
            )}
            {roster.data?.length === 0 && <p className="text-sm text-muted-foreground">No students enrolled.</p>}
            {roster.data?.map((entry) => (
              <div key={entry.studentId} className="flex items-center gap-3 border-b pb-3 last:border-b-0">
                <div className="flex-1">
                  <p className="text-sm font-medium">{entry.studentName}</p>
                  <p className="text-xs text-muted-foreground">
                    {entry.published ? `Published${entry.publishedAt ? ` ${new Date(entry.publishedAt).toLocaleString()}` : ''}` : 'Not published'}
                  </p>
                </div>
                <input
                  className="w-24 rounded-md border px-3 py-2 text-sm"
                  type="number"
                  inputMode="decimal"
                  placeholder="Marks"
                  value={valueFor(entry)}
                  onChange={(e) => setDraftMarks((prev) => ({ ...prev, [entry.studentId]: e.target.value }))}
                />
                <Button variant="outline" onClick={() => handleSave(entry, false)} disabled={marksMutation.isPending}>
                  Save
                </Button>
                <Button onClick={() => handleSave(entry, true)} disabled={marksMutation.isPending}>
                  Publish
                </Button>
              </div>
            ))}
          </CardContent>
        </Card>
      )}

      {message && <p className="text-sm">{message}</p>}
    </div>
  )
}
