import { useState } from 'react'
import { useMutation } from '@tanstack/react-query'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card'
import { createEvent, ApiError } from '@/lib/api'

// #156 — an empty/blank segment must be dropped before Number() conversion, not after:
// Number('') is 0, not NaN, so a blank "restrict to years" field (meant to mean "everyone")
// or a trailing comma ("1,2,") would otherwise silently inject a spurious year 0 that no
// section ever has, making the event invisible/unregisterable for anyone.
export function parseRestrictedYears(input: string): number[] | null {
  const years = input
    .split(',')
    .map((y) => y.trim())
    .filter((y) => y.length > 0)
    .map((y) => Number(y))
    .filter((y) => !Number.isNaN(y))
  return years.length ? years : null
}

export function EventsPage() {
  const [title, setTitle] = useState('')
  const [startTime, setStartTime] = useState('')
  const [endTime, setEndTime] = useState('')
  const [restrictedYears, setRestrictedYears] = useState('')
  const [message, setMessage] = useState<string | null>(null)

  const createEventMutation = useMutation({
    mutationFn: createEvent,
    onSuccess: () => {
      setMessage('Event published — eligible students can now register (SDA-20).')
      setTitle('')
      setStartTime('')
      setEndTime('')
      setRestrictedYears('')
    },
    onError: (err) =>
      setMessage(
        err instanceof ApiError && err.status === 403
          ? "You don't hold the create_event permission."
          : 'Failed to create event.',
      ),
  })

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    createEventMutation.mutate({
      title,
      startTime: new Date(startTime).toISOString(),
      endTime: new Date(endTime).toISOString(),
      restrictedYears: parseRestrictedYears(restrictedYears),
      restrictedDepartments: null,
    })
  }

  return (
    <div className="mx-auto flex max-w-2xl flex-col gap-6 p-8">
      <Card>
        <CardHeader>
          <CardTitle>Create an institution-wide event</CardTitle>
          <CardDescription>
            Optionally restrict to specific years — leave blank for everyone (AWA-11).
          </CardDescription>
        </CardHeader>
        <CardContent>
          <form onSubmit={handleSubmit} className="flex flex-col gap-3">
            <input
              className="rounded-md border px-3 py-2 text-sm"
              placeholder="Title"
              value={title}
              onChange={(e) => setTitle(e.target.value)}
            />
            <label className="text-sm text-muted-foreground">Start time</label>
            <input
              className="rounded-md border px-3 py-2 text-sm"
              type="datetime-local"
              value={startTime}
              onChange={(e) => setStartTime(e.target.value)}
            />
            <label className="text-sm text-muted-foreground">End time</label>
            <input
              className="rounded-md border px-3 py-2 text-sm"
              type="datetime-local"
              value={endTime}
              onChange={(e) => setEndTime(e.target.value)}
            />
            <input
              className="rounded-md border px-3 py-2 text-sm"
              placeholder="Restrict to years (comma-separated, optional)"
              value={restrictedYears}
              onChange={(e) => setRestrictedYears(e.target.value)}
            />
            <Button type="submit" disabled={!title || !startTime || !endTime || createEventMutation.isPending}>
              Publish event
            </Button>
            {message && <p className="text-sm">{message}</p>}
          </form>
        </CardContent>
      </Card>
    </div>
  )
}
