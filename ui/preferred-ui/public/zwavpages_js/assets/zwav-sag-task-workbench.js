import {
  bindDialog,
  byId,
  closeDialog,
  escapeHtml,
  formatDateTime,
  formatFileSize,
  getApiBaseUrl,
  openDialog,
  qs,
  setHtml,
  setText
} from './common.js'
import { zwavApi } from './zwav-api.js'

const ANALYZE_STATUS_MAP = {
  0: { type: 'warning', text: '待处理' },
  1: { type: 'primary', text: '处理中' },
  2: { type: 'success', text: '成功' },
  3: { type: 'danger', text: '失败' }
}

function createListState() {
  return {
    page: 1,
    pageSize: 10,
    total: 0,
    data: [],
    selected: new Set()
  }
}

const state = {
  mode: 'create',
  queryTaskId: 0,
  polling: null,
  analysis: createListState(),
  taskList: createListState(),
  taskFiles: createListState(),
  currentTask: null,
  currentTaskId: 0,
  analyzeRecoverAuto: true
}

function on(id, event, handler) {
  const el = byId(id)
  if (!el) return
  el.addEventListener(event, handler)
}

function showAlert(type, message) {
  const alertDiv = document.createElement('div')
  alertDiv.className = `el-alert el-alert--${type}`
  alertDiv.style.cssText = `
    position: fixed;
    top: 20px;
    right: 20px;
    z-index: 9999;
    min-width: 280px;
    max-width: 460px;
    padding: 12px 16px;
    border-radius: 12px;
    background: #fff;
    border: 1px solid rgba(148, 163, 184, 0.24);
    box-shadow: 0 20px 40px rgba(15, 23, 42, 0.14);
    color: #1f2937;
  `
  alertDiv.innerHTML = `<div>${escapeHtml(message)}</div>`
  document.body.appendChild(alertDiv)
  setTimeout(() => alertDiv.remove(), 3000)
}

function showConfirmDialog(title, message, warningText = '') {
  return new Promise((resolve) => {
    const confirmDialog = document.createElement('div')
    confirmDialog.className = 'el-dialog-mask is-open'
    confirmDialog.innerHTML = `
      <div class="el-dialog" style="width: 420px;">
        <div class="el-dialog__header">
          <div class="el-dialog__title">${escapeHtml(title)}</div>
          <button class="el-dialog__close" data-action="cancel">×</button>
        </div>
        <div class="el-dialog__body">
          <p>${escapeHtml(message)}</p>
          ${warningText ? `<p style="margin-top:8px; color: var(--el-color-danger); font-size:12px;">${escapeHtml(warningText)}</p>` : ''}
        </div>
        <div class="el-dialog__footer">
          <button class="el-button" data-action="cancel">取消</button>
          <button class="el-button el-button--danger" data-action="confirm">确定</button>
        </div>
      </div>
    `
    document.body.appendChild(confirmDialog)

    const cleanup = (result) => {
      confirmDialog.remove()
      resolve(result)
    }

    confirmDialog.addEventListener('click', (e) => {
      const action = e.target?.getAttribute?.('data-action')
      if (action === 'cancel') cleanup(false)
      if (action === 'confirm') cleanup(true)
      if (e.target === confirmDialog) cleanup(false)
    })
  })
}

function updatePagination(total, page, pageSize, totalTextId, pageTextId, prevBtnId, nextBtnId) {
  const totalPages = Math.max(1, Math.ceil(total / pageSize))
  setText(totalTextId, `共 ${total} 条`)
  setText(pageTextId, `${page}/${totalPages}`)
  const prevBtn = byId(prevBtnId)
  const nextBtn = byId(nextBtnId)
  if (prevBtn) prevBtn.disabled = page <= 1
  if (nextBtn) nextBtn.disabled = page >= totalPages
}

function normalizeProgress(v) {
  const n = Number(v)
  if (!Number.isFinite(n)) return 0
  if (n < 0) return 0
  if (n > 100) return 100
  return Math.round(n)
}

function getAnalyzeStatusMeta(status) {
  return ANALYZE_STATUS_MAP[Number(status)] || { type: 'info', text: '-' }
}

function getTaskStatusMeta(task) {
  const status = Number(task?.status)
  if (status === 0) return { type: 'warning', text: '接收中' }
  if (status === 1) return { type: 'primary', text: '已关闭待收尾' }
  if (status === 2) return { type: 'success', text: '已完成' }
  if (status === 3) return { type: 'danger', text: '已完成（有失败）' }
  return { type: 'info', text: '新任务' }
}

function formatDurationHms(ms) {
  const totalMs = Number(ms)
  if (!Number.isFinite(totalMs) || totalMs < 0) return '--:--:--'
  const totalSeconds = Math.floor(totalMs / 1000)
  const hours = String(Math.floor(totalSeconds / 3600)).padStart(2, '0')
  const minutes = String(Math.floor((totalSeconds % 3600) / 60)).padStart(2, '0')
  const seconds = String(totalSeconds % 60).padStart(2, '0')
  return `${hours}:${minutes}:${seconds}`
}

