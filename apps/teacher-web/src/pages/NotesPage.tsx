import { useEffect, useMemo, useState } from 'react'
import { NotesEditor, type Note, type UserContext } from '@campus/shared-editor-kit'
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card'
import { Button } from '@/components/ui/button'
import { cn } from '@/lib/utils'
import { useAuth } from '@/lib/auth'
import { getToken, notesListMine, notesGet, notesSave, notesDelete, notesBacklinks, type NoteSummaryDto } from '@/lib/api'

// TWA-14 — Teacher Web App embeds the Shared Editor Kit rather than implementing its own
// note editor. Unlike the Student Desktop App (SDA-19), TWA is already React, so this is
// a direct component embedding — no WebView/bridge layer needed. All editing/annotation
// logic lives in @campus/shared-editor-kit; this page only supplies the embedder
// callbacks (auth + the /notes/* fetch client), per SEK's "owns no persistence" contract.
export function NotesPage() {
  const { userId, accountType } = useAuth()
  const [notes, setNotes] = useState<ReadonlyArray<NoteSummaryDto>>([])
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [currentNote, setCurrentNote] = useState<Note | null>(null)
  const [error, setError] = useState<string | null>(null)

  const user: UserContext | null = useMemo(() => {
    if (!userId) return null
    return {
      userId,
      sessionToken: getToken() ?? '',
      // SDA's UserContext.role has the same gap for the same reason — see SDA-19.
      role: accountType?.toLowerCase() === 'admin' ? 'admin' : 'teacher',
      // TWA has no real college-tenant context client-side yet (session/auth,
      // Track 1 territory) and NotesEditor doesn't read this field, so an honest
      // empty string beats fabricating a value nothing actually validates.
      collegeId: '',
    }
  }, [userId, accountType])

  const refreshNotes = async () => {
    const result = await notesListMine()
    if (result.ok) setNotes(result.value)
  }

  useEffect(() => {
    void refreshNotes()
  }, [])

  const selectNote = async (id: string | null) => {
    setSelectedId(id)
    setError(null)
    if (id === null || !user) {
      setCurrentNote(null)
      return
    }
    const result = await notesGet(id, user.userId)
    if (result.ok) {
      setCurrentNote(result.value)
    } else {
      setError(result.error.message)
    }
  }

  if (!user) return null

  return (
    <div className="mx-auto flex max-w-5xl flex-col gap-6 p-8">
      <Card>
        <CardHeader>
          <CardTitle>Notes</CardTitle>
          <CardDescription>Powered by the Shared Editor Kit's note editor (SEK-03, TWA-14).</CardDescription>
        </CardHeader>
        <CardContent>
          {error && <p className="mb-4 text-sm text-destructive">{error}</p>}
          <div className="grid grid-cols-1 gap-6 md:grid-cols-[240px_1fr]">
            <div className="border-r pr-4">
              <Button className="mb-4 w-full" onClick={() => selectNote(null)}>
                New note
              </Button>
              <ul className="space-y-1">
                {notes.map((note) => (
                  <li key={note.id}>
                    <button
                      type="button"
                      onClick={() => selectNote(note.id)}
                      className={cn(
                        'w-full truncate rounded px-2 py-1 text-left text-sm hover:bg-accent',
                        selectedId === note.id && 'bg-accent font-medium'
                      )}
                    >
                      {note.title}
                    </button>
                  </li>
                ))}
                {notes.length === 0 && <li className="text-sm text-muted-foreground">No notes yet.</li>}
              </ul>
            </div>
            <NotesEditor
              user={user}
              currentNote={currentNote}
              canEdit
              onSave={async (note) => {
                const result = await notesSave(note)
                if (result.ok) await refreshNotes()
                return result
              }}
              onDelete={async (noteId) => {
                const result = await notesDelete(noteId)
                if (result.ok) {
                  await refreshNotes()
                  setCurrentNote(null)
                  setSelectedId(null)
                }
                return result
              }}
              onResolveLink={(toNoteId) => notesGet(toNoteId, user.userId)}
              onListBacklinks={(toNoteId) => notesBacklinks(toNoteId, user.userId)}
            />
          </div>
        </CardContent>
      </Card>
    </div>
  )
}
