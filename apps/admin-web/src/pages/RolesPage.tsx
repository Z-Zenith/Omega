import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card'
import {
  listRoleBindings,
  createRoleBinding,
  listPermissionGrants,
  createPermissionGrant,
  deletePermissionGrant,
  ApiError,
  type RoleBindingDto,
} from '@/lib/api'

const ROLE_CODES = ['lecturer', 'hod', 'finance', 'it', 'admin']

export function RolesPage() {
  const queryClient = useQueryClient()
  const [message, setMessage] = useState<string | null>(null)

  const [bindingUserId, setBindingUserId] = useState('')
  const [roleCode, setRoleCode] = useState(ROLE_CODES[0])
  const [scopeType, setScopeType] = useState<'Global' | 'Department'>('Global')
  const [departmentId, setDepartmentId] = useState('')

  const [grantUserId, setGrantUserId] = useState('')
  const [permissionCode, setPermissionCode] = useState('')
  const [expiresAt, setExpiresAt] = useState('')

  const bindingsQuery = useQuery({ queryKey: ['role-bindings'], queryFn: listRoleBindings })
  const grantsQuery = useQuery({ queryKey: ['permission-grants'], queryFn: listPermissionGrants })

  const createBindingMutation = useMutation({
    mutationFn: () =>
      createRoleBinding({
        userId: bindingUserId,
        roleCode,
        scopeType,
        departmentId: scopeType === 'Department' ? departmentId : null,
      }),
    onSuccess: () => {
      setMessage('Role binding assigned.')
      setBindingUserId('')
      setDepartmentId('')
      queryClient.invalidateQueries({ queryKey: ['role-bindings'] })
    },
    onError: (err) =>
      setMessage(
        err instanceof ApiError && err.status === 403
          ? "You don't hold the manage_roles_and_permissions permission."
          : 'Failed to assign role binding.',
      ),
  })

  const createGrantMutation = useMutation({
    mutationFn: (granted: boolean) =>
      createPermissionGrant({
        userId: grantUserId,
        permissionCode,
        granted,
        expiresAt: expiresAt ? new Date(expiresAt).toISOString() : null,
      }),
    onSuccess: () => {
      setMessage('Permission override saved.')
      setGrantUserId('')
      setPermissionCode('')
      setExpiresAt('')
      queryClient.invalidateQueries({ queryKey: ['permission-grants'] })
    },
    onError: (err) =>
      setMessage(
        err instanceof ApiError && err.status === 403
          ? "You don't hold the manage_roles_and_permissions permission."
          : 'Failed to save permission override.',
      ),
  })

  const revokeGrantMutation = useMutation({
    mutationFn: (id: string) => deletePermissionGrant(id),
    onSuccess: () => {
      setMessage('Permission override revoked — takes effect immediately, no re-login required.')
      queryClient.invalidateQueries({ queryKey: ['permission-grants'] })
    },
    onError: () => setMessage('Failed to revoke permission override.'),
  })

  const isExpired = (grant: { expiresAt: string | null }) =>
    grant.expiresAt !== null && new Date(grant.expiresAt).getTime() <= Date.now()

  return (
    <div className="mx-auto flex max-w-5xl flex-col gap-6 p-8">
      <Card>
        <CardHeader>
          <CardTitle>Assign a role binding</CardTitle>
          <CardDescription>
            Grant a user one of the platform's roles, scoped globally or to a department (AWA-13).
          </CardDescription>
        </CardHeader>
        <CardContent>
          <form
            onSubmit={(e) => {
              e.preventDefault()
              createBindingMutation.mutate()
            }}
            className="flex flex-col gap-3"
          >
            <input
              className="rounded-md border px-3 py-2 text-sm"
              placeholder="User ID"
              value={bindingUserId}
              onChange={(e) => setBindingUserId(e.target.value)}
            />
            <select
              className="rounded-md border px-3 py-2 text-sm"
              value={roleCode}
              onChange={(e) => setRoleCode(e.target.value)}
            >
              {ROLE_CODES.map((code) => (
                <option key={code} value={code}>
                  {code}
                </option>
              ))}
            </select>
            <select
              className="rounded-md border px-3 py-2 text-sm"
              value={scopeType}
              onChange={(e) => setScopeType(e.target.value as 'Global' | 'Department')}
            >
              <option value="Global">Global</option>
              <option value="Department">Department</option>
            </select>
            {scopeType === 'Department' && (
              <input
                className="rounded-md border px-3 py-2 text-sm"
                placeholder="Department ID"
                value={departmentId}
                onChange={(e) => setDepartmentId(e.target.value)}
              />
            )}
            <Button
              type="submit"
              disabled={!bindingUserId || (scopeType === 'Department' && !departmentId) || createBindingMutation.isPending}
              className="w-fit"
            >
              Assign role
            </Button>
          </form>

          <div className="mt-6 flex flex-col gap-2">
            {bindingsQuery.data?.map((binding: RoleBindingDto) => (
              <div key={binding.id} className="rounded-md border px-3 py-2 text-sm">
                {binding.userFullName} — {binding.roleCode} ({binding.scopeType}
                {binding.departmentId ? `: ${binding.departmentId}` : ''})
              </div>
            ))}
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Grant or revoke a permission override</CardTitle>
          <CardDescription>
            Overrides a user's role-derived permissions, optionally expiring automatically. A
            revoked or expired override applies immediately — no re-login required.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <form className="flex flex-col gap-3">
            <input
              className="rounded-md border px-3 py-2 text-sm"
              placeholder="User ID"
              value={grantUserId}
              onChange={(e) => setGrantUserId(e.target.value)}
            />
            <input
              className="rounded-md border px-3 py-2 text-sm"
              placeholder="Permission code (e.g. create_timetable)"
              value={permissionCode}
              onChange={(e) => setPermissionCode(e.target.value)}
            />
            <label className="text-sm text-muted-foreground">Expires at (optional)</label>
            <input
              className="rounded-md border px-3 py-2 text-sm"
              type="datetime-local"
              value={expiresAt}
              onChange={(e) => setExpiresAt(e.target.value)}
            />
            <div className="flex gap-2">
              <Button
                type="button"
                disabled={!grantUserId || !permissionCode || createGrantMutation.isPending}
                onClick={() => createGrantMutation.mutate(true)}
              >
                Grant
              </Button>
              <Button
                type="button"
                disabled={!grantUserId || !permissionCode || createGrantMutation.isPending}
                onClick={() => createGrantMutation.mutate(false)}
                className="bg-transparent text-foreground hover:bg-muted"
              >
                Explicitly revoke
              </Button>
            </div>
          </form>

          <div className="mt-6 flex flex-col gap-2">
            {grantsQuery.data?.map((grant) => (
              <div key={grant.id} className="flex items-center justify-between rounded-md border px-3 py-2 text-sm">
                <span>
                  {grant.userFullName} — {grant.permissionCode} ({grant.granted ? 'granted' : 'revoked'})
                  {grant.expiresAt && (isExpired(grant) ? ' — expired' : ` — expires ${new Date(grant.expiresAt).toLocaleString()}`)}
                </span>
                <Button
                  type="button"
                  disabled={revokeGrantMutation.isPending}
                  onClick={() => revokeGrantMutation.mutate(grant.id)}
                  className="bg-transparent text-foreground hover:bg-muted"
                >
                  Delete
                </Button>
              </div>
            ))}
          </div>

          {message && <p className="mt-4 text-sm">{message}</p>}
        </CardContent>
      </Card>
    </div>
  )
}