function formatPercent(value) {
  const n = Number(value)
  if (!Number.isFinite(n)) return '-'
  return n.toFixed(2)
}

function formatEventType(type) {
  if (!type) return '-'
  if (type === 'Normal') return '正常'
  if (type === 'Sag') return '暂降'
  if (type === 'Interruption') return '中断'
  return type
}

function buildWorkbenchUrl(mode, taskId) {
  const url = new URL('./ZwavSagTaskWorkbench.html', window.location.href)
  url.searchParams.set('mode', mode || 'create')
  const apiBase = getApiBaseUrl()
  if (apiBase) url.searchParams.set('apiBase', apiBase)
  if (taskId) url.searchParams.set('taskId', String(taskId))
  return url.toString()
}

function openWorkbench(mode, taskId) {
  window.open(buildWorkbenchUrl(mode, taskId), '_blank', 'noopener')
}

function getTaskMetaText(task) {
  if (!task) return '当前没有正在接收录波文件的任务，提交后会自动新建一条暂降任务。'
  const taskName = task.taskName || '暂降分析任务'
  const statusText = task.isClosed ? '任务已关闭，不再接收新文件' : '任务未关闭，可继续接收新文件'
  return `${task.taskNo || '-'} | ${taskName} | ${statusText}`
}

function getSelectedReferenceMode() {
  return String(document.querySelector('input[name="referenceTypeRadio"]:checked')?.value || 'Adaptive').trim()
}

function updateReferenceModeUi() {
  const activeMode = getSelectedReferenceMode()
  document.querySelectorAll('[data-reference-mode]').forEach((el) => {
    el.classList.toggle('is-active', el.getAttribute('data-reference-mode') === activeMode)
  })
  const customWrap = byId('customReferenceWrap')
  if (customWrap) customWrap.classList.toggle('is-visible', activeMode === 'CustomVoltage')
}

function computeRecoverThresholdPct(sagThresholdPct, hysteresisPct) {
  const sag = Number(sagThresholdPct)
  const hysteresis = Number(hysteresisPct)
  if (!Number.isFinite(sag) || !Number.isFinite(hysteresis)) return ''
  const value = Math.min(100, sag + hysteresis)
  return String(Math.max(sag, value))
}

function syncRecoverThresholdIfAuto() {
  if (!state.analyzeRecoverAuto) return
  const value = computeRecoverThresholdPct(byId('sagThresholdPct')?.value, byId('hysteresisPct')?.value)
  if (value === '') return
  const el = byId('recoverThresholdPct')
  if (el) el.value = value
}

function resolveReferenceSelection() {
  const mode = getSelectedReferenceMode()
  if (mode === 'LineVoltage') return { referenceType: 'LineVoltage', referenceVoltage: 100 }
  if (mode === 'Adaptive') return { referenceType: 'Adaptive', referenceVoltage: null }
  if (mode === 'CustomVoltage') {
    return {
      referenceType: 'CustomVoltage',
      referenceVoltage: Number(byId('customReferenceVoltage')?.value || 0)
    }
  }
  return { referenceType: 'PhaseVoltage', referenceVoltage: 57.74 }
}

function toggleAnalysisParamsDisabled(disabled) {
  ;[
    'sagThresholdPct',
    'minDurationMs',
    'interruptThresholdPct',
    'hysteresisPct',
    'recoverThresholdPct',
    'customReferenceVoltage'
  ].forEach((id) => {
    const el = byId(id)
    if (el) el.disabled = !!disabled
  })
  document.querySelectorAll('input[name="referenceTypeRadio"]').forEach((el) => {
    el.disabled = !!disabled
  })
}

function selectReferenceMode(mode, voltage) {
  const value = String(mode || 'Adaptive')
  const radio = document.querySelector(`input[name="referenceTypeRadio"][value="${value}"]`)
  if (radio) radio.checked = true
  const customInput = byId('customReferenceVoltage')
  if (customInput && Number.isFinite(Number(voltage)) && Number(voltage) > 0) {
    customInput.value = String(voltage)
  }
  updateReferenceModeUi()
}

function resetAnalyzeFormDefaults() {
  const adaptiveRadio = document.querySelector('input[name="referenceTypeRadio"][value="Adaptive"]')
  if (adaptiveRadio) adaptiveRadio.checked = true
  const customReferenceInput = byId('customReferenceVoltage')
  if (customReferenceInput) customReferenceInput.value = ''
  byId('sagThresholdPct').value = '90'
  byId('interruptThresholdPct').value = '10'
  byId('hysteresisPct').value = '2'
  byId('minDurationMs').value = '10'
  state.analyzeRecoverAuto = true
  syncRecoverThresholdIfAuto()
  updateReferenceModeUi()
}

