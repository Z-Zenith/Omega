import { useQuery } from '@tanstack/react-query'
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card'
import { listAllGroups, ApiError } from '@/lib/api'

// AWA-06: institution-wide view — every group in the college, regardless of who
// created it or whether the signed-in Admin is a member.
export function GroupsPage() {
  const groupsQuery = useQuery({ queryKey: ['groups', 'all'], queryFn: listAllGroups })

  return (
    <div className="mx-auto flex max-w-5xl flex-col gap-6 p-8">
      <Card>
        <CardHeader>
          <CardTitle>Community groups</CardTitle>
          <CardDescription>
            Every community group across the institution — teacher-created, admin-created, and
            auto-provisioned class groups alike.
          </CardDescription>
        </CardHeader>
        <CardContent>
          {groupsQuery.isError && (
            <p className="text-sm text-destructive">
              {groupsQuery.error instanceof ApiError && groupsQuery.error.status === 403
                ? "You don't hold the view_all_groups permission."
                : 'Failed to load groups.'}
            </p>
          )}
          {groupsQuery.data && groupsQuery.data.groups.length === 0 && (
            <p className="text-sm text-muted-foreground">No groups exist yet.</p>
          )}
          <div className="flex flex-col gap-2">
            {groupsQuery.data?.groups.map((group) => (
              <div key={group.id} className="rounded-md border px-3 py-2 text-sm">
                {group.name} — {group.type}
                {group.sectionId ? ` (section: ${group.sectionId})` : ''}
                {' — '}
                {group.createdBy ? `created by ${group.createdBy}` : 'auto-provisioned'}
              </div>
            ))}
          </div>
        </CardContent>
      </Card>
    </div>
  )
}
