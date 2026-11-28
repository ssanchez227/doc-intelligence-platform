import { useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { Link } from 'react-router'
import type { DocumentSummary } from '../content/types'
import { formatDate } from '../format'
import { json, useApi } from '../useApi'

// Wireframe: Library. Only documents the signed-in user may read are listed.
export default function LibraryPage() {
  const api = useApi()
  const [retired, setRetired] = useState(false)
  const documents = useQuery({
    queryKey: ['documents', { retired }],
    queryFn: () => json<DocumentSummary[]>(api, `/api/documents?retired=${retired}`),
  })

  return (
    <>
      <h1>Library</h1>
      <div className="toolbar">
        <label className="check">
          <input type="checkbox" checked={retired} onChange={(e) => setRetired(e.target.checked)} />
          Show retired documents
        </label>
        <span className="muted">Only documents you are allowed to read are listed.</span>
      </div>
      {documents.isPending ? (
        <p>Loading documents…</p>
      ) : documents.isError ? (
        <p role="alert">The documents could not be loaded: {documents.error.message}</p>
      ) : documents.data.length === 0 ? (
        <p className="muted">{retired ? 'No retired documents.' : 'No documents yet.'}</p>
      ) : (
        <div className="card table-wrap">
          <table>
            <thead>
              <tr>
                <th>Title</th>
                <th>Category</th>
                <th>Version</th>
                <th>State</th>
                <th>Updated</th>
                <th>Checked out</th>
              </tr>
            </thead>
            <tbody>
              {documents.data.map((d) => (
                <tr key={d.id}>
                  <td>
                    <Link to={`/documents/${d.id}`}>{d.title}</Link>
                  </td>
                  <td>{d.category}</td>
                  <td>
                    {d.publishedNumber ?? '—'}
                    {d.latestNumber !== d.publishedNumber && <span className="muted"> (latest {d.latestNumber})</span>}
                  </td>
                  <td>
                    <span className="chip">{d.isRetired ? 'Retired' : d.latestStatus}</span>
                  </td>
                  <td>{formatDate(d.updatedAt)}</td>
                  <td className={d.checkoutCount ? undefined : 'muted'}>
                    {d.checkoutCount ? `${d.checkoutCount} editing` : 'No'}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </>
  )
}