function applyTaskParamsToForm(task) {
  if (!task || task.isClosed) {
    toggleAnalysisParamsDisabled(false)
    updateReferenceModeUi()
    setText('analysisParamTip', '没有活动任务时可调整参数；如果追加到未关闭任务，参数会自动沿用任务配置。')
    return
  }

  byId('sagThresholdPct').value = Number(task.sagThresholdPct || 90)
  byId('minDurationMs').value = Number(task.minDurationMs || 10)
  byId('interruptThresholdPct').value = Number(task.interruptThresholdPct || 10)
  byId('hysteresisPct').value = Number(task.hysteresisPct || 2)
  byId('recoverThresholdPct').value = task.recoverThresholdPct !== undefined && task.recoverThresholdPct !== null
    ? Number(task.recoverThresholdPct)
    : ''
  selectReferenceMode(task.referenceType || 'Adaptive', task.referenceVoltage)
  toggleAnalysisParamsDisabled(true)
  setText('analysisParamTip', '当前将追加到未关闭任务，分析参数已锁定为该任务的既有配置。')
}

function canDeleteTask(task) {
  return !!task && !!task.isClosed && Number(task.pendingFileCount || 0) === 0
}

function setMode(mode) {
  state.mode = mode === 'list' ? 'list' : 'create'
  byId('createPanel').classList.toggle('workbench-hidden', state.mode !== 'create')
  byId('listPanel').classList.toggle('workbench-hidden', state.mode !== 'list')
  byId('btnModeCreate').classList.toggle('is-active', state.mode === 'create')
  byId('btnModeList').classList.toggle('is-active', state.mode === 'list')
  byId('btnOpenListWindow').textContent = state.mode === 'create' ? '打开任务列表窗口' : '打开创建任务窗口'
  setText('pageTitle', state.mode === 'create' ? '暂降任务工作台' : '暂降任务列表')
  setText(
    'pageSubtitle',
    state.mode === 'create'
      ? '左侧勾选待加入任务的录波文件，右侧持续跟踪当前任务的接收数量、完成数量、成功/失败、待解析、开始/结束时间和预计剩余时间。'
      : '左侧查看全部暂降任务记录，支持修改、删除、查看详情；右侧持续展示当前选中任务的统计摘要与逐文件分析进度。'
  )
  setText('mainPanelTitle', state.mode === 'create' ? '待加入任务的录波文件' : '暂降任务记录')
  setText(
    'mainPanelSubtitle',
    state.mode === 'create'
      ? '选择已完成解析的录波文件，把它们持续加入当前任务；如果当前没有可追加任务，系统会自动新建。'
      : '这里展示每一条暂降分析任务记录。可以查看详情、修改任务名称、删除任务，删除时会级联清理任务下所有暂降结果记录。'
  )
}

function renderEmptyTableRow(colspan, text) {
  return `<tr><td class="task-empty" colspan="${colspan}">${escapeHtml(text)}</td></tr>`
}

function renderAnalysisList() {
  updatePagination(
    state.analysis.total,
    state.analysis.page,
    state.analysis.pageSize,
    'analysisTotalText',
    'analysisPageText',
    'analysisPrev',
    'analysisNext'
  )

  const allGuids = state.analysis.data.map((x) => x.analysisGuid).filter(Boolean)
  const checkedCount = allGuids.filter((guid) => state.analysis.selected.has(guid)).length
  const checkAll = byId('analysisCheckAll')
  if (checkAll) {
    checkAll.checked = allGuids.length > 0 && checkedCount === allGuids.length
    checkAll.indeterminate = checkedCount > 0 && checkedCount < allGuids.length
  }

  const rows = state.analysis.data.length === 0
    ? renderEmptyTableRow(6, '暂无可加入任务的录波文件')
    : state.analysis.data.map((row, idx) => {
      const guid = row.analysisGuid
      const checked = state.analysis.selected.has(guid) ? 'checked' : ''
      const statusMeta = getAnalyzeStatusMeta(row.status)
      return `
        <tr>
          <td class="cell-center"><input type="checkbox" class="analysis-check" data-guid="${escapeHtml(guid)}" ${checked}></td>
          <td class="cell-center">${idx + 1 + (state.analysis.page - 1) * state.analysis.pageSize}</td>
          <td class="file-name-cell" title="${escapeHtml(row.originalName || '-')}">${escapeHtml(row.originalName || '-')}</td>
          <td><span class="el-tag el-tag--${statusMeta.type}">${escapeHtml(statusMeta.text)}</span></td>
          <td>${escapeHtml(formatFileSize(row.fileSize))}</td>
          <td>${escapeHtml(row.crtTime ? formatDateTime(row.crtTime) : '-')}</td>
        </tr>
      `
    }).join('')

  setHtml('analysisTbody', rows)
  setText('selectedAnalysisText', `已选择 ${state.analysis.selected.size} 个录波文件`)
}

async function loadAnalysisList(silent = false) {
  try {
    const params = {
      page: state.analysis.page,
      pageSize: state.analysis.pageSize,
      keyword: (byId('analysisKeyword')?.value || '').trim() || undefined,
      status: 'Completed'
    }
    const res = await zwavApi.getList(params)
    if (!res?.success) {
      if (!silent) showAlert('error', res?.message || '获取录波解析列表失败')
      return
    }
    state.analysis.data = res.data?.data || []
    state.analysis.total = res.data?.total || 0
    renderAnalysisList()
  } catch (e) {
    if (!silent) showAlert('error', e.message || '获取录波解析列表失败')
  }
}

