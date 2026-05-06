const DEFAULT_API_BASE_PROD = 'http://159.75.184.108:8080'
const DEFAULT_API_BASE_DEV = 'http://localhost:5000'
const API_BASE_STORAGE_KEY = 'video_analytics_api_base'

export function normalizeBaseUrl(value) {
  const text = String(value || '').trim()
  if (!text || text === 'null' || text === 'undefined') return ''
  return text.replace(/\/+$/, '')
}

export function isLocalHostName(hostname) {
  const text = String(hostname || '').trim().toLowerCase()
  return text === 'localhost' || text === '127.0.0.1' || text === '0.0.0.0'
}

export function getDefaultApiBaseUrl() {
  try {
    if (window.location.protocol === 'file:' || isLocalHostName(window.location.hostname)) {
      return DEFAULT_API_BASE_DEV
    }
  } catch {
    return DEFAULT_API_BASE_PROD
  }
  return DEFAULT_API_BASE_PROD
}

export function getApiBaseUrl() {
  const qp = normalizeBaseUrl(new URL(window.location.href).searchParams.get('apiBase'))
  if (qp) return qp
  const saved = normalizeBaseUrl(localStorage.getItem(API_BASE_STORAGE_KEY))
  if (saved) return saved
  return getDefaultApiBaseUrl()
}

export function setApiBaseUrl(url) {
  const value = normalizeBaseUrl(url)
  if (!value) return
  localStorage.setItem(API_BASE_STORAGE_KEY, value)
}

export function getAuthHeaders() {
  const token = localStorage.getItem('token')
  if (token) return { Authorization: `Bearer ${token}` }
  return { 'X-Guest-Mode': 'true' }
}

export function buildUrl(baseUrl, path, params) {
  const url = new URL(path, baseUrl)
  const data = params || {}
  Object.keys(data).forEach((key) => {
    const value = data[key]
    if (value === undefined || value === null || value === '') return
    url.searchParams.set(key, String(value))
  })
  url.searchParams.set('_t', String(Date.now()))
  return url.toString()
}

async function parseResponse(res) {
  const text = await res.text()
  let data = null
  try {
    data = text ? JSON.parse(text) : null
  } catch {
    data = null
  }

  if (!res.ok) {
    const message = (data && (data.message || data.Message)) || text || `HTTP ${res.status}`
    throw new Error(message)
  }
  return data
}

export async function apiJson(path, { method = 'GET', params, body, headers } = {}) {
  const url = buildUrl(getApiBaseUrl(), path, params)
  const finalHeaders = Object.assign({ 'Content-Type': 'application/json' }, getAuthHeaders(), headers || {})

  const res = await fetch(url, {
    method,
    headers: finalHeaders,
    body: body === undefined ? undefined : JSON.stringify(body)
  })

  if (res.status === 401) {
    localStorage.removeItem('token')
  }

  return parseResponse(res)
}

export async function apiForm(path, formData, { method = 'POST', params, headers } = {}) {
  const url = buildUrl(getApiBaseUrl(), path, params)
  const finalHeaders = Object.assign({}, getAuthHeaders(), headers || {})
  delete finalHeaders['Content-Type']

  const res = await fetch(url, {
    method,
    headers: finalHeaders,
    body: formData
  })

  if (res.status === 401) {
    localStorage.removeItem('token')
  }

  return parseResponse(res)
}

export async function apiBlob(path, { params } = {}) {
  const url = buildUrl(getApiBaseUrl(), path, params)
  const res = await fetch(url, {
    headers: getAuthHeaders()
  })

  if (res.status === 401) {
    localStorage.removeItem('token')
  }

  if (!res.ok) {
    const text = await res.text().catch(() => '')
    throw new Error(text || `HTTP ${res.status}`)
  }

  return {
    blob: await res.blob(),
    contentDisposition: res.headers.get('content-disposition') || ''
  }
}

export function parseFileNameFromContentDisposition(contentDisposition) {
  if (!contentDisposition) return ''
  const match = contentDisposition.match(/filename\*?=(?:UTF-8''|\"?)([^\";]+)\"?/i)
  if (!match || !match[1]) return ''
  return decodeURIComponent(match[1].replace(/['"]/g, '').trim())
}

export function downloadBlob(blob, fileName) {
  const objectUrl = window.URL.createObjectURL(blob)
  const anchor = document.createElement('a')
  anchor.href = objectUrl
  anchor.download = fileName || 'download'
  document.body.appendChild(anchor)
  anchor.click()
  anchor.remove()
  window.URL.revokeObjectURL(objectUrl)
}

export function escapeHtml(value) {
  return String(value ?? '')
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;')
    .replaceAll("'", '&#39;')
}

export function qs(name) {
  return new URL(window.location.href).searchParams.get(name)
}

export function byId(id) {
  return document.getElementById(id)
}

export function setText(id, value) {
  const el = byId(id)
  if (el) el.textContent = value ?? ''
}

export function setHtml(id, value) {
  const el = byId(id)
  if (el) el.innerHTML = value ?? ''
}

export function setHidden(id, hidden) {
  const el = byId(id)
  if (!el) return
  el.classList.toggle('is-hidden', !!hidden)
}

export function openDialog(id) {
  const el = byId(id)
  if (el) el.classList.add('is-open')
}

export function closeDialog(id) {
  const el = byId(id)
  if (el) el.classList.remove('is-open')
}

export function bindDialog(id) {
  const mask = byId(id)
  if (!mask) return
  mask.addEventListener('click', (event) => {
    const target = event.target
    if (!(target instanceof HTMLElement)) return
    if (target === mask) closeDialog(id)
    const closeTarget = target.closest('[data-dialog-close]')
    if (closeTarget) closeDialog(closeTarget.getAttribute('data-dialog-close'))
  })
}

export function formatDateTime(value) {
  if (!value) return '-'
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return String(value)
  return date.toLocaleString('zh-CN', { hour12: false })
}

export function formatTime(sec) {
  const num = Number(sec || 0)
  const mm = Math.floor(num / 60)
  const ss = Math.floor(num % 60)
  return `${String(mm).padStart(2, '0')}:${String(ss).padStart(2, '0')}`
}

export function formatDurationSeconds(sec) {
  const num = Number(sec)
  if (!Number.isFinite(num)) return '-'
  if (num <= 0) return '0s'
  if (num < 60) return `${Math.round(num)}s`
  const mm = Math.floor(num / 60)
  const ss = Math.round(num % 60)
  return `${mm}m ${ss}s`
}
