import { fireEvent, screen, waitFor, within } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import type { DocumentDetail, DocumentSummary, Version } from '../content/types'
import { fakeApi, json, me, renderApp, route, signIn } from '../testing'

const version = (number: string, status: Version['status'], extra: Partial<Version> = {}): Version => ({
  id: `v-${number}`,
  number,
  status,
  fileName: 'policy.md',
  contentType: 'text/markdown',
  sizeBytes: 1200,
  authorName: 'Test Contributor',
  createdAt: '2026-11-27T10:00:00Z',
  comment: null,
  sourceNumber: null,
  pdf: { status: 'None', error: null },
  ...extra,
})

const detail = (extra: Partial<DocumentDetail> = {}): DocumentDetail => ({
  id: 'doc-1',
  title: 'Travel policy',
  description: null,
  category: { id: 2, name: 'Policies' },
  ownerName: 'Test Contributor',
  createdAt: '2026-11-27T09:00:00Z',
  isRetired: false,
  retiredAt: null,
  retiredByName: null,
  publishedVersion: version('1.0', 'Published'),
  latestVersion: version('1.0', 'Published'),
  checkouts: [],
  myCheckout: null,
  canEdit: true,
  canAdminister: false,
  canPublish: false,
  groups: ['contributors', 'readers'],
  ...extra,
})

const history = [version('1.0', 'Published', { sourceNumber: '0.1' }), version('0.1', 'Draft')]

beforeEach(() => {
  vi.clearAllMocks()
  signIn()
})
afterEach(() => vi.unstubAllGlobals())

describe('Library', () => {
  it('lists the documents the API returns, with version, state and who is editing', async () => {
    const docs: DocumentSummary[] = [
      {
        id: 'doc-1',
        title: 'Travel policy',
        category: 'Policies',
        ownerName: 'Test Contributor',
        isRetired: false,
        publishedNumber: '1.0',
        latestNumber: '1.1',
        latestStatus: 'Draft',
        updatedAt: '2026-11-27T10:00:00Z',
        checkoutCount: 2,
      },
    ]
    fakeApi(me(), route('GET', '/api/documents?retired=false', () => json(docs)))

    renderApp('/')

    const link = await screen.findByRole('link', { name: 'Travel policy' })
    expect(link).toHaveAttribute('href', '/documents/doc-1')
    const row = link.closest('tr')!
    expect(within(row).getByText('(latest 1.1)')).toBeInTheDocument()
    expect(within(row).getByText('2 editing')).toBeInTheDocument()
  })

  it('asks for retired documents when the box is ticked', async () => {
    const { calls } = fakeApi(me(), route('GET', /^\/api\/documents\?retired=/, () => json([])))

    renderApp('/')
    fireEvent.click(await screen.findByLabelText('Show retired documents'))

    await screen.findByText('No retired documents.')
    expect(calls.some((c) => c.path === '/api/documents?retired=true')).toBe(true)
  })
})

