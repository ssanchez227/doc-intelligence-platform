// Base URL of the API, baked in at build time (VITE_API_BASE_URL). Empty means same origin.
const apiBaseUrl = (import.meta.env.VITE_API_BASE_URL ?? '').replace(/\/$/, '')

export type Health = { status: string }

export async function getHealth(): Promise<Health> {
  const response = await fetch(`${apiBaseUrl}/health`)
  if (!response.ok) {
    throw new Error(`API health check failed with ${response.status}`)
  }
  return (await response.json()) as Health
}
