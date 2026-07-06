import { createContext, useContext, useState, type ReactNode } from 'react'
import { getToken, setToken as persistToken, type ParentLoginResponse } from './api'

interface AuthState {
  token: string | null
  wardFullName: string | null
  wardStudentId: string | null
  setSession: (session: ParentLoginResponse | null) => void
}

const AuthContext = createContext<AuthState | null>(null)

export function AuthProvider({ children }: { children: ReactNode }) {
  const [token, setTokenState] = useState<string | null>(getToken())
  const [wardFullName, setWardFullName] = useState<string | null>(null)
  const [wardStudentId, setWardStudentId] = useState<string | null>(null)

  const setSession = (session: ParentLoginResponse | null) => {
    persistToken(session?.token ?? null)
    setTokenState(session?.token ?? null)
    setWardFullName(session?.wardFullName ?? null)
    setWardStudentId(session?.wardStudentId ?? null)
  }

  return (
    <AuthContext.Provider value={{ token, wardFullName, wardStudentId, setSession }}>
      {children}
    </AuthContext.Provider>
  )
}

export function useAuth() {
  const ctx = useContext(AuthContext)
  if (!ctx) throw new Error('useAuth must be used within AuthProvider')
  return ctx
}
