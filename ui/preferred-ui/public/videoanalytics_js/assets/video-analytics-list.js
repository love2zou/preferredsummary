import { bindDialog, byId, closeDialog, escapeHtml, formatDateTime, getApiBaseUrl, openDialog, setApiBaseUrl, setHtml } from './common.js'
import { videoAnalyticsApi } from './video-analytics-api.js'
import { buildWorkbenchUrl, calcProgress, createTag, getJobHistory, getJobStatusText, getStatusType, pushJobHistory, removeJobHistory, showToast } from './video-analytics-shared.js'

const state = {
  loading: false,
  searchJobNo: '',
  jobList: []
}

function renderTable() {
  if (state.loading) {
    setHtml('jobTableBody', '<tr><td colspan="7" class="cell-empty">加载中...</td></tr>')
    return
  }

  if (!state.jobList.length) {
    setHtml('jobTableBody', '<tr><td colspan="7" class="cell-empty">暂无任务数据</td></tr>')
    return
  }

  const rows = state.jobList.map((row, index) => {
    const progress = Number(row.progress ?? calcProgress(row))
    const statusType = getStatusType(row.status)
    const videoCount = (row.videos && row.videos.length) || 0
    return `
      <tr>
        <td class="cell-center">${index + 1}</td>
        <td><span class="link-text" data-action="enter-workbench" data-job-no="${escapeHtml(row.jobNo)}">${escapeHtml(row.jobNo)}</span></td>
        <td>${createTag(escapeHtml(getJobStatusText(row)), statusType)}</td>
        <td>
          <div class="el-progress">
            <div class="el-progress-bar">
              <div class="el-progress-bar__inner ${Number(row.status) === 3 ? 'is-exception' : ''}" style="width:${progress}%"></div>
            </div>
            <span class="el-progress__text">${progress}%</span>
          </div>
        </td>
        <td>${escapeHtml(formatDateTime(row.startTime))}</td>
        <td class="cell-center">${videoCount}</td>
        <td class="cell-actions">
          <button class="el-button is-link" data-action="enter-workbench" data-job-no="${escapeHtml(row.jobNo)}" type="button">进入工作台</button>
          <button class="el-button is-link danger-link" data-action="delete-job" data-job-no="${escapeHtml(row.jobNo)}" type="button">删除</button>
        </td>
      </tr>
    `
  }).join('')

  setHtml('jobTableBody', rows)
}

async function fetchJobList() {
  state.loading = true
  renderTable()

  const list = []
  if (state.searchJobNo.trim()) {
    try {
      const res = await videoAnalyticsApi.getJob(state.searchJobNo.trim())
      if (res?.success && res.data) list.push(res.data)
    } catch {
      showToast('未查询到该任务', 'warning')
    }
  } else {
    const recent = getJobHistory().slice(0, 10)
    const toRemove = []
    await Promise.all(recent.map(async (jobNo) => {
      try {
        const res = await videoAnalyticsApi.getJob(jobNo)
        if (res?.success && res.data) list.push(res.data)
      } catch {
        toRemove.push(jobNo)
      }
    }))
    if (toRemove.length) {
      toRemove.forEach(removeJobHistory)
    }
  }

  state.jobList = list.sort((a, b) => {
    const ta = a.startTime ? new Date(a.startTime).getTime() : 0
    const tb = b.startTime ? new Date(b.startTime).getTime() : 0
    return tb - ta
  })
  state.loading = false
  renderTable()
}

async function createJobAndEnter() {
  const btn = byId('btnCreateJob')
  btn.disabled = true
  btn.textContent = '创建中...'
  try {
    const res = await videoAnalyticsApi.createJob('')
    if (!res?.success) {
      showToast(res?.message || '创建失败', 'error')
      return
    }
    pushJobHistory(res.data.jobNo)
    closeDialog('createDialog')
    window.location.href = buildWorkbenchUrl(res.data.jobNo)
  } catch {
    showToast('创建失败', 'error')
  } finally {
    btn.disabled = false
    btn.textContent = '创建并进入工作台'
  }
}

async function deleteJob(jobNo) {
  if (!window.confirm(`确认删除任务 ${jobNo} ?`)) return
  try {
    const res = await videoAnalyticsApi.deleteJob(jobNo)
    if (!res?.success) {
      showToast(res?.message || '删除失败', 'error')
      return
    }
    removeJobHistory(jobNo)
    showToast('删除成功', 'success')
    await fetchJobList()
  } catch {
    showToast('删除请求失败', 'error')
  }
}

function bindEvents() {
  bindDialog('apiDialog')
  bindDialog('createDialog')

  byId('searchJobNo').addEventListener('input', (e) => {
    state.searchJobNo = e.target.value
  })
  byId('searchJobNo').addEventListener('keydown', (e) => {
    if (e.key === 'Enter') fetchJobList()
  })
  byId('btnSearchJob').addEventListener('click', fetchJobList)
  byId('btnResetSearch').addEventListener('click', () => {
    state.searchJobNo = ''
    byId('searchJobNo').value = ''
    fetchJobList()
  })

  byId('btnOpenCreateDialog').addEventListener('click', () => openDialog('createDialog'))
  byId('btnCreateJob').addEventListener('click', createJobAndEnter)

  byId('btnOpenApiConfig').addEventListener('click', () => {
    byId('apiBaseInput').value = getApiBaseUrl()
    openDialog('apiDialog')
  })

  byId('btnSaveApiBase').addEventListener('click', () => {
    const value = byId('apiBaseInput').value.trim()
    if (!value) {
      showToast('请输入 API Base URL', 'warning')
      return
    }
    setApiBaseUrl(value)
    closeDialog('apiDialog')
    showToast('接口配置已保存', 'success')
    fetchJobList()
  })

  document.addEventListener('click', async (e) => {
    const target = e.target
    if (!(target instanceof HTMLElement)) return
    const actionEl = target.closest('[data-action]')
    if (!actionEl) return

    const action = actionEl.getAttribute('data-action')
    const jobNo = actionEl.getAttribute('data-job-no') || ''
    if (action === 'enter-workbench') {
      pushJobHistory(jobNo)
      window.location.href = buildWorkbenchUrl(jobNo)
    }
    if (action === 'delete-job') {
      await deleteJob(jobNo)
    }
  })
}

async function init() {
  bindEvents()
  await fetchJobList()
}

init()
