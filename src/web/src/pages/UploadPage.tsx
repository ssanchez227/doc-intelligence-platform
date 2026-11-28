import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { type FormEvent, useState } from 'react'
import { useNavigate } from 'react-router'
import { acceptedExtensions, type Category, type DocumentDetail, maxFileBytes } from '../content/types'
import { json, useApi } from '../useApi'

// Wireframe: Upload / check in (UC-01). Duplicate warnings, suggestions and the
// sensitive-data check in the wireframe come with M7.
export default function UploadPage() {
  const api = useApi()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const categories = useQuery({ queryKey: ['categories'], queryFn: () => json<Category[]>(api, '/api/categories') })

  const [file, setFile] = useState<File | null>(null)
  const [title, setTitle] = useState('')
  const [categoryId, setCategoryId] = useState('')
  const [description, setDescription] = useState('')
  const [reviewers, setReviewers] = useState('')
  const [problem, setProblem] = useState<string | null>(null)

  const checkIn = useMutation({
    mutationFn: (form: FormData) => json<DocumentDetail>(api, '/api/documents', { method: 'POST', body: form }),
    onSuccess: async (doc) => {
      await queryClient.invalidateQueries({ queryKey: ['documents'] })
      navigate(`/documents/${doc.id}`)
    },
  })

  function chooseFile(chosen: File | null) {
    setFile(chosen)
    setProblem(null)
    if (chosen && !title) setTitle(chosen.name.replace(/\.[^.]+$/, ''))
  }

  function submit(event: FormEvent) {
    event.preventDefault()
    const extension = file ? file.name.slice(file.name.lastIndexOf('.')).toLowerCase() : ''
    if (!file) return setProblem('Choose a file to check in.')
    if (!acceptedExtensions.includes(extension)) return setProblem(`This format is not accepted. Use one of: ${acceptedExtensions.join(', ')}.`)
    if (file.size > maxFileBytes) return setProblem('The file is larger than 25 MB.')
    if (!title.trim()) return setProblem('Give the document a title.')
    if (!categoryId) return setProblem('Choose a category.')
    setProblem(null)

    const form = new FormData()
    form.append('file', file)
    form.append('title', title)
    form.append('categoryId', categoryId)
    form.append('description', description)
    form.append('reviewers', reviewers)
    checkIn.mutate(form)
  }

  const error = problem ?? (checkIn.isError ? checkIn.error.message : null)

  return (
    <>
      <h1>Check in a document</h1>
      <form className="form" onSubmit={submit} noValidate>
        <div className="field">
          <label htmlFor="file">File</label>
          <input id="file" type="file" accept={acceptedExtensions.join(',')} onChange={(e) => chooseFile(e.target.files?.[0] ?? null)} />
          <span className="hint">Non-proprietary formats only: {acceptedExtensions.join(', ')}. Up to 25 MB.</span>
        </div>
        <div className="field">
          <label htmlFor="title">Title</label>
          <input id="title" value={title} onChange={(e) => setTitle(e.target.value)} required />
        </div>
        <div className="field">
          <label htmlFor="category">Category</label>
          <select id="category" value={categoryId} onChange={(e) => setCategoryId(e.target.value)} required>
            <option value="">Choose a category</option>
            {categories.data?.map((c) => (
              <option key={c.id} value={c.id}>
                {c.name}
              </option>
            ))}
          </select>
          <span className="hint">The category decides who can read it.</span>
        </div>
        <div className="field">
          <label htmlFor="description">Description (optional)</label>
          <textarea id="description" rows={3} value={description} onChange={(e) => setDescription(e.target.value)} />
        </div>
        <div className="field">
          <label htmlFor="reviewers">Reviewers, in order</label>
          <input id="reviewers" value={reviewers} onChange={(e) => setReviewers(e.target.value)} placeholder="reviewer@example.com, second@example.com" />
          <span className="hint">
            Saved with the version; the review itself starts once reviews exist (M5). This will be saved as version 0.1, a draft.
          </span>
        </div>
        {error && (
          <p role="alert" className="error">
            {error}
          </p>
        )}
        <div className="actions">
          <button type="submit" className="btn primary" disabled={checkIn.isPending}>
            {checkIn.isPending ? 'Checking in…' : 'Check in'}
          </button>
          <button type="button" className="btn" onClick={() => navigate('/')}>
            Cancel
          </button>
        </div>
      </form>
    </>
  )
}