describe('Upload (UC-01)', () => {
  const categories = route('GET', '/api/categories', () => json([{ id: 2, name: 'Policies' }]))

  it('checks in the file with its metadata and opens the new document', async () => {
    const { calls } = fakeApi(
      me(),
      categories,
      route('POST', '/api/documents', () => json(detail({ latestVersion: version('0.1', 'Draft'), publishedVersion: null }), 201)),
      route('GET', '/api/documents/doc-1', () => json(detail({ latestVersion: version('0.1', 'Draft'), publishedVersion: null }))),
      route('GET', '/api/documents/doc-1/versions', () => json([version('0.1', 'Draft')])),
    )
    renderApp('/upload')

    fireEvent.change(await screen.findByLabelText('File'), { target: { files: [new File(['# Travel'], 'travel-policy.md')] } })
    fireEvent.change(screen.getByLabelText('Category'), { target: { value: '2' } })
    await screen.findByRole('option', { name: 'Policies' })
    fireEvent.change(screen.getByLabelText('Category'), { target: { value: '2' } })
    fireEvent.change(screen.getByLabelText('Reviewers, in order'), { target: { value: 'r1@example.com, r2@example.com' } })
    fireEvent.click(screen.getByRole('button', { name: 'Check in' }))

    expect(await screen.findByRole('heading', { name: 'Travel policy' })).toBeInTheDocument()
    const post = calls.find((c) => c.method === 'POST')!
    const form = post.init.body as FormData
    expect(form.get('title')).toBe('travel-policy') // taken from the file name
    expect(form.get('categoryId')).toBe('2')
    expect(form.get('reviewers')).toBe('r1@example.com, r2@example.com')
    expect((form.get('file') as File).name).toBe('travel-policy.md')
  })

  it('refuses a proprietary format before calling the API', async () => {
    const { calls } = fakeApi(me(), categories)
    renderApp('/upload')

    fireEvent.change(await screen.findByLabelText('File'), { target: { files: [new File(['x'], 'report.docx')] } })
    fireEvent.click(screen.getByRole('button', { name: 'Check in' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('This format is not accepted')
    expect(calls.some((c) => c.method === 'POST')).toBe(false)
  })

  it('shows the API refusal', async () => {
    fakeApi(me(), categories, route('POST', '/api/documents', () => json({ error: 'Only contributors can check in documents.' }, 403)))
    renderApp('/upload')

    fireEvent.change(await screen.findByLabelText('File'), { target: { files: [new File(['x'], 'a.txt')] } })
    await screen.findByRole('option', { name: 'Policies' })
    fireEvent.change(screen.getByLabelText('Category'), { target: { value: '2' } })
    fireEvent.click(screen.getByRole('button', { name: 'Check in' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Only contributors can check in documents.')
  })
})

describe('Document detail', () => {
  it('shows the versions, the details and who has it checked out (UC-02, UC-05)', async () => {
    fakeApi(
      me(),
      route('GET', '/api/documents/doc-1', () =>
        json(detail({ checkouts: [{ userName: 'Second Contributor', takenAt: '2026-11-27T11:00:00Z', baseVersionNumber: '1.0', isMine: false }] })),
      ),
      route('GET', '/api/documents/doc-1/versions', () => json(history)),
    )

    renderApp('/documents/doc-1')

    const versions = await screen.findByRole('region', { name: 'Versions' })
    expect(await within(versions).findByText('from 0.1')).toBeInTheDocument()
    expect(within(versions).getAllByRole('button', { name: 'Open' })).toHaveLength(2)
    expect(screen.getByText(/Second Contributor, since/)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Check out' })).toBeInTheDocument()
  })

  it('checks out with one click (UC-02)', async () => {
    const { calls } = fakeApi(
      me(),
      route('GET', '/api/documents/doc-1', () => json(detail())),
      route('GET', '/api/documents/doc-1/versions', () => json(history)),
      route('POST', '/api/documents/doc-1/checkout', () =>
        json(detail({ myCheckout: { userName: 'Test Contributor', takenAt: '2026-11-27T12:00:00Z', baseVersionNumber: '1.0', isMine: true } })),
      ),
    )
    renderApp('/documents/doc-1')

    fireEvent.click(await screen.findByRole('button', { name: 'Check out' }))

    expect(await screen.findByRole('form', { name: 'Check in a new version' })).toBeInTheDocument()
    expect(calls.some((c) => c.method === 'POST' && c.path === '/api/documents/doc-1/checkout')).toBe(true)
    expect(screen.getByRole('button', { name: 'Cancel checkout' })).toBeInTheDocument()
  })

  it('shows who checked in first when an out-of-date check-in is refused (UC-03)', async () => {
    const mine = { userName: 'Test Contributor', takenAt: '2026-11-27T12:00:00Z', baseVersionNumber: '1.0', isMine: true }
    fakeApi(
      me(),
      route('GET', '/api/documents/doc-1', () => json(detail({ myCheckout: mine, checkouts: [mine] }))),
      route('GET', '/api/documents/doc-1/versions', () => json(history)),
      route('POST', '/api/documents/doc-1/versions', () =>
        json(
          {
            error: 'Second Contributor checked in version 1.1 after you checked out. Check out again to start from the latest version.',
            latestNumber: '1.1',
            latestAuthorName: 'Second Contributor',
            latestCreatedAt: '2026-11-27T12:30:00Z',
          },
          409,
        ),
      ),
    )
    renderApp('/documents/doc-1')

    fireEvent.change(await screen.findByLabelText('Changed file'), { target: { files: [new File(['# new'], 'policy.md')] } })
    fireEvent.click(screen.getByRole('button', { name: 'Check in new version' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Second Contributor checked in version 1.1 after you checked out')
  })

  it('offers to start again from the latest version when the checkout is out of date', async () => {
    const mine = { userName: 'Test Contributor', takenAt: '2026-11-27T12:00:00Z', baseVersionNumber: '1.0', isMine: true }
    fakeApi(
      me(),
      route('GET', '/api/documents/doc-1', () => json(detail({ myCheckout: mine, checkouts: [mine], latestVersion: version('1.1', 'Draft') }))),
      route('GET', '/api/documents/doc-1/versions', () => json(history)),
    )
    renderApp('/documents/doc-1')

    expect(await screen.findByRole('button', { name: 'Start again from 1.1' })).toBeInTheDocument()
  })

  it('restores an older version as a new draft (UC-06)', async () => {
    const { calls } = fakeApi(
      me(),
      route('GET', '/api/documents/doc-1', () => json(detail())),
      route('GET', '/api/documents/doc-1/versions', () => json(history)),
      route('POST', '/api/documents/doc-1/versions/v-0.1/restore', () => json(detail({ latestVersion: version('1.1', 'Draft') }))),
    )
    renderApp('/documents/doc-1')

    fireEvent.click(await screen.findByRole('button', { name: 'Restore' }))

    expect(await screen.findByRole('status')).toHaveTextContent('Version 0.1 restored as a new draft.')
    expect(calls.some((c) => c.method === 'POST' && c.path.endsWith('/restore'))).toBe(true)
  })

  it('lets an administrator retire with a reason, and shows the retirement (UC-08)', async () => {
    const { calls } = fakeApi(
      me(['administrators'], 'Test Admin'),
      route('GET', '/api/documents/doc-1', () => json(detail({ canEdit: false, canAdminister: true }))),
      route('GET', '/api/documents/doc-1/versions', () => json(history)),
      route('POST', '/api/documents/doc-1/retire', () =>
        json(detail({ canEdit: false, canAdminister: true, isRetired: true, retiredAt: '2026-11-27T13:00:00Z', retiredByName: 'Test Admin' })),
      ),
    )
    renderApp('/documents/doc-1')

    fireEvent.click(await screen.findByRole('button', { name: 'Retire' }))
    fireEvent.change(screen.getByLabelText('Reason'), { target: { value: 'Superseded' } })
    fireEvent.click(screen.getByRole('button', { name: 'Confirm retire' }))

    expect(await screen.findByRole('button', { name: 'Reverse retirement' })).toBeInTheDocument()
    expect(screen.getByText(/by Test Admin/)).toBeInTheDocument()
    const post = calls.find((c) => c.path.endsWith('/retire'))!
    expect(JSON.parse(post.init.body as string)).toEqual({ reason: 'Superseded' })
  })

  it('offers readers no changes, and a PDF once it exists (UC-07)', async () => {
    fakeApi(
      me(['readers'], 'Test Reader'),
      route('GET', '/api/documents/doc-1', () =>
        json(detail({ canEdit: false, publishedVersion: version('1.0', 'Published', { pdf: { status: 'Done', error: null } }) })),
      ),
      route('GET', '/api/documents/doc-1/versions', () => json([version('1.0', 'Published', { pdf: { status: 'Done', error: null } })])),
    )
    renderApp('/documents/doc-1')

    expect(await screen.findByRole('button', { name: 'PDF of 1.0' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Check out' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Restore' })).not.toBeInTheDocument()
  })

  it('says a document is missing or not accessible without revealing which', async () => {
    fakeApi(me(), route('GET', '/api/documents/doc-x', () => json({ error: 'Document not found.' }, 404)))

    renderApp('/documents/doc-x')

    expect(await screen.findByRole('alert')).toHaveTextContent('does not exist, or you do not have access to it')
  })

  it('shows a failed PDF conversion against the version', async () => {
    fakeApi(
      me(),
      route('GET', '/api/documents/doc-1', () => json(detail())),
      route('GET', '/api/documents/doc-1/versions', () =>
        json([version('1.0', 'Published', { pdf: { status: 'Failed', error: 'Converting x to PDF is not supported yet.' } })]),
      ),
    )
    renderApp('/documents/doc-1')

    await waitFor(() => expect(screen.getByText(/No PDF: Converting x to PDF is not supported yet./)).toBeInTheDocument())
  })
})
