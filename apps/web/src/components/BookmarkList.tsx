import type { Bookmark } from '../api'

export type ListState =
  | { status: 'loading' }
  | { status: 'error' }
  | { status: 'ready'; items: Bookmark[] }

type BookmarkListProps = {
  state: ListState
  filtered: boolean
  deletingId: number | null
  onDelete: (bookmark: Bookmark) => void
  onRetry: () => void
  onClearFilters: () => void
}

export function BookmarkList({ state, filtered, deletingId, onDelete, onRetry, onClearFilters }: BookmarkListProps) {
  if (state.status === 'loading') {
    return (
      <ul className="list" aria-busy="true" aria-label="Loading bookmarks">
        {[0, 1, 2].map((i) => (
          <li key={i} className="card skeleton" aria-hidden="true" />
        ))}
      </ul>
    )
  }

  if (state.status === 'error') {
    return (
      <div className="state" role="alert">
        <p>Couldn't load your bookmarks.</p>
        <button type="button" className="button" onClick={onRetry}>
          Try again
        </button>
      </div>
    )
  }

  if (state.items.length === 0) {
    return filtered ? (
      <div className="state">
        <p>No bookmarks match these filters.</p>
        <button type="button" className="button" onClick={onClearFilters}>
          Clear filters
        </button>
      </div>
    ) : (
      <div className="state">
        <p>No bookmarks yet. Add your first link to get started.</p>
      </div>
    )
  }

  return (
    <ul className="list">
      {state.items.map((bookmark) => (
        <BookmarkCard
          key={bookmark.id}
          bookmark={bookmark}
          deleting={deletingId === bookmark.id}
          onDelete={onDelete}
        />
      ))}
    </ul>
  )
}

function BookmarkCard({
  bookmark,
  deleting,
  onDelete,
}: {
  bookmark: Bookmark
  deleting: boolean
  onDelete: (bookmark: Bookmark) => void
}) {
  const host = hostOf(bookmark.url)

  return (
    <li className="card" data-testid="bookmark-item">
      <span className="monogram" style={{ background: `hsl(${hueOf(host)} 62% 44%)` }} aria-hidden="true">
        {host.charAt(0).toUpperCase()}
      </span>
      <div className="card-body">
        <a className="card-title" href={bookmark.url} target="_blank" rel="noreferrer">
          {bookmark.title}
        </a>
        <div className="card-meta">
          <span>{host}</span>
          <span aria-hidden="true">·</span>
          <time dateTime={bookmark.createdAt}>{formatDate(bookmark.createdAt)}</time>
        </div>
        {bookmark.tags.length > 0 && (
          <ul className="tags" aria-label="Tags">
            {bookmark.tags.map((tag) => (
              <li key={tag} className="tag">
                {tag}
              </li>
            ))}
          </ul>
        )}
      </div>
      <button
        type="button"
        className="delete"
        onClick={() => onDelete(bookmark)}
        disabled={deleting}
        aria-label={`Delete ${bookmark.title}`}
      >
        {deleting ? 'Deleting…' : 'Delete'}
      </button>
    </li>
  )
}

function hostOf(url: string) {
  try {
    return new URL(url).hostname.replace(/^www\./, '')
  } catch {
    return url
  }
}

// Stable colour per site so the monograms vary without storing anything.
function hueOf(text: string) {
  let hash = 0
  for (const char of text) hash = (hash * 31 + char.charCodeAt(0)) % 360
  return hash
}

function formatDate(iso: string) {
  return new Date(iso).toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' })
}
