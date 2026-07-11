import { useEffect, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card'
import { Button } from '@/components/ui/button'
import { cn } from '@/lib/utils'
import { useActiveSection } from '@/lib/activeSection'
import {
  ApiError,
  createGroup,
  listMyGroups,
  listGroupPosts,
  createGroupPost,
  type GroupType,
  type GroupDto,
} from '@/lib/api'

// TWA-05 — Teacher Web App lets a teacher create community groups and view/post in the
// ones they belong to. Class groups are auto-provisioned (API-02) and excluded from the
// type picker, matching CommunityController's own rejection of that type here.
export function CommunityPage() {
  const queryClient = useQueryClient()
  const { sectionId: activeSectionId } = useActiveSection()
  const [selectedGroupId, setSelectedGroupId] = useState<string | null>(null)
  const [postContent, setPostContent] = useState('')
  const [name, setName] = useState('')
  const [type, setType] = useState<GroupType>('Club')
  const [sectionId, setSectionId] = useState('')
  const [formError, setFormError] = useState<string | null>(null)

  const groupsQuery = useQuery({ queryKey: ['groups', 'mine'], queryFn: listMyGroups })
  const groups: GroupDto[] = groupsQuery.data?.groups ?? []

  useEffect(() => {
    if (type === 'SubjectSection' && activeSectionId && !sectionId) {
      setSectionId(activeSectionId)
    }
  }, [type, activeSectionId, sectionId])

  const postsQuery = useQuery({
    queryKey: ['groups', selectedGroupId, 'posts'],
    queryFn: () => listGroupPosts(selectedGroupId!),
    enabled: selectedGroupId !== null,
  })

  const createGroupMutation = useMutation({
    mutationFn: createGroup,
    onSuccess: (group) => {
      setFormError(null)
      setName('')
      setSectionId('')
      queryClient.invalidateQueries({ queryKey: ['groups', 'mine'] })
      setSelectedGroupId(group.id)
    },
    onError: (err) => {
      setFormError(err instanceof ApiError ? `Failed to create group: ${err.message || err.status}` : 'Failed to create group.')
    },
  })

  const createPostMutation = useMutation({
    mutationFn: () => createGroupPost(selectedGroupId!, postContent),
    onSuccess: () => {
      setPostContent('')
      queryClient.invalidateQueries({ queryKey: ['groups', selectedGroupId, 'posts'] })
    },
  })

  const handleCreateGroup = (e: React.FormEvent) => {
    e.preventDefault()
    setFormError(null)
    createGroupMutation.mutate({ name, type, sectionId: sectionId.trim() ? sectionId.trim() : null })
  }

  const handlePost = (e: React.FormEvent) => {
    e.preventDefault()
    if (postContent.trim()) createPostMutation.mutate()
  }

  return (
    <div className="mx-auto flex max-w-5xl flex-col gap-6 p-8">
      <Card>
        <CardHeader>
          <CardTitle>Community</CardTitle>
          <CardDescription>Groups you belong to (TWA-05) — class groups, clubs, and subject sections.</CardDescription>
        </CardHeader>
        <CardContent>
          <div className="grid grid-cols-1 gap-6 md:grid-cols-[260px_1fr]">
            <div className="flex flex-col gap-4 border-r pr-4">
              <ul className="space-y-1">
                {groups.map((group) => (
                  <li key={group.id}>
                    <button
                      type="button"
                      onClick={() => setSelectedGroupId(group.id)}
                      className={cn(
                        'w-full truncate rounded px-2 py-1 text-left text-sm hover:bg-accent',
                        selectedGroupId === group.id && 'bg-accent font-medium'
                      )}
                    >
                      {group.name} <span className="text-xs text-muted-foreground">({group.type})</span>
                    </button>
                  </li>
                ))}
                {groups.length === 0 && <li className="text-sm text-muted-foreground">No groups yet.</li>}
              </ul>

              <form onSubmit={handleCreateGroup} className="flex flex-col gap-2 border-t pt-4">
                <label className="text-sm text-muted-foreground">New group</label>
                <select
                  className="rounded-md border px-3 py-2 text-sm"
                  value={type}
                  onChange={(e) => setType(e.target.value as GroupType)}
                >
                  <option value="Club">Club</option>
                  <option value="SubjectSection">Subject section</option>
                  <option value="TeacherOnly">Teacher only</option>
                </select>
                <input
                  className="rounded-md border px-3 py-2 text-sm"
                  placeholder="Group name"
                  value={name}
                  onChange={(e) => setName(e.target.value)}
                />
                <input
                  className="rounded-md border px-3 py-2 text-sm"
                  placeholder="Section ID (optional)"
                  value={sectionId}
                  onChange={(e) => setSectionId(e.target.value)}
                />
                <Button type="submit" disabled={!name.trim() || createGroupMutation.isPending}>
                  {createGroupMutation.isPending ? 'Creating…' : 'Create group'}
                </Button>
                {formError && <p className="text-sm text-destructive">{formError}</p>}
              </form>
            </div>

            <div>
              {!selectedGroupId ? (
                <p className="text-sm text-muted-foreground">Select a group to view its posts.</p>
              ) : (
                <div className="flex flex-col gap-4">
                  <form onSubmit={handlePost} className="flex flex-col gap-2">
                    <textarea
                      className="min-h-24 rounded-md border px-3 py-2 text-sm"
                      placeholder="Post something to this group…"
                      value={postContent}
                      onChange={(e) => setPostContent(e.target.value)}
                    />
                    <Button type="submit" disabled={!postContent.trim() || createPostMutation.isPending} className="self-start">
                      {createPostMutation.isPending ? 'Posting…' : 'Post'}
                    </Button>
                  </form>

                  <ul className="flex flex-col gap-3">
                    {(postsQuery.data ?? []).map((post) => (
                      <li key={post.id} className="rounded-md border p-3">
                        <p className="text-sm whitespace-pre-wrap">{post.content}</p>
                        <p className="mt-1 text-xs text-muted-foreground">{new Date(post.createdAt).toLocaleString()}</p>
                      </li>
                    ))}
                    {postsQuery.data?.length === 0 && (
                      <li className="text-sm text-muted-foreground">No posts yet.</li>
                    )}
                  </ul>
                </div>
              )}
            </div>
          </div>
        </CardContent>
      </Card>
    </div>
  )
}
