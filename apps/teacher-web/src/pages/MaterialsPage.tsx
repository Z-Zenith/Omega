import { useState } from 'react'
import { useMutation } from '@tanstack/react-query'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card'
import { uploadMaterial, ApiError, type MaterialDto } from '@/lib/api'

// TWA-06 — a teacher uploads material and attaches it to the material section (a
// subject), a group, or both. Posting to a group also surfaces it in that group's
// Materials section automatically (SDA-16, GET /groups/{id}/materials reads the same rows).
export function MaterialsPage() {
  const [title, setTitle] = useState('')
  const [fileUrl, setFileUrl] = useState('')
  const [subjectId, setSubjectId] = useState('')
  const [groupId, setGroupId] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [uploaded, setUploaded] = useState<MaterialDto | null>(null)

  const uploadMutation = useMutation({
    mutationFn: uploadMaterial,
    onSuccess: (material) => {
      setUploaded(material)
      setError(null)
      setTitle('')
      setFileUrl('')
      setSubjectId('')
      setGroupId('')
    },
    onError: (err) => {
      setUploaded(null)
      setError(err instanceof ApiError ? `Failed to upload material: ${err.message || err.status}` : 'Failed to upload material.')
    },
  })

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    setError(null)
    uploadMutation.mutate({
      title,
      fileUrl,
      subjectId: subjectId.trim() ? subjectId.trim() : null,
      groupId: groupId.trim() ? groupId.trim() : null,
    })
  }

  const canSubmit = Boolean(title.trim() && fileUrl.trim() && (subjectId.trim() || groupId.trim())) && !uploadMutation.isPending

  return (
    <div className="mx-auto flex max-w-2xl flex-col gap-6 p-8">
      <Card>
        <CardHeader>
          <CardTitle>Upload material</CardTitle>
          <CardDescription>TWA-06 — attach to a subject, a group, or both.</CardDescription>
        </CardHeader>
        <CardContent>
          <form onSubmit={handleSubmit} className="flex flex-col gap-3">
            <label className="text-sm text-muted-foreground">Title</label>
            <input
              className="rounded-md border px-3 py-2 text-sm"
              placeholder="Material title"
              value={title}
              onChange={(e) => setTitle(e.target.value)}
            />

            <label className="text-sm text-muted-foreground">File URL</label>
            <input
              className="rounded-md border px-3 py-2 text-sm"
              placeholder="https://…"
              value={fileUrl}
              onChange={(e) => setFileUrl(e.target.value)}
            />

            <label className="text-sm text-muted-foreground">Subject ID (optional)</label>
            <input
              className="rounded-md border px-3 py-2 text-sm"
              placeholder="Subject ID (GUID, optional)"
              value={subjectId}
              onChange={(e) => setSubjectId(e.target.value)}
            />

            <label className="text-sm text-muted-foreground">Group ID (optional)</label>
            <input
              className="rounded-md border px-3 py-2 text-sm"
              placeholder="Group ID (GUID, optional)"
              value={groupId}
              onChange={(e) => setGroupId(e.target.value)}
            />
            <p className="text-xs text-muted-foreground">At least one of subject or group is required.</p>

            <Button type="submit" disabled={!canSubmit}>
              {uploadMutation.isPending ? 'Uploading…' : 'Upload material'}
            </Button>
            {error && <p className="text-sm text-destructive">{error}</p>}
          </form>
        </CardContent>
      </Card>

      {uploaded && (
        <Card>
          <CardHeader>
            <CardTitle>Material uploaded</CardTitle>
            <CardDescription>"{uploaded.title}" is ready.</CardDescription>
          </CardHeader>
        </Card>
      )}
    </div>
  )
}
