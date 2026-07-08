import { useState } from 'react'
import { useMutation } from '@tanstack/react-query'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card'
import { sendPaymentReminders, ApiError, type SendFeeRemindersResponse } from '@/lib/api'

// AWA-05 — notify parents whose ward's fee is due within N days. No scheduler exists yet
// to run this automatically, so it's a manually-triggered action for now (a natural
// follow-up once one exists). Re-running for the same day is safe — the backend dedupes.
export function PaymentRemindersPage() {
  const [daysBefore, setDaysBefore] = useState('7')
  const [error, setError] = useState<string | null>(null)
  const [result, setResult] = useState<SendFeeRemindersResponse | null>(null)

  const sendRemindersMutation = useMutation({
    mutationFn: () => sendPaymentReminders(Number(daysBefore)),
    onSuccess: (response) => {
      setResult(response)
      setError(null)
    },
    onError: (err) => {
      setResult(null)
      setError(err instanceof ApiError ? `Failed to send reminders: ${err.message || err.status}` : 'Failed to send reminders.')
    },
  })

  return (
    <div className="mx-auto flex max-w-2xl flex-col gap-6 p-8">
      <Card>
        <CardHeader>
          <CardTitle>Send payment reminders</CardTitle>
          <CardDescription>AWA-05 — notify parents whose ward's fee is due soon.</CardDescription>
        </CardHeader>
        <CardContent>
          <form
            onSubmit={(e) => {
              e.preventDefault()
              setError(null)
              sendRemindersMutation.mutate()
            }}
            className="flex flex-col gap-3"
          >
            <label className="text-sm text-muted-foreground">Days before due date</label>
            <input
              className="rounded-md border px-3 py-2 text-sm"
              type="number"
              min="0"
              value={daysBefore}
              onChange={(e) => setDaysBefore(e.target.value)}
            />
            <Button type="submit" disabled={sendRemindersMutation.isPending} className="w-fit">
              {sendRemindersMutation.isPending ? 'Sending…' : 'Send reminders'}
            </Button>
            {error && <p className="text-sm text-destructive">{error}</p>}
          </form>

          {result && (
            <p className="mt-4 text-sm text-muted-foreground">
              {result.feesDueSoon} fee(s) due in {daysBefore} day(s) — notified {result.notifiedParentIds.length} parent(s).
            </p>
          )}
        </CardContent>
      </Card>
    </div>
  )
}
