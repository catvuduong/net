import { useEffect, useState } from 'react'

const API_URL = import.meta.env.VITE_API_URL ?? 'http://localhost:3001'

export default function App() {
  const [health, setHealth] = useState('checking...')

  useEffect(() => {
    fetch(`${API_URL}/health`)
      .then((r) => r.json())
      .then((d) => setHealth(`api ${d.status} / db ${d.db}`))
      .catch(() => setHealth('api unreachable'))
  }, [])

  return (
    <main style={{ padding: 32, fontFamily: 'system-ui' }}>
      <h1>net</h1>
      <p data-testid="health">{health}</p>
    </main>
  )
}
