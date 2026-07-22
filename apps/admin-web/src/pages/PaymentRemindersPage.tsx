import { useState } from 'react'
import { useMutation } from '@tanstack/react-query'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card'
import { sendPaymentReminders, ApiError, type SendFeeRemindersResponse } from '@/lib/api'

// AWA-05 — notify parents whose ward's fee is due within the configured reminder
// window (FeeReminder:DaysBeforeDue, server-side). No scheduler exists yet to run this
// automatically, so it's a manually-triggered action for now. Re-running the same day
// is safe — the backend dedupes per parent per day.
export function PaymentRemindersPage() {
  const [error, setError] = useState<string | null>(null)
  const [result, setResult] = useState<SendFeeRemindersResponse | null>(null)

  const sendRemindersMutation = useMutation({
    mutationFn: sendPaymentReminders,
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
        <CardContent className="flex flex-col gap-3">
          <Button
            onClick={() => {
              setError(null)
              sendRemindersMutation.mutate()
            }}
            disabled={sendRemindersMutation.isPending}
            className="w-fit"
          >
            {sendRemindersMutation.isPending ? 'Sending…' : 'Send reminders'}
          </Button>
          {error && <p className="text-sm text-destructive">{error}</p>}
          {result && (
            <p className="text-sm text-muted-foreground">Notified {result.remindersSent} parent(s).</p>
          )}
        </CardContent>
      </Card>
    </div>
  )
}
