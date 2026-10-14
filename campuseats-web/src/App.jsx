import { useEffect, useState } from 'react'
import './App.css'

// The API address comes from the environment. It is NOT hard-coded.
const API = (import.meta.env.VITE_API_URL || '').replace(/\/+$/, '')

function App() {
  const [items, setItems] = useState([])
  const [loading, setLoading] = useState(Boolean(API))
  const [error, setError] = useState(
    API ? '' : 'VITE_API_URL is not set. Add it as an environment variable and rebuild.'
  )

  useEffect(() => {
    if (!API) return
    const controller = new AbortController()

    fetch(`${API}/api/menu`, { signal: controller.signal })
      .then((res) => {
        if (!res.ok) throw new Error(`API returned status ${res.status}`)
        return res.json()
      })
      .then((data) => {
        setItems(data)
        setLoading(false)
      })
      .catch((err) => {
        if (err.name === 'AbortError') return
        setError(`Could not load the menu. ${err.message}`)
        setLoading(false)
      })

    return () => controller.abort()
  }, [])

  return (
    <div className="page">
      <header className="header">
        <h1>CampusEats</h1>
        <p>Order food on campus. Fast and simple.</p>
      </header>

      <main>
        <h2>Today&apos;s menu</h2>

        {loading && (
          <p className="info">
            Loading menu... The free server may be asleep. The first request can take up to 60 seconds.
          </p>
        )}
        {error && <p className="error">{error}</p>}

        <div className="grid">
          {items.map((item) => (
            <article className="card" key={item.id}>
              <span className="badge">{item.category}</span>
              <h3>{item.name}</h3>
              <p>{item.description}</p>
              <strong>Rs. {item.price.toFixed(2)}</strong>
            </article>
          ))}
        </div>
      </main>

      <footer className="footer">
        <small>API: {API || '(not set)'} · Deployed automatically with CI/CD</small>
      </footer>
    </div>
  )
}

export default App
