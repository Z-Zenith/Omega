import { motion } from 'framer-motion'
import { useQuery } from '@tanstack/react-query'
import { Button } from '@/components/ui/button'
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/card'

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
          <CardTitle>Admin Web App</CardTitle>
          <CardDescription>
            Track 1 scaffold — same stack as Teacher Web App (Tailwind,
            shadcn/ui, Framer Motion, TanStack Query).
          </CardDescription>
        </CardHeader>
        <CardContent>
          <Button>
            Backend reachable: {health.data?.reachable ? 'yes' : 'checking…'}
          </Button>
        </CardContent>
      </Card>
    </motion.main>
  )
}

export default App