function renderTaskList() {
  updatePagination(
    state.taskList.total,
    state.taskList.page,
    state.taskList.pageSize,
    'taskTotalText',
    'taskPageText',
    'taskPrev',
    'taskNext'
  )

  const rows = state.taskList.data.length === 0
    ? renderEmptyTableRow(10, '暂无暂降任务记录')
    : state.taskList.data.map((row, idx) => {
      const statusMeta = getTaskStatusMeta(row)
      const deleteDisabled = canDeleteTask(row) ? '' : 'disabled'
      return `
        <tr>
          <td class="cell-center">${idx + 1 + (state.taskList.page - 1) * state.taskList.pageSize}</td>
          <td>${escapeHtml(row.taskNo || '-')}</td>
          <td class="file-name-cell" title="${escapeHtml(row.taskName || '-')}">${escapeHtml(row.taskName || '-')}</td>
          <td><span class="el-tag el-tag--${statusMeta.type}">${escapeHtml(statusMeta.text)}</span></td>
          <td class="cell-center">${Number(row.receivedFileCount || 0)}</td>
          <td class="cell-center">${Number(row.finishedFileCount || 0)}</td>
          <td class="cell-center">${Number(row.pendingFileCount || 0)}</td>
          <td>${escapeHtml(row.startParseTime ? formatDateTime(row.startParseTime) : '-')}</td>
          <td>${escapeHtml(row.finishParseTime ? formatDateTime(row.finishParseTime) : '-')}</td>
          <td class="cell-actions">
            <button class="el-button is-link action-task-detail" data-id="${escapeHtml(row.id)}">详情</button>
            <button class="el-button is-link action-task-edit" data-id="${escapeHtml(row.id)}">修改</button>
            <button class="el-button is-link action-task-workbench" data-id="${escapeHtml(row.id)}">工作台</button>
            <button class="el-button is-link action-task-delete" data-id="${escapeHtml(row.id)}" style="color: var(--el-color-danger)" ${deleteDisabled}>删除</button>
          </td>
        </tr>
      `
    }).join('')

  setHtml('taskTbody', rows)
}

async function loadTaskList(silent = false) {
  try {
    const statusRaw = String(byId('taskStatusFilter')?.value || '').trim()
    const closedRaw = String(byId('taskClosedFilter')?.value || '').trim()
    const params = {
      keyword: (byId('taskKeyword')?.value || '').trim() || undefined,
      status: statusRaw === '' ? undefined : Number(statusRaw),
      isClosed: closedRaw === '' ? undefined : closedRaw === 'true',
      page: state.taskList.page,
      pageSize: state.taskList.pageSize
    }
    const res = await zwavApi.sagTaskList(params)
    if (!res?.success) {
      if (!silent) showAlert('error', res?.message || '获取暂降任务列表失败')
      return
    }
    state.taskList.data = res.data?.data || []
    state.taskList.total = res.data?.total || 0
    renderTaskList()

    if (state.mode === 'list' && !state.currentTaskId && state.taskList.data.length > 0) {
      await loadTaskDetail(state.taskList.data[0].id, { silent: true })
    }
  } catch (e) {
    if (!silent) showAlert('error', e.message || '获取暂降任务列表失败')
  }
}

function renderTaskFiles() {
  updatePagination(
    state.taskFiles.total,
    state.taskFiles.page,
    state.taskFiles.pageSize,
    'taskFileTotalText',
    'taskFilePageText',
    'taskFilePrev',
    'taskFileNext'
  )

  const rows = state.currentTaskId <= 0
    ? renderEmptyTableRow(10, '当前没有可展示的任务文件进度')
    : state.taskFiles.data.length === 0
      ? renderEmptyTableRow(10, '当前任务下暂无录波文件记录')
      : state.taskFiles.data.map((row, idx) => {
        const statusMeta = getAnalyzeStatusMeta(row.status)
        const progress = row.status === 2 || row.status === 3 ? 100 : normalizeProgress(row.progress)
        const progressInnerClass = Number(row.status) === 3 ? 'is-exception' : Number(row.status) === 2 ? 'is-success' : ''
        return `
          <tr>
            <td class="cell-center">${idx + 1 + (state.taskFiles.page - 1) * state.taskFiles.pageSize}</td>
            <td class="file-name-cell" title="${escapeHtml(row.originalName || '-')}">${escapeHtml(row.originalName || '-')}</td>
            <td><span class="el-tag el-tag--${statusMeta.type}">${escapeHtml(statusMeta.text)}</span></td>
            <td class="file-status-cell">
              <div class="compact-progress">
                <div class="el-progress-bar"><div class="el-progress-bar__inner ${progressInnerClass}" style="width:${progress}%"></div></div>
                <span class="el-progress__text">${progress}%</span>
              </div>
            </td>
            <td class="cell-center">${row.hasSag ? '是' : '否'}</td>
            <td>${escapeHtml(formatEventType(row.eventType))}</td>
            <td>${escapeHtml(row.worstPhase || '-')}</td>
            <td>${escapeHtml(row.startTime ? formatDateTime(row.startTime) : '-')}</td>
            <td>${escapeHtml(row.finishTime ? formatDateTime(row.finishTime) : '-')}</td>
            <td class="file-name-cell" title="${escapeHtml(row.errorMessage || '-')}">${escapeHtml(row.errorMessage || '-')}</td>
          </tr>
        `
      }).join('')

  setHtml('taskFileTbody', rows)
}

