import { useState } from 'react'
import { useMutation } from '@tanstack/react-query'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card'
import { getUserProfile, resetUserPassword, ApiError, type UserProfileDto } from '@/lib/api'
import { generateTempPassword } from '@/lib/generatePassword'

export function PasswordResetPage() {
  const [userId, setUserId] = useState('')
  const [user, setUser] = useState<UserProfileDto | null>(null)
  const [lookupError, setLookupError] = useState<string | null>(null)
  const [tempPassword, setTempPassword] = useState<string | null>(null)
  const [resetError, setResetError] = useState<string | null>(null)

  const lookupMutation = useMutation({
    mutationFn: getUserProfile,
    onSuccess: (profile) => {
      setUser(profile)
      setLookupError(null)
      setTempPassword(null)
      setResetError(null)
    },
    onError: (err) => {
      setUser(null)
      setLookupError(
        err instanceof ApiError && err.status === 404
          ? 'No user found with that ID.'
          : 'Failed to look up user.',
      )
    },
  })

  const resetMutation = useMutation({
    mutationFn: (password: string) => resetUserPassword(user!.id, password),
    onSuccess: (_data, password) => {
      setTempPassword(password)
      setResetError(null)
    },
    onError: (err) => {
      setTempPassword(null)
      setResetError(
        err instanceof ApiError && err.status === 403
          ? "You don't hold the reset_password permission."
          : 'Failed to reset password.',
      )
    },
  })

  const handleLookup = (e: React.FormEvent) => {
    e.preventDefault()
    if (!userId.trim()) return
    lookupMutation.mutate(userId.trim())
  }

  const handleReset = () => {
    if (!user) return
    resetMutation.mutate(generateTempPassword())
  }

  return (
    <div className="mx-auto flex max-w-2xl flex-col gap-6 p-8">
      <Card>
        <CardHeader>
          <CardTitle>Reset a user's password</CardTitle>
          <CardDescription>
            Look up a user by ID, then generate a new temporary password (AWA-10). Resetting
            immediately invalidates the old password.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <form onSubmit={handleLookup} className="flex gap-2">
            <input
              className="flex-1 rounded-md border px-3 py-2 text-sm"
              placeholder="User ID"
              value={userId}
              onChange={(e) => setUserId(e.target.value)}
            />
            <Button type="submit" disabled={!userId.trim() || lookupMutation.isPending}>
              Look up
            </Button>
          </form>
          {lookupError && <p className="mt-2 text-sm text-destructive">{lookupError}</p>}

          {user && (
            <div className="mt-6 flex flex-col gap-3 rounded-md border p-4">
              <div className="text-sm">
                <p className="font-medium">{user.fullName}</p>
                <p className="text-muted-foreground">
                  {user.identifier} &middot; {user.accountType}
                  {!user.isActive && ' · inactive'}
                </p>
              </div>
              <Button onClick={handleReset} disabled={resetMutation.isPending} variant="destructive">
                Reset password
              </Button>
              {resetError && <p className="text-sm text-destructive">{resetError}</p>}
              {tempPassword && (
                <div className="rounded-md border bg-muted p-3 text-sm">
                  <p className="mb-1 font-medium">
                    Password reset. The old password no longer works.
                  </p>
                  <p>
                    New temporary password:{' '}
                    <code className="rounded bg-background px-1.5 py-0.5 font-mono">
                      {tempPassword}
                    </code>
                  </p>
                  <p className="mt-1 text-muted-foreground">
                    Share this with the user through a secure, out-of-band channel. It will not be
                    shown again.
                  </p>
                </div>
              )}
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  )
}
