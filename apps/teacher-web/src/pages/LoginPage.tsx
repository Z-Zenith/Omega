import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { login, ApiError } from '@/lib/api'
import { useAuth } from '@/lib/auth'

export function LoginPage() {
  const [identifier, setIdentifier] = useState('')
  const [password, setPassword] = useState('')
  const [totpCode, setTotpCode] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)
  const { setSession } = useAuth()
  const navigate = useNavigate()

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setError(null)
    setLoading(true)
    try {
      const session = await login(identifier, password, totpCode)
      setSession(session)
      navigate('/timetable')
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Login failed')
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="mx-auto flex min-h-svh max-w-sm flex-col justify-center p-8">
      <Card>
        <CardHeader>
          <CardTitle>Teacher Web App — Sign in</CardTitle>
        </CardHeader>
        <CardContent>
          <form onSubmit={handleSubmit} className="flex flex-col gap-3">
            <input
              className="rounded-md border px-3 py-2 text-sm"
              placeholder="Roll number / username"
              value={identifier}
              onChange={(e) => setIdentifier(e.target.value)}
            />
            <input
              className="rounded-md border px-3 py-2 text-sm"
              placeholder="Password"
              type="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
            />
            <input
              className="rounded-md border px-3 py-2 text-sm"
              placeholder="TOTP code"
              value={totpCode}
              onChange={(e) => setTotpCode(e.target.value)}
            />
            {error && <p className="text-sm text-destructive">{error}</p>}
            <Button type="submit" disabled={loading}>
              {loading ? 'Signing in…' : 'Sign in'}
            </Button>
          </form>
        </CardContent>
      </Card>
    </div>
  )
}
