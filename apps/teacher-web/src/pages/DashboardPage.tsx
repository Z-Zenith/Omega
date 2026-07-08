import { useMemo, useState, useEffect } from 'react'
import { useQuery } from '@tanstack/react-query'
import { BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer } from 'recharts'
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card'
import { getMyTimetable, getSectionPerformanceSummary } from '@/lib/api'
import { useActiveSection } from '@/lib/activeSection'

// Re-fetched on this interval so the dashboard reflects marks/attendance no older than
// the last sync, without requiring a manual page reload (TWA-04 AC).
const REFRESH_INTERVAL_MS = 30_000

export function DashboardPage() {
  const { sectionId: autoSectionId } = useActiveSection()
  const [selectedSectionId, setSelectedSectionId] = useState<string | null>(null)

  const timetable = useQuery({ queryKey: ['timetable', 'mine'], queryFn: getMyTimetable })

  const taughtSections = useMemo(() => {
    const bySectionId = new Map<string, string>()
    for (const slot of timetable.data ?? []) {
      bySectionId.set(slot.sectionId, slot.sectionName)
    }
    return Array.from(bySectionId, ([sectionId, sectionName]) => ({ sectionId, sectionName }))
  }, [timetable.data])

  // Defaults to the teacher's currently-scheduled section (TWA-01) once timetable data
  // arrives, but the teacher can still pick a different taught section from the dropdown.
  useEffect(() => {
    if (!selectedSectionId && (autoSectionId || taughtSections.length > 0)) {
      setSelectedSectionId(autoSectionId ?? taughtSections[0].sectionId)
    }
  }, [autoSectionId, taughtSections, selectedSectionId])

  const summary = useQuery({
    queryKey: ['timetable', 'sections', selectedSectionId, 'performance-summary'],
    queryFn: () => getSectionPerformanceSummary(selectedSectionId!),
    enabled: !!selectedSectionId,
    refetchInterval: REFRESH_INTERVAL_MS,
  })

  const marksChartData = (summary.data?.marksBySubject ?? []).map((s) => ({
    name: s.subjectName,
    averageMarks: s.averageMarks ?? 0,
  }))

  const attendanceChartData = (summary.data?.studentAttendance ?? []).map((s) => ({
    name: s.studentName,
    attendance: s.attendancePercentage ?? 0,
  }))

  return (
    <div className="mx-auto flex max-w-5xl flex-col gap-6 p-8">
      <Card>
        <CardHeader>
          <CardTitle>Class performance dashboard</CardTitle>
          <CardDescription>Attendance and marks for the selected section (TWA-04).</CardDescription>
        </CardHeader>
        <CardContent className="flex flex-col gap-4">
          {taughtSections.length > 0 && (
            <select
              className="w-fit rounded-md border px-3 py-2 text-sm"
              value={selectedSectionId ?? ''}
              onChange={(e) => setSelectedSectionId(e.target.value)}
            >
              {taughtSections.map((section) => (
                <option key={section.sectionId} value={section.sectionId}>
                  {section.sectionName}
                </option>
              ))}
            </select>
          )}

          {summary.isLoading && <p>Loading…</p>}
          {summary.isError && <p className="text-destructive">Could not load performance summary.</p>}

          {summary.data && (
            <p className="text-sm text-muted-foreground">
              Overall attendance:{' '}
              <span className="font-medium text-foreground">
                {summary.data.overallAttendancePercentage === null ? 'No data yet' : `${summary.data.overallAttendancePercentage}%`}
              </span>
            </p>
          )}
        </CardContent>
      </Card>

      {summary.data && summary.data.studentAttendance.length > 0 && (
        <Card>
          <CardHeader>
            <CardTitle>Attendance by student</CardTitle>
          </CardHeader>
          <CardContent style={{ width: '100%', height: 300 }}>
            <ResponsiveContainer>
              <BarChart data={attendanceChartData}>
                <CartesianGrid strokeDasharray="3 3" />
                <XAxis dataKey="name" tick={{ fontSize: 12 }} />
                <YAxis domain={[0, 100]} />
                <Tooltip />
                <Bar dataKey="attendance" fill="#2563eb" />
              </BarChart>
            </ResponsiveContainer>
          </CardContent>
        </Card>
      )}

      {summary.data && summary.data.marksBySubject.length > 0 && (
        <Card>
          <CardHeader>
            <CardTitle>Average marks by subject</CardTitle>
          </CardHeader>
          <CardContent style={{ width: '100%', height: 300 }}>
            <ResponsiveContainer>
              <BarChart data={marksChartData}>
                <CartesianGrid strokeDasharray="3 3" />
                <XAxis dataKey="name" tick={{ fontSize: 12 }} />
                <YAxis />
                <Tooltip />
                <Bar dataKey="averageMarks" fill="#16a34a" />
              </BarChart>
            </ResponsiveContainer>
          </CardContent>
        </Card>
      )}
    </div>
  )
}
