export const JOB_HISTORY_KEY = 'video_analytics_jobs'
export const ALGO_STORAGE_KEY = 'video_analytics_algo_params_json'

export function getJobHistory() {
  try {
    const value = JSON.parse(localStorage.getItem(JOB_HISTORY_KEY) || '[]')
    return Array.isArray(value) ? value.filter(Boolean) : []
  } catch {
    return []
  }
}

export function saveJobHistory(list) {
  localStorage.setItem(JOB_HISTORY_KEY, JSON.stringify((list || []).slice(0, 20)))
}

export function pushJobHistory(jobNo) {
  const list = [jobNo, ...getJobHistory().filter((item) => item !== jobNo)]
  saveJobHistory(list)
}

export function removeJobHistory(jobNo) {
  saveJobHistory(getJobHistory().filter((item) => item !== jobNo))
}

export function calcProgress(job) {
  const videos = job?.videos || []
  if (!videos.length) return 0
  const done = videos.filter((item) => [2, 3].includes(Number(item.status))).length
  return Math.round((done / videos.length) * 100)
}

export function getStatusText(status) {
  switch (Number(status)) {
    case 0: return '等待中'
    case 1: return '分析中'
    case 2: return '已完成'
    case 3: return '失败'
    case 4: return '已取消'
    default: return String(status ?? '-')
  }
}

export function getStatusType(status) {
  switch (Number(status)) {
    case 2: return 'success'
    case 1: return 'primary'
    case 3: return 'danger'
    case 4: return 'info'
    default: return 'warning'
  }
}

export function getTagClassByType(type) {
  switch (type) {
    case 'success': return 'el-tag--success'
    case 'primary': return 'el-tag--primary'
    case 'danger': return 'el-tag--danger'
    case 'info': return 'el-tag--info'
    default: return 'el-tag--warning'
  }
}

export function getJobStatusText(job) {
  const status = Number(job?.status)
  if (status === 1) {
    const totalVideoCount = Number(job?.totalVideoCount ?? 0)
    const videos = job?.videos || []
    if (totalVideoCount <= 0) {
      if (!videos.length) return '等待上传'
      const progress = calcProgress(job)
      if (progress >= 100) return '待关闭上传'
      return '分析中'
    }
  }
  return getStatusText(status)
}

export function getVideoStatusText(status) {
  switch (Number(status)) {
    case 0: return '待处理'
    case 1: return '处理中'
    case 2: return '完成'
    case 3: return '失败'
    default: return String(status ?? '-')
  }
}

export function toNum(value) {
  return Number(value ?? 0)
}

export function createTag(text, type) {
  return `<span class="el-tag ${getTagClassByType(type)}">${text}</span>`
}

export function buildWorkbenchUrl(jobNo) {
  const url = new URL('./VideoAnalyticsWorkbench.html', window.location.href)
  url.searchParams.set('jobNo', jobNo)
  const apiBase = new URL(window.location.href).searchParams.get('apiBase')
  if (apiBase) url.searchParams.set('apiBase', apiBase)
  return url.toString()
}

export function showToast(message, type = 'info') {
  const root = document.getElementById('toastRoot')
  if (!root) return
  const node = document.createElement('div')
  node.className = `toast toast-${type}`
  node.textContent = message
  root.appendChild(node)
  requestAnimationFrame(() => node.classList.add('is-show'))
  window.setTimeout(() => node.classList.remove('is-show'), 2600)
  window.setTimeout(() => node.remove(), 3200)
}
