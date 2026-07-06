import { useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { ApiError, getWardFees, payFee } from '@/lib/api'
import { useAuth } from '@/lib/auth'

export function FeesPage() {
  const { wardStudentId } = useAuth()
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
      await queryClient.invalidateQueries({ queryKey: ['ward-fees', wardStudentId] })
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Payment failed')
    } finally {
      setPayingId(null)
    }
  }

  if (isLoading) return <p className="p-8 text-sm text-muted-foreground">Loading…</p>
  if (loadError) return <p className="p-8 text-sm text-destructive">Could not load fees.</p>

  return (
    <div className="mx-auto flex max-w-3xl flex-col gap-4 p-8">
      <h1 className="text-xl font-semibold">Fees</h1>
      {error && <p className="text-sm text-destructive">{error}</p>}
      {(data ?? []).length === 0 ? (
        <p className="text-sm text-muted-foreground">No fee records yet.</p>
      ) : (
        data!.map((fee) => (
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
