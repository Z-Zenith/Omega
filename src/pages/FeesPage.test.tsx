import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import '@testing-library/jest-dom/vitest'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { FeesPage } from './FeesPage'
import * as api from '@/lib/api'
import * as auth from '@/lib/auth'

// Regression test for #157: a 409 (already-paid) response must still resync the fee list,
// not leave a stale "Pending"/"Pay now" card on a fee that's actually already settled.
vi.mock('@/lib/api', async () => {
  const actual = await vi.importActual<typeof api>('@/lib/api')
  return {
    ...actual,
    getWardFees: vi.fn(),
    payFee: vi.fn(),
  }
})

vi.mock('@/lib/auth', () => ({
  useAuth: vi.fn(),
}))

const pendingFee = { id: 'fee-1', amount: 5000, dueDate: '2026-08-01', status: 'Pending', paidAt: null }
const paidFee = { id: 'fee-1', amount: 5000, dueDate: '2026-08-01', status: 'Paid', paidAt: '2026-07-01T00:00:00Z' }

function renderPage() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={client}>
      <FeesPage />
    </QueryClientProvider>,
  )
}

describe('FeesPage (#157)', () => {
  beforeEach(() => {
    vi.mocked(auth.useAuth).mockReturnValue({
      token: 'tok',
      wardFullName: 'Jane Doe',
      wardStudentId: 'student-1',
      setSession: vi.fn(),
    })
  })

  it('resyncs the fee list after a 409 already_paid response, instead of leaving a stale Pending card', async () => {
    vi.mocked(api.getWardFees)
      .mockResolvedValueOnce([pendingFee])
      .mockResolvedValueOnce([paidFee])
    vi.mocked(api.payFee).mockRejectedValueOnce(new api.ApiError(409, 'This fee has already been paid.'))

    renderPage()

    await screen.findByText('Pay now')
    fireEvent.click(screen.getByText('Pay now'))

    await screen.findByText('This fee has already been paid.')
    await waitFor(() => expect(api.getWardFees).toHaveBeenCalledTimes(2))
    await waitFor(() => expect(screen.queryByText('Pay now')).not.toBeInTheDocument())
    expect(screen.getAllByText(/Paid/).length).toBeGreaterThan(0)
  })
})
