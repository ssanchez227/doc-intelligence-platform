import { InteractionRequiredAuthError } from '@azure/msal-browser'
import { useMsal } from '@azure/msal-react'
import { useCallback } from 'react'
import { callApi } from './api'
import { apiScopes } from './auth'

// Returns a function that calls the API as the signed-in user. When a fresh
// sign-in or consent is needed, MSAL redirects and brings the user back.
export function useApi() {
  const { instance, accounts } = useMsal()
  const account = accounts[0]

  return useCallback(
    async (path: string, init?: RequestInit) => {
      let token: string
      try {
        token = (await instance.acquireTokenSilent({ scopes: apiScopes, account })).accessToken
      } catch (error) {
        if (error instanceof InteractionRequiredAuthError) {
          await instance.acquireTokenRedirect({ scopes: apiScopes, account })
        }
        throw error
      }
      return callApi(token, path, init)
    },
    [instance, account],
  )
}

export type Api = ReturnType<typeof useApi>

export async function json<T>(api: Api, path: string, init?: RequestInit): Promise<T> {
  return (await (await api(path, init)).json()) as T
}
