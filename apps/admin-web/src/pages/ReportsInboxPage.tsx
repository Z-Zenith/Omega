import { useQuery } from '@tanstack/react-query'
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card'
import { getReports } from '@/lib/api'

export function ReportsInboxPage() {
  const reports = useQuery({ queryKey: ['reports'], queryFn: getReports })

  return (
    <div className="mx-auto flex max-w-5xl flex-col gap-6 p-8">
      <Card>
        <CardHeader>
          <CardTitle>Teacher reports inbox</CardTitle>
          <CardDescription>Section/student reports submitted by teachers (TWA-11).</CardDescription>
        </CardHeader>
        <CardContent className="flex flex-col gap-3">
          {reports.isLoading && <p>Loading…</p>}
          {reports.isError && <p className="text-destructive">Could not load reports.</p>}
          {reports.data && reports.data.length === 0 && (
            <p className="text-sm text-muted-foreground">No reports submitted yet.</p>
          )}
          {reports.data && reports.data.length > 0 && (
            <div className="overflow-x-auto">
              <table className="w-full text-left text-sm">
                <thead>
                  <tr className="border-b text-muted-foreground">
                    <th className="py-2 pr-4">Teacher</th>
                    <th className="py-2 pr-4">Section / Student</th>
                    <th className="py-2 pr-4">Report</th>
                    <th className="py-2 pr-4">Submitted</th>
                  </tr>
                </thead>
                <tbody>
                  {reports.data.map((report) => (
                    <tr key={report.id} className="border-b last:border-0">
                      <td className="py-2 pr-4">{report.teacherName}</td>
                      <td className="py-2 pr-4">
                        {report.sectionName ?? '—'}
                        {report.studentName ? ` / ${report.studentName}` : ''}
                      </td>
                      <td className="py-2 pr-4">{report.content}</td>
                      <td className="py-2 pr-4">{new Date(report.submittedAt).toLocaleString()}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  )
}
