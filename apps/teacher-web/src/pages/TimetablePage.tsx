import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card'
import { CalendarGrid } from '@/components/CalendarGrid'
import { getMyTimetable, createChangeRequest, ApiError } from '@/lib/api'
import { useActiveSection } from '@/lib/activeSection'

export function TimetablePage() {
  const [description, setDescription] = useState('')
  const [message, setMessage] = useState<string | null>(null)
  const queryClient = useQueryClient()

  const timetable = useQuery({ queryKey: ['timetable', 'mine'], queryFn: getMyTimetable })
  const { activeSlot } = useActiveSection()

  const changeRequestMutation = useMutation({
    mutationFn: createChangeRequest,
    onSuccess: () => {
      setMessage('Change request submitted — pending Admin approval.')
      setDescription('')
      queryClient.invalidateQueries({ queryKey: ['timetable', 'mine'] })
    },
    onError: (err) => setMessage(err instanceof ApiError ? err.message : 'Failed to submit request'),
  })

  return (
    <div className="mx-auto flex max-w-5xl flex-col gap-6 p-8">
      <Card>
        <CardHeader>
          <CardTitle>My Timetable</CardTitle>
          <CardDescription>Reflects the latest Admin-approved version (TWA-10).</CardDescription>
        </CardHeader>
        <CardContent>
          {activeSlot && (
            <p className="mb-3 text-sm">
              Currently teaching <span className="font-medium">{activeSlot.sectionName}</span> —{' '}
              {activeSlot.subjectName} (TWA-01).
            </p>
          )}
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
    </div>
  )
}
