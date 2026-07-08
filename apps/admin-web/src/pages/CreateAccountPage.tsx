import { useState } from 'react'
import { useMutation } from '@tanstack/react-query'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card'
import { createUser, ApiError, type AccountType, type CreateUserResponse } from '@/lib/api'

export function CreateAccountPage() {
  const [collegeId, setCollegeId] = useState('')
  const [accountType, setAccountType] = useState<AccountType>('Student')
  const [identifier, setIdentifier] = useState('')
  const [initialPassword, setInitialPassword] = useState('')
  const [fullName, setFullName] = useState('')
  const [departmentId, setDepartmentId] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [created, setCreated] = useState<CreateUserResponse | null>(null)

  const createUserMutation = useMutation({
    mutationFn: createUser,
    onSuccess: (response) => {
      setCreated(response)
      setError(null)
      setIdentifier('')
      setInitialPassword('')
      setFullName('')
      setDepartmentId('')
    },
    onError: (err) => {
      setCreated(null)
      setError(
        err instanceof ApiError
          ? `Failed to create account: ${err.message || err.status}`
          : 'Failed to create account.',
      )
    },
  })

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    setError(null)
    createUserMutation.mutate({
      collegeId,
      accountType,
      identifier,
      initialPassword,
      fullName,
      departmentId: departmentId.trim() ? departmentId.trim() : null,
    })
  }

  const canSubmit = Boolean(collegeId && identifier && initialPassword && fullName) && !createUserMutation.isPending

  return (
    <div className="mx-auto flex max-w-2xl flex-col gap-6 p-8">
      <Card>
        <CardHeader>
          <CardTitle>Create a student or teacher account</CardTitle>
          <CardDescription>
            AWA-09 — the account can sign in immediately with the password and TOTP code shown below.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <form onSubmit={handleSubmit} className="flex flex-col gap-3">
            <label className="text-sm text-muted-foreground">Account type</label>
            <select
              className="rounded-md border px-3 py-2 text-sm"
              value={accountType}
              onChange={(e) => setAccountType(e.target.value as AccountType)}
            >
              <option value="Student">Student</option>
              <option value="Teacher">Teacher</option>
            </select>

            <label className="text-sm text-muted-foreground">College ID</label>
            <input
              className="rounded-md border px-3 py-2 text-sm"
              placeholder="College ID (GUID)"
              value={collegeId}
              onChange={(e) => setCollegeId(e.target.value)}
            />

            <label className="text-sm text-muted-foreground">Full name</label>
            <input
              className="rounded-md border px-3 py-2 text-sm"
              placeholder="Full name"
              value={fullName}
              onChange={(e) => setFullName(e.target.value)}
            />

            <label className="text-sm text-muted-foreground">
              {accountType === 'Student' ? 'Roll number' : 'Username'}
            </label>
            <input
              className="rounded-md border px-3 py-2 text-sm"
              placeholder={accountType === 'Student' ? 'Roll number' : 'Username'}
              value={identifier}
              onChange={(e) => setIdentifier(e.target.value)}
            />

            <label className="text-sm text-muted-foreground">Initial password</label>
            <input
              className="rounded-md border px-3 py-2 text-sm"
              placeholder="Initial password"
              type="password"
              value={initialPassword}
              onChange={(e) => setInitialPassword(e.target.value)}
            />

            <label className="text-sm text-muted-foreground">Department ID (optional)</label>
            <input
              className="rounded-md border px-3 py-2 text-sm"
              placeholder="Department ID (GUID, optional)"
              value={departmentId}
              onChange={(e) => setDepartmentId(e.target.value)}
            />

            <Button type="submit" disabled={!canSubmit}>
              {createUserMutation.isPending ? 'Creating…' : 'Create account'}
            </Button>
            {error && <p className="text-sm text-destructive">{error}</p>}
          </form>
        </CardContent>
      </Card>

      {created && (
        <Card>
          <CardHeader>
            <CardTitle>Account created</CardTitle>
            <CardDescription>
              Share this TOTP setup with the new user — it's only shown once. They'll need it alongside
              their password to sign in (SDA-02/TWA-03 require a TOTP code on every login).
            </CardDescription>
          </CardHeader>
          <CardContent className="flex flex-col gap-2">
            <p className="text-sm">
              <span className="text-muted-foreground">User ID: </span>
              {created.userId}
            </p>
            <p className="text-sm">
              <span className="text-muted-foreground">TOTP secret: </span>
              <code className="rounded bg-muted px-1.5 py-0.5">{created.totpSecret}</code>
            </p>
            <p className="text-sm break-all">
              <span className="text-muted-foreground">Provisioning URI: </span>
              <a
                className="text-primary underline underline-offset-4"
                href={created.totpProvisioningUri}
              >
                {created.totpProvisioningUri}
              </a>
            </p>
          </CardContent>
        </Card>
      )}
    </div>
  )
}