function renderTaskDetail() {
  const task = state.currentTask
  const statusMeta = getTaskStatusMeta(task)
  setText('taskDetailTaskNo', task ? `${task.taskNo || '-'} / ${task.taskName || '暂降分析任务'}` : '当前没有活动任务')
  setText('taskDetailMeta', getTaskMetaText(task))
  setText('taskDetailSummary', task?.summaryText || '当前暂无活动任务。')
  setText(
    'taskDetailFoot',
    task
      ? `参考电压处理：${task.referenceType || 'Adaptive'}；参考电压值：${task.referenceVoltage ?? '-'}；暂降阈值：${task.sagThresholdPct ?? '-'}%；开始解析：${task.startParseTime ? formatDateTime(task.startParseTime) : '-'}；结束解析：${task.finishParseTime ? formatDateTime(task.finishParseTime) : '-'}。`
      : '任务关闭前，可以一直追加新的录波文件；关闭后不能再加入到已有任务。'
  )

  const statusTag = byId('taskDetailStatusTag')
  if (statusTag) {
    statusTag.className = `el-tag el-tag--${statusMeta.type}`
    statusTag.textContent = task ? statusMeta.text : '新任务'
  }

  setText('taskReceivedCount', String(Number(task?.receivedFileCount || 0)))
  setText('taskFinishedCount', String(Number(task?.finishedFileCount || 0)))
  setText('taskFinishedSub', `成功 ${Number(task?.successFileCount || 0)} 个，失败 ${Number(task?.failedFileCount || 0)} 个`)
  setText('taskSuccessCount', String(Number(task?.successFileCount || 0)))
  setText('taskFailedCount', String(Number(task?.failedFileCount || 0)))
  setText('taskPendingCount', String(Number(task?.pendingFileCount || 0)))
  setText('taskPendingSub', task ? `待自动解析 ${Number(task?.pendingFileCount || 0)} 个` : '正在排队或处理中')
  setText('taskEtaText', formatDurationHms(task?.estimatedRemainingMs))
  setText('taskProgressSub', task ? `当前进度 ${Number(task?.progress || 0)}%` : '当前进度 0%')
  setText('taskStartTimeText', task?.startParseTime ? formatDateTime(task.startParseTime) : '-')
  setText('taskFinishTimeText', task?.finishParseTime ? formatDateTime(task.finishParseTime) : '-')

  const renameBtn = byId('btnRenameTask')
  const closeBtn = byId('btnCloseTask')
  const deleteBtn = byId('btnDeleteTask')
  if (renameBtn) renameBtn.disabled = !task
  if (closeBtn) closeBtn.disabled = !task || !!task.isClosed
  if (deleteBtn) deleteBtn.disabled = !canDeleteTask(task)

  if (state.mode === 'create') {
    applyTaskParamsToForm(task)
  }
}

async function loadTaskFiles(silent = false) {
  if (!state.currentTaskId) {
    state.taskFiles.data = []
    state.taskFiles.total = 0
    renderTaskFiles()
    return
  }

  try {
    const statusRaw = String(byId('taskFileStatusFilter')?.value || '').trim()
    const params = {
      keyword: (byId('taskFileKeyword')?.value || '').trim() || undefined,
      status: statusRaw === '' ? undefined : Number(statusRaw),
      page: state.taskFiles.page,
      pageSize: state.taskFiles.pageSize
    }
    const res = await zwavApi.sagTaskEventList(state.currentTaskId, params)
    if (!res?.success) {
      if (!silent) showAlert('error', res?.message || '获取任务文件进度失败')
      return
    }
    state.taskFiles.data = res.data?.data || []
    state.taskFiles.total = res.data?.total || 0
    renderTaskFiles()
  } catch (e) {
    if (!silent) showAlert('error', e.message || '获取任务文件进度失败')
  }
}

async function loadTaskDetail(taskId, options = {}) {
  const { silent = false, reloadFiles = true } = options

  if (!taskId) {
    state.currentTask = null
    state.currentTaskId = 0
    renderTaskDetail()
    await loadTaskFiles(true)
    return
  }

  try {
    const res = await zwavApi.sagTaskDetail(taskId)
    if (!res?.success) {
      if (!silent) showAlert('error', res?.message || '获取任务详情失败')
      return
    }
    state.currentTask = res.data || null
    state.currentTaskId = Number(res.data?.id || 0)
    renderTaskDetail()
    if (reloadFiles) await loadTaskFiles(silent)
  } catch (e) {
    if (!silent) showAlert('error', e.message || '获取任务详情失败')
  }
}

