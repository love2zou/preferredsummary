const DEFAULT_API_BASE = 'http://159.75.184.108:8080'
let resolvedBaseUrl = ''
let resolvingBaseUrlPromise = null

function normalizeBaseUrl(v) {
  const s = String(v || '').trim()
  if (!s || s === 'null' || s === 'undefined') return ''
  return s.replace(/\/+$/, '')
}

export function getApiBaseUrl() {
  if (resolvedBaseUrl) return resolvedBaseUrl
  const qp = new URL(window.location.href).searchParams.get('apiBase')
  if (qp) return normalizeBaseUrl(qp)
  const saved = localStorage.getItem('zwav_api_base')
  if (saved) return normalizeBaseUrl(saved)
  return DEFAULT_API_BASE
}

export function setApiBaseUrl(url) {
  const v = normalizeBaseUrl(url)
  if (!v) return
  localStorage.setItem('zwav_api_base', v)
  resolvedBaseUrl = ''
  resolvingBaseUrlPromise = null
}

export function getAuthHeaders() {
  const token = localStorage.getItem('token')
  if (token) return { Authorization: `Bearer ${token}` }
  return { 'X-Guest-Mode': 'true' }
}

export function buildUrl(baseUrl, path, params) {
  const u = new URL(path, baseUrl)
  const p = params || {}
  Object.keys(p).forEach((k) => {
    const v = p[k]
    if (v === undefined || v === null || v === '') return
    u.searchParams.set(k, String(v))
  })
  u.searchParams.set('_t', String(Date.now()))
  return u.toString()
}

async function probeBaseUrl(baseUrl) {
  try {
    const url = buildUrl(baseUrl, '/api/ZwavAnalyses', { page: 1, pageSize: 1 })
    const res = await fetch(url, { headers: getAuthHeaders() })
    if (!res.ok) return false
    const text = await res.text()
    const data = text ? JSON.parse(text) : null
    return !!(data && typeof data.success === 'boolean')
  } catch {
    return false
  }
}

async function resolveApiBaseUrl() {
  if (resolvedBaseUrl) return resolvedBaseUrl
  if (resolvingBaseUrlPromise) return resolvingBaseUrlPromise

  resolvingBaseUrlPromise = (async () => {
    const qp = normalizeBaseUrl(new URL(window.location.href).searchParams.get('apiBase'))
    const saved = normalizeBaseUrl(localStorage.getItem('zwav_api_base'))
    const origin = window.location.origin === 'null' ? '' : normalizeBaseUrl(window.location.origin)
    const candidates = [qp, saved, origin, DEFAULT_API_BASE].filter(Boolean)

    for (const c of candidates) {
      const ok = await probeBaseUrl(c)
      if (ok) {
        resolvedBaseUrl = c
        return c
      }
    }

    resolvedBaseUrl = qp || saved || origin || DEFAULT_API_BASE
    return resolvedBaseUrl
  })()

  return resolvingBaseUrlPromise
}

export async function apiJson(path, { method = 'GET', params, body, headers } = {}) {
  const baseUrl = await resolveApiBaseUrl()
  const url = buildUrl(baseUrl, path, params)
  const h = Object.assign({ 'Content-Type': 'application/json' }, getAuthHeaders(), headers || {})
  const res = await fetch(url, {
    method,
    headers: h,
    body: body === undefined ? undefined : JSON.stringify(body)
  })
  if (res.status === 401) {
    localStorage.removeItem('token')
  }
  const text = await res.text()
  let data = null
  try {
    data = text ? JSON.parse(text) : null
  } catch {
    data = null
  }
  if (!res.ok) {
    const msg = (data && (data.message || data.Message)) || text || `HTTP ${res.status}`
    throw new Error(msg)
  }
  return data
}

export async function apiBlob(path, { params } = {}) {
  const baseUrl = await resolveApiBaseUrl()
  const url = buildUrl(baseUrl, path, params)
  const res = await fetch(url, { headers: getAuthHeaders() })
  if (res.status === 401) {
    localStorage.removeItem('token')
  }
  if (!res.ok) {
    const text = await res.text().catch(() => '')
    throw new Error(text || `HTTP ${res.status}`)
  }
  const blob = await res.blob()
  const cd = res.headers.get('content-disposition') || ''
  return { blob, contentDisposition: cd }
}

export function downloadBlob(blob, fileName) {
  const url = window.URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = fileName || 'download'
  document.body.appendChild(a)
  a.click()
  document.body.removeChild(a)
  window.URL.revokeObjectURL(url)
}

export function parseFileNameFromContentDisposition(cd) {
  if (!cd) return ''
  const m = cd.match(/filename\*?=(?:UTF-8''|\"?)([^\";]+)\"?/i)
  if (m && m[1]) return decodeURIComponent(m[1].replace(/['"]/g, '').trim())
  return ''
}

export function formatFileSize(bytes) {
  if (bytes === undefined || bytes === null) return '-'
  const n = Number(bytes)
  if (!Number.isFinite(n)) return '-'
  if (n < 1024) return `${n} B`
  if (n < 1024 * 1024) return `${(n / 1024).toFixed(2)} KB`
  if (n < 1024 * 1024 * 1024) return `${(n / 1024 / 1024).toFixed(2)} MB`
  return `${(n / 1024 / 1024 / 1024).toFixed(2)} GB`
}

export function formatDateTime(str) {
  if (!str) return '-'
  const d = new Date(str)
  if (Number.isNaN(d.getTime())) return String(str)
  return d.toLocaleString()
}

export function escapeHtml(s) {
  return String(s ?? '')
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;')
    .replaceAll("'", '&#39;')
}

export function qs(name) {
  const u = new URL(window.location.href)
  return u.searchParams.get(name)
}

export function setText(id, value) {
  const el = document.getElementById(id)
  if (!el) return
  el.textContent = value ?? ''
}

export function setHtml(id, html) {
  const el = document.getElementById(id)
  if (!el) return
  el.innerHTML = html
}

export function byId(id) {
  return document.getElementById(id)
}

export function openDialog(id) {
  const el = byId(id)
  if (!el) return
  if (window.bootstrap && window.bootstrap.Modal && el.classList && el.classList.contains('modal')) {
    window.bootstrap.Modal.getOrCreateInstance(el).show()
    return
  }
  el.classList.add('is-open')
}

export function closeDialog(id) {
  const el = byId(id)
  if (!el) return
  if (window.bootstrap && window.bootstrap.Modal && el.classList && el.classList.contains('modal')) {
    window.bootstrap.Modal.getOrCreateInstance(el).hide()
    return
  }
  el.classList.remove('is-open')
}

export function bindDialog(id) {
  const mask = byId(id)
  if (!mask) return
  if (window.bootstrap && window.bootstrap.Modal && mask.classList && mask.classList.contains('modal')) return
  mask.addEventListener('click', (e) => {
    const t = e.target
    if (!t) return
    if (t === mask) closeDialog(id)
    if (t.closest && t.closest('[data-dialog-close]')) closeDialog(id)
  })
}
