import {
  bindDialog,
  byId,
  closeDialog,
  escapeHtml,
  formatDateTime,
  formatFileSize,
  getApiBaseUrl,
  openDialog,
  parseFileNameFromContentDisposition,
  setApiBaseUrl,
  setText
} from './common.js'
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
  uploading: false,
  deleting: false,
  uploadFiles: []
}

function statusText(status) {
  const map = {
    Queued: '排队中',
    Canceled: '已取消',
    Completed: '已完成',
    Failed: '失败',
    ParsingRead: '读取录波',
    ParsingExtract: '解压中',
    ParsingCfg: '解析 CFG',
    ParsingHdr: '解析 HDR',
    ParsingChannel: '解析通道',
    ParsingDat: '解析 DAT 数据',
    Parsing: '解析中'
  }
  return map[status] || status || '-'
}

function formatFileSizeKB(bytes) {
  if (bytes === undefined || bytes === null) return '-'
  const kb = Number(bytes) / 1024
  if (!Number.isFinite(kb)) return '-'
  return `${kb.toFixed(2)} kb`
}

function createTagType(status) {
  if (status === 'Failed') return 'el-tag--danger'
  if (status === 'Completed') return 'el-tag--success'
  if (status === 'Queued') return 'el-tag--warning'
  return 'el-tag--primary'
}

function createProgressInnerClass(status) {
  if (status === 'Failed') return 'is-exception'
  if (status === 'Completed') return 'is-success'
  return ''
}

function getRowNameByGuid(guid) {
  const row = state.data.find((item) => item.analysisGuid === guid)
  return (row && row.originalName) || guid
}

function render() {
  const tbody = byId('tbody')
  if (!tbody) return

  const rows = state.data || []
  const pageStartIndex = (state.page - 1) * state.pageSize

  tbody.innerHTML = rows
    .map((row, index) => {
      const guid = row.analysisGuid
      const checked = state.selected.has(guid) ? 'checked' : ''
      const disabledAttr = state.deleting ? 'disabled' : ''
      const errorText = row.errorMessage ? `<span class="text-danger">${escapeHtml(row.errorMessage)}</span>` : '-'
      const pct = Number(row.progress || 0)
      const pctText = Number.isFinite(pct) ? pct : 0
      const tagType = createTagType(row.status)
      const pill = `<span class="el-tag ${tagType}">${escapeHtml(statusText(row.status))}</span>`
      const progressClass = createProgressInnerClass(row.status)
      const progressBar = `
        <div class="el-progress">
          <div class="el-progress-bar">
            <div class="el-progress-bar__inner ${progressClass}" style="width:${pctText}%"></div>
          </div>
          <div class="el-progress__text">${pctText}%</div>
        </div>
      `

      return `
        <tr>
          <td class="cell-center"><input type="checkbox" class="row-check" data-guid="${escapeHtml(guid)}" ${checked} ${disabledAttr}></td>
          <td class="cell-center">${pageStartIndex + index + 1}</td>
          <td class="file-name-cell" title="${escapeHtml(row.originalName || '')}">${escapeHtml(row.originalName || '-')}</td>
          <td>${formatFileSizeKB(row.fileSize)}</td>
          <td>${pill}</td>
          <td>${progressBar}</td>
          <td>${escapeHtml(formatDateTime(row.crtTime))}</td>
          <td>${errorText}</td>
          <td class="cell-center">
            <div class="cell-actions">
              <button class="el-button is-link action-view" data-guid="${escapeHtml(guid)}" ${disabledAttr}>在线浏览</button>
              <button class="el-button is-link action-download" data-guid="${escapeHtml(guid)}" ${disabledAttr}>下载</button>
              <button class="el-button is-link action-delete" data-guid="${escapeHtml(guid)}" style="color: var(--el-color-danger)" ${disabledAttr}>删除</button>
            </div>
          </td>
        </tr>
      `
    })
    .join('')

  const checkAll = byId('checkAll')
  if (checkAll) {
    checkAll.checked = rows.length > 0 && rows.every((row) => state.selected.has(row.analysisGuid))
    checkAll.disabled = state.deleting
  }

  const batchDeleteButton = byId('btnBatchDelete')
  if (batchDeleteButton) {
    batchDeleteButton.disabled = state.selected.size === 0 || state.deleting
  }

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
      params.fromUtc = fromUtc
      params.toUtc = end.toISOString()
    }

    const res = await zwavApi.getList(params)
    if (res && res.success) {
      state.data = (res.data && res.data.data) || []
      state.total = (res.data && res.data.total) || 0
      state.totalPages = (res.data && res.data.totalPages) || 1
    } else if (!silent) {
      alert((res && res.message) || '获取列表失败')
    }
  } catch (error) {
    if (!silent) alert(error.message || '获取列表失败')
  } finally {
    state.loading = false
    render()
  }
}