async function loadActiveTask(silent = false) {
  try {
    const res = await zwavApi.sagActiveTask()
    if (!res?.success) {
      if (!silent) showAlert('error', res?.message || '获取当前任务失败')
      return
    }
    const task = res.data || null
    if (task?.id) {
      await loadTaskDetail(task.id, { silent: true })
      return
    }
    state.currentTask = null
    state.currentTaskId = 0
    renderTaskDetail()
    await loadTaskFiles(true)
  } catch (e) {
    if (!silent) showAlert('error', e.message || '获取当前任务失败')
  }
}

async function submitTaskAnalyze() {
  const ids = Array.from(state.analysis.selected)
  if (ids.length === 0) {
    showAlert('warning', '请先勾选要加入任务的录波文件')
    return
  }

  const referenceSelection = resolveReferenceSelection()
  const sagThresholdPct = Number(byId('sagThresholdPct').value || 90)
  const interruptThresholdPct = Number(byId('interruptThresholdPct').value || 10)
  const hysteresisPct = Number(byId('hysteresisPct').value || 2)
  const recoverThresholdPct = Number(byId('recoverThresholdPct').value || 0)
  const minDurationMs = Number(byId('minDurationMs').value || 10)

  if (referenceSelection.referenceType === 'CustomVoltage' && referenceSelection.referenceVoltage <= 0) {
    showAlert('warning', '自定义参考电压必须大于 0')
    return
  }
  if (sagThresholdPct <= 0 || sagThresholdPct > 100) {
    showAlert('warning', '暂降阈值必须在 0-100 之间')
    return
  }
  if (interruptThresholdPct <= 0 || interruptThresholdPct > 100) {
    showAlert('warning', '中断阈值必须在 0-100 之间')
    return
  }
  if (hysteresisPct < 0 || hysteresisPct > 20) {
    showAlert('warning', '迟滞必须在 0-20 之间')
    return
  }
  if (recoverThresholdPct > 0 && (recoverThresholdPct < sagThresholdPct || recoverThresholdPct > 100)) {
    showAlert('warning', '恢复阈值必须在暂降阈值到 100 之间')
    return
  }

  const activeTask = state.currentTask && !state.currentTask.isClosed ? state.currentTask : null
  const body = {
    taskId: activeTask?.id || null,
    createTask: !activeTask,
    fileIds: [],
    analysisGuids: ids,
    referenceType: referenceSelection.referenceType,
    referenceVoltage: referenceSelection.referenceVoltage,
    sagThresholdPct,
    recoverThresholdPct: recoverThresholdPct > 0 ? recoverThresholdPct : null,
    interruptThresholdPct,
    hysteresisPct,
    minDurationMs
  }

  try {
    const res = await zwavApi.sagAnalyze(body)
    if (!res?.success) {
      showAlert('error', res?.message || '加入暂降任务失败')
      return
    }

    const taskId = Number(res.data?.taskId || activeTask?.id || 0)
    const queuedCount = res.data?.queuedCount ?? res.data?.createdEventCount ?? 0
    state.analysis.selected.clear()
    renderAnalysisList()
    await loadAnalysisList(true)

    if (taskId > 0) {
      state.taskFiles.page = 1
      await loadTaskDetail(taskId, { silent: true, reloadFiles: true })
    } else {
      await loadActiveTask(true)
    }

    showAlert('success', `${activeTask ? '已追加录波文件到当前任务' : '暂降任务已创建'}，本次共提交 ${queuedCount} 条`)
  } catch (e) {
    showAlert('error', e.message || '加入暂降任务失败')
  }
}

function openRenameTaskDialog() {
  if (!state.currentTask?.id) {
    showAlert('warning', '当前没有可修改的任务')
    return
  }
  byId('taskNameInput').value = state.currentTask.taskName || ''
  openDialog('taskEditModal')
}

async function saveTaskName() {
  if (!state.currentTask?.id) {
    showAlert('warning', '当前没有可修改的任务')
    return
  }

  const taskName = String(byId('taskNameInput').value || '').trim()
  if (!taskName) {
    showAlert('warning', '任务名称不能为空')
    return
  }

  try {
    const res = await zwavApi.sagTaskUpdate(state.currentTask.id, { taskName })
    if (!res?.success) {
      showAlert('error', res?.message || '修改任务失败')
      return
    }
    closeDialog('taskEditModal')
    showAlert('success', '任务名称已更新')
    await loadTaskDetail(state.currentTask.id, { silent: true, reloadFiles: false })
    if (state.mode === 'list') await loadTaskList(true)
  } catch (e) {
    showAlert('error', e.message || '修改任务失败')
  }
}

