import { useState } from 'react'
import { useMutation } from '@tanstack/react-query'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card'
import { createReport, ApiError } from '@/lib/api'

export function ReportsPage() {
  const [sectionId, setSectionId] = useState('')
  const [studentId, setStudentId] = useState('')
  const [content, setContent] = useState('')
  const [message, setMessage] = useState<string | null>(null)

  const createReportMutation = useMutation({
    mutationFn: () =>
      createReport({
        sectionId: sectionId.trim() || null,
        studentId: studentId.trim() || null,
        content,
      }),
    onSuccess: () => {
      setMessage('Report submitted to Admin.')
      setSectionId('')
      setStudentId('')
      setContent('')
    },
    onError: (err) =>
      setMessage(err instanceof ApiError ? err.message || 'Failed to submit report' : 'Failed to submit report'),
  })

  const canSubmit = content.trim().length > 0 && (sectionId.trim().length > 0 || studentId.trim().length > 0)

  return (
    <div className="mx-auto flex max-w-5xl flex-col gap-6 p-8">
      <Card>
        <CardHeader>
          <CardTitle>Report a section or student</CardTitle>
          <CardDescription>Routes directly to the Admin inbox (TWA-11).</CardDescription>
        </CardHeader>
        <CardContent className="flex flex-col gap-3">
          <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
            <div className="flex flex-col gap-1">
              <label className="text-sm font-medium">Section ID</label>
              <input
                className="rounded-md border px-3 py-2 text-sm"
                placeholder="Section UUID (optional if reporting a student)"
                value={sectionId}
                onChange={(e) => setSectionId(e.target.value)}
              />
            </div>
            <div className="flex flex-col gap-1">
              <label className="text-sm font-medium">Student ID</label>
              <input
                className="rounded-md border px-3 py-2 text-sm"
                placeholder="Student UUID (optional if reporting a section)"
                value={studentId}
                onChange={(e) => setStudentId(e.target.value)}
              />
            </div>
          </div>
          <div className="flex flex-col gap-1">
            <label className="text-sm font-medium">Report details</label>
            <textarea
              className="rounded-md border px-3 py-2 text-sm"
              rows={4}
              placeholder="Describe the issue…"
              value={content}
              onChange={(e) => setContent(e.target.value)}
            />
          </div>
          <Button
            onClick={() => createReportMutation.mutate()}
            disabled={!canSubmit || createReportMutation.isPending}
            className="w-fit"
          >
            {createReportMutation.isPending ? 'Submitting…' : 'Submit report'}
          </Button>
          {message && <p className="text-sm">{message}</p>}
        </CardContent>
      </Card>
    </div>
  )
}
