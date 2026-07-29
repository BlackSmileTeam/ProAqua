import { useEffect, useState } from 'react'
import api from '../api'

export default function ApiBanner() {
  const [down, setDown] = useState(false)

  useEffect(() => {
    let cancelled = false
    async function check() {
      try {
        await api.get('/api/health', { timeout: 3000 })
        if (!cancelled) setDown(false)
      } catch {
        if (!cancelled) setDown(true)
      }
    }
    check()
    const id = setInterval(check, 15000)
    return () => { cancelled = true; clearInterval(id) }
  }, [])

  if (!down) return null

  return (
    <div className="api-banner" role="alert">
      <strong>API недоступен.</strong>{' '}
      Запустите backend: <code>cd backend/ProAqua.Api &amp;&amp; dotnet run</code> (порт 5080).
      Админка проксирует <code>/api</code> на localhost:5080.
    </div>
  )
}
