import { useEffect, type ReactNode } from 'react'
import { Navigate, Route, Routes, Link, useNavigate } from 'react-router-dom'
import { useQueryClient } from '@tanstack/react-query'
import { AuthProvider, useAuth } from '@/lib/auth'
import { ApiError } from '@/lib/api'
import { LoginPage } from '@/pages/LoginPage'
import { RecordsPage } from '@/pages/RecordsPage'
import { FeesPage } from '@/pages/FeesPage'

function RequireAuth({ children }: { children: ReactNode }) {
  const { token } = useAuth()
  if (!token) return <Navigate to="/login" replace />
  return children
}

// #160 item 4 — RequireAuth only checks token *presence*, not validity, so once a session is
// invalidated server-side (expired/revoked), every page under Shell would otherwise show a
// dead-end "Could not load ..." with no way back to /login. Neither teacher-web nor admin-web
// have a global 401 handler to mirror (both only special-case 403 messages inline), so this
// adds a reasonable one: watch every query for a 401 ApiError and, on the first one, clear the
// stored session and redirect to /login. Lives in Shell (rendered for every authenticated
// route) rather than duplicated per-page.
function useRedirectOnSessionExpiry() {
  const { setSession } = useAuth()
  const navigate = useNavigate()
  const queryClient = useQueryClient()

  useEffect(() => {
    const unsubscribe = queryClient.getQueryCache().subscribe((event) => {
      if (event.type !== 'updated' || event.query.state.status !== 'error') return
      const error = event.query.state.error
      if (error instanceof ApiError && error.status === 401) {
        setSession(null)
        navigate('/login', { replace: true })
      }
    })
    return unsubscribe
  }, [queryClient, setSession, navigate])
}

function Shell({ children }: { children: ReactNode }) {
  useRedirectOnSessionExpiry()
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