async function closeCurrentTask() {
  if (!state.currentTask?.id) {
    showAlert('warning', '当前没有可关闭的任务')
    return
  }

  const confirmed = await showConfirmDialog('关闭任务', '关闭后将不再接收新的录波文件，是否继续？')
  if (!confirmed) return

  try {
    const res = await zwavApi.sagCloseTask(state.currentTask.id)
    if (!res?.success) {
      showAlert('error', res?.message || '关闭任务失败')
      return
    }
    showAlert('success', '任务已关闭')
    await loadTaskDetail(state.currentTask.id, { silent: true, reloadFiles: true })
    if (state.mode === 'list') await loadTaskList(true)
  } catch (e) {
    showAlert('error', e.message || '关闭任务失败')
  }
}

async function deleteTask(taskId) {
  const id = Number(taskId || state.currentTask?.id || 0)
  if (!id) {
    showAlert('warning', '当前没有可删除的任务')
    return
  }

  const task = Number(state.currentTask?.id || 0) === id
    ? state.currentTask
    : state.taskList.data.find((item) => Number(item.id) === id)
  const taskNo = task?.taskNo || `#${id}`
  const confirmed = await showConfirmDialog(
    '删除任务',
    `确定删除任务 ${taskNo} 吗？`,
    '删除后会级联删除该任务关联的暂降结果、相别结果和 RMS 点记录，且不可恢复。'
  )
  if (!confirmed) return

  try {
    const res = await zwavApi.sagTaskDelete(id)
    if (!res?.success) {
      showAlert('error', res?.message || '删除任务失败')
      return
    }

    showAlert('success', '任务及关联分析记录已删除')
    if (Number(state.currentTaskId) === id) {
      state.currentTask = null
      state.currentTaskId = 0
      renderTaskDetail()
      await loadTaskFiles(true)
    }
    if (state.mode === 'list') {
      await loadTaskList(true)
    } else {
      await loadActiveTask(true)
    }
  } catch (e) {
    showAlert('error', e.message || '删除任务失败')
  }
}

function startPolling() {
  stopPolling()
  state.polling = setInterval(async () => {
    if (state.currentTaskId > 0) {
      await loadTaskDetail(state.currentTaskId, { silent: true, reloadFiles: true })
    } else if (state.mode === 'create') {
      await loadActiveTask(true)
    }
    if (state.mode === 'list') {
      await loadTaskList(true)
    }
  }, 5000)
}

function stopPolling() {
  if (state.polling) clearInterval(state.polling)
  state.polling = null
}

async function initFromQuery() {
  const mode = String(qs('mode') || 'create').trim().toLowerCase()
  const taskId = Number(qs('taskId') || 0)
  state.queryTaskId = taskId > 0 ? taskId : 0
  setMode(mode === 'list' ? 'list' : 'create')

  if (state.mode === 'create') {
    resetAnalyzeFormDefaults()
    await loadAnalysisList(true)
    if (state.queryTaskId > 0) {
      await loadTaskDetail(state.queryTaskId, { silent: true, reloadFiles: true })
    } else {
      await loadActiveTask(true)
    }
  } else {
    await loadTaskList(true)
    if (state.queryTaskId > 0) {
      await loadTaskDetail(state.queryTaskId, { silent: true, reloadFiles: true })
    }
  }
}

function bindAnalysisEvents() {
  on('analysisKeyword', 'keydown', (e) => {
    if (e.key === 'Enter') {
      state.analysis.page = 1
      loadAnalysisList()
    }
  })
  on('btnAnalysisSearch', 'click', () => {
    state.analysis.page = 1
    loadAnalysisList()
  })
  on('analysisPageSize', 'change', () => {
    state.analysis.pageSize = Number(byId('analysisPageSize').value || 10)
    state.analysis.page = 1
    loadAnalysisList()
  })
  on('analysisPrev', 'click', () => {
    if (state.analysis.page > 1) state.analysis.page--
    loadAnalysisList()
  })
  on('analysisNext', 'click', () => {
    const totalPages = Math.max(1, Math.ceil(state.analysis.total / state.analysis.pageSize))
    if (state.analysis.page < totalPages) state.analysis.page++
    loadAnalysisList()
  })
  on('analysisCheckAll', 'change', () => {
    const allGuids = state.analysis.data.map((x) => x.analysisGuid).filter(Boolean)
    if (byId('analysisCheckAll').checked) {
      allGuids.forEach((guid) => state.analysis.selected.add(guid))
    } else {
      allGuids.forEach((guid) => state.analysis.selected.delete(guid))
    }
    renderAnalysisList()
  })

  const tbody = byId('analysisTbody')
  if (tbody) {
    tbody.addEventListener('change', (e) => {
      const target = e.target
      if (!target.classList.contains('analysis-check')) return
      const guid = String(target.getAttribute('data-guid') || '')
      if (target.checked) state.analysis.selected.add(guid)
      else state.analysis.selected.delete(guid)
      renderAnalysisList()
    })
  }
}

