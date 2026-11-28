// Base URL of the API, baked in at build time (VITE_API_BASE_URL). Empty means same origin.
export const apiBaseUrl = (import.meta.env.VITE_API_BASE_URL ?? '').replace(/\/$/, '')

export type Health = { status: string }

export type Me = { name: string | null; objectId: string | null; groups: string[]; groupKeys: string[] }

export async function getHealth(): Promise<Health> {
  const response = await fetch(`${apiBaseUrl}/health`)
  if (!response.ok) {
    throw new Error(`API health check failed with ${response.status}`)
  }
  return (await response.json()) as Health
}

// A refusal from the API, carrying its status and the message it sent.
export class ApiError extends Error {
  readonly status: number
  readonly body: unknown

  constructor(status: number, body: unknown) {
    const message =
      typeof body === 'object' && body !== null && 'error' in body && typeof body.error === 'string'
        ? body.error
        : `The API answered ${status}.`
    super(message)
    this.status = status
    this.body = body
  }
}

// Calls the API with a bearer token. Returns the response for the caller to read;
// throws ApiError when the status is not a success.
export async function callApi(accessToken: string, path: string, init: RequestInit = {}): Promise<Response> {
  const response = await fetch(`${apiBaseUrl}${path}`, {
    ...init,
    headers: { ...(init.headers as Record<string, string> | undefined), Authorization: `Bearer ${accessToken}` },
  })
  if (!response.ok) {
    let body: unknown = null
    try {
      body = await response.json()
    } catch {
      // No JSON body; the status is enough.
    }
    throw new ApiError(response.status, body)
  }
  return response
}

export async function getMe(accessToken: string): Promise<Me> {
  const response = await fetch(`${apiBaseUrl}/api/me`, {
    headers: { Authorization: `Bearer ${accessToken}` },
  })
  if (!response.ok) {
    throw new Error(`API /api/me failed with ${response.status}`)
  }
  return (await response.json()) as Me
}
