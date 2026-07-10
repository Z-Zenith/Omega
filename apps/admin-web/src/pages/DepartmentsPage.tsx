import { useState } from 'react'
import { useMutation } from '@tanstack/react-query'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card'
import { createDepartment, assignHod, ApiError } from '@/lib/api'

export function DepartmentsPage() {
  const [collegeId, setCollegeId] = useState('')
  const [name, setName] = useState('')
  const [createMessage, setCreateMessage] = useState<string | null>(null)

  const [departmentId, setDepartmentId] = useState('')
  const [hodUserId, setHodUserId] = useState('')
  const [hodMessage, setHodMessage] = useState<string | null>(null)

  const createMutation = useMutation({
    mutationFn: createDepartment,
    onSuccess: (department) => {
      setCreateMessage(`Created department "${department.name}" (${department.id}).`)
      setDepartmentId(department.id)
      setCollegeId('')
      setName('')
    },
    onError: (err) => setCreateMessage(err instanceof ApiError ? err.message : 'Failed to create department'),
  })

  const hodMutation = useMutation({
    mutationFn: ({ departmentId, userId }: { departmentId: string; userId: string }) => assignHod(departmentId, userId),
    onSuccess: (department) => {
      setHodMessage(`HoD assigned for department ${department.id}.`)
    },
    onError: (err) => setHodMessage(err instanceof ApiError ? err.message : 'Failed to assign HoD'),
  })

  return (
    <div className="mx-auto flex max-w-3xl flex-col gap-6 p-8">
      <Card>
        <CardHeader>
          <CardTitle>Create department</CardTitle>
          <CardDescription>AWA-14</CardDescription>
        </CardHeader>
        <CardContent className="flex flex-col gap-3">
          <input
            className="rounded-md border px-3 py-2 text-sm"
            placeholder="College ID"
            value={collegeId}
            onChange={(e) => setCollegeId(e.target.value)}
          />
          <input
            className="rounded-md border px-3 py-2 text-sm"
            placeholder="Department name"
            value={name}
            onChange={(e) => setName(e.target.value)}
          />
          <Button
            onClick={() => createMutation.mutate({ collegeId, name })}
            disabled={!collegeId || !name || createMutation.isPending}
          >
            Create
          </Button>
          {createMessage && <p className="text-sm">{createMessage}</p>}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Assign Head of Department</CardTitle>
          <CardDescription>
            A department has at most one active HoD at a time — assigning a new one replaces the previous binding.
          </CardDescription>
        </CardHeader>
        <CardContent className="flex flex-col gap-3">
          <input
            className="rounded-md border px-3 py-2 text-sm"
            placeholder="Department ID"
            value={departmentId}
            onChange={(e) => setDepartmentId(e.target.value)}
          />
          <input
            className="rounded-md border px-3 py-2 text-sm"
            placeholder="User ID (must belong to the department's college)"
            value={hodUserId}
            onChange={(e) => setHodUserId(e.target.value)}
          />
          <Button
            onClick={() => hodMutation.mutate({ departmentId, userId: hodUserId })}
            disabled={!departmentId || !hodUserId || hodMutation.isPending}
          >
            Assign HoD
          </Button>
          {hodMessage && <p className="text-sm">{hodMessage}</p>}
        </CardContent>
      </Card>
    </div>
  )
}
