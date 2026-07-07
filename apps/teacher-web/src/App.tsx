import { Navigate, Route, Routes, Link } from 'react-router-dom'
import { AuthProvider, useAuth } from '@/lib/auth'
import { ActiveSectionProvider } from '@/lib/activeSection'
import { LoginPage } from '@/pages/LoginPage'
import { TimetablePage } from '@/pages/TimetablePage'
import { EventsPage } from '@/pages/EventsPage'
import { MarksPage } from '@/pages/MarksPage'
import { MessagesPage } from '@/pages/MessagesPage'

function RequireAuth({ children }: { children: React.ReactNode }) {
  const { token } = useAuth()
  if (!token) return <Navigate to="/login" replace />
  return children
}

function Shell({ children }: { children: React.ReactNode }) {
  const { fullName, setSession } = useAuth()
  return (
    <div className="min-h-svh">
      <nav className="flex items-center justify-between border-b px-8 py-4">
        <div className="flex gap-6 text-sm font-medium">
          <Link to="/timetable">Timetable</Link>
          <Link to="/events">Events</Link>
          <Link to="/marks">Marks</Link>
          <Link to="/messages">Messages</Link>
        </div>
        <div className="flex items-center gap-4 text-sm text-muted-foreground">
          <span>{fullName}</span>
          <button onClick={() => setSession(null)} className="underline">
            Sign out
          </button>
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
