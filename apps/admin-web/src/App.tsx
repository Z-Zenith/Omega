import { Navigate, Route, Routes, Link } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { AuthProvider, useAuth } from '@/lib/auth'
import { listRoleBindings } from '@/lib/api'
import { LoginPage } from '@/pages/LoginPage'
import { TimetablePage } from '@/pages/TimetablePage'
import { EventsPage } from '@/pages/EventsPage'
import { ReportsInboxPage } from '@/pages/ReportsInboxPage'
import { RolesPage } from '@/pages/RolesPage'
import { CreateAccountPage } from '@/pages/CreateAccountPage'
import { StudentRecordPage } from '@/pages/StudentRecordPage'

function RequireAuth({ children }: { children: React.ReactNode }) {
  const { token } = useAuth()
  if (!token) return <Navigate to="/login" replace />
  return children
}

// AWA-13: the JWT only carries account_type (e.g. "AdminTier"), not the specific role_code
// (admin/it/hod/finance all share that account_type), so "only Admin/IT see this" is enforced
// by asking the already permission-gated backend rather than inspecting a client-side claim.
function useCanManageRoles() {
  const { token } = useAuth()
  const query = useQuery({
    queryKey: ['role-bindings'],
    queryFn: listRoleBindings,
    enabled: !!token,
    retry: false,
  })
  return !query.isError
}

function RequireRoleManagement({ children }: { children: React.ReactNode }) {
  const canManageRoles = useCanManageRoles()
  if (!canManageRoles) return <Navigate to="/timetable" replace />
  return children
}

function Shell({ children }: { children: React.ReactNode }) {
  const { fullName, setSession } = useAuth()
  const canManageRoles = useCanManageRoles()
  return (
    <div className="min-h-svh">
      <nav className="flex items-center justify-between border-b px-8 py-4">
        <div className="flex gap-6 text-sm font-medium">
          <Link to="/timetable">Timetable</Link>
          <Link to="/events">Events</Link>
          <Link to="/reports">Reports</Link>
          {canManageRoles && <Link to="/roles">Roles & Permissions</Link>}
          <Link to="/accounts/new">Create account</Link>
          <Link to="/students">Student Records</Link>
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
          path="/reports"
          element={
            <RequireAuth>
              <Shell>
                <ReportsInboxPage />
              </Shell>
            </RequireAuth>
          }
        />
        <Route
          path="/roles"
          element={
            <RequireAuth>
              <RequireRoleManagement>
                <Shell>
                  <RolesPage />
                </Shell>
              </RequireRoleManagement>
            </RequireAuth>
          }
        />
        <Route
          path="/accounts/new"
          element={
            <RequireAuth>
              <Shell>
                <CreateAccountPage />
              </Shell>
            </RequireAuth>
          }
        />
        <Route
          path="/students"
          element={
            <RequireAuth>
              <Shell>
                <StudentRecordPage />
              </Shell>
            </RequireAuth>
          }
        />
        <Route path="*" element={<Navigate to="/timetable" replace />} />
      </Routes>
    </AuthProvider>
  )
}

export default App
