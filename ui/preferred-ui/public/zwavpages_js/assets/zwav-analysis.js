import { bindDialog, byId, closeDialog, escapeHtml, formatDateTime, formatFileSize, getApiBaseUrl, openDialog, parseFileNameFromContentDisposition, setApiBaseUrl, setText } from './common.js'
import { zwavApi } from './zwav-api.js'

const state = {
  loading: false,
  page: 1,
  pageSize: 20,
  total: 0,
  totalPages: 1,
  data: [],
  selected: new Set(),
  polling: null,
  uploading: false
}

function statusClass(status) {
  if (!status) return 'parsing'
  const s = String(status)
  if (s === 'Queued') return 'queued'
  if (s === 'Completed') return 'completed'
  if (s === 'Failed') return 'failed'
  return 'parsing'
}

function statusText(status) {
  const map = {
    Queued: '排队中',
    Canceled: '已取消',
    Completed: '已完成',
    Failed: '失败',
    ParsingRead: '读取录波',
    ParsingExtract: '解压中',
    ParsingCfg: '解析CFG',
    ParsingHdr: '解析HDR',
    ParsingChannel: '解析通道',
    ParsingDat: '解析Dat数据',
    Parsing: '解析中'
  }
  return map[status] || status || '-'
}

function render() {
  const tbody = byId('tbody')
  if (!tbody) return

  const rows = state.data || []
  const pageStartIndex = (state.page - 1) * state.pageSize

  tbody.innerHTML = rows
    .map((r, idx) => {
      const guid = r.analysisGuid
      const checked = state.selected.has(guid) ? 'checked' : ''
      const errorText = r.errorMessage ? `<span class="text-danger">${escapeHtml(r.errorMessage)}</span>` : '-'
      const pct = Number(r.progress || 0)
      const pctText = Number.isFinite(pct) ? pct : 0
      const pillClass = statusClass(r.status)
      const tagType = r.status === 'Failed' ? 'el-tag--danger' : r.status === 'Completed' ? 'el-tag--success' : r.status === 'Queued' ? 'el-tag--warning' : 'el-tag--primary'
      const pill = `<span class="el-tag ${tagType}">${escapeHtml(statusText(r.status))}</span>`
      const innerCls = r.status === 'Failed' ? 'is-exception' : r.status === 'Completed' ? 'is-success' : ''
      const progressBar = `<div class="el-progress"><div class="el-progress-bar"><div class="el-progress-bar__inner ${innerCls}" style="width:${pctText}%"></div></div><div class="el-progress__text">${pctText}%</div></div>`
      return `
         <tr>
           <td class="cell-center"><input type="checkbox" class="row-check" data-guid="${escapeHtml(guid)}" ${checked}></td>
           <td class="cell-center">${pageStartIndex + idx + 1}</td>
           <td class="file-name-cell" title="${escapeHtml(r.originalName || '')}">${escapeHtml(r.originalName || '-')}</td>
           <td>${formatFileSize(r.fileSize)}</td>
           <td>${pill}</td>
           <td>${progressBar}</td>
           <td>${escapeHtml(formatDateTime(r.crtTime))}</td>
           <td>${errorText}</td>
           <td class="cell-center">
             <div class="cell-actions">
               <button class="el-button is-link action-view" data-guid="${escapeHtml(guid)}">在线浏览</button>
               <button class="el-button is-link action-download" data-guid="${escapeHtml(guid)}">下载</button>
               <button class="el-button is-link action-delete" data-guid="${escapeHtml(guid)}" style="color: var(--el-color-danger)">删除</button>
             </div>
           </td>
         </tr>
       `
    })
    .join('')

  byId('checkAll').checked = rows.length > 0 && rows.every((r) => state.selected.has(r.analysisGuid))
  byId('btnBatchDelete').disabled = state.selected.size === 0
  setText('pageText', String(state.page))
  setText('totalText', `共 ${state.total || 0} 条，页大小 ${state.pageSize}`)
}

