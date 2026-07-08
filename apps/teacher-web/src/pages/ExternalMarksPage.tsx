import { useState } from 'react'
import { useQuery, useMutation } from '@tanstack/react-query'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card'
import { getExternalMarksPermissionStatus, submitExternalMark, ApiError } from '@/lib/api'

export function ExternalMarksPage() {
  const [studentId, setStudentId] = useState('')
  const [subjectId, setSubjectId] = useState('')
  const [grade, setGrade] = useState('')
  const [message, setMessage] = useState<string | null>(null)

  const permissionStatus = useQuery({
    queryKey: ['marks', 'external', 'permission-status'],
    queryFn: getExternalMarksPermissionStatus,
    refetchInterval: 30_000,
  })

  const submitMutation = useMutation({
    mutationFn: submitExternalMark,
    onSuccess: () => {
      setMessage('Submitted — pending approval (TWA-20) before it becomes visible to the student.')
      setStudentId('')
      setSubjectId('')
      setGrade('')
    },
    onError: (err) => setMessage(err instanceof ApiError ? err.message : 'Failed to submit mark'),
  })

  if (permissionStatus.isLoading) {
    return (
      <div className="mx-auto max-w-3xl p-8">
        <p>Loading…</p>
      </div>
    )
  }

  const granted = permissionStatus.data?.granted ?? false

  return (
    <div className="mx-auto flex max-w-3xl flex-col gap-6 p-8">
      <Card>
        <CardHeader>
          <CardTitle>Submit external marks</CardTitle>
          <CardDescription>
            {granted
              ? `Time-limited grant active${permissionStatus.data?.expiresAt ? ` until ${new Date(permissionStatus.data.expiresAt).toLocaleString()}` : ''}. Submissions are held for approval (TWA-20).`
              : 'You do not currently hold an active grant to submit external marks. This option disappears automatically when your grant expires.'}
          </CardDescription>
        </CardHeader>
        {granted && (
          <CardContent className="flex flex-col gap-3">
            <input
              className="rounded-md border px-3 py-2 text-sm"
              placeholder="Student ID"
              value={studentId}
              onChange={(e) => setStudentId(e.target.value)}
            />
            <input
              className="rounded-md border px-3 py-2 text-sm"
              placeholder="Subject ID"
              value={subjectId}
              onChange={(e) => setSubjectId(e.target.value)}
            />
            <input
              className="rounded-md border px-3 py-2 text-sm"
              placeholder="Grade"
              value={grade}
              onChange={(e) => setGrade(e.target.value)}
            />
            <Button
              onClick={() => submitMutation.mutate({ studentId, subjectId, grade })}
              disabled={!studentId || !subjectId || !grade || submitMutation.isPending}
            >
              Submit for approval
            </Button>
            {message && <p className="text-sm">{message}</p>}
          </CardContent>
        )}
      </Card>
    </div>
  )
}
