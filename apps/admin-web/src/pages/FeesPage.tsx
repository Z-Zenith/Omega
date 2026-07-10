import { useState } from 'react'
import { useMutation } from '@tanstack/react-query'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card'
import { createFeeLink, ApiError, type FeeLinkResponse } from '@/lib/api'

// AWA-04 — generate a fee payment link for a student. Backend (FeesController.CreateLink)
// mints one FeeRecord per link, valid only for the exact amount/due-date it was created
// with — the payment link resolves to that record, actual gateway integration is external.
export function FeesPage() {
  const [studentId, setStudentId] = useState('')
  const [amount, setAmount] = useState('')
  const [dueDate, setDueDate] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [created, setCreated] = useState<FeeLinkResponse | null>(null)

  const createFeeLinkMutation = useMutation({
    mutationFn: createFeeLink,
    onSuccess: (link) => {
      setCreated(link)
      setError(null)
      setStudentId('')
      setAmount('')
      setDueDate('')
    },
    onError: (err) => {
      setCreated(null)
      setError(err instanceof ApiError ? `Failed to create fee link: ${err.message || err.status}` : 'Failed to create fee link.')
    },
  })

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    setError(null)
    createFeeLinkMutation.mutate({ studentId, amount: Number(amount), dueDate })
  }

  const canSubmit = Boolean(studentId.trim() && Number(amount) > 0 && dueDate) && !createFeeLinkMutation.isPending

  return (
    <div className="mx-auto flex max-w-2xl flex-col gap-6 p-8">
      <Card>
        <CardHeader>
          <CardTitle>Create a fee payment link</CardTitle>
          <CardDescription>AWA-04 — the link is only valid for this exact amount and due date.</CardDescription>
        </CardHeader>
        <CardContent>
          <form onSubmit={handleSubmit} className="flex flex-col gap-3">
            <label className="text-sm text-muted-foreground">Student ID</label>
            <input
              className="rounded-md border px-3 py-2 text-sm"
              placeholder="Student ID (GUID)"
              value={studentId}
              onChange={(e) => setStudentId(e.target.value)}
            />

            <label className="text-sm text-muted-foreground">Amount</label>
            <input
              className="rounded-md border px-3 py-2 text-sm"
              type="number"
              min="0.01"
              step="0.01"
              placeholder="Amount"
              value={amount}
              onChange={(e) => setAmount(e.target.value)}
            />

            <label className="text-sm text-muted-foreground">Due date</label>
            <input
              className="rounded-md border px-3 py-2 text-sm"
              type="date"
              value={dueDate}
              onChange={(e) => setDueDate(e.target.value)}
            />

            <Button type="submit" disabled={!canSubmit}>
              {createFeeLinkMutation.isPending ? 'Creating…' : 'Create link'}
            </Button>
            {error && <p className="text-sm text-destructive">{error}</p>}
          </form>
        </CardContent>
      </Card>

      {created && (
        <Card>
          <CardHeader>
            <CardTitle>Fee link created</CardTitle>
            <CardDescription>
              {created.amount} due {created.dueDate} — {created.status}
            </CardDescription>
          </CardHeader>
          <CardContent>
            <a className="text-sm text-primary underline underline-offset-4 break-all" href={created.paymentLink}>
              {created.paymentLink}
            </a>
          </CardContent>
        </Card>
      )}
    </div>
  )
}
