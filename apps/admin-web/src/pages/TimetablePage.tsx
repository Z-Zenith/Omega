import { useState } from 'react'
import { useMutation } from '@tanstack/react-query'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card'
import { CalendarGrid } from '@/components/CalendarGrid'
import { generateTimetable, patchTimetableSlot, ApiError, type TimetableSlotDto } from '@/lib/api'

export function TimetablePage() {
  const [slots, setSlots] = useState<TimetableSlotDto[]>([])
  const [selectedSlot, setSelectedSlot] = useState<TimetableSlotDto | null>(null)
  const [room, setRoom] = useState('')
  const [message, setMessage] = useState<string | null>(null)

  const generateMutation = useMutation({
    mutationFn: () => generateTimetable(),
    onSuccess: (result) => {
      setSlots(result)
      setMessage('Timetable generated.')
    },
    onError: (err) =>
      setMessage(
        err instanceof ApiError && err.status === 403
          ? "You don't hold the create_timetable permission."
          : 'Failed to generate timetable.',
      ),
  })

  const patchMutation = useMutation({
    mutationFn: () => patchTimetableSlot(selectedSlot!.id, { room }),
    onSuccess: (updated) => {
      setSlots((prev) => prev.map((s) => (s.id === updated.id ? updated : s)))
      setMessage(`Slot updated — ${updated.subjectName} now in ${updated.room ?? 'no room set'}.`)
      setSelectedSlot(null)
    },
    onError: () => setMessage('Failed to update slot.'),
  })

  const selectSlot = (slot: TimetableSlotDto) => {
    setSelectedSlot(slot)
    setRoom(slot.room ?? '')
  }

  return (
    <div className="mx-auto flex max-w-5xl flex-col gap-6 p-8">
      <Card>
        <CardHeader>
          <CardTitle>Timetable Engine</CardTitle>
          <CardDescription>
            Generate applies feedback-based exclusions (AWA-02); manual edits persist through
            regeneration (AWA-03). Click a slot to edit its room.
          </CardDescription>
        </CardHeader>
        <CardContent className="flex flex-col gap-4">
          <Button onClick={() => generateMutation.mutate()} disabled={generateMutation.isPending} className="w-fit">
            {generateMutation.isPending ? 'Generating…' : 'Generate timetable'}
          </Button>
          {message && <p className="text-sm">{message}</p>}
          {slots.length > 0 && <CalendarGrid slots={slots} onSlotClick={selectSlot} />}
        </CardContent>
      </Card>

      {selectedSlot && (
        <Card>
          <CardHeader>
            <CardTitle>
              Edit slot — {selectedSlot.subjectName} ({selectedSlot.sectionName})
            </CardTitle>
          </CardHeader>
          <CardContent className="flex flex-col gap-3">
            <input
              className="rounded-md border px-3 py-2 text-sm"
              placeholder="Room"
              value={room}
              onChange={(e) => setRoom(e.target.value)}
            />
            <div className="flex gap-2">
              <Button onClick={() => patchMutation.mutate()} disabled={patchMutation.isPending}>
                Save
              </Button>
              <Button onClick={() => setSelectedSlot(null)} className="bg-transparent text-foreground hover:bg-muted">
                Cancel
              </Button>
            </div>
          </CardContent>
        </Card>
      )}
    </div>
  )
}
