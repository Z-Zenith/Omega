import { Navigate, Route, Routes, Link } from 'react-router-dom'
import { AuthProvider, useAuth } from '@/lib/auth'
import { ActiveSectionProvider, useActiveSection } from '@/lib/activeSection'
import { LoginPage } from '@/pages/LoginPage'
import { TimetablePage } from '@/pages/TimetablePage'
import { EventsPage } from '@/pages/EventsPage'
import { ExternalMarksPage } from '@/pages/ExternalMarksPage'
import { AttendancePage } from '@/pages/AttendancePage'
import { MarksPage } from '@/pages/MarksPage'
import { MessagesPage } from '@/pages/MessagesPage'

function RequireAuth({ children }: { children: React.ReactNode }) {
  const { token } = useAuth()
  if (!token) return <Navigate to="/login" replace />
  return children
}

// TWA-02: global section switcher — lets the teacher pick any assigned section, taking
// precedence over TWA-01's auto-computed one, and reverts on "Auto" so consumers of
// useActiveSection() (dashboard/attendance/materials) update immediately either way.
function SectionSwitcher() {
  const { sectionId, sectionName, isManualOverride, assignedSections, selectSection, clearManualSelection } =
    useActiveSection()

  if (assignedSections.length === 0) return null

  return (
    <div className="flex items-center gap-2 text-sm">
      <label className="text-muted-foreground">Section:</label>
      <select
        className="rounded-md border px-2 py-1 text-sm"
        value={sectionId ?? ''}
        onChange={(e) => (e.target.value ? selectSection(e.target.value) : clearManualSelection())}
      >
        <option value="">— none —</option>
        {assignedSections.map((section) => (
          <option key={section.sectionId} value={section.sectionId}>
            {section.sectionName}
          </option>
        ))}
      </select>
      {isManualOverride && (
        <button onClick={clearManualSelection} className="underline">
          Auto ({sectionName ? 'switch back' : 'clear'})
        </button>
      )}
    </div>
  )
}

function Shell({ children }: { children: React.ReactNode }) {
  const { fullName, setSession } = useAuth()
  return (
    <div className="min-h-svh">
      <nav className="flex items-center justify-between border-b px-8 py-4">
        <div className="flex gap-6 text-sm font-medium">
          <Link to="/timetable">Timetable</Link>
          <Link to="/attendance">Attendance</Link>
          <Link to="/events">Events</Link>
          <Link to="/external-marks">External Marks</Link>
          <Link to="/marks">Marks</Link>
          <Link to="/messages">Messages</Link>
        </div>
        <div className="flex items-center gap-4">
          <SectionSwitcher />
          <div className="flex items-center gap-4 text-sm text-muted-foreground">
            <span>{fullName}</span>
            <button onClick={() => setSession(null)} className="underline">
              Sign out
            </button>
          </div>
        </div>
      </nav>
      {children}
    </div>
  )
}

function App() {
  return (
    <AuthProvider>
      <ActiveSectionProvider>
        <Routes>
          <Route path="/login" element={<LoginPage />} />
          <Route
            path="/timetable"
            element={
              <RequireAuth>
                <Shell>
                  <TimetablePage />
                </Shell>
              </RequireAuth>
            }
          />
          <Route
            path="/attendance"
            element={
              <RequireAuth>
                <Shell>
                  <AttendancePage />
                </Shell>
              </RequireAuth>
            }
          />
          <Route
            path="/events"
            element={
              <RequireAuth>
                <Shell>
                  <EventsPage />
                </Shell>
              </RequireAuth>
            }
          />
          <Route
            path="/external-marks"
            element={
              <RequireAuth>
                <Shell>
                  <ExternalMarksPage />
                </Shell>
              </RequireAuth>
            }
          />
          <Route
            path="/marks"
            element={
              <RequireAuth>
                <Shell>
                  <MarksPage />
                </Shell>
              </RequireAuth>
            }
          />
          <Route
            path="/messages"
            element={
              <RequireAuth>
                <Shell>
                  <MessagesPage />
                </Shell>
              </RequireAuth>
            }
          />
          <Route path="*" element={<Navigate to="/timetable" replace />} />
        </Routes>
      </ActiveSectionProvider>
    </AuthProvider>
  )
}

export default App
