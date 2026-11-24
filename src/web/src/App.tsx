import { useQuery } from '@tanstack/react-query'
import { getHealth } from './api'

// M0 placeholder: proves the web app is deployed and can reach the API.
// Sign-in (Entra, PKCE) is added in M0 step 6.
export default function App() {
  const health = useQuery({ queryKey: ['health'], queryFn: getHealth, retry: false })

  return (
    <main>
      <h1>Document Intelligence Platform</h1>
      <p>
        API status:{' '}
        <strong>
          {health.isPending ? 'checking' : health.isError ? 'unreachable' : health.data.status}
        </strong>
      </p>
    </main>
  )
}