function openApiModal() {
  byId('apiBaseInput').value = getApiBaseUrl()
  openDialog('apiModal')
}

function resetUploadState() {
  state.uploadFiles = []
  state.uploading = false
  const fileInput = byId('fileInput')
  if (fileInput) fileInput.value = ''
  renderUploadList()
}

function openUploadModal() {
  resetUploadState()
  openDialog('uploadModal')
}

function makeUploadKey(file) {
  return [file.name, file.size, file.lastModified].join('__')
}

function isSupportedUploadFile(file) {
  const name = String((file && file.name) || '').toLowerCase()
  return name.endsWith('.zwav') || name.endsWith('.zip')
}

function createUploadItem(file, index) {
  return {
    id: `up_${Date.now()}_${index}_${Math.random().toString(36).slice(2, 8)}`,
    key: makeUploadKey(file),
    name: file.name,
    size: file.size,
    file,
    progress: 0,
    statusText: '等待上传',
    progressClass: ''
  }
}

function updateUploadActions() {
  const count = state.uploadFiles.length
  const summary = count === 0 ? '未选择文件' : `已选择 ${count} 个文件`
  setText('uploadSummary', summary)

  const clearButton = byId('btnClearUploadFiles')
  if (clearButton) clearButton.disabled = count === 0 || state.uploading

  const startButton = byId('btnStartUpload')
  if (startButton) {
    startButton.disabled = count === 0 || state.uploading
    startButton.textContent = state.uploading ? '上传中...' : '开始上传'
  }
}

function renderUploadList() {
  const list = byId('uploadList')
  if (!list) return

  updateUploadActions()

  if (state.uploadFiles.length === 0) {
    list.innerHTML = '<div class="upload-list-empty">请拖拽文件到上方区域，或点击区域选择文件。</div>'
    return
  }

  list.innerHTML = state.uploadFiles
    .map((item) => {
      const removeButton = state.uploading
        ? ''
        : `<button class="el-button is-link action-remove-upload" data-upload-id="${escapeHtml(item.id)}" type="button">移除</button>`

      return `
        <div class="upload-file-card">
          <div style="display:flex; justify-content:space-between; align-items:center; gap:10px;">
            <div class="upload-file-name" title="${escapeHtml(item.name)}">${escapeHtml(item.name)}</div>
            <div style="display:flex; align-items:center; gap:12px;">
              <div class="upload-file-meta">${escapeHtml(formatFileSize(item.size))}</div>
              ${removeButton}
              <div class="upload-file-meta" id="txt_${item.id}">${escapeHtml(item.statusText)}</div>
            </div>
          </div>
          <div class="el-progress" style="margin-top:8px;">
            <div class="el-progress-bar">
              <div class="el-progress-bar__inner ${item.progressClass}" id="bar_${item.id}" style="width:${item.progress}%"></div>
            </div>
            <div class="el-progress__text">${item.progress}%</div>
          </div>
        </div>
      `
    })
    .join('')
}

function updateUploadItemProgress(item, progress, text, progressClass) {
  item.progress = progress
  item.statusText = text
  item.progressClass = progressClass || ''

  const bar = byId(`bar_${item.id}`)
  if (bar) {
    bar.style.width = `${progress}%`
    bar.classList.remove('is-success', 'is-exception')
    if (item.progressClass) bar.classList.add(item.progressClass)
  }

  const statusTextEl = byId(`txt_${item.id}`)
  if (statusTextEl) statusTextEl.textContent = text

  const barText = bar && bar.parentElement && bar.parentElement.nextElementSibling
  if (barText) barText.textContent = `${progress}%`
}

function addUploadFiles(fileList) {
  const incomingFiles = Array.from(fileList || [])
  if (incomingFiles.length === 0) return

  const existingKeys = new Set(state.uploadFiles.map((item) => item.key))
  const nextItems = [...state.uploadFiles]
  const invalidNames = []

  incomingFiles.forEach((file, index) => {
    if (!isSupportedUploadFile(file)) {
      invalidNames.push(file.name)
      return
    }

    const key = makeUploadKey(file)
    if (existingKeys.has(key)) return

    nextItems.push(createUploadItem(file, index))
    existingKeys.add(key)
  })

  if (invalidNames.length > 0) {
    alert(`以下文件格式不支持：\n${invalidNames.join('\n')}`)
  }

  state.uploadFiles = nextItems
  renderUploadList()
}

