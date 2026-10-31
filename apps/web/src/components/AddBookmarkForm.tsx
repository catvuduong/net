import { useState, type FormEvent } from 'react'
import { ApiError, createBookmark, type FieldErrors } from '../api'

type AddBookmarkFormProps = {
  onSaved: () => void
}

export function AddBookmarkForm({ onSaved }: AddBookmarkFormProps) {
  const [title, setTitle] = useState('')
  const [url, setUrl] = useState('')
  const [tags, setTags] = useState('')
  const [errors, setErrors] = useState<FieldErrors>({})
  const [formError, setFormError] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setSaving(true)
    setErrors({})
    setFormError(null)
    try {
      // The API trims, lowercases and validates; its field errors are shown next to each input.
      await createBookmark({ title, url, tags: tags.split(',').map((tag) => tag.trim()).filter(Boolean) })
      setTitle('')
      setUrl('')
      setTags('')
      onSaved()
    } catch (error) {
      if (error instanceof ApiError && Object.keys(error.fieldErrors).length > 0) {
        setErrors(error.fieldErrors)
      } else {
        setFormError("Couldn't save the bookmark. Check the connection and try again.")
      }
    } finally {
      setSaving(false)
    }
  }

  return (
    <form data-testid="add-form" className="card form" onSubmit={handleSubmit} noValidate>
      <h2>Add a bookmark</h2>

      <div className="field">
        <label htmlFor="add-title">Title</label>
        <input
          id="add-title"
          value={title}
          onChange={(event) => setTitle(event.target.value)}
          placeholder="Vite guide"
          aria-invalid={errors.title ? true : undefined}
          aria-describedby={errors.title ? 'add-title-error' : undefined}
        />
        {errors.title && <p id="add-title-error" className="field-error">{errors.title}</p>}
      </div>

      <div className="field">
        <label htmlFor="add-url">URL</label>
        <input
          id="add-url"
          type="url"
          value={url}
          onChange={(event) => setUrl(event.target.value)}
          placeholder="https://vite.dev"
          aria-invalid={errors.url ? true : undefined}
          aria-describedby={errors.url ? 'add-url-error' : undefined}
        />
        {errors.url && <p id="add-url-error" className="field-error">{errors.url}</p>}
      </div>

      <div className="field">
        <label htmlFor="add-tags">Tags</label>
        <input
          id="add-tags"
          value={tags}
          onChange={(event) => setTags(event.target.value)}
          placeholder="frontend, tooling"
          aria-invalid={errors.tags ? true : undefined}
          aria-describedby={errors.tags ? 'add-tags-error' : 'add-tags-hint'}
        />
        {errors.tags ? (
          <p id="add-tags-error" className="field-error">{errors.tags}</p>
        ) : (
          <p id="add-tags-hint" className="hint">Separate tags with commas.</p>
        )}
      </div>

      {formError && <p role="alert" className="banner">{formError}</p>}

      <button type="submit" className="button primary" disabled={saving}>
        {saving ? 'Saving…' : 'Add bookmark'}
      </button>
    </form>
  )
}
