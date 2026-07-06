import type { ReactNode } from 'react'
import { Navigate, Route, Routes, Link } from 'react-router-dom'
import { AuthProvider, useAuth } from '@/lib/auth'
import { LoginPage } from '@/pages/LoginPage'
import { RecordsPage } from '@/pages/RecordsPage'
import { FeesPage } from '@/pages/FeesPage'

function RequireAuth({ children }: { children: ReactNode }) {
  const { token } = useAuth()
  if (!token) return <Navigate to="/login" replace />
  return children
}

function Shell({ children }: { children: ReactNode }) {
  const { wardFullName, setSession } = useAuth()
  return (
    <div className="min-h-svh">
      <nav className="flex items-center justify-between border-b px-8 py-4">
        <div className="flex gap-6 text-sm font-medium">
          <Link to="/records">Attendance &amp; Marks</Link>
          <Link to="/fees">Fees</Link>
        </div>
        <div className="flex items-center gap-4 text-sm text-muted-foreground">
          <span>{wardFullName}</span>
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
          path="/records"
          element={
            <RequireAuth>
              <Shell>
                <RecordsPage />
              </Shell>
            </RequireAuth>
          }
        />
        <Route
          path="/fees"
          element={
            <RequireAuth>
              <Shell>
                <FeesPage />
              </Shell>
            </RequireAuth>
          }
        />
        <Route path="*" element={<Navigate to="/records" replace />} />
      </Routes>
    </AuthProvider>
  )
}

export default App
