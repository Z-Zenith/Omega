import { useState } from 'react'
import { useMutation } from '@tanstack/react-query'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card'
import { createGroup, ApiError, type GroupType, type GroupDto } from '@/lib/api'

// AWA-12 — Admin can create a community group directly, in addition to viewing all
// existing groups (AWA-06). Posts to the same endpoint TWA-05 uses for teacher-created
// groups, so the result is indistinguishable in structure from a teacher's group.
export function CreateGroupPage() {
  const [name, setName] = useState('')
  const [type, setType] = useState<GroupType>('Club')
  const [sectionId, setSectionId] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [created, setCreated] = useState<GroupDto | null>(null)

  const createGroupMutation = useMutation({
    mutationFn: createGroup,
    onSuccess: (group) => {
      setCreated(group)
      setError(null)
      setName('')
      setSectionId('')
    },
    onError: (err) => {
      setCreated(null)
      setError(
        err instanceof ApiError
          ? `Failed to create group: ${err.message || err.status}`
          : 'Failed to create group.',
      )
    },
  })

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    setError(null)
    createGroupMutation.mutate({
      name,
      type,
      sectionId: sectionId.trim() ? sectionId.trim() : null,
    })
  }

  const canSubmit = Boolean(name.trim()) && !createGroupMutation.isPending

  return (
    <div className="mx-auto flex max-w-2xl flex-col gap-6 p-8">
      <Card>
        <CardHeader>
          <CardTitle>Create a community group</CardTitle>
          <CardDescription>
            AWA-12 — class groups are auto-provisioned (API-02) and can't be created here.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <form onSubmit={handleSubmit} className="flex flex-col gap-3">
            <label className="text-sm text-muted-foreground">Group type</label>
            <select
              className="rounded-md border px-3 py-2 text-sm"
              value={type}
              onChange={(e) => setType(e.target.value as GroupType)}
            >
              <option value="Club">Club</option>
              <option value="SubjectSection">Subject section</option>
              <option value="TeacherOnly">Teacher only</option>
            </select>

            <label className="text-sm text-muted-foreground">Group name</label>
            <input
              className="rounded-md border px-3 py-2 text-sm"
              placeholder="Group name"
              value={name}
              onChange={(e) => setName(e.target.value)}
            />

            <label className="text-sm text-muted-foreground">Section ID (optional)</label>
            <input
              className="rounded-md border px-3 py-2 text-sm"
              placeholder="Section ID (GUID, optional)"
              value={sectionId}
              onChange={(e) => setSectionId(e.target.value)}
            />

            <Button type="submit" disabled={!canSubmit}>
              {createGroupMutation.isPending ? 'Creating…' : 'Create group'}
            </Button>
            {error && <p className="text-sm text-destructive">{error}</p>}
          </form>
        </CardContent>
      </Card>

      {created && (
        <Card>
          <CardHeader>
            <CardTitle>Group created</CardTitle>
            <CardDescription>"{created.name}" ({created.type}) is ready.</CardDescription>
          </CardHeader>
        </Card>
      )}
    </div>
  )
}
