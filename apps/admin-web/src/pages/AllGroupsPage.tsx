import { useQuery } from '@tanstack/react-query'
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card'
import { listAllGroups } from '@/lib/api'

// AWA-06 — Admin can view all community groups institution-wide, regardless of who
// created them. Backend (CommunityController.AllGroups) returns every group, not
// scoped to the caller's own college the way most other reads are.
export function AllGroupsPage() {
  const groupsQuery = useQuery({ queryKey: ['groups', 'all'], queryFn: listAllGroups })
  const groups = groupsQuery.data?.groups ?? []

  return (
    <div className="mx-auto flex max-w-2xl flex-col gap-6 p-8">
      <Card>
        <CardHeader>
          <CardTitle>All community groups</CardTitle>
          <CardDescription>AWA-06 — every group institution-wide, regardless of who created it.</CardDescription>
        </CardHeader>
        <CardContent>
          {groupsQuery.isError && <p className="text-sm text-destructive">Failed to load groups.</p>}
          <ul className="flex flex-col gap-2">
            {groups.map((group) => (
              <li key={group.id} className="rounded-md border px-3 py-2 text-sm">
                <span className="font-medium">{group.name}</span>{' '}
                <span className="text-xs text-muted-foreground">({group.type})</span>
              </li>
            ))}
            {groups.length === 0 && !groupsQuery.isLoading && (
              <li className="text-sm text-muted-foreground">No groups yet.</li>
            )}
          </ul>
        </CardContent>
      </Card>
    </div>
  )
}
