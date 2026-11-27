import { type Configuration, PublicClientApplication } from '@azure/msal-browser'

// Entra settings, baked in at build time. None of them are secrets: a single-page
// app's client ID is visible to anyone who loads it.
const tenantId = import.meta.env.VITE_ENTRA_TENANT_ID ?? ''
const clientId = import.meta.env.VITE_ENTRA_CLIENT_ID ?? ''

// The API scope the access token is requested for.
export const apiScopes = [import.meta.env.VITE_API_SCOPE ?? '']

const config: Configuration = {
  auth: {
    clientId,
    authority: `https://login.microsoftonline.com/${tenantId}`,
    // Registered in infra/identity for localhost and each environment's web URL.
    redirectUri: `${window.location.origin}/`,
    postLogoutRedirectUri: `${window.location.origin}/`,
  },
  cache: { cacheLocation: 'sessionStorage' },
}

// Authorization code flow with PKCE; MSAL creates the verifier and challenge.
export const msalInstance = new PublicClientApplication(config)