async function startUpload() {
  if (state.uploading) return
  if (state.uploadFiles.length === 0) {
    alert('请先选择文件')
    return
  }

  state.uploading = true
  renderUploadList()

  try {
    for (const item of state.uploadFiles) {
      updateUploadItemProgress(item, 0, '上传中...', '')

      let uploadRes = null
      try {
        uploadRes = await zwavApi.uploadFile(item.file, (pct) => {
          updateUploadItemProgress(item, pct, '上传中...', '')
        })
      } catch (error) {
        updateUploadItemProgress(item, item.progress || 0, error.message || '上传失败', 'is-exception')
        continue
      }

      if (!uploadRes || !uploadRes.success) {
        updateUploadItemProgress(item, item.progress || 0, (uploadRes && uploadRes.message) || '上传失败', 'is-exception')
        continue
      }

      updateUploadItemProgress(item, 100, '创建任务中...', '')

      try {
        const fileId = uploadRes.data && uploadRes.data.fileId
        const createRes = await zwavApi.createAnalysis(fileId, false)
        if (createRes && createRes.success) {
          updateUploadItemProgress(item, 100, '已完成', 'is-success')
        } else {
          updateUploadItemProgress(item, 100, (createRes && createRes.message) || '创建任务失败', 'is-exception')
        }
      } catch (error) {
        updateUploadItemProgress(item, 100, error.message || '创建任务失败', 'is-exception')
      }
    }

    await fetchList({ silent: true })
  } finally {
    state.uploading = false
    updateUploadActions()
  }
}

function renderDeleteProgressStats(total, successCount, failCount) {
  return `
    <div class="delete-progress-stat">
      <div class="delete-progress-stat__label">总文件数</div>
      <div class="delete-progress-stat__value">${total}</div>
    </div>
    <div class="delete-progress-stat">
      <div class="delete-progress-stat__label">成功删除</div>
      <div class="delete-progress-stat__value">${successCount}</div>
    </div>
    <div class="delete-progress-stat">
      <div class="delete-progress-stat__label">删除失败</div>
      <div class="delete-progress-stat__value">${failCount}</div>
    </div>
  `
}

function closeDeleteProgressModal() {
  if (state.deleting) return
  closeDialog('deleteProgressModal')
}

function updateDeleteProgressView({
  title,
  summary,
  completed,
  total,
  successCount,
  failCount,
  currentName,
  failures,
  done
}) {
  setText('deleteProgressTitle', title)
  setText('deleteProgressSummary', summary)

  const percent = total > 0 ? Math.round((completed / total) * 100) : 0
  const progressBar = byId('deleteProgressBar')
  if (progressBar) {
    progressBar.style.width = `${percent}%`
    progressBar.classList.remove('is-success', 'is-exception')
    if (done && failCount > 0) progressBar.classList.add('is-exception')
    if (done && failCount === 0) progressBar.classList.add('is-success')
  }

  setText('deleteProgressText', `${percent}%`)

  const stats = byId('deleteProgressStats')
  if (stats) {
    stats.innerHTML = renderDeleteProgressStats(total, successCount, failCount)
  }

  setText('deleteProgressCurrent', `当前文件：${currentName || '-'}`)

  const failureBox = byId('deleteProgressFailures')
  if (failureBox) {
    if (failures && failures.length > 0) {
      failureBox.style.display = 'block'
      failureBox.innerHTML = `
        <strong>失败详情：</strong><br />
        ${failures.map((item) => `${escapeHtml(item.name)}：${escapeHtml(item.message)}`).join('<br />')}
      `
    } else {
      failureBox.style.display = 'none'
      failureBox.innerHTML = ''
    }
  }

  const closeButton = byId('btnCloseDeleteProgress')
  const doneButton = byId('btnDeleteProgressDone')
  if (closeButton) closeButton.disabled = !done
  if (doneButton) doneButton.disabled = !done
}