async function fetchList({ silent } = {}) {
  if (state.loading) return
  state.loading = true
  try {
    const keyword = byId('keyword').value || ''
    const status = byId('status').value || ''
    const fromDate = byId('fromDate').value || ''
    const toDate = byId('toDate').value || ''

    const params = {
      page: state.page,
      pageSize: state.pageSize,
      keyword,
      status
    }

    if (fromDate && toDate) {
      const fromUtc = new Date(fromDate).toISOString()
      const end = new Date(toDate)
      end.setDate(end.getDate() + 1)
      const toUtc = end.toISOString()
      params.fromUtc = fromUtc
      params.toUtc = toUtc
    }

    const res = await zwavApi.getList(params)
    if (res && res.success) {
      state.data = (res.data && res.data.data) || []
      state.total = (res.data && res.data.total) || 0
      state.totalPages = (res.data && res.data.totalPages) || 1
    } else if (!silent) {
      alert((res && res.message) || '获取列表失败')
    }
  } catch (e) {
    if (!silent) alert(e.message || '获取列表失败')
  } finally {
    state.loading = false
    render()
  }
}

function openApiModal() {
  byId('apiBaseInput').value = getApiBaseUrl()
  openDialog('apiModal')
}

function openUploadModal() {
  byId('fileInput').value = ''
  byId('uploadList').innerHTML = ''
  openDialog('uploadModal')
}

function renderUploadList(files) {
  const list = byId('uploadList')
  list.innerHTML = files
    .map((f) => {
      const id = `up_${f._id}`
      const barId = `bar_${f._id}`
      const textId = `txt_${f._id}`
      return `
        <div style="border:1px solid var(--el-border-color); border-radius:4px; padding:10px;">
          <div style="display:flex; justify-content:space-between; align-items:center; gap:10px;">
            <div style="font-weight:700; color: var(--el-text-color-primary);" id="${id}">${escapeHtml(f.name)}</div>
            <div style="font-size:12px; color: var(--el-text-color-secondary);" id="${textId}">等待上传</div>
          </div>
          <div class="el-progress" style="margin-top:8px;">
            <div class="el-progress-bar"><div class="el-progress-bar__inner" id="${barId}" style="width:0%"></div></div>
            <div class="el-progress__text"></div>
          </div>
        </div>
       `
    })
    .join('')
}

async function startUpload() {
  if (state.uploading) return
  const input = byId('fileInput')
  const rawFiles = Array.from(input.files || [])
  if (rawFiles.length === 0) {
    alert('请先选择文件')
    return
  }

  const files = rawFiles.map((f, i) => Object.assign(f, { _id: `${Date.now()}_${i}` }))
  renderUploadList(files)

  state.uploading = true
  try {
    for (const file of files) {
      const bar = byId(`bar_${file._id}`)
      const txt = byId(`txt_${file._id}`)

      txt.textContent = '上传中...'
      const uploadRes = await zwavApi.uploadFile(file, (pct) => {
        if (bar) bar.style.width = `${pct}%`
      })

      if (!uploadRes || !uploadRes.success) {
        txt.textContent = (uploadRes && uploadRes.message) || '上传失败'
        if (bar) bar.classList.add('bg-danger')
        continue
      }

      txt.textContent = '创建任务中...'
      const fileId = uploadRes.data && uploadRes.data.fileId
      const createRes = await zwavApi.createAnalysis(fileId, false)
      if (createRes && createRes.success) {
        txt.textContent = '已完成'
        if (bar) {
          bar.style.width = '100%'
          bar.classList.add('bg-success')
        }
      } else {
        txt.textContent = (createRes && createRes.message) || '创建任务失败'
        if (bar) bar.classList.add('bg-danger')
      }
    }

    await fetchList({ silent: true })
  } finally {
    state.uploading = false
  }
}

async function deleteOne(guid) {
  if (!confirm('确定删除该任务和文件吗？')) return
  try {
    const res = await zwavApi.deleteAnalysis(guid, true)
    if (res && res.success) {
      state.selected.delete(guid)
      await fetchList({ silent: true })
    } else {
      alert((res && res.message) || '删除失败')
    }
  } catch (e) {
    alert(e.message || '删除失败')
  }
}