function bindTaskListEvents() {
  on('taskKeyword', 'keydown', (e) => {
    if (e.key === 'Enter') {
      state.taskList.page = 1
      loadTaskList()
    }
  })
  on('btnTaskSearch', 'click', () => {
    state.taskList.page = 1
    loadTaskList()
  })
  on('btnTaskListRefresh', 'click', () => loadTaskList())
  on('taskPageSize', 'change', () => {
    state.taskList.pageSize = Number(byId('taskPageSize').value || 10)
    state.taskList.page = 1
    loadTaskList()
  })
  on('taskPrev', 'click', () => {
    if (state.taskList.page > 1) state.taskList.page--
    loadTaskList()
  })
  on('taskNext', 'click', () => {
    const totalPages = Math.max(1, Math.ceil(state.taskList.total / state.taskList.pageSize))
    if (state.taskList.page < totalPages) state.taskList.page++
    loadTaskList()
  })
  on('btnListOpenCreate', 'click', () => {
    window.location.href = buildWorkbenchUrl('create', state.currentTask?.id || 0)
  })

  const tbody = byId('taskTbody')
  if (tbody) {
    tbody.addEventListener('click', async (e) => {
      const detailBtn = e.target.closest('.action-task-detail')
      if (detailBtn) {
        const taskId = Number(detailBtn.getAttribute('data-id'))
        state.taskFiles.page = 1
        await loadTaskDetail(taskId)
        return
      }

      const editBtn = e.target.closest('.action-task-edit')
      if (editBtn) {
        const taskId = Number(editBtn.getAttribute('data-id'))
        await loadTaskDetail(taskId, { silent: true, reloadFiles: false })
        openRenameTaskDialog()
        return
      }

      const workbenchBtn = e.target.closest('.action-task-workbench')
      if (workbenchBtn) {
        const taskId = Number(workbenchBtn.getAttribute('data-id'))
        openWorkbench('create', taskId)
        return
      }

      const deleteBtn = e.target.closest('.action-task-delete')
      if (deleteBtn) {
        const taskId = Number(deleteBtn.getAttribute('data-id'))
        if (!deleteBtn.disabled) await deleteTask(taskId)
      }
    })
  }
}

function bindTaskDetailEvents() {
  on('taskFileKeyword', 'keydown', (e) => {
    if (e.key === 'Enter') {
      state.taskFiles.page = 1
      loadTaskFiles()
    }
  })
  on('btnTaskFileSearch', 'click', () => {
    state.taskFiles.page = 1
    loadTaskFiles()
  })
  on('taskFilePageSize', 'change', () => {
    state.taskFiles.pageSize = Number(byId('taskFilePageSize').value || 10)
    state.taskFiles.page = 1
    loadTaskFiles()
  })
  on('taskFilePrev', 'click', () => {
    if (state.taskFiles.page > 1) state.taskFiles.page--
    loadTaskFiles()
  })
  on('taskFileNext', 'click', () => {
    const totalPages = Math.max(1, Math.ceil(state.taskFiles.total / state.taskFiles.pageSize))
    if (state.taskFiles.page < totalPages) state.taskFiles.page++
    loadTaskFiles()
  })
  on('btnRenameTask', 'click', openRenameTaskDialog)
  on('btnSaveTaskName', 'click', saveTaskName)
  on('btnCloseTask', 'click', closeCurrentTask)
  on('btnDeleteTask', 'click', () => deleteTask())
  on('btnRefreshTask', 'click', async () => {
    if (state.currentTaskId > 0) {
      await loadTaskDetail(state.currentTaskId)
    } else if (state.mode === 'create') {
      await loadActiveTask()
    }
  })
}

function bindModeAndPageEvents() {
  on('btnModeCreate', 'click', () => {
    window.location.href = buildWorkbenchUrl('create', state.currentTask?.id || 0)
  })
  on('btnModeList', 'click', () => {
    window.location.href = buildWorkbenchUrl('list', state.currentTask?.id || 0)
  })
  on('btnOpenListWindow', 'click', () => {
    openWorkbench(state.mode === 'create' ? 'list' : 'create', state.currentTask?.id || 0)
  })
  on('btnRefreshPage', 'click', async () => {
    if (state.mode === 'create') {
      await loadAnalysisList()
      if (state.currentTaskId > 0) await loadTaskDetail(state.currentTaskId)
      else await loadActiveTask()
    } else {
      await loadTaskList()
      if (state.currentTaskId > 0) await loadTaskDetail(state.currentTaskId)
    }
  })
  on('btnSubmitTaskAnalyze', 'click', submitTaskAnalyze)

  document.querySelectorAll('input[name="referenceTypeRadio"]').forEach((el) => {
    el.addEventListener('change', updateReferenceModeUi)
  })
  on('sagThresholdPct', 'input', () => syncRecoverThresholdIfAuto())
  on('hysteresisPct', 'input', () => syncRecoverThresholdIfAuto())
  on('recoverThresholdPct', 'input', () => {
    state.analyzeRecoverAuto = false
  })
}

async function init() {
  bindDialog('taskEditModal')
  bindAnalysisEvents()
  bindTaskListEvents()
  bindTaskDetailEvents()
  bindModeAndPageEvents()

  updateReferenceModeUi()
  await initFromQuery()
  renderAnalysisList()
  renderTaskList()
  renderTaskDetail()
  renderTaskFiles()
  startPolling()
}

window.addEventListener('beforeunload', stopPolling)

init()
