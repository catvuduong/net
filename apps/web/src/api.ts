export const API_URL = import.meta.env.VITE_API_URL ?? 'http://localhost:3001'

export type Bookmark = {
  id: number
  title: string
  url: string
  tags: string[]
  createdAt: string
}

export type TagCount = { tag: string; count: number }

export type NewBookmark = { title: string; url: string; tags: string[] }

export type FieldErrors = Partial<Record<'title' | 'url' | 'tags', string>>

export class ApiError extends Error {
  status: number
  fieldErrors: FieldErrors

  constructor(status: number, fieldErrors: FieldErrors = {}) {
    super(`Request failed with status ${status}`)
    this.status = status
    this.fieldErrors = fieldErrors
  }
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`${API_URL}${path}`, init)
  if (!response.ok) throw new ApiError(response.status, await readFieldErrors(response))
  return response.status === 204 ? (undefined as T) : ((await response.json()) as T)
}

// The API answers invalid input with a ValidationProblem: { errors: { field: [message, ...] } }.
async function readFieldErrors(response: Response): Promise<FieldErrors> {
  if (response.status !== 400) return {}
  const body = await response.json().catch(() => null)
  const errors: Record<string, string[]> = body?.errors ?? {}
  const fieldErrors: FieldErrors = {}
  for (const [field, messages] of Object.entries(errors)) {
    if (field === 'title' || field === 'url' || field === 'tags') fieldErrors[field] = messages[0]
  }
  return fieldErrors
}

export function listBookmarks(filters: { q?: string; tag?: string }, signal?: AbortSignal) {
  const params = new URLSearchParams()
  if (filters.q) params.set('q', filters.q)
  if (filters.tag) params.set('tag', filters.tag)
  const query = params.toString()
  return request<Bookmark[]>(`/bookmarks${query ? `?${query}` : ''}`, { signal })
}

export function listTags(signal?: AbortSignal) {
  return request<TagCount[]>('/tags', { signal })
}

export function createBookmark(input: NewBookmark) {
  return request<Bookmark>('/bookmarks', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(input),
  })
}

export function deleteBookmark(id: number) {
  return request<void>(`/bookmarks/${id}`, { method: 'DELETE' })
}

export function fetchHealth(signal?: AbortSignal) {
  return request<{ status: string; db: string }>('/health', { signal })
}
