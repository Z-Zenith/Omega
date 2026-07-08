import { describe, it, expect, beforeEach } from 'vitest'
import { render, screen, act } from '@testing-library/react'
import { AuthProvider, useAuth } from './auth'

function Probe() {
  const { token, wardFullName, wardStudentId, setSession } = useAuth()
  return (
    <div>
      <span data-testid="token">{token ?? 'none'}</span>
      <span data-testid="ward-name">{wardFullName ?? 'none'}</span>
      <span data-testid="ward-id">{wardStudentId ?? 'none'}</span>
      <button
        onClick={() =>
          setSession({
            token: 'jwt-token',
            parentUserId: 'parent-1',
            sessionId: 'session-1',
            wardStudentId: 'stu-1',
            wardFullName: 'Jane Doe',
            wardIdentifier: 'ROLL-1',
          })
        }
      >
        login
      </button>
      <button onClick={() => setSession(null)}>logout</button>
    </div>
  )
}

describe('AuthProvider / useAuth', () => {
  beforeEach(() => {
    localStorage.clear()
  })

  it('starts with no session when storage is empty', () => {
    render(
      <AuthProvider>
        <Probe />
      </AuthProvider>,
    )
    expect(screen.getByTestId('token')).toHaveTextContent('none')
    expect(screen.getByTestId('ward-name')).toHaveTextContent('none')
  })

  it('persists the session to localStorage and updates context state on setSession', () => {
    render(
      <AuthProvider>
        <Probe />
      </AuthProvider>,
    )

    act(() => screen.getByText('login').click())

    expect(screen.getByTestId('token')).toHaveTextContent('jwt-token')
    expect(screen.getByTestId('ward-name')).toHaveTextContent('Jane Doe')
    expect(screen.getByTestId('ward-id')).toHaveTextContent('stu-1')
    expect(localStorage.getItem('campus.token')).toBe('jwt-token')
  })

  it('clears the session and localStorage on logout', () => {
    render(
      <AuthProvider>
        <Probe />
      </AuthProvider>,
    )

    act(() => screen.getByText('login').click())
    act(() => screen.getByText('logout').click())

    expect(screen.getByTestId('token')).toHaveTextContent('none')
    expect(localStorage.getItem('campus.token')).toBeNull()
  })

  it('throws when useAuth is used outside AuthProvider', () => {
    function Bare() {
      useAuth()
      return null
    }
    expect(() => render(<Bare />)).toThrow('useAuth must be used within AuthProvider')
  })
})
