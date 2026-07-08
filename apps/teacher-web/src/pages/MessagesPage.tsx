import { useMemo, useState } from 'react'
import { MessageInbox, MessageThreadView, type ThreadSummary, type UserContext } from '@campus/direct-messaging'
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card'
import { useAuth } from '@/lib/auth'
import { getToken } from '@/lib/api'
import { dmsListThreads, dmsListMessages, dmsSendMessage } from '@/lib/api'

// TWA-18 — student-message inbox for teachers. All inbox/thread rendering and
// send/receive logic lives in the shared @campus/direct-messaging package
// (DMS-01); this page only supplies the embedder callbacks (auth + fetch)
// the package asks for, per its "DMS owns no persistence, no auth" contract.
export function MessagesPage() {
  const { userId, accountType } = useAuth()
  const [selectedThreadId, setSelectedThreadId] = useState<string | null>(null)
  const [threads, setThreads] = useState<ReadonlyArray<ThreadSummary>>([])

  const user: UserContext | null = useMemo(() => {
    if (!userId) return null
    return {
      userId,
      sessionToken: getToken() ?? '',
      role: accountType?.toLowerCase() === 'student' ? 'student' : 'teacher',
    }
  }, [userId, accountType])

  const handleListThreads = async () => {
    const result = await dmsListThreads()
    if (result.ok) setThreads(result.value)
    return result
  }

  const selectedThread = threads.find((t) => t.id === selectedThreadId) ?? null

  if (!user) return null

  return (
    <div className="mx-auto flex max-w-5xl flex-col gap-6 p-8">
      <Card>
        <CardHeader>
          <CardTitle>Messages</CardTitle>
          <CardDescription>Direct messages with your students (DMS-01).</CardDescription>
        </CardHeader>
        <CardContent>
          <div className="grid grid-cols-1 gap-6 md:grid-cols-[280px_1fr]">
            <div className="border-r pr-4">
              <MessageInbox
                user={user}
                selectedThreadId={selectedThreadId}
                onSelectThread={setSelectedThreadId}
                onListThreads={handleListThreads}
              />
            </div>
            <div>
              {selectedThread ? (
                <MessageThreadView
                  user={user}
                  thread={selectedThread}
                  onListMessages={dmsListMessages}
                  onSendMessage={dmsSendMessage}
                />
              ) : (
                <p className="text-sm text-muted-foreground">Select a conversation to view messages.</p>
              )}
            </div>
          </div>
        </CardContent>
      </Card>
    </div>
  )
}
