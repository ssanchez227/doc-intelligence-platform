import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { fireEvent, render, screen } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import App from './App'

// A stand-in for MSAL: each test chooses whether a user is signed in.
const msal = vi.hoisted(() => ({
  isAuthenticated: false,
  instance: {
    loginRedirect: vi.fn(),
    logoutRedirect: vi.fn(),
    acquireTokenSilent: vi.fn(),
    acquireTokenRedirect: vi.fn(),
  },
  accounts: [] as { homeAccountId: string }[],
}))

vi.mock('@azure/msal-react', () => ({
  useIsAuthenticated: () => msal.isAuthenticated,
  useMsal: () => ({ instance: msal.instance, accounts: msal.accounts }),
}))

function renderApp() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={client}>
      <App />
    </QueryClientProvider>,
  )
}

function json(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), { status })
}

function signIn() {
  msal.isAuthenticated = true
  msal.accounts = [{ homeAccountId: 'user-1' }]
  msal.instance.acquireTokenSilent.mockResolvedValue({ accessToken: 'token-1' })
}

describe('App', () => {
  beforeEach(() => {
    msal.isAuthenticated = false
    msal.accounts = []
    vi.clearAllMocks()
  })
  afterEach(() => vi.unstubAllGlobals())

  it('shows the API status when the API answers', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(json({ status: 'ok' })))

    renderApp()

    expect(await screen.findByText('ok')).toBeInTheDocument()
  })

  it('says the API is unreachable when the call fails', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response('', { status: 503 })))

    renderApp()

    expect(await screen.findByText('unreachable')).toBeInTheDocument()
  })

  it('offers sign-in to an anonymous visitor', () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(json({ status: 'ok' })))

    renderApp()
    fireEvent.click(screen.getByRole('button', { name: 'Sign in' }))

    expect(msal.instance.loginRedirect).toHaveBeenCalledExactlyOnceWith(
      expect.objectContaining({ prompt: 'select_account' }),
    )
  })

  it('calls the protected endpoint with the access token and shows the user', async () => {
    signIn()
    const fetch = vi.fn((url: string) =>
      Promise.resolve(
        url.endsWith('/api/me')
          ? json({ name: 'Test Reader', objectId: 'oid-1', groups: ['g1', 'g2'] })
          : json({ status: 'ok' }),
      ),
    )
    vi.stubGlobal('fetch', fetch)

    renderApp()

    expect(await screen.findByText('Test Reader')).toBeInTheDocument()
    expect(screen.getByText('Groups in your token: 2')).toBeInTheDocument()
    expect(fetch).toHaveBeenCalledWith(expect.stringMatching(/\/api\/me$/), {
      headers: { Authorization: 'Bearer token-1' },
    })
  })

  it('says so when the API refuses the signed-in user', async () => {
    signIn()
    vi.stubGlobal(
      'fetch',
      vi.fn((url: string) =>
        Promise.resolve(url.endsWith('/api/me') ? new Response('', { status: 401 }) : json({ status: 'ok' })),
      ),
    )

    renderApp()

    expect(await screen.findByRole('alert')).toHaveTextContent('The API refused the request.')
  })
})
