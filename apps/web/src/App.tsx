import { useEffect, useState } from 'react'
import { ApiError, deleteBookmark, fetchHealth, listBookmarks, listTags, type Bookmark, type TagCount } from './api'
import { AddBookmarkForm } from './components/AddBookmarkForm'
import { BookmarkList, type ListState } from './components/BookmarkList'
import { TagChips } from './components/TagChips'

export default function App() {
  const [search, setSearch] = useState('')
  const [query, setQuery] = useState('')
  const [activeTag, setActiveTag] = useState<string | null>(null)
  const [reloadKey, setReloadKey] = useState(0)
  // The list remembers which request produced it; until that matches the current filters it shows as loading.
  const requestKey = `${query}\n${activeTag ?? ''}\n${reloadKey}`
  const [result, setResult] = useState<{ key: string; state: ListState } | null>(null)
  const list: ListState = result?.key === requestKey ? result.state : { status: 'loading' }
  const [tags, setTags] = useState<TagCount[]>([])
  const [tagsFailed, setTagsFailed] = useState(false)
  const [health, setHealth] = useState('checking…')
  const [deletingId, setDeletingId] = useState<number | null>(null)
  const [actionError, setActionError] = useState<string | null>(null)

  const filtered = query !== '' || activeTag !== null

  useEffect(() => {
    const timer = setTimeout(() => setQuery(search.trim()), 250)
    return () => clearTimeout(timer)
  }, [search])

  useEffect(() => {
    const controller = new AbortController()
    fetchHealth(controller.signal)
      .then((result) => setHealth(`api ${result.status} / db ${result.db}`))
      .catch(() => {
        if (!controller.signal.aborted) setHealth('api unreachable')
      })
    return () => controller.abort()
  }, [])

  useEffect(() => {
    const controller = new AbortController()
    listBookmarks({ q: query, tag: activeTag ?? undefined }, controller.signal)
      .then((items) => setResult({ key: requestKey, state: { status: 'ready', items } }))
      .catch(() => {
        if (!controller.signal.aborted) setResult({ key: requestKey, state: { status: 'error' } })
      })
    return () => controller.abort()
  }, [requestKey, query, activeTag])

  useEffect(() => {
    const controller = new AbortController()
    listTags(controller.signal)
      .then((counts) => {
        setTags(counts)
        setTagsFailed(false)
      })
      .catch(() => {
        if (!controller.signal.aborted) setTagsFailed(true)
      })
    return () => controller.abort()
  }, [reloadKey])

  function reload() {
    setReloadKey((key) => key + 1)
  }

  function clearFilters() {
    setSearch('')
    setQuery('')
    setActiveTag(null)
  }

  async function handleDelete(bookmark: Bookmark) {
    setDeletingId(bookmark.id)
    setActionError(null)
    try {
      await deleteBookmark(bookmark.id)
    } catch (error) {
      // A 404 means it is already gone, so the reload below shows the truth either way.
      if (!(error instanceof ApiError && error.status === 404)) {
        setActionError(`Couldn't delete "${bookmark.title}". Try again.`)
      }
    } finally {
      setDeletingId(null)
      reload()
    }
  }

  const count = list.status === 'ready' ? list.items.length : null

  return (
    <div className="app">
      <header className="header">
        <div>
          <h1>Bookmarks</h1>
          <p className="subtitle">Save links, tag them, and find them again.</p>
        </div>
        <span className="pill" data-testid="health">
          {health}
        </span>
      </header>

      <div className="layout">
        <section className="main" aria-label="Your bookmarks">
          <input
            data-testid="search-input"
            className="search"
            type="search"
            value={search}
            onChange={(event) => setSearch(event.target.value)}
            placeholder="Search titles and URLs"
            aria-label="Search bookmarks"
          />

          {tags.length > 0 && <TagChips tags={tags} activeTag={activeTag} onSelect={setActiveTag} />}
          {tagsFailed && <p className="hint">Tags are unavailable right now.</p>}

          <div className="section-head">
            <h2>{filtered ? 'Matching bookmarks' : 'All bookmarks'}</h2>
            {count !== null && <span className="count">{count}</span>}
          </div>

          {actionError && <p role="alert" className="banner">{actionError}</p>}

          <BookmarkList
            state={list}
            filtered={filtered}
            deletingId={deletingId}
            onDelete={handleDelete}
            onRetry={reload}
            onClearFilters={clearFilters}
          />
        </section>

        <aside className="aside">
          <AddBookmarkForm onSaved={reload} />
        </aside>
      </div>
    </div>
  )
}
