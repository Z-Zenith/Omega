import { useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { ApiError, getWardFees, payFee } from '@/lib/api'
import { useAuth } from '@/lib/auth'

export function FeesPage() {
  const { wardStudentId, setSession } = useAuth()
  const queryClient = useQueryClient()
  const [payingId, setPayingId] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)

  const { data, isLoading, error: loadError } = useQuery({
    queryKey: ['ward-fees', wardStudentId],
    queryFn: () => getWardFees(wardStudentId!),
    enabled: !!wardStudentId,
  })

  const handlePay = async (feeId: string) => {
    setError(null)
    setPayingId(feeId)
    try {
      await payFee(feeId)
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Payment failed')
    } finally {
      // #157 — resync on every outcome, not just success: a 409 (already paid, e.g. from
      // another tab or a losing double-click) means the fee genuinely is settled, so the
      // stale "Pending" card and clickable "Pay now" button need to refresh here too,
      // not just disappear on the next unrelated navigation.
      await queryClient.invalidateQueries({ queryKey: ['ward-fees', wardStudentId] })
      setPayingId(null)
    }
  }

  // #160 item 3 — a missing/corrupted wardStudentId (e.g. malformed localStorage) disables
  // the query entirely, so there's never a query error — `data ?? []` would otherwise render
  // as a false "No fee records yet." empty-state instead of surfacing the real problem.
  if (!wardStudentId) {
    return (
      <div className="mx-auto flex max-w-3xl flex-col gap-3 p-8">
        <p className="text-sm text-destructive">
          We couldn't find your linked student account. Please sign in again.
        </p>
        <Button variant="outline" className="self-start" onClick={() => setSession(null)}>
          Sign in again
        </Button>
      </div>
    )
  }

  if (isLoading) return <p className="p-8 text-sm text-muted-foreground">Loading…</p>

  // #160 item 6 — a background refetch failure shouldn't blank out an already-loaded, valid
  // view. Only show the hard error state when there's no data at all yet.
  if (!data) {
    if (loadError) return <p className="p-8 text-sm text-destructive">Could not load fees.</p>
    return null
  }

  return (
    <div className="mx-auto flex max-w-3xl flex-col gap-4 p-8">
      <h1 className="text-xl font-semibold">Fees</h1>
      {loadError && (
        <p className="rounded-md bg-amber-500/10 px-3 py-2 text-sm text-amber-600 dark:text-amber-400">
          Couldn't refresh — showing the last loaded data.
        </p>
      )}
      {error && <p className="text-sm text-destructive">{error}</p>}
      {data.length === 0 ? (
        <p className="text-sm text-muted-foreground">No fee records yet.</p>
      ) : (
        data.map((fee) => (
          <Card key={fee.id}>
            <CardHeader>
              <CardTitle className="flex items-center justify-between text-base">
                <span>₹{fee.amount}</span>
                <span className="text-sm font-normal text-muted-foreground">
                  Due {fee.dueDate}
                </span>
              </CardTitle>
            </CardHeader>
            <CardContent className="flex items-center justify-between">
              <span className="text-sm capitalize">{fee.status}</span>
              {fee.status.toLowerCase() === 'pending' ? (
                <Button disabled={payingId === fee.id} onClick={() => handlePay(fee.id)}>
                  {payingId === fee.id ? 'Paying…' : 'Pay now'}
                </Button>
              ) : (
                <span className="text-sm text-muted-foreground">
                  Paid {fee.paidAt ? new Date(fee.paidAt).toLocaleDateString() : ''}
                </span>
              )}
            </CardContent>
          </Card>
        ))
      )}
    </div>
  )
}
