import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import { vi } from 'vitest'
import App from './App'

// A stand-in for MSAL: each test chooses whether a user is signed in. The mock
// reads it through globalThis at render time, because vi.mock is hoisted above
// this module's declarations.
export const msal = {
  isAuthenticated: false,
  instance: {
    loginRedirect: vi.fn(),
    logoutRedirect: vi.fn(),
    acquireTokenSilent: vi.fn(),
    acquireTokenRedirect: vi.fn(),
  },
  accounts: [] as { homeAccountId: string }[],
}
;(globalThis as { __msal?: typeof msal }).__msal = msal

vi.mock('@azure/msal-react', () => {
  const state = () => (globalThis as unknown as { __msal: typeof msal }).__msal
  return {
    useIsAuthenticated: () => state().isAuthenticated,
    useMsal: () => ({ instance: state().instance, accounts: state().accounts }),
  }
})

export function signIn() {
  msal.isAuthenticated = true
  msal.accounts = [{ homeAccountId: 'user-1' }]
  msal.instance.acquireTokenSilent.mockResolvedValue({ accessToken: 'token-1' })
}

export function signOut() {
  msal.isAuthenticated = false
  msal.accounts = []
}

export function json(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } })
}

type Handler = (url: string, init: RequestInit) => Response | undefined

// Answers fetch calls with the first handler that returns a response, and
// records every call for assertions.
export function fakeApi(...handlers: Handler[]) {
  const calls: { method: string; path: string; init: RequestInit }[] = []
  const fetch = vi.fn((url: string, init: RequestInit = {}) => {
    const path = new URL(url, 'http://localhost').pathname + new URL(url, 'http://localhost').search
    calls.push({ method: init.method ?? 'GET', path, init })
    for (const handler of handlers) {
      const response = handler(path, init)
      if (response) return Promise.resolve(response)
    }
    return Promise.resolve(new Response(`no fake for ${init.method ?? 'GET'} ${path}`, { status: 599 }))
  })
  vi.stubGlobal('fetch', fetch)
  return { fetch, calls }
}

export const route =
  (method: string, path: string | RegExp, respond: (init: RequestInit) => Response): Handler =>
  (url, init) =>
    (init.method ?? 'GET') === method && (typeof path === 'string' ? url === path : path.test(url)) ? respond(init) : undefined

export function me(groupKeys: string[] = ['contributors'], name = 'Test Contributor'): Handler {
  return route('GET', '/api/me', () => json({ name, objectId: 'oid-1', groups: ['g1'], groupKeys }))
}

export function renderApp(path = '/') {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={client}>
      <MemoryRouter initialEntries={[path]}>
        <App />
      </MemoryRouter>
    </QueryClientProvider>,
  )
}
