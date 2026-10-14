import { useEffect, useState, useRef } from 'react'
import './App.css'

// The API base URL comes strictly from the environment variable (VITE_API_URL).
// Safely trim whitespace and remove any trailing slashes.
const rawApi = import.meta.env.VITE_API_URL || ''
const API = rawApi.trim().replace(/\/+$/, '')

function App() {
  const [items, setItems] = useState([])
  const [loading, setLoading] = useState(Boolean(API))
  const [dataSource, setDataSource] = useState(null)
  const [error, setError] = useState(
    API ? '' : 'VITE_API_URL is not configured. Add it in Vercel or your local .env and rebuild.'
  )
  const [reloadTrigger, setReloadTrigger] = useState(0)
  const isRetryingRef = useRef(false)

  useEffect(() => {
    if (!API) return

    const controller = new AbortController()

    fetch(`${API}/api/menu`, { signal: controller.signal })
      .then(async (res) => {
        const sourceHeader = res.headers.get('X-Data-Source')
        if (sourceHeader) {
          setDataSource(sourceHeader)
        }

        if (!res.ok) {
          let errorDetail = `HTTP ${res.status}`
          try {
            const errorJson = await res.json()
            if (errorJson && errorJson.message) {
              errorDetail = errorJson.message
            }
          } catch {
            if (res.statusText) {
              errorDetail = `HTTP ${res.status}: ${res.statusText}`
            }
          }
          throw new Error(errorDetail)
        }

        return res.json()
      })
      .then((data) => {
        setItems(Array.isArray(data) ? data : [])
        setError('')
        setLoading(false)
      })
      .catch((err) => {
        if (err.name === 'AbortError') return

        let message = err.message
        if (err.message.includes('Failed to fetch') || err.message.includes('NetworkError')) {
          message = `Could not connect to backend at ${API}. The server may be starting up, offline, or inaccessible.`
        }

        setError(message)
        setLoading(false)
      })

    return () => controller.abort()
  }, [reloadTrigger])

  const handleManualRetry = () => {
    if (isRetryingRef.current) return
    isRetryingRef.current = true
    setLoading(true)
    setError('')
    setReloadTrigger((prev) => prev + 1)
    setTimeout(() => {
      isRetryingRef.current = false
    }, 500)
  }

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
            Loading menu from database... If the cloud server is waking from idle, this may take a few moments.
          </p>
        )}

        {error && (
          <div className="error-box">
            <p className="error">{error}</p>
            <button
              type="button"
              className="retry-btn"
              onClick={handleManualRetry}
              disabled={loading}
            >
              {loading ? 'Retrying...' : 'Retry Connection'}
            </button>
          </div>
        )}

        {!loading && !error && items.length === 0 && (
          <div className="empty-state">
            <p>No menu items found in the database. Please check back shortly.</p>
          </div>
        )}

        <div className="grid">
          {items.map((item) => (
            <article className="card" key={item.id}>
              <span className="badge">{item.category}</span>
              <h3>{item.name}</h3>
              <p>{item.description}</p>
              <strong>
                Rs. {typeof item.price === 'number' ? item.price.toFixed(2) : item.price}
              </strong>
            </article>
          ))}
        </div>
      </main>

      <footer className="footer">
        <small>
          API: {API || '(not set)'}
          {dataSource ? ` · Source: ${dataSource}` : ''}
          {' '}· Deployed automatically with CI/CD
        </small>
      </footer>
    </div>
  )
}

export default App