async function runDeleteWithProgress(items) {
  if (state.deleting || !items || items.length === 0) return

  state.deleting = true
  render()
  openDialog('deleteProgressModal')
  updateDeleteProgressView({
    title: items.length > 1 ? '批量删除中' : '删除中',
    summary: `准备删除 ${items.length} 个文件，请稍候...`,
    completed: 0,
    total: items.length,
    successCount: 0,
    failCount: 0,
    currentName: '-',
    failures: [],
    done: false
  })

  let successCount = 0
  let failCount = 0
  const failures = []
  const successIds = []

  try {
    for (let index = 0; index < items.length; index++) {
      const item = items[index]

      updateDeleteProgressView({
        title: items.length > 1 ? '批量删除中' : '删除中',
        summary: `正在删除第 ${index + 1} / ${items.length} 个文件...`,
        completed: index,
        total: items.length,
        successCount,
        failCount,
        currentName: item.name,
        failures,
        done: false
      })

      try {
        const res = await zwavApi.deleteAnalysis(item.guid, true)
        if (res && res.success) {
          successCount++
          successIds.push(item.guid)
        } else {
          failCount++
          failures.push({
            guid: item.guid,
            name: item.name,
            message: (res && res.message) || '删除失败'
          })
        }
      } catch (error) {
        failCount++
        failures.push({
          guid: item.guid,
          name: item.name,
          message: error.message || '删除失败'
        })
      }

      updateDeleteProgressView({
        title: items.length > 1 ? '批量删除中' : '删除中',
        summary: `正在删除第 ${Math.min(index + 1, items.length)} / ${items.length} 个文件...`,
        completed: index + 1,
        total: items.length,
        successCount,
        failCount,
        currentName: item.name,
        failures,
        done: false
      })
    }

    successIds.forEach((guid) => state.selected.delete(guid))
    await fetchList({ silent: true })
  } finally {
    state.deleting = false
    render()
  }

  updateDeleteProgressView({
    title: failCount > 0 ? '删除完成（部分失败）' : '删除完成',
    summary: failCount > 0 ? `删除已完成，成功 ${successCount} 个，失败 ${failCount} 个。` : `删除已完成，${successCount} 个文件已全部删除成功。`,
    completed: items.length,
    total: items.length,
    successCount,
    failCount,
    currentName: failCount > 0 ? '请查看失败详情后再决定是否重试。' : '删除完成',
    failures,
    done: true
  })
}

async function deleteOne(guid) {
  if (!guid) return
  if (!window.confirm('确认删除该任务和文件吗？')) return
  await runDeleteWithProgress([{ guid, name: getRowNameByGuid(guid) }])
}

async function batchDelete() {
  if (state.selected.size === 0) return
  const items = Array.from(state.selected).map((guid) => ({
    guid,
    name: getRowNameByGuid(guid)
  }))

  if (!window.confirm(`确认删除选中的 ${items.length} 个任务和文件吗？`)) return
  await runDeleteWithProgress(items)
}

async function downloadOne(guid, originalName) {
  try {
    const res = await zwavApi.downloadFile(guid)
    const nameFromHeader = parseFileNameFromContentDisposition(res.contentDisposition)
    const name = nameFromHeader || originalName || 'download.zwav'
    const anchor = document.createElement('a')
    const url = window.URL.createObjectURL(res.blob)
    anchor.href = url
    anchor.download = name
    document.body.appendChild(anchor)
    anchor.click()
    document.body.removeChild(anchor)
    window.URL.revokeObjectURL(url)
  } catch (error) {
    alert(error.message || '下载失败')
  }
}

function viewOnline(guid) {
  const url = new URL('./ZwavOnlineViewer.html', window.location.href)
  url.searchParams.set('guid', guid)
  window.open(url.toString(), '_blank')
}

function goSagEvents() {
  const url = new URL('./ZwavSagEvents.html', window.location.href)
  window.open(url.toString(), '_blank')
}

