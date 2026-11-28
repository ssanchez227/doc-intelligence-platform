import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { type FormEvent, useState } from 'react'
import { Link, useParams } from 'react-router'
import { ApiError } from '../api'
import { openVersionFile } from '../content/openFile'
import type { DocumentDetail, Version } from '../content/types'
import { formatDate, formatSize } from '../format'
import { json, useApi } from '../useApi'

// Wireframe: Document detail. Check out, check in, cancel (UC-02 to UC-04), history
// and opening versions (UC-05), restore (UC-06), PDF (UC-07), retire (UC-08).
export default function DocumentPage() {
  const { id = '' } = useParams()
  const api = useApi()
  const queryClient = useQueryClient()
  const [message, setMessage] = useState<{ kind: 'error' | 'info'; text: string } | null>(null)

  const detail = useQuery({
    queryKey: ['document', id],
    queryFn: () => json<DocumentDetail>(api, `/api/documents/${id}`),
    retry: false,
    // While a PDF is being made, look again shortly.
    refetchInterval: (query) => (query.state.data?.publishedVersion?.pdf.status === 'Pending' ? 2000 : false),
  })
  const history = useQuery({
    queryKey: ['history', id],
    queryFn: () => json<Version[]>(api, `/api/documents/${id}/versions`),
    enabled: detail.isSuccess,
  })

  const action = useMutation({
    mutationFn: async (run: { label: string; call: () => Promise<DocumentDetail> }) => run.call(),
    onSuccess: (doc, run) => {
      queryClient.setQueryData(['document', id], doc)
      setMessage({ kind: 'info', text: run.label })
    },
    onError: (error) => {
      setMessage({ kind: 'error', text: error.message })
      // A refusal often means the document moved on (a newer version, a retirement).
      void queryClient.invalidateQueries({ queryKey: ['document', id] })
    },
    onSettled: () => {
      void queryClient.invalidateQueries({ queryKey: ['history', id] })
      void queryClient.invalidateQueries({ queryKey: ['documents'] })
    },
  })

  const post = (path: string, init: RequestInit = {}) => () =>
    json<DocumentDetail>(api, `/api/documents/${id}${path}`, { method: 'POST', ...init })

  if (detail.isPending) return <p>Loading…</p>
  if (detail.isError) {
    const notFound = detail.error instanceof ApiError && detail.error.status === 404
    return (
      <>
        <p role="alert">{notFound ? 'This document does not exist, or you do not have access to it.' : detail.error.message}</p>
        <Link to="/">Back to the library</Link>
      </>
    )
  }

  const doc = detail.data
  const shown = doc.publishedVersion ?? doc.latestVersion
  const busy = action.isPending
  const open = (version: Version, pdf = false) =>
    openVersionFile(api, doc.id, version, pdf).catch((e: Error) => setMessage({ kind: 'error', text: e.message }))

  return (
    <>
      <div className="crumbs muted">
        <Link to="/">Library</Link> / {doc.category.name}
      </div>
      <div className="title-row">
        <h1>{doc.title}</h1>
        {doc.isRetired && <span className="chip">Retired</span>}
        {shown && <span className="chip">{shown.status}</span>}
        {shown && <span className="chip">{shown.number}</span>}
      </div>
      {doc.description && <p className="muted">{doc.description}</p>}

      {doc.isRetired && (
        <p className="notice">
          Retired {doc.retiredAt && formatDate(doc.retiredAt)} by {doc.retiredByName}. It is hidden from the library and cannot be changed;
          its versions stay readable.
        </p>
      )}

      <div className="actions">
        {doc.canEdit && !doc.myCheckout && (
          <button className="btn primary" disabled={busy} onClick={() => action.mutate({ label: 'Checked out.', call: post('/checkout') })}>
            Check out
          </button>
        )}
        {shown && (
          <button className="btn" onClick={() => open(shown)}>
            Open {shown.number}
          </button>
        )}
        {doc.publishedVersion?.pdf.status === 'Done' && (
          <button className="btn" onClick={() => open(doc.publishedVersion!, true)}>
            PDF of {doc.publishedVersion.number}
          </button>
        )}
        {doc.canPublish && doc.latestVersion && (
          <button
            className="btn"
            disabled={busy}
            onClick={() => action.mutate({ label: 'Published.', call: post(`/versions/${doc.latestVersion!.id}/publish`) })}
          >
            Publish {doc.latestVersion.number} (dev only)
          </button>
        )}
        {doc.canAdminister && (
          <RetireButton
            retired={doc.isRetired}
            busy={busy}
            onConfirm={(reason) =>
              action.mutate(
                doc.isRetired
                  ? { label: 'Retirement reversed.', call: post('/unretire', jsonBody({})) }
                  : { label: 'Retired.', call: post('/retire', jsonBody({ reason })) },
              )
            }
          />
        )}
      </div>

      {message && (
        <p role={message.kind === 'error' ? 'alert' : 'status'} className={message.kind === 'error' ? 'error' : 'info'}>
          {message.text}
        </p>
      )}

      {doc.myCheckout && (
        <CheckInPanel
          doc={doc}
          busy={busy}
          onCheckIn={(form) => action.mutate({ label: 'New version checked in.', call: post('/versions', { body: form }) })}
          onCancel={() =>
            action.mutate({
              label: 'Checkout cancelled.',
              call: () => json<DocumentDetail>(api, `/api/documents/${id}/checkout`, { method: 'DELETE' }),
            })
          }
          onRestart={() => action.mutate({ label: 'Checked out again from the latest version.', call: post('/checkout') })}
        />
      )}

      <div className="split">
        <section className="card table-wrap grow" aria-label="Versions">
          <table>
            <thead>
              <tr>
                <th>Version</th>
                <th>State</th>
                <th>Author</th>
                <th>Date</th>
                <th>Actions</th>
              </tr>
            </thead>
            <tbody>
              {history.data?.map((v) => (
                <tr key={v.id}>
                  <td>
                    {v.number}
                    {v.sourceNumber && <div className="hint">from {v.sourceNumber}</div>}
                  </td>
                  <td>
                    <span className="chip">{v.status}</span>
                  </td>
                  <td>{v.authorName}</td>
                  <td>{formatDate(v.createdAt)}</td>
                  <td className="row-actions">
                    <button className="link" onClick={() => open(v)}>
                      Open
                    </button>
                    {v.pdf.status === 'Done' && (
                      <button className="link" onClick={() => open(v, true)}>
                        PDF
                      </button>
                    )}
                    {v.pdf.status === 'Pending' && <span className="hint">PDF being made</span>}
                    {v.pdf.status === 'Failed' && <span className="hint" title={v.pdf.error ?? undefined}>No PDF: {v.pdf.error}</span>}
                    {doc.canEdit && v.id !== doc.latestVersion?.id && (
                      <button
                        className="link"
                        disabled={busy}
                        onClick={() =>
                          action.mutate({ label: `Version ${v.number} restored as a new draft.`, call: post(`/versions/${v.id}/restore`) })
                        }
                      >
                        Restore
                      </button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </section>

        <aside className="card details" aria-label="Details">
          <strong>Details</strong>
          <span className="muted">Category: {doc.category.name}</span>
          <span className="muted">Owner: {doc.ownerName}</span>
          <span className="muted">Readable by: {doc.groups.join(', ') || 'owner only'}</span>
          {shown && (
            <span className="muted">
              File: {shown.fileName} ({formatSize(shown.sizeBytes)})
            </span>
          )}
          <strong>Checked out</strong>
          {doc.checkouts.length === 0 ? (
            <span className="muted">Not checked out</span>
          ) : (
            <ul className="plain">
              {doc.checkouts.map((c) => (
                <li key={c.userName}>
                  {c.isMine ? 'You' : c.userName}, since {formatDate(c.takenAt)} (from {c.baseVersionNumber})
                </li>
              ))}
            </ul>
          )}
          {doc.checkouts.length > 0 && (
            <span className="hint">Checking out does not lock. If someone checks in first, a later check-in from an older version is refused.</span>
          )}
        </aside>
      </div>
    </>
  )
}

function jsonBody(body: unknown): RequestInit {
  return { headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) }
}

function CheckInPanel(props: {
  doc: DocumentDetail
  busy: boolean
  onCheckIn: (form: FormData) => void
  onCancel: () => void
  onRestart: () => void
}) {
  const { doc, busy } = props
  const [file, setFile] = useState<File | null>(null)
  const [comment, setComment] = useState('')
  const checkout = doc.myCheckout!
  const outOfDate = doc.latestVersion !== null && checkout.baseVersionNumber !== doc.latestVersion.number

  function submit(event: FormEvent) {
    event.preventDefault()
    if (!file) return
    const form = new FormData()
    form.append('file', file)
    form.append('comment', comment)
    props.onCheckIn(form)
  }

  return (
    <form className="card form" onSubmit={submit} aria-label="Check in a new version">
      <strong>
        You checked this out {formatDate(checkout.takenAt)}, from version {checkout.baseVersionNumber}.
      </strong>
      {outOfDate && (
        <p className="notice">
          Version {doc.latestVersion!.number} was checked in after you checked out, so a check-in now would be refused.{' '}
          <button type="button" className="link" onClick={props.onRestart} disabled={busy}>
            Start again from {doc.latestVersion!.number}
          </button>
        </p>
      )}
      <div className="field">
        <label htmlFor="new-file">Changed file</label>
        <input id="new-file" type="file" onChange={(e) => setFile(e.target.files?.[0] ?? null)} />
      </div>
      <div className="field">
        <label htmlFor="comment">What changed (optional)</label>
        <input id="comment" value={comment} onChange={(e) => setComment(e.target.value)} />
      </div>
      <div className="actions">
        <button type="submit" className="btn primary" disabled={busy || !file}>
          Check in new version
        </button>
        <button type="button" className="btn" disabled={busy} onClick={props.onCancel}>
          Cancel checkout
        </button>
      </div>
    </form>
  )
}

function RetireButton(props: { retired: boolean; busy: boolean; onConfirm: (reason: string) => void }) {
  const [asking, setAsking] = useState(false)
  const [reason, setReason] = useState('')

  if (props.retired) {
    return (
      <button className="btn" disabled={props.busy} onClick={() => props.onConfirm('')}>
        Reverse retirement
      </button>
    )
  }
  if (!asking) {
    return (
      <button className="btn" onClick={() => setAsking(true)}>
        Retire
      </button>
    )
  }
  return (
    <span className="inline-form">
      <label htmlFor="reason">Reason</label>
      <input id="reason" value={reason} onChange={(e) => setReason(e.target.value)} />
      <button className="btn" disabled={props.busy} onClick={() => props.onConfirm(reason)}>
        Confirm retire
      </button>
      <button className="btn" onClick={() => setAsking(false)}>
        Keep
      </button>
    </span>
  )
}
