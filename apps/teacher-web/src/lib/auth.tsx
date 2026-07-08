import { createContext, useContext, useState, type ReactNode } from 'react'
import { getToken, setToken as persistToken, type LoginResponse } from './api'

interface AuthState {
  token: string | null
  userId: string | null
  fullName: string | null
  accountType: string | null
  setSession: (session: LoginResponse | null) => void
}

const AuthContext = createContext<AuthState | null>(null)

export function AuthProvider({ children }: { children: ReactNode }) {
  const [token, setTokenState] = useState<string | null>(getToken())
  const [userId, setUserId] = useState<string | null>(null)
  const [fullName, setFullName] = useState<string | null>(null)
  const [accountType, setAccountType] = useState<string | null>(null)

  const setSession = (session: LoginResponse | null) => {
    persistToken(session?.token ?? null)
    setTokenState(session?.token ?? null)
    setUserId(session?.userId ?? null)
    setFullName(session?.fullName ?? null)
    setAccountType(session?.accountType ?? null)
  }

  return (
    <AuthContext.Provider value={{ token, userId, fullName, accountType, setSession }}>
      {children}
    </AuthContext.Provider>
  )
}

export function useAuth() {
  const ctx = useContext(AuthContext)
  if (!ctx) throw new Error('useAuth must be used within AuthProvider')
  return ctx
}
