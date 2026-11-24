import { InteractionRequiredAuthError } from '@azure/msal-browser'
import { useIsAuthenticated, useMsal } from '@azure/msal-react'
import { useQuery } from '@tanstack/react-query'
import { getHealth, getMe } from './api'
import { apiScopes } from './auth'

// M0 placeholder: proves the web app is deployed, can reach the API, and that a
// signed-in user reaches a protected endpoint.
export default function App() {
  const isAuthenticated = useIsAuthenticated()
  const { instance } = useMsal()
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
      {isAuthenticated ? (
        <>
          <SignedInUser />
          <button type="button" onClick={() => instance.logoutRedirect()}>
            Sign out
          </button>
        </>
      ) : (
        <button type="button" onClick={() => instance.loginRedirect({ scopes: apiScopes })}>
          Sign in
        </button>
      )}
    </main>
  )
}

function SignedInUser() {
  const { instance, accounts } = useMsal()
  const account = accounts[0]

  const me = useQuery({
    queryKey: ['me', account?.homeAccountId],
    retry: false,
    queryFn: async () => {
      try {
        const result = await instance.acquireTokenSilent({ scopes: apiScopes, account })
        return await getMe(result.accessToken)
      } catch (error) {
        // Consent or a fresh sign-in is needed; MSAL brings the user back here.
        if (error instanceof InteractionRequiredAuthError) {
          await instance.acquireTokenRedirect({ scopes: apiScopes, account })
        }
        throw error
      }
    },
  })

  if (me.isPending) return <p>Checking your access…</p>
  if (me.isError) return <p role="alert">The API refused the request.</p>

  return (
    <section aria-label="Signed-in user">
      <p>
        Signed in as <strong>{me.data.name}</strong>
      </p>
      <p>Groups in your token: {me.data.groups.length}</p>
    </section>
  )
}
