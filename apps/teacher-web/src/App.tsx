import { motion } from 'framer-motion'
import { useQuery } from '@tanstack/react-query'
import {
  Bar,
  BarChart,
  CartesianGrid,
  ResponsiveContainer,
  XAxis,
  YAxis,
} from 'recharts'
import { Button } from '@/components/ui/button'
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/card'

const sampleAttendanceData = [
  { section: 'A', percent: 92 },
  { section: 'B', percent: 78 },
  { section: 'C', percent: 88 },
]

function useHealthCheck() {
  return useQuery({
    queryKey: ['health'],
    queryFn: async () => {
      const res = await fetch('/api/v1/auth/session').catch(() => null)
      return { reachable: res !== null }
    },
  })
}

function App() {
  const health = useHealthCheck()

  return (
    <motion.main
      initial={{ opacity: 0, y: 8 }}
      animate={{ opacity: 1, y: 0 }}
      className="mx-auto flex min-h-svh max-w-3xl flex-col gap-6 p-8"
    >
      <Card>
        <CardHeader>
          <CardTitle>Teacher Web App</CardTitle>
          <CardDescription>
            Track 1 scaffold — Tailwind, shadcn/ui, Framer Motion, TanStack
            Query, and Recharts wired together.
          </CardDescription>
        </CardHeader>
        <CardContent className="flex flex-col gap-4">
          <div className="h-48">
            <ResponsiveContainer width="100%" height="100%">
              <BarChart data={sampleAttendanceData}>
                <CartesianGrid strokeDasharray="3 3" />
                <XAxis dataKey="section" />
                <YAxis />
                <Bar dataKey="percent" fill="var(--color-primary)" />
              </BarChart>
            </ResponsiveContainer>
          </div>
          <Button>
            Backend reachable: {health.data?.reachable ? 'yes' : 'checking…'}
          </Button>
        </CardContent>
      </Card>
    </motion.main>
  )
}

export default App
