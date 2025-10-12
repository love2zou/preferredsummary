import { API_CONFIG } from '@/config/api'

export const getServerUrl = (path: string): string => {
  if (!path) return ''
  const lower = path.toLowerCase()
  // 保留 blob/data 直接返回
  if (lower.startsWith('blob:') || lower.startsWith('data:')) {
    return path
  }
  // 对 http(s) 做跨主机统一：不同源时改用当前 BASE_URL 的源 + pathname
  if (lower.startsWith('http')) {
    try {
      const incoming = new URL(path)
      const current = new URL(API_CONFIG.BASE_URL)
      if (incoming.origin !== current.origin) {
        const base = current.origin.replace(/\/+$/, '')
        const pathname = incoming.pathname.startsWith('/') ? incoming.pathname : `/${incoming.pathname}`
        return `${base}${pathname}`
      }
      return path
    } catch {
      // 解析失败则按相对路径逻辑处理
    }
  }
  // 处理相对路径与反斜杠
  const normalized = path.replace(/\\/g, '/')
  const withSlash = normalized.startsWith('/') ? normalized : `/${normalized}`
  const base = (API_CONFIG.BASE_URL || '').replace(/\/+$/, '')
  return `${base}${withSlash}`
}

export const toServerPath = (path: string): string => {
  if (!path) return ''
  const lower = path.toLowerCase()
  if (lower.startsWith('http')) {
    try {
      const u = new URL(path)
      // 始终返回 pathname 供后端识别，如 /upload/images/xxx.jpg
      return u.pathname.startsWith('/') ? u.pathname : `/${u.pathname}`
    } catch {
      // 解析失败时退化为相对路径处理
    }
  }
  const normalized = path.replace(/\\/g, '/')
  return normalized.startsWith('/') ? normalized : `/${normalized}`
}