function bindUploadEvents() {
  const fileInput = byId('fileInput')
  const dropzone = byId('uploadDropzone')
  const uploadList = byId('uploadList')
  const clearButton = byId('btnClearUploadFiles')

  if (fileInput) {
    fileInput.addEventListener('change', (event) => {
      addUploadFiles(event.target.files)
      event.target.value = ''
    })
  }

  if (dropzone) {
    dropzone.addEventListener('click', () => {
      if (state.uploading) return
      fileInput.click()
    })

    dropzone.addEventListener('keydown', (event) => {
      if (state.uploading) return
      if (event.key !== 'Enter' && event.key !== ' ') return
      event.preventDefault()
      fileInput.click()
    })

    ;['dragenter', 'dragover'].forEach((eventName) => {
      dropzone.addEventListener(eventName, (event) => {
        event.preventDefault()
        if (state.uploading) return
        dropzone.classList.add('is-dragover')
      })
    })

    ;['dragleave', 'drop'].forEach((eventName) => {
      dropzone.addEventListener(eventName, (event) => {
        event.preventDefault()
        dropzone.classList.remove('is-dragover')
      })
    })

    dropzone.addEventListener('drop', (event) => {
      if (state.uploading) return
      addUploadFiles((event.dataTransfer && event.dataTransfer.files) || [])
    })
  }

  if (uploadList) {
    uploadList.addEventListener('click', (event) => {
      const target = event.target
      if (!target.classList.contains('action-remove-upload')) return
      const uploadId = target.getAttribute('data-upload-id')
      if (!uploadId || state.uploading) return
      state.uploadFiles = state.uploadFiles.filter((item) => item.id !== uploadId)
      renderUploadList()
    })
  }

  if (clearButton) {
    clearButton.addEventListener('click', () => {
      if (state.uploading) return
      state.uploadFiles = []
      renderUploadList()
    })
  }
}

function bindDeleteProgressEvents() {
  const mask = byId('deleteProgressModal')
  const closeButton = byId('btnCloseDeleteProgress')
  const doneButton = byId('btnDeleteProgressDone')

  if (mask) {
    mask.addEventListener('click', (event) => {
      if (event.target !== mask) return
      closeDeleteProgressModal()
    })
  }

  if (closeButton) closeButton.addEventListener('click', closeDeleteProgressModal)
  if (doneButton) doneButton.addEventListener('click', closeDeleteProgressModal)
}

function bindEvents() {
  bindDialog('apiModal')
  bindDialog('uploadModal')
  bindUploadEvents()
  bindDeleteProgressEvents()

  byId('btnOpenApiConfig').addEventListener('click', openApiModal)
  byId('btnSaveApiBase').addEventListener('click', () => {
    const value = byId('apiBaseInput').value
    setApiBaseUrl(value)
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

  byId('pageSize').addEventListener('change', (event) => {
    state.pageSize = Number(event.target.value || 20)
    state.page = 1
    fetchList()
  })

  byId('btnPrev').addEventListener('click', () => {
    if (state.page <= 1 || state.deleting) return
    state.page--
    fetchList({ silent: true })
  })

  byId('btnNext').addEventListener('click', () => {
    if (state.page >= state.totalPages || state.deleting) return
    state.page++
    fetchList({ silent: true })
  })

  byId('btnUpload').addEventListener('click', openUploadModal)
  byId('btnGoSagEvents').addEventListener('click', goSagEvents)
  byId('btnStartUpload').addEventListener('click', startUpload)
  byId('btnBatchDelete').addEventListener('click', batchDelete)

  byId('checkAll').addEventListener('change', (event) => {
    if (state.deleting) return
    const checked = !!event.target.checked
    if (checked) state.data.forEach((row) => state.selected.add(row.analysisGuid))
    else state.data.forEach((row) => state.selected.delete(row.analysisGuid))
    render()
  })

  byId('tbody').addEventListener('change', (event) => {
    if (state.deleting) return
    const target = event.target
    if (!target.classList.contains('row-check')) return
    const guid = target.getAttribute('data-guid')
    if (!guid) return
    if (target.checked) state.selected.add(guid)
    else state.selected.delete(guid)
    render()
  })

  byId('tbody').addEventListener('click', (event) => {
    const target = event.target
    if (!target || state.deleting || target.disabled) return

    if (target.classList.contains('action-delete')) {
      const guid = target.getAttribute('data-guid')
      deleteOne(guid)
      return
    }

    if (target.classList.contains('action-download')) {
      const guid = target.getAttribute('data-guid')
      const row = state.data.find((item) => item.analysisGuid === guid)
      downloadOne(guid, row && row.originalName)
      return
    }

    if (target.classList.contains('action-view')) {
      const guid = target.getAttribute('data-guid')
      viewOnline(guid)
    }
  })
}

function startPolling() {
  if (state.polling) clearInterval(state.polling)
  state.polling = setInterval(() => {
    if (state.uploading || state.loading || state.deleting) return
    fetchList({ silent: true })
  }, 10000)
}

bindEvents()
renderUploadList()
fetchList()
startPolling()
