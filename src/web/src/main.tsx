import { EventType, type AuthenticationResult } from '@azure/msal-browser'
import { MsalProvider } from '@azure/msal-react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { BrowserRouter } from 'react-router'
import './index.css'
import App from './App.tsx'
import { msalInstance } from './auth'

const queryClient = new QueryClient()

async function start() {
  await msalInstance.initialize()

  // Finish a sign-in redirect, if this page load is the return from Entra.
  const result = await msalInstance.handleRedirectPromise()
  const account = result?.account ?? msalInstance.getAllAccounts()[0]
  if (account) msalInstance.setActiveAccount(account)

  msalInstance.addEventCallback((event) => {
    if (event.eventType === EventType.LOGIN_SUCCESS && event.payload) {
      msalInstance.setActiveAccount((event.payload as AuthenticationResult).account)
    }
  })

  createRoot(document.getElementById('root')!).render(
    <StrictMode>
      <MsalProvider instance={msalInstance}>
        <QueryClientProvider client={queryClient}>
          <BrowserRouter>
            <App />
          </BrowserRouter>
        </QueryClientProvider>
      </MsalProvider>
    </StrictMode>,
  )
}

void start()
