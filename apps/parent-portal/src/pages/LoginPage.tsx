import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { parentLogin, ApiError } from '@/lib/api'
import { useAuth } from '@/lib/auth'

export function LoginPage() {
  const [rollNumber, setRollNumber] = useState('')
  const [dateOfBirth, setDateOfBirth] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)
  const { setSession } = useAuth()
  const navigate = useNavigate()

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setError(null)
    setLoading(true)
    try {
      const session = await parentLogin(rollNumber, dateOfBirth)
      setSession(session)
      navigate('/records')
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
          <CardTitle>Parent Portal — Sign in</CardTitle>
        </CardHeader>
        <CardContent>
          <form onSubmit={handleSubmit} className="flex flex-col gap-3">
            <label className="text-sm text-muted-foreground" htmlFor="rollNumber">
              Ward's roll number
            </label>
            <input
              id="rollNumber"
              className="rounded-md border px-3 py-2 text-sm"
              placeholder="Roll number"
              value={rollNumber}
              onChange={(e) => setRollNumber(e.target.value)}
            />
            <label className="text-sm text-muted-foreground" htmlFor="dateOfBirth">
              Ward's date of birth
            </label>
            <input
              id="dateOfBirth"
              className="rounded-md border px-3 py-2 text-sm"
              type="date"
              value={dateOfBirth}
              onChange={(e) => setDateOfBirth(e.target.value)}
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