async function batchDelete() {
  if (state.selected.size === 0) return
  if (!confirm('确定要删除选中的任务和文件吗？')) return

  const ids = Array.from(state.selected)
  let ok = 0
  let fail = 0
  for (const guid of ids) {
    try {
      const res = await zwavApi.deleteAnalysis(guid, true)
      if (res && res.success) ok++
      else fail++
    } catch {
      fail++
    }
  }
  alert(`删除完成：成功 ${ok} 条，失败 ${fail} 条`)
  state.selected.clear()
  await fetchList({ silent: true })
}

async function downloadOne(guid, originalName) {
  try {
    const res = await zwavApi.downloadFile(guid)
    const nameFromHeader = parseFileNameFromContentDisposition(res.contentDisposition)
    const name = nameFromHeader || originalName || 'download.zwav'
    const a = document.createElement('a')
    const url = window.URL.createObjectURL(res.blob)
    a.href = url
    a.download = name
    document.body.appendChild(a)
    a.click()
    document.body.removeChild(a)
    window.URL.revokeObjectURL(url)
  } catch (e) {
    alert(e.message || '下载失败')
  }
}

function viewOnline(guid) {
  const u = new URL('./ZwavOnlineViewer.html', window.location.href)
  u.searchParams.set('guid', guid)
  window.open(u.toString(), '_blank')
}

function bindEvents() {
  bindDialog('apiModal')
  bindDialog('uploadModal')

  byId('btnOpenApiConfig').addEventListener('click', openApiModal)
  byId('btnSaveApiBase').addEventListener('click', () => {
    const v = byId('apiBaseInput').value
    setApiBaseUrl(v)
    closeDialog('apiModal')
    fetchList({ silent: true })
  })

  byId('btnSearch').addEventListener('click', () => {
    state.page = 1
    fetchList()
  })
  byId('btnReset').addEventListener('click', () => {
    byId('keyword').value = ''
    byId('status').value = ''
    byId('fromDate').value = ''
    byId('toDate').value = ''
    state.page = 1
    fetchList()
  })

  byId('pageSize').addEventListener('change', (e) => {
    state.pageSize = Number(e.target.value || 20)
    state.page = 1
    fetchList()
  })

  byId('btnPrev').addEventListener('click', () => {
    if (state.page <= 1) return
    state.page--
    fetchList({ silent: true })
  })
  byId('btnNext').addEventListener('click', () => {
    if (state.page >= state.totalPages) return
    state.page++
    fetchList({ silent: true })
  })

  byId('btnUpload').addEventListener('click', openUploadModal)
  byId('btnStartUpload').addEventListener('click', startUpload)
  byId('btnBatchDelete').addEventListener('click', batchDelete)

  byId('checkAll').addEventListener('change', (e) => {
    const checked = !!e.target.checked
    if (checked) state.data.forEach((r) => state.selected.add(r.analysisGuid))
    else state.data.forEach((r) => state.selected.delete(r.analysisGuid))
    render()
  })

  byId('tbody').addEventListener('change', (e) => {
    const t = e.target
    if (!t.classList.contains('row-check')) return
    const guid = t.getAttribute('data-guid')
    if (!guid) return
    if (t.checked) state.selected.add(guid)
    else state.selected.delete(guid)
    render()
  })

  byId('tbody').addEventListener('click', (e) => {
    const t = e.target
    if (t.classList.contains('action-delete')) {
      const guid = t.getAttribute('data-guid')
      deleteOne(guid)
      return
    }
    if (t.classList.contains('action-download')) {
      const guid = t.getAttribute('data-guid')
      const row = state.data.find((x) => x.analysisGuid === guid)
      downloadOne(guid, row && row.originalName)
      return
    }
    if (t.classList.contains('action-view')) {
      const guid = t.getAttribute('data-guid')
      viewOnline(guid)
    }
  })
}

function startPolling() {
  if (state.polling) clearInterval(state.polling)
  state.polling = setInterval(() => {
    if (state.uploading) return
    if (state.loading) return
    fetchList({ silent: true })
  }, 10000)
}

bindEvents()
fetchList()
startPolling()

