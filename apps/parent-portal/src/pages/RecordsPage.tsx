import { useQuery } from '@tanstack/react-query'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { getWardRecord } from '@/lib/api'
import { useAuth } from '@/lib/auth'

export function RecordsPage() {
  const { wardStudentId } = useAuth()
  const { data, isLoading, error } = useQuery({
    queryKey: ['ward-record', wardStudentId],
    queryFn: () => getWardRecord(wardStudentId!),
    enabled: !!wardStudentId,
  })

  if (isLoading) return <p className="p-8 text-sm text-muted-foreground">Loading…</p>
  if (error) return <p className="p-8 text-sm text-destructive">Could not load records.</p>
  if (!data) return null

  return (
    <div className="mx-auto flex max-w-3xl flex-col gap-6 p-8">
      <h1 className="text-xl font-semibold">{data.studentFullName}'s records</h1>

      <Card>
        <CardHeader>
          <CardTitle>Attendance</CardTitle>
        </CardHeader>
        <CardContent>
          {data.attendance.length === 0 ? (
            <p className="text-sm text-muted-foreground">No attendance records yet.</p>
          ) : (
            <table className="w-full text-sm">
              <thead>
                <tr className="text-left text-muted-foreground">
                  <th className="py-1">Date</th>
                  <th className="py-1">Subject</th>
                  <th className="py-1">Status</th>
                </tr>
              </thead>
              <tbody>
                {data.attendance.map((a) => (
                  <tr key={`${a.sessionDate}-${a.subjectId}`} className="border-t">
                    <td className="py-1">{a.sessionDate}</td>
                    <td className="py-1">{a.subjectName}</td>
                    <td className="py-1">{a.status}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Internal marks</CardTitle>
        </CardHeader>
        <CardContent>
          {data.internalMarks.length === 0 ? (
            <p className="text-sm text-muted-foreground">No published internal marks yet.</p>
          ) : (
            <table className="w-full text-sm">
              <thead>
                <tr className="text-left text-muted-foreground">
                  <th className="py-1">Subject</th>
                  <th className="py-1">Marks</th>
                </tr>
              </thead>
              <tbody>
                {data.internalMarks.map((m) => (
                  <tr key={m.subjectId} className="border-t">
                    <td className="py-1">{m.subjectName}</td>
                    <td className="py-1">{m.marks}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>External marks</CardTitle>
        </CardHeader>
        <CardContent>
          {data.externalMarks.length === 0 ? (
            <p className="text-sm text-muted-foreground">No published external marks yet.</p>
          ) : (
            <table className="w-full text-sm">
              <thead>
                <tr className="text-left text-muted-foreground">
                  <th className="py-1">Subject</th>
                  <th className="py-1">Grade</th>
                </tr>
              </thead>
              <tbody>
                {data.externalMarks.map((m) => (
                  <tr key={m.subjectId} className="border-t">
                    <td className="py-1">{m.subjectName}</td>
                    <td className="py-1">{m.grade}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </CardContent>
      </Card>
    </div>
  )
}
