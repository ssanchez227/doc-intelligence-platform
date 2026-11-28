import type { Api } from '../useApi'
import type { Version } from './types'

// Opens a version's file (or its PDF) fetched with the user's token. PDFs and plain
// text open in a new tab; everything else downloads. HTML, XML, JSON and ODF are
// never rendered by the browser here: an uploaded file must not run as part of the
// web app (decision log, M1).
export async function openVersionFile(api: Api, documentId: string, version: Version, pdf = false) {
  const response = await api(`/api/documents/${documentId}/versions/${version.id}/content${pdf ? '?pdf=true' : ''}`)
  const data = await response.blob()

  const textual = ['text/plain', 'text/markdown', 'text/csv'].includes(version.contentType)
  const inline = pdf || version.contentType === 'application/pdf' || textual
  const type = pdf || version.contentType === 'application/pdf' ? 'application/pdf' : textual ? 'text/plain;charset=utf-8' : 'application/octet-stream'
  const url = URL.createObjectURL(new Blob([data], { type }))

  if (inline) {
    window.open(url, '_blank', 'noopener')
  } else {
    const link = document.createElement('a')
    link.href = url
    link.download = version.fileName
    link.click()
  }
  setTimeout(() => URL.revokeObjectURL(url), 60_000)
}
