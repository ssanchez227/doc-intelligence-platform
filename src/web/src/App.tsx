import { useIsAuthenticated, useMsal } from '@azure/msal-react'
import { useQuery } from '@tanstack/react-query'
import { NavLink, Route, Routes } from 'react-router'
import { getHealth, type Me } from './api'
import { apiScopes } from './auth'
import DocumentPage from './pages/DocumentPage'
import LibraryPage from './pages/LibraryPage'
import UploadPage from './pages/UploadPage'
import { json, useApi } from './useApi'

const productName = 'Document Intelligence Platform'

export default function App() {
  const isAuthenticated = useIsAuthenticated()
  return isAuthenticated ? <SignedIn /> : <SignedOut />
}

function SignedOut() {
  const { instance } = useMsal()
  const health = useQuery({ queryKey: ['health'], queryFn: getHealth, retry: false })

  return (
    <main className="welcome">
      <h1>{productName}</h1>
      <p>
        API status:{' '}
        <strong>{health.isPending ? 'checking' : health.isError ? 'unreachable' : health.data.status}</strong>
      </p>
      {/* select_account: always offer the account picker, so a different test
          user can sign in even while Entra still has a session for another one. */}
      <button
        type="button"
        className="btn primary"
        onClick={() => instance.loginRedirect({ scopes: apiScopes, prompt: 'select_account' })}
      >
        Sign in
      </button>
    </main>
  )
}

// Wireframe layout: top bar, side navigation, main area.
function SignedIn() {
  const { instance, accounts } = useMsal()
  const api = useApi()
  const me = useQuery({
    queryKey: ['me', accounts[0]?.homeAccountId],
    queryFn: () => json<Me>(api, '/api/me'),
    retry: false,
  })

  if (me.isPending) return <p className="welcome">Checking your access…</p>
  if (me.isError) return <p role="alert" className="welcome">The API refused the request.</p>

  const isContributor = me.data.groupKeys.includes('contributors')

  return (
    <div className="app">
      <header className="top">
        <span className="logo">{productName}</span>
        <span className="spacer" />
        <span className="muted">
          Signed in as <strong>{me.data.name}</strong>
        </span>
        <button type="button" className="btn" onClick={() => instance.logoutRedirect()}>
          Sign out
        </button>
      </header>
      <div className="row">
        <nav className="nav" aria-label="Main">
          <NavLink to="/" end>
            Library
          </NavLink>
          {isContributor && <NavLink to="/upload">Upload</NavLink>}
        </nav>
        <main className="main">
          <Routes>
            <Route path="/" element={<LibraryPage />} />
            <Route path="/upload" element={<UploadPage />} />
            <Route path="/documents/:id" element={<DocumentPage />} />
            <Route path="*" element={<p>Page not found.</p>} />
          </Routes>
        </main>
      </div>
    </div>
  )
}
