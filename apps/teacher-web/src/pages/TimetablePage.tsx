import { useMemo, useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card'
import { CalendarGrid } from '@/components/CalendarGrid'
import { getMyTimetable, createChangeRequest, submitSectionFeedback, ApiError } from '@/lib/api'

export function TimetablePage() {
  const [description, setDescription] = useState('')
  const [message, setMessage] = useState<string | null>(null)
  const [feedbackSectionId, setFeedbackSectionId] = useState('')
  const [feedbackRating, setFeedbackRating] = useState(5)
  const [feedbackComments, setFeedbackComments] = useState('')
  const [feedbackMessage, setFeedbackMessage] = useState<string | null>(null)
  const queryClient = useQueryClient()

  const timetable = useQuery({ queryKey: ['timetable', 'mine'], queryFn: getMyTimetable })

  // A teacher may only rate sections they've actually taught — approximated here by
  // "appears in my own timetable" (TWA-12), same set the backend re-validates against
  // TeacherSectionAssignments before writing the feedback row.
  const taughtSections = useMemo(() => {
    const bySectionId = new Map<string, string>()
    for (const slot of timetable.data ?? []) {
      bySectionId.set(slot.sectionId, slot.sectionName)
    }
    return Array.from(bySectionId, ([sectionId, sectionName]) => ({ sectionId, sectionName }))
  }, [timetable.data])

  const changeRequestMutation = useMutation({
    mutationFn: createChangeRequest,
    onSuccess: () => {
      setMessage('Change request submitted — pending Admin approval.')
      setDescription('')
      queryClient.invalidateQueries({ queryKey: ['timetable', 'mine'] })
    },
    onError: (err) => setMessage(err instanceof ApiError ? err.message : 'Failed to submit request'),
  })

  const sectionFeedbackMutation = useMutation({
    mutationFn: () => submitSectionFeedback(feedbackSectionId, feedbackRating, feedbackComments.trim() || undefined),
    onSuccess: () => {
      setFeedbackMessage('Feedback submitted.')
      setFeedbackComments('')
    },
    onError: (err) => setFeedbackMessage(err instanceof ApiError ? err.message : 'Failed to submit feedback'),
  })

  return (
    <div className="mx-auto flex max-w-5xl flex-col gap-6 p-8">
      <Card>
        <CardHeader>
          <CardTitle>My Timetable</CardTitle>
          <CardDescription>Reflects the latest Admin-approved version (TWA-10).</CardDescription>
        </CardHeader>
        <CardContent>
          {timetable.isLoading && <p>Loading…</p>}
          {timetable.isError && <p className="text-destructive">Could not load timetable.</p>}
          {timetable.data && <CalendarGrid slots={timetable.data} />}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Request a timetable change</CardTitle>
          <CardDescription>Routes to Admin for approval (TWA-13).</CardDescription>
        </CardHeader>
        <CardContent className="flex flex-col gap-3">
          <textarea
            className="rounded-md border px-3 py-2 text-sm"
            rows={3}
            placeholder="Describe the change you'd like…"
            value={description}
            onChange={(e) => setDescription(e.target.value)}
          />
          <Button
            onClick={() => changeRequestMutation.mutate(description)}
            disabled={!description.trim() || changeRequestMutation.isPending}
          >
            Submit request
          </Button>
          {message && <p className="text-sm">{message}</p>}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Rate a section</CardTitle>
          <CardDescription>
            Feed into timetable generation (TWA-12) — a low rating excludes you from being
            auto-assigned to that section again (AWA-02).
          </CardDescription>
        </CardHeader>
        <CardContent className="flex flex-col gap-3">
          <select
            className="rounded-md border px-3 py-2 text-sm"
            value={feedbackSectionId}
            onChange={(e) => setFeedbackSectionId(e.target.value)}
          >
            <option value="">Select a section you've taught…</option>
            {taughtSections.map((section) => (
              <option key={section.sectionId} value={section.sectionId}>
                {section.sectionName}
              </option>
            ))}
          </select>
          <div className="flex items-center gap-2">
            <span className="text-sm">Rating:</span>
            {[1, 2, 3, 4, 5].map((value) => (
              <button
                key={value}
                type="button"
                aria-label={`Rate ${value}`}
                className={`h-8 w-8 rounded-md border text-sm ${feedbackRating === value ? 'bg-primary text-primary-foreground' : ''}`}
                onClick={() => setFeedbackRating(value)}
              >
                {value}
              </button>
            ))}
          </div>
          <textarea
            className="rounded-md border px-3 py-2 text-sm"
            rows={3}
            placeholder="Optional comments…"
            value={feedbackComments}
            onChange={(e) => setFeedbackComments(e.target.value)}
          />
          <Button
            onClick={() => sectionFeedbackMutation.mutate()}
            disabled={!feedbackSectionId || sectionFeedbackMutation.isPending}
          >
            Submit rating
          </Button>
          {feedbackMessage && <p className="text-sm">{feedbackMessage}</p>}
        </CardContent>
      </Card>
    </div>
  )
}
