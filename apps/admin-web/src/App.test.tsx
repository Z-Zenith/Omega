import '@testing-library/jest-dom/vitest'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter, Routes, Route } from 'react-router-dom'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { RequireRoleManagement } from './App'
import { AuthProvider } from '@/lib/auth'
import { listRoleBindings } from '@/lib/api'

// AuthProvider (used by every test via renderAtRoles) reads/writes the token through
// getToken/setToken, so the mock below must cover those too, not just listRoleBindings —
// otherwise React blows up before the permission-check logic under test even runs.
vi.mock('@/lib/api', () => ({
  listRoleBindings: vi.fn(),
  getToken: () => localStorage.getItem('campus.token'),
  setToken: (token: string | null) => {
    if (token) localStorage.setItem('campus.token', token)
    else localStorage.removeItem('campus.token')
  },
}))

function renderAtRoles() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={['/roles']}>
        <AuthProvider>
          <Routes>
            <Route
              path="/roles"
              element={
                <RequireRoleManagement>
                  <div data-testid="protected">Roles &amp; Permissions</div>
                </RequireRoleManagement>
              }
            />
            <Route path="/timetable" element={<div data-testid="redirected">Timetable</div>} />
          </Routes>
        </AuthProvider>
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

// #160 item 1 — AWA-13's permission check must never default "hasn't errored yet" to
// "allowed": that previously let any authenticated user see the Roles page/nav link for a
// moment before the query resolved to 403 and redirected them away.
describe('RequireRoleManagement (AWA-13 / #160 item 1)', () => {
  beforeEach(() => {
    localStorage.clear()
    localStorage.setItem('campus.token', 'fake-jwt')
    vi.mocked(listRoleBindings).mockReset()
  })

  it('renders nothing while the permission check is still in flight', async () => {
    vi.mocked(listRoleBindings).mockReturnValue(new Promise(() => {}))
    renderAtRoles()

    expect(screen.queryByTestId('protected')).not.toBeInTheDocument()
    expect(screen.queryByTestId('redirected')).not.toBeInTheDocument()
  })

  it('renders the protected content once the permission check succeeds', async () => {
    vi.mocked(listRoleBindings).mockResolvedValue([])
    renderAtRoles()

    await waitFor(() => expect(screen.getByTestId('protected')).toBeInTheDocument())
  })

  it('redirects to /timetable once the permission check fails (403)', async () => {
    vi.mocked(listRoleBindings).mockRejectedValue(new Error('403 Forbidden'))
    renderAtRoles()

    await waitFor(() => expect(screen.getByTestId('redirected')).toBeInTheDocument())
    expect(screen.queryByTestId('protected')).not.toBeInTheDocument()
  })
})
