import { fireEvent, screen } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { fakeApi, json, me, msal, renderApp, route, signIn, signOut } from './testing'

describe('App', () => {
  beforeEach(() => {
    signOut()
    vi.clearAllMocks()
  })
  afterEach(() => vi.unstubAllGlobals())

  it('shows the API status when the API answers', async () => {
    fakeApi(route('GET', '/health', () => json({ status: 'ok' })))

    renderApp()

    expect(await screen.findByText('ok')).toBeInTheDocument()
  })

  it('says the API is unreachable when the call fails', async () => {
    fakeApi(route('GET', '/health', () => new Response('', { status: 503 })))

    renderApp()

    expect(await screen.findByText('unreachable')).toBeInTheDocument()
  })

  it('offers sign-in to an anonymous visitor', () => {
    fakeApi(route('GET', '/health', () => json({ status: 'ok' })))

    renderApp()
    fireEvent.click(screen.getByRole('button', { name: 'Sign in' }))

    expect(msal.instance.loginRedirect).toHaveBeenCalledExactlyOnceWith(expect.objectContaining({ prompt: 'select_account' }))
  })

  it('calls the API with the access token and shows the signed-in user', async () => {
    signIn()
    const { calls } = fakeApi(me(['readers'], 'Test Reader'), route('GET', /^\/api\/documents/, () => json([])))

    renderApp()

    expect(await screen.findByText('Test Reader')).toBeInTheDocument()
    expect(calls[0].init.headers).toEqual({ Authorization: 'Bearer token-1' })
  })

  it('says so when the API refuses the signed-in user', async () => {
    signIn()
    fakeApi(route('GET', '/api/me', () => new Response('', { status: 401 })))

    renderApp()

    expect(await screen.findByRole('alert')).toHaveTextContent('The API refused the request.')
  })

  it('offers Upload to contributors only', async () => {
    signIn()
    fakeApi(me(['readers'], 'Test Reader'), route('GET', /^\/api\/documents/, () => json([])))

    renderApp()

    expect(await screen.findByRole('link', { name: 'Library' })).toBeInTheDocument()
    expect(screen.queryByRole('link', { name: 'Upload' })).not.toBeInTheDocument()
  })
})
