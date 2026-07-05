import { Fragment } from 'react'
import type { TimetableSlotDto } from '@/lib/api'

const DAYS = ['Mon', 'Tue', 'Wed', 'Thu', 'Fri']
const HOURS = [9, 10, 11, 12, 13, 14]

function slotStartHour(slot: TimetableSlotDto) {
  return Number(slot.startTime.split(':')[0])
}

interface CalendarGridProps {
  slots: TimetableSlotDto[]
  onSlotClick?: (slot: TimetableSlotDto) => void
}

// Google Calendar-style week grid: day columns x hour rows, class blocks placed by
// (dayOfWeek, startTime). Events/todos aren't rendered here — this is the Timetable
// engine's own grid (TWA-10/AWA-01-03), a different screen from the Student Calendar.
export function CalendarGrid({ slots, onSlotClick }: CalendarGridProps) {
  return (
    <div className="overflow-x-auto rounded-lg border">
      <div className="grid min-w-[640px] grid-cols-[60px_repeat(5,1fr)]">
        <div className="border-b border-r bg-muted/40" />
        {DAYS.map((day) => (
          <div key={day} className="border-b border-r bg-muted/40 p-2 text-center text-sm font-medium last:border-r-0">
            {day}
          </div>
        ))}
        {HOURS.map((hour) => (
          <Fragment key={`row-${hour}`}>
            <div className="border-b border-r p-2 text-right text-xs text-muted-foreground">
              {hour}:00
            </div>
            {DAYS.map((_, dayIndex) => {
              const dayOfWeek = dayIndex + 1
              const slot = slots.find((s) => s.dayOfWeek === dayOfWeek && slotStartHour(s) === hour)
              return (
                <div
                  key={`${hour}-${dayOfWeek}`}
                  className="min-h-16 border-b border-r p-1 last:border-r-0"
                >
                  {slot && (
                    <button
                      type="button"
                      onClick={() => onSlotClick?.(slot)}
                      className={`h-full w-full rounded-md p-2 text-left text-xs ${
                        slot.manuallyEdited ? 'bg-amber-500/20 hover:bg-amber-500/30' : 'bg-primary/15 hover:bg-primary/25'
                      } ${onSlotClick ? 'cursor-pointer' : 'cursor-default'}`}
                    >
                      <div className="font-medium">{slot.subjectName}</div>
                      <div className="text-muted-foreground">{slot.sectionName}</div>
                      {slot.room && <div className="text-muted-foreground">{slot.room}</div>}
                    </button>
                  )}
                </div>
              )
            })}
          </Fragment>
        ))}
      </div>
    </div>
  )
}
