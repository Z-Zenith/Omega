import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card'
import { getStudentRecord, ApiError } from '@/lib/api'

// AWA-07: Admin looks up any student's info, teacher remarks, and system-generated
// reports by user ID — there's no student directory/search endpoint yet, so this
// mirrors RolesPage's raw-ID-input pattern rather than a picker.
export function StudentRecordPage() {
  const [studentId, setStudentId] = useState('')
  const [lookupId, setLookupId] = useState<string | null>(null)

  const recordQuery = useQuery({
    queryKey: ['student-record', lookupId],
    queryFn: () => getStudentRecord(lookupId!),
    enabled: lookupId !== null,
  })

  return (
    <div className="mx-auto flex max-w-5xl flex-col gap-6 p-8">
      <Card>
        <CardHeader>
          <CardTitle>Student record</CardTitle>
          <CardDescription>
            View any student's information, teacher-submitted remarks, and system-generated
            reports — remarks stay visible even if the submitting teacher's account is later
            deactivated.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <form
            onSubmit={(e) => {
              e.preventDefault()
              setLookupId(studentId)
            }}
            className="flex gap-3"
          >
            <input
              className="flex-1 rounded-md border px-3 py-2 text-sm"
              placeholder="Student user ID"
              value={studentId}
              onChange={(e) => setStudentId(e.target.value)}
            />
            <Button type="submit" disabled={!studentId || recordQuery.isFetching} className="w-fit">
              Look up
            </Button>
          </form>

          {recordQuery.isError && (
            <p className="mt-4 text-sm text-destructive">
              {recordQuery.error instanceof ApiError && recordQuery.error.status === 403
                ? "You don't hold the view_all_student_records permission, or this isn't a student in your college."
                : recordQuery.error instanceof ApiError && recordQuery.error.status === 404
                  ? 'No user exists with that ID.'
                  : 'Failed to load the student record.'}
            </p>
          )}

          {recordQuery.data && (
            <div className="mt-6 flex flex-col gap-6">
              <div>
                <h3 className="text-sm font-semibold">{recordQuery.data.fullName}</h3>
                <p className="text-sm text-muted-foreground">
                  {recordQuery.data.identifier} — {recordQuery.data.accountType}
                  {!recordQuery.data.isActive && ' — inactive'}
                </p>
              </div>

              <div>
                <h4 className="text-sm font-medium">Teacher remarks</h4>
                {recordQuery.data.remarks.length === 0 && (
                  <p className="text-sm text-muted-foreground">No remarks submitted.</p>
                )}
                <div className="mt-2 flex flex-col gap-2">
                  {recordQuery.data.remarks.map((remark) => (
                    <div key={remark.id} className="rounded-md border px-3 py-2 text-sm">
                      {remark.content}
                      <div className="mt-1 text-xs text-muted-foreground">
                        {remark.teacherName} — {new Date(remark.submittedAt).toLocaleString()}
                      </div>
                    </div>
                  ))}
                </div>
              </div>

              <div>
                <h4 className="text-sm font-medium">Browsing-history summaries</h4>
                {recordQuery.data.browsingSummaries.length === 0 && (
                  <p className="text-sm text-muted-foreground">No summaries generated.</p>
                )}
                <div className="mt-2 flex flex-col gap-2">
                  {recordQuery.data.browsingSummaries.map((summary) => (
                    <div key={summary.id} className="rounded-md border px-3 py-2 text-sm">
                      {summary.summaryText}
                      <div className="mt-1 text-xs text-muted-foreground">
                        {new Date(summary.generatedAt).toLocaleString()}
                      </div>
                    </div>
                  ))}
                </div>
              </div>

              <div>
                <h4 className="text-sm font-medium">Suspicious-behaviour flags</h4>
                {recordQuery.data.suspiciousFlags.length === 0 && (
                  <p className="text-sm text-muted-foreground">No flags raised.</p>
                )}
                <div className="mt-2 flex flex-col gap-2">
                  {recordQuery.data.suspiciousFlags.map((flag) => (
                    <div key={flag.id} className="rounded-md border px-3 py-2 text-sm">
                      Confidence {(flag.confidenceScore * 100).toFixed(0)}%
                      <div className="mt-1 text-xs text-muted-foreground">
                        {new Date(flag.flaggedAt).toLocaleString()}
                      </div>
                    </div>
                  ))}
                </div>
              </div>
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  )
}
