import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card'
import { approveExternalMark, getPendingExternalMarks, ApiError } from '@/lib/api'

const PENDING_EXTERNAL_MARKS_KEY = ['marks', 'external', 'pending']

export function ApproveMarksPage() {
  const queryClient = useQueryClient()

  // Gated server-side by the approve_external_marks permission (TWA-20) — a 403 here
  // means the signed-in account doesn't hold an active grant, so the page shows an
  // access-denied message instead of a queue nobody but the caller can act on.
  const pending = useQuery({
    queryKey: PENDING_EXTERNAL_MARKS_KEY,
    queryFn: getPendingExternalMarks,
    retry: false,
  })

  const approveMutation = useMutation({
    mutationFn: approveExternalMark,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: PENDING_EXTERNAL_MARKS_KEY })
    },
  })

  const forbidden = pending.isError && pending.error instanceof ApiError && pending.error.status === 403

  return (
    <div className="mx-auto flex max-w-3xl flex-col gap-6 p-8">
      <Card>
        <CardHeader>
          <CardTitle>Approve external marks</CardTitle>
          <CardDescription>
            External (SEE/CIE-style) marks stay invisible to the student until approved here (TWA-20).
            Internal marks published directly (TWA-16) don't go through this queue.
          </CardDescription>
        </CardHeader>
        <CardContent>
          {pending.isLoading && <p>Loading…</p>}

          {forbidden && (
            <p className="text-sm text-destructive">
              You don't hold the approve_external_marks permission, so there's nothing to show here.
            </p>
          )}

          {pending.isError && !forbidden && (
            <p className="text-sm text-destructive">Could not load the pending marks queue.</p>
          )}

          {pending.data && pending.data.length === 0 && (
            <p className="text-sm text-muted-foreground">No external marks are waiting for approval.</p>
          )}

          {pending.data && pending.data.length > 0 && (
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b text-left text-muted-foreground">
                  <th className="py-2 pr-4">Student</th>
                  <th className="py-2 pr-4">Subject</th>
                  <th className="py-2 pr-4">Grade</th>
                  <th className="py-2 pr-4">Submitted by</th>
                  <th className="py-2 pr-4">Submitted at</th>
                  <th className="py-2" />
                </tr>
              </thead>
              <tbody>
                {pending.data.map((mark) => (
                  <tr key={mark.id} className="border-b last:border-0">
                    <td className="py-2 pr-4">{mark.studentFullName}</td>
                    <td className="py-2 pr-4">{mark.subjectName}</td>
                    <td className="py-2 pr-4">{mark.grade}</td>
                    <td className="py-2 pr-4">{mark.submittedByFullName}</td>
                    <td className="py-2 pr-4">{new Date(mark.submittedAt).toLocaleString()}</td>
                    <td className="py-2">
                      <Button
                        size="sm"
                        onClick={() => approveMutation.mutate(mark.id)}
                        disabled={approveMutation.isPending}
                      >
                        Approve
                      </Button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}

          {approveMutation.isError && (
            <p className="mt-3 text-sm text-destructive">
              {approveMutation.error instanceof ApiError && approveMutation.error.status === 403
                ? "You don't hold the approve_external_marks permission."
                : 'Failed to approve this mark.'}
            </p>
          )}
        </CardContent>
      </Card>
    </div>
  )
}
