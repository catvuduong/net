import type { TagCount } from '../api'

type TagChipsProps = {
  tags: TagCount[]
  activeTag: string | null
  onSelect: (tag: string | null) => void
}

export function TagChips({ tags, activeTag, onSelect }: TagChipsProps) {
  return (
    <div className="chips" role="group" aria-label="Filter by tag">
      <button
        type="button"
        data-testid="tag-chip"
        className="chip"
        aria-pressed={activeTag === null}
        onClick={() => onSelect(null)}
      >
        All
      </button>
      {tags.map(({ tag, count }) => (
        <button
          key={tag}
          type="button"
          data-testid="tag-chip"
          className="chip"
          aria-pressed={activeTag === tag}
          onClick={() => onSelect(activeTag === tag ? null : tag)}
        >
          #{tag} <span className="chip-count">{count}</span>
        </button>
      ))}
    </div>
  )
}
