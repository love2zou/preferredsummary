import {
  bindDialog,
  byId,
  closeDialog,
  escapeHtml,
  formatDateTime,
  getApiBaseUrl,
  openDialog,
  qs,
  setApiBaseUrl,
  setHtml,
  setText
} from './common.js'
import { zwavApi } from './zwav-api.js'

const RULE_TAB_PHASE = 'phase'
const RULE_TAB_GROUP = 'group'

function createRuleListState() {
  return {
    loading: false,
    page: 1,
    pageSize: 10,
    total: 0,
    data: []
  }
}

const state = {
  loading: false,
  deleting: false,
  polling: null,
  selected: new Set(),
  page: 1,
  pageSize: 10,
  total: 0,
  data: [],

  analysis: {
    loading: false,
    page: 1,
    pageSize: 10,
    total: 0,
    data: [],
    selected: new Set(),
    taskContext: null,
    mode: 'legacy',
    taskPolling: null
  },

  tasks: {
    loading: false,
    page: 1,
    pageSize: 10,
    total: 0,
    data: [],
    detailTaskId: 0,
    detailTask: null,
    detailLoading: false,
    detailPage: 1,
    detailPageSize: 10,
    detailTotal: 0,
    detailData: [],
    polling: null
  },

  rules: {
    activeTab: RULE_TAB_PHASE,
    tabs: {
      [RULE_TAB_PHASE]: createRuleListState(),
      [RULE_TAB_GROUP]: createRuleListState()
    }
  },

  ruleEditing: {
    id: 0,
    isEdit: false,
    tab: RULE_TAB_PHASE
  },

  analyzeRecoverAuto: true
}

const ANALYZE_STATUS_MAP = {
  0: { type: 'warning', text: '待处理' },
  1: { type: 'primary', text: '处理中' },
  2: { type: 'success', text: '成功' },
  3: { type: 'danger', text: '失败' }
}

const TASK_STATUS_MAP = {
  Queued: { type: 'warning', text: '排队中' },
  Canceled: { type: 'info', text: '已取消' },
  Completed: { type: 'success', text: '已完成' },
  Failed: { type: 'danger', text: '失败' },
  ParsingRead: { type: 'primary', text: '读取录波' },
  ParsingExtract: { type: 'primary', text: '解压中' },
  ParsingCfg: { type: 'primary', text: '解析CFG' },
  ParsingHdr: { type: 'primary', text: '解析HDR' },
  ParsingHDR: { type: 'primary', text: '解析HDR' },
  ParsingChannel: { type: 'primary', text: '解析通道' },
  ParsingDat: { type: 'primary', text: '解析DAT数据' },
  ParsingDAT: { type: 'primary', text: '解析DAT数据' }
}

const RULE_TAB_META = {
  [RULE_TAB_PHASE]: {
    buttonId: 'btnRuleTabPhase',
    listApi: 'sagChannelRuleList',
    deleteApi: 'sagChannelRuleDelete',
    updateApi: 'sagChannelRuleUpdate',
    createApi: 'sagChannelRuleCreate',
    valueHeader: '相别',
    valueProp: 'phaseName',
    addTitle: '新增相别识别规则',
    editTitle: '编辑相别识别规则',
    deleteConfirmText: '确定要删除该相别识别规则吗？'
  },
  [RULE_TAB_GROUP]: {
    buttonId: 'btnRuleTabGroup',
    listApi: 'sagGroupRuleList',
    deleteApi: 'sagGroupRuleDelete',
    updateApi: 'sagGroupRuleUpdate',
    createApi: 'sagGroupRuleCreate',
    valueHeader: '分组名称',
    valueProp: 'groupName',
    addTitle: '新增分组识别规则',
    editTitle: '编辑分组识别规则',
    deleteConfirmText: '确定要删除该分组识别规则吗？'
  }
}

const PHASE_TYPE_PHASE = 1
const PHASE_TYPE_LINE = 2

function on(id, event, handler) {
  const el = byId(id)
  if (!el) return
  el.addEventListener(event, handler)
}

function openTaskWorkbenchWindow(mode, taskId) {
  const url = new URL('./ZwavSagTaskWorkbench.html', window.location.href)
  url.searchParams.set('mode', mode === 'list' ? 'list' : 'create')
  const apiBase = getApiBaseUrl()
  if (apiBase) url.searchParams.set('apiBase', apiBase)
  if (taskId) url.searchParams.set('taskId', String(taskId))
  window.open(url.toString(), '_blank', 'noopener')
}

function getAnalyzeStatusType(status) {
  return ANALYZE_STATUS_MAP[status]?.type || 'info'
}

function getAnalyzeStatusText(status) {
  return ANALYZE_STATUS_MAP[status]?.text || '-'
}

function getStatusType(status) {
  return TASK_STATUS_MAP[status]?.type || 'info'
}

function getStatusText(status) {
  return status ? TASK_STATUS_MAP[status]?.text || status : '-'
}

function formatPercent(v) {
  const n = Number(v)
  if (!Number.isFinite(n)) return '-'
  return n.toFixed(2)
}

function formatMs(v) {
  const n = Number(v)
  if (!Number.isFinite(n)) return '-'
  return n.toFixed(0)
}

function formatNumber(v) {
  const n = Number(v)
  if (!Number.isFinite(n)) return '-'
  return n.toFixed(2)
}

function normalizeProgress(v) {
  const n = Number(v)
  if (!Number.isFinite(n)) return 0
  if (n < 0) return 0
  if (n > 100) return 100
  return Math.round(n)
}

function formatEventType(type) {
  if (!type) return '-'
  if (type === 'Normal') return '正常'
  if (type === 'Sag') return '暂降'
  if (type === 'Interruption') return '中断'
  return type
}

function formatDateTimeMs(str) {
  if (!str) return '-'
  const d = new Date(str)
  if (isNaN(d.getTime())) return str
  const pad = (n, len = 2) => String(n).padStart(len, '0')
  const yyyy = d.getFullYear()
  const mm = pad(d.getMonth() + 1)
  const dd = pad(d.getDate())
  const hh = pad(d.getHours())
  const mi = pad(d.getMinutes())
  const ss = pad(d.getSeconds())
  const ms = pad(d.getMilliseconds(), 3)
  return `${yyyy}-${mm}-${dd} ${hh}:${mi}:${ss}.${ms}`
}

function getDateRangeParams() {
  const fromDate = String(byId('fromDate').value || '').trim()
  const toDate = String(byId('toDate').value || '').trim()
  const params = {}

  if (fromDate) {
    const from = new Date(fromDate)
    if (!isNaN(from.getTime())) {
      params.fromUtc = from.toISOString()
    }
  }

  if (toDate) {
    const end = new Date(toDate)
    if (!isNaN(end.getTime())) {
      end.setDate(end.getDate() + 1)
      params.toUtc = end.toISOString()
    }
  }

  return params
}

function updatePagination(total, page, pageSize, totalTextId, pageTextId, prevBtnId, nextBtnId) {
  const totalPages = Math.max(1, Math.ceil(total / pageSize))
  setText(totalTextId, `共 ${total} 条`)
  setText(pageTextId, `${page}/${totalPages}`)
  byId(prevBtnId).disabled = page <= 1
  byId(nextBtnId).disabled = page >= totalPages
}

function getRuleTabMeta(tab = state.rules.activeTab) {
  return RULE_TAB_META[tab] || RULE_TAB_META[RULE_TAB_PHASE]
}

function getRuleListState(tab = state.rules.activeTab) {
  return state.rules.tabs[tab] || state.rules.tabs[RULE_TAB_PHASE]
}

function getDefaultPhaseType(phaseName) {
  const value = String(phaseName || '').trim().toUpperCase()
  return value === 'AB' || value === 'BC' || value === 'CA' ? PHASE_TYPE_LINE : PHASE_TYPE_PHASE
}

function getDefaultPhaseValue(phaseType) {
  return Number(phaseType) === PHASE_TYPE_LINE ? 100 : 57.74
}

function getPhaseTypeText(phaseType) {
  return Number(phaseType) === PHASE_TYPE_LINE ? '绾跨數鍘?' : '鐩哥數鍘?'
}

function formatPhaseValue(value) {
  const n = Number(value)
  if (!Number.isFinite(n) || n <= 0) return '-'
  return n.toFixed(6).replace(/\.?0+$/, '')
}

function getDisplayPhaseValue(row) {
  const phaseType = Number(row?.phaseType || getDefaultPhaseType(row?.phaseName))
  const phaseValue = Number(row?.phaseValue)
  if (Number.isFinite(phaseValue) && phaseValue > 0) return phaseValue
  return getDefaultPhaseValue(phaseType)
}

function getSafePhaseTypeText(phaseType) {
  return Number(phaseType) === PHASE_TYPE_LINE
    ? '\u7ebf\u7535\u538b'
    : '\u76f8\u7535\u538b'
}

function ensurePhaseRuleExtraHeaders() {
  const valueHeader = byId('ruleValueHeader')
  if (!valueHeader || byId('rulePhaseTypeHeader') || !valueHeader.parentNode) return

  valueHeader.insertAdjacentHTML(
    'afterend',
    '<th class="cell-center" style="width: 96px" id="rulePhaseTypeHeader">鐢靛帇绫诲瀷</th>' +
    '<th class="cell-center" style="width: 110px" id="rulePhaseValueHeader">鐩稿埆鐢靛帇鍊?V)</th>'
  )
}

function syncPhaseRuleLabels() {
  const phaseTypeHeader = byId('rulePhaseTypeHeader')
  if (phaseTypeHeader) phaseTypeHeader.textContent = '\u7535\u538b\u7c7b\u578b'

  const phaseValueHeader = byId('rulePhaseValueHeader')
  if (phaseValueHeader) phaseValueHeader.textContent = '\u76f8\u522b\u7535\u538b\u503c(V)'

  const phaseTypeWrap = byId('phaseTypeFieldWrap')
  if (phaseTypeWrap) {
    const label = phaseTypeWrap.querySelector('label')
    if (label) label.textContent = '\u7535\u538b\u7c7b\u578b'
  }

  const phaseTypeSelect = byId('phaseTypeSelect')
  if (phaseTypeSelect && phaseTypeSelect.options.length >= 2) {
    phaseTypeSelect.options[0].text = '\u76f8\u7535\u538b'
    phaseTypeSelect.options[1].text = '\u7ebf\u7535\u538b'
  }

  const phaseValueWrap = byId('phaseValueFieldWrap')
  if (phaseValueWrap) {
    const label = phaseValueWrap.querySelector('label')
    if (label) label.textContent = '\u76f8\u522b\u7535\u538b\u503c(V)'
  }

  const phaseValueInput = byId('phaseValueInput')
  if (phaseValueInput) {
    phaseValueInput.placeholder = '\u4f8b\u5982\uff1a57.74 \u6216 100'
  }
}

function syncPhaseRuleValueByType(force = false) {
  const phaseTypeEl = byId('phaseTypeSelect')
  const phaseValueEl = byId('phaseValueInput')
  if (!phaseTypeEl || !phaseValueEl) return

  const next = getDefaultPhaseValue(Number(phaseTypeEl.value || PHASE_TYPE_PHASE))
  const current = Number(phaseValueEl.value || 0)
  if (force || !Number.isFinite(current) || current <= 0) {
    phaseValueEl.value = String(next)
  }
}

function syncRuleEditUi(tab = state.ruleEditing.tab || state.rules.activeTab) {
  const isPhase = tab === RULE_TAB_PHASE
  const phaseFieldWrap = byId('phaseFieldWrap')
  const phaseTypeFieldWrap = byId('phaseTypeFieldWrap')
  const phaseValueFieldWrap = byId('phaseValueFieldWrap')
  const enabledFieldWrap = byId('enabledFieldWrap')
  const groupFieldWrap = byId('groupFieldWrap')

  if (phaseFieldWrap) phaseFieldWrap.style.display = isPhase ? '' : 'none'
  if (phaseTypeFieldWrap) phaseTypeFieldWrap.style.display = isPhase ? '' : 'none'
  if (phaseValueFieldWrap) phaseValueFieldWrap.style.display = isPhase ? '' : 'none'
  if (enabledFieldWrap) enabledFieldWrap.style.display = isPhase ? '' : 'none'
  if (groupFieldWrap) groupFieldWrap.style.display = isPhase ? 'none' : ''
}

function syncRuleTabUi() {
  ensurePhaseRuleExtraHeaders()
  syncPhaseRuleLabels()
  const meta = getRuleTabMeta()
  const activeTab = state.rules.activeTab

  Object.values(RULE_TAB_META).forEach((item) => {
    const btn = byId(item.buttonId)
    if (!btn) return
    btn.classList.toggle('is-active', item === meta)
  })

  setText('ruleValueHeader', meta.valueHeader)
  const phaseTypeHeader = byId('rulePhaseTypeHeader')
  if (phaseTypeHeader) phaseTypeHeader.style.display = activeTab === RULE_TAB_PHASE ? '' : 'none'
  const phaseValueHeader = byId('rulePhaseValueHeader')
  if (phaseValueHeader) phaseValueHeader.style.display = activeTab === RULE_TAB_PHASE ? '' : 'none'
  const enabledHeader = byId('ruleEnabledHeader')
  if (enabledHeader) enabledHeader.style.display = activeTab === RULE_TAB_PHASE ? '' : 'none'
  const enabledFilter = byId('ruleEnabledFilter')
  if (enabledFilter) enabledFilter.style.display = activeTab === RULE_TAB_PHASE ? '' : 'none'
  syncRuleEditUi(activeTab)
}

function getEventNameById(id) {
  const row = state.data.find((item) => Number(item.id) === Number(id))
  return row?.originalName || `ID ${id}`
}

async function switchRuleTab(tab) {
  if (!RULE_TAB_META[tab] || state.rules.activeTab === tab) return

  state.rules.activeTab = tab
  const ruleState = getRuleListState(tab)
  byId('rulePageSize').value = String(ruleState.pageSize)
  syncRuleTabUi()
  await loadRules()
}

function render() {
  updatePagination(state.total, state.page, state.pageSize, 'totalText', 'pageText', 'btnPrev', 'btnNext')
  byId('btnBatchDelete').disabled = state.selected.size === 0 || state.deleting
  byId('btnSearch').disabled = state.deleting
  byId('btnReset').disabled = state.deleting
  byId('pageSize').disabled = state.deleting
  byId('btnPrev').disabled = state.deleting || byId('btnPrev').disabled
  byId('btnNext').disabled = state.deleting || byId('btnNext').disabled

  const checkAll = byId('checkAll')
  const allIds = state.data.map(x => x.id).filter(x => x !== undefined && x !== null)
  const checkedCount = allIds.filter(id => state.selected.has(id)).length
  checkAll.checked = allIds.length > 0 && checkedCount === allIds.length
  checkAll.indeterminate = checkedCount > 0 && checkedCount < allIds.length
  checkAll.disabled = state.deleting

  const tbody = state.data.map((row, idx) => {
    const id = row.id
    const checked = state.selected.has(id) ? 'checked' : ''
    const disabledAttr = state.deleting ? 'disabled' : ''
    const statusType = getAnalyzeStatusType(row.status)
    const statusText = getAnalyzeStatusText(row.status)
    const progress = Number(row.status) === 2 || Number(row.status) === 3
      ? 100
      : normalizeProgress(row.progress)
    const progressInnerClass = row.status === 3 ? 'is-exception' : row.status === 2 ? 'is-success' : ''
    const progressBar = `
      <div class="el-progress">
        <div class="el-progress-bar">
          <div class="el-progress-bar__inner ${progressInnerClass}" style="width:${progress}%"></div>
        </div>
        <div class="el-progress__text">${progress}%</div>
      </div>
    `
    const hasSag = !!row.hasSag
    const occur = row.occurTimeText ? String(row.occurTimeText) : (row.occurTimeUtc ? formatDateTimeMs(row.occurTimeUtc) : '-')
    const errorRaw = row.errorMessage ? String(row.errorMessage) : ''
    const errorText = errorRaw
      ? `<span class="error-text" title="${escapeHtml(errorRaw)}">${escapeHtml(errorRaw)}</span>`
      : '-'
    const eventTypeText = formatEventType(row.eventType)
    const worstRaw = row.worstPhase ? String(row.worstPhase) : ''
    const worstText = worstRaw ? worstRaw : '-'

    return `
      <tr>
        <td class="cell-center"><input type="checkbox" class="row-check" data-id="${escapeHtml(id)}" ${checked} ${disabledAttr}></td>
        <td class="cell-center">${idx + 1 + (state.page - 1) * state.pageSize}</td>
        <td class="file-name-cell" title="${escapeHtml(row.originalName || '')}">${escapeHtml(row.originalName || '-')}</td>
        <td><span class="el-tag el-tag--${statusType}">${escapeHtml(statusText)}</span></td>
        <td class="sag-progress-cell">${progressBar}</td>
        <td class="cell-center">
          <span class="el-tag ${hasSag ? 'el-tag--danger' : 'el-tag--success'}">${hasSag ? '有' : '无'}</span>
        </td>
        <td>${escapeHtml(eventTypeText)}</td>
        <td class="cell-center" title="${escapeHtml(worstRaw)}">${escapeHtml(worstText)}</td>
        <td>${escapeHtml(occur)}</td>
        <td>${escapeHtml(formatMs(row.durationMs))}</td>
        <td>${escapeHtml(formatPercent(row.sagPercent))}</td>
        <td>${escapeHtml(formatPercent(row.residualVoltagePct))}</td>
        <td class="file-name-cell">${errorText}</td>
        <td class="cell-actions">
          <button class="el-button is-link action-process" data-id="${escapeHtml(id)}" ${disabledAttr}>暂降分析</button>
          <button class="el-button is-link action-view" data-fileid="${escapeHtml(row.fileId)}" ${disabledAttr}>录波浏览</button>
          <button class="el-button is-link action-delete" data-id="${escapeHtml(id)}" style="color: var(--el-color-danger)" ${disabledAttr}>删除</button>
        </td>
      </tr>
    `
  }).join('')

  setHtml('tbody', tbody)

  if (state.deleting) {
    byId('tbody')?.querySelectorAll('button').forEach((button) => {
      button.disabled = true
    })
  }
}

async function loadList(silent = false) {
  state.loading = true
  try {
    const params = {
      keyword: (byId('keyword').value || '').trim() || undefined,
      eventType: (byId('eventType').value || '').trim() || undefined,
      phase: (byId('phase').value || '').trim() || undefined,
      page: state.page,
      pageSize: state.pageSize,
      ...getDateRangeParams()
    }

    const res = await zwavApi.sagList(params)
    if (!res || !res.success) {
      if (!silent) showAlert('error', res?.message || '查询失败')
      return
    }

    state.data = res.data?.data || []
    state.total = res.data?.total || 0

    const presentIds = new Set(state.data.map(x => x.id))
    Array.from(state.selected).forEach(id => {
      if (!presentIds.has(id)) state.selected.delete(id)
    })

    render()
  } catch (e) {
    if (!silent) showAlert('error', e.message || '查询失败')
  } finally {
    state.loading = false
  }
}

function hasRunningSagAnalysis() {
  return state.data.some((row) => Number(row.status) === 1 || (normalizeProgress(row.progress) > 0 && normalizeProgress(row.progress) < 100))
}

function startPolling() {
  if (state.polling) clearInterval(state.polling)
  state.polling = setInterval(() => {
    if (state.loading || state.deleting) return
    if (!hasRunningSagAnalysis()) return
    loadList(true)
  }, 5000)
}

async function batchDeleteLegacy() {
  const ids = Array.from(state.selected)
  if (!ids.length) return

  const confirmed = await showConfirmDialog('确认删除', `确定要批量删除选中的 ${ids.length} 个事件吗？`, '此操作不可恢复')
  if (!confirmed) return

  let ok = 0
  let fail = 0

  for (const id of ids) {
    try {
      const res = await zwavApi.sagDelete(id)
      if (res?.success) ok++
      else fail++
    } catch {
      fail++
    }
  }

  if (fail === 0) {
    showAlert('success', `成功删除 ${ok} 条记录`)
  } else {
    showAlert('warning', `删除完成：成功 ${ok} 条，失败 ${fail} 条`)
  }

  state.selected.clear()
  await loadList()
}

function openApiModal() {
  byId('apiBaseInput').value = getApiBaseUrl()
  openDialog('apiModal')
}

function openSagProcess(id) {
  const u = new URL('./ZwavSagProcess.html', window.location.href)
  u.searchParams.set('id', id)
  window.open(u.toString(), '_blank')
}

async function viewOnline(fileId) {
  if (!fileId) return
  try {
    const r = await zwavApi.createAnalysis(fileId, false)
    const guid = r?.data?.analysisGuid
    if (!r?.success || !guid) {
      showAlert('error', r?.message || '获取解析任务失败')
      return
    }
    const u = new URL('./ZwavOnlineViewer.html', window.location.href)
    u.searchParams.set('guid', guid)
    window.open(u.toString(), '_blank')
  } catch (e) {
    showAlert('error', e.message || '打开在线浏览失败')
  }
}

async function handleDeleteLegacy(id) {
  if (!id) return

  const confirmed = await showConfirmDialog('确认删除', '确定要删除该事件吗？', '此操作不可恢复')
  if (!confirmed) return

  try {
    const res = await zwavApi.sagDelete(id)
    if (res?.success) {
      state.selected.delete(id)
      showAlert('success', '删除成功')
      await loadList()
    } else {
      showAlert('error', res?.message || '删除失败')
    }
  } catch (e) {
    showAlert('error', e.message || '删除失败')
  }
}

function showAlert(type, message) {
  const alertDiv = document.createElement('div')
  alertDiv.className = `el-alert el-alert--${type}`
  alertDiv.style.cssText = `
    position: fixed;
    top: 20px;
    right: 20px;
    z-index: 9999;
    min-width: 300px;
    padding: 12px 16px;
    border-radius: 4px;
    margin-bottom: 10px;
  `
  alertDiv.innerHTML = `
    <div class="el-alert__content">
      <span class="el-alert__title">${escapeHtml(message)}</span>
    </div>
  `
  document.body.appendChild(alertDiv)

  setTimeout(() => {
    alertDiv.remove()
  }, 3000)
}

function showConfirmDialog(title, message, warningText = '') {
  return new Promise((resolve) => {
    const confirmDialog = document.createElement('div')
    confirmDialog.className = 'el-dialog-mask'
    confirmDialog.classList.add('is-open')
    confirmDialog.innerHTML = `
      <div class="el-dialog" style="width: 400px;">
        <div class="el-dialog__header">
          <div class="el-dialog__title">${escapeHtml(title)}</div>
          <button class="el-dialog__close" data-action="cancel">×</button>
        </div>
        <div class="el-dialog__body">
          <p>${escapeHtml(message)}</p>
          ${warningText ? `<p style="color: var(--el-color-danger); font-size: 12px; margin-top: 8px;">${escapeHtml(warningText)}</p>` : ''}
        </div>
        <div class="el-dialog__footer">
          <button class="el-button" data-action="cancel">取消</button>
          <button class="el-button el-button--danger" data-action="confirm">确定删除</button>
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
    if (failures.length > 0) {
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
  if (state.deleting || !items.length) return

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
        const res = await zwavApi.sagDelete(item.id)
        if (res?.success) {
          successCount++
          successIds.push(item.id)
        } else {
          failCount++
          failures.push({
            id: item.id,
            name: item.name,
            message: res?.message || '删除失败'
          })
        }
      } catch (error) {
        failCount++
        failures.push({
          id: item.id,
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

    successIds.forEach((id) => state.selected.delete(id))
    await loadList()
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

async function batchDelete() {
  const ids = Array.from(state.selected)
  if (!ids.length) return

  const confirmed = await showConfirmDialog('确认删除', `确定要批量删除选中的 ${ids.length} 个事件吗？`, '此操作不可恢复')
  if (!confirmed) return

  await runDeleteWithProgress(ids.map((id) => ({
    id,
    name: getEventNameById(id)
  })))
}

async function handleDelete(id) {
  if (!id) return

  const confirmed = await showConfirmDialog('确认删除', '确定要删除该事件吗？', '此操作不可恢复')
  if (!confirmed) return

  await runDeleteWithProgress([{
    id,
    name: getEventNameById(id)
  }])
}

async function loadAnalyses() {
  state.analysis.loading = true
  try {
    const params = {
      page: state.analysis.page,
      pageSize: state.analysis.pageSize,
      keyword: (byId('analysisKeyword').value || '').trim() || undefined,
      status: 'Completed'
    }

    const res = await zwavApi.getList(params)
    if (!res?.success) {
      showAlert('error', res?.message || '获取解析任务失败')
      return
    }

    state.analysis.data = res.data?.data || []
    state.analysis.total = res.data?.total || 0
    renderAnalyses()
  } catch (e) {
    showAlert('error', e.message || '获取解析任务失败')
  } finally {
    state.analysis.loading = false
  }
}

function renderAnalyses() {
  updatePagination(
    state.analysis.total,
    state.analysis.page,
    state.analysis.pageSize,
    'analysisTotalText',
    'analysisPageText',
    'analysisPrev',
    'analysisNext'
  )

  const allGuids = state.analysis.data.map(x => x.analysisGuid).filter(Boolean)
  const checkedCount = allGuids.filter(g => state.analysis.selected.has(g)).length
  const checkAll = byId('analysisCheckAll')
  checkAll.checked = allGuids.length > 0 && checkedCount === allGuids.length
  checkAll.indeterminate = checkedCount > 0 && checkedCount < allGuids.length

  const tbody = state.analysis.data.map((row, idx) => {
    const guid = row.analysisGuid
    const checked = state.analysis.selected.has(guid) ? 'checked' : ''
    const name = row.originalName || '-'
    const status = getStatusText(row.status || '-')
    const statusType = getStatusType(row.status || '-')
    const fileSize = row.fileSize !== undefined && row.fileSize !== null ? String(row.fileSize) : '-'

    return `
      <tr>
        <td class="cell-center"><input type="checkbox" class="analysis-check" data-guid="${escapeHtml(guid)}" ${checked}></td>
        <td class="cell-center">${idx + 1 + (state.analysis.page - 1) * state.analysis.pageSize}</td>
        <td class="file-name-cell" title="${escapeHtml(name)}">${escapeHtml(name)}</td>
        <td><span class="el-tag el-tag--${statusType}">${escapeHtml(status)}</span></td>
        <td>${escapeHtml(fileSize)}</td>
        <td>${escapeHtml(row.crtTime ? formatDateTime(row.crtTime) : '-')}</td>
      </tr>
    `
  }).join('')

setHtml('analysisTbody', tbody)
}

/*
function getSagTaskStatusMeta(task) {
  const status = Number(task?.status)
  if (status === 0) return { type: 'warning', text: '接收中' }
  if (status === 1) return { type: 'primary', text: '已关闭待收尾' }
  if (status === 2) return { type: 'success', text: '已完成' }
  if (status === 3) return { type: 'danger', text: '已完成（有失败）' }
  return { type: 'info', text: '未创建' }
}

function selectReferenceMode(mode, voltage) {
  const value = String(mode || 'Adaptive')
  const radio = document.querySelector(`input[name="referenceTypeRadio"][value="${value}"]`)
  if (radio) radio.checked = true
  const customInput = byId('customReferenceVoltage')
  if (customInput && Number.isFinite(Number(voltage)) && Number(voltage) > 0) {
    customInput.value = String(voltage)
  }
  updateAnalyzeReferenceModeUi()
}

function toggleAnalyzeParamsDisabled(disabled) {
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

function applyTaskParamsToForm(task) {
  if (!task) {
    toggleAnalyzeParamsDisabled(false)
    const defaultReferenceRadio = document.querySelector('input[name="referenceTypeRadio"][value="Adaptive"]')
    if (defaultReferenceRadio) defaultReferenceRadio.checked = true
    const customReferenceInput = byId('customReferenceVoltage')
    if (customReferenceInput) customReferenceInput.value = ''
    updateAnalyzeReferenceModeUi()
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
  toggleAnalyzeParamsDisabled(true)
}

function renderSagTaskSummary() {
  const task = state.analysis.activeTask
  const statusMeta = getSagTaskStatusMeta(task)
  const statusTag = byId('sagTaskStatusTag')
  if (statusTag) {
    statusTag.className = `el-tag el-tag--${statusMeta.type}`
    statusTag.textContent = statusMeta.text
  }

  setText(
    'sagTaskMetaText',
    task
      ? `${task.taskNo || '-'} | ${task.isClosed ? '已关闭，不再接收新文件' : '未关闭，可继续追加录波文件'}`
      : '当前没有活动任务，首次加入录波文件时会自动创建。'
  )
  setText('sagTaskSummaryText', task?.summaryText || '当前暂无活动任务。')
  setText(
    'sagTaskFootText',
    task
      ? `当前进度 ${Number(task.progress || 0)}%，已完成 ${Number(task.finishedFileCount || 0)}/${Number(task.receivedFileCount || 0)}。`
      : '任务未关闭前，可以多次勾选录波文件继续追加到同一任务。'
  )

  const closeBtn = byId('btnCloseSagTask')
  if (closeBtn) closeBtn.disabled = !task
}

async function loadActiveSagTask(taskId) {
  try {
    const res = taskId ? await zwavApi.sagTaskDetail(taskId) : await zwavApi.sagActiveTask()
    state.analysis.activeTask = res?.success ? (res.data || null) : null
  } catch {
    state.analysis.activeTask = null
  }

  renderSagTaskSummary()
  applyTaskParamsToForm(state.analysis.activeTask)
}

function stopSagTaskPolling() {
  if (state.analysis.taskPolling) {
    clearInterval(state.analysis.taskPolling)
    state.analysis.taskPolling = null
  }
}

function startSagTaskPolling() {
  stopSagTaskPolling()
  state.analysis.taskPolling = setInterval(() => {
    const modal = byId('analyzeModal')
    if (!modal || !modal.classList.contains('is-open')) return
    if (!state.analysis.activeTask?.id) return
    loadActiveSagTask(state.analysis.activeTask.id)
  }, 5000)
}

function ensureSagTaskUi() {
  if (!document.getElementById('zwavSagTaskStyle')) {
    const style = document.createElement('style')
    style.id = 'zwavSagTaskStyle'
    style.textContent = `
      .task-summary-card{margin-bottom:14px;padding:14px 16px;border:1px solid #dcdfe6;border-radius:12px;background:linear-gradient(180deg,#f9fbff 0%,#ffffff 100%)}
      .task-summary-head{display:flex;align-items:flex-start;justify-content:space-between;gap:12px;margin-bottom:8px}
      .task-summary-title{font-size:15px;font-weight:600;color:#303133}
      .task-summary-sub{margin-top:4px;font-size:12px;color:#909399}
      .task-summary-body{color:#303133;line-height:1.8}
      .task-summary-foot{margin-top:8px;font-size:12px;color:#606266}
    `
    document.head.appendChild(style)
  }

  if (!byId('btnCloseSagTask')) {
    const footer = byId('analyzeModal')?.querySelector('.el-dialog__footer')
    const startBtn = byId('btnStartAnalyze')
    if (footer && startBtn) {
      const btn = document.createElement('button')
      btn.className = 'el-button el-button--danger'
      btn.id = 'btnCloseSagTask'
      btn.textContent = '关闭当前任务'
      footer.insertBefore(btn, startBtn)
    }
  }
}

function getSagTaskStatusMeta(task) {
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

function isDialogOpen(id) {
  return !!byId(id)?.classList.contains('is-open')
}

function getTaskMetaText(task) {
  if (!task) return '当前会创建一个新的暂降分析任务，后续可以在任务详情中继续追加录波文件。'
  const taskName = task.taskName || '暂降分析任务'
  const statusText = task.isClosed ? '已关闭，不可再追加文件' : '未关闭，可继续追加文件'
  return `${task.taskNo || '-'} | ${taskName} | ${statusText}`
}

function resetAnalyzeFormDefaults() {
  const defaultReferenceRadio = document.querySelector('input[name="referenceTypeRadio"][value="Adaptive"]')
  if (defaultReferenceRadio) defaultReferenceRadio.checked = true
  const customReferenceInput = byId('customReferenceVoltage')
  if (customReferenceInput) customReferenceInput.value = ''
  byId('sagThresholdPct').value = '90'
  byId('interruptThresholdPct').value = '10'
  byId('hysteresisPct').value = '2'
  byId('minDurationMs').value = '10'
  state.analyzeRecoverAuto = true
  syncAnalyzeRecoverThresholdIfAuto()
  updateAnalyzeReferenceModeUi()
}

function setAnalyzeTaskContext(task, mode) {
  state.analysis.taskContext = task || null
  state.analysis.mode = mode || (task ? 'append' : 'create')
  renderSagTaskSummary()
  applyTaskParamsToForm(state.analysis.taskContext)
}

function renderSagTaskSummary() {
  const task = state.analysis.taskContext
  const mode = state.analysis.mode || 'create'
  const statusMeta = getSagTaskStatusMeta(task)
  const statusTag = byId('sagTaskStatusTag')
  if (statusTag) {
    statusTag.className = `el-tag el-tag--${statusMeta.type}`
    statusTag.textContent = task ? statusMeta.text : '新任务'
  }

  setText('analyzeModalTitle', task ? '向已有任务追加录波文件' : '创建暂降任务')
  setText('sagTaskMetaText', getTaskMetaText(task))
  setText(
    'sagTaskSummaryText',
    task?.summaryText || '提交后会立即创建任务，并把当前勾选的录波文件加入该任务进行暂降分析。'
  )
  setText(
    'sagTaskFootText',
    task
      ? `当前进度 ${Number(task.progress || 0)}%，已完成 ${Number(task.finishedFileCount || 0)}/${Number(task.receivedFileCount || 0)}，预计剩余时间 ${formatDurationHms(task.estimatedRemainingMs)}。`
      : '任务创建后，只要没有被手动关闭，就可以在任务详情中继续追加录波文件。'
  )
  setText('btnStartAnalyze', mode === 'append' ? '追加并开始分析' : '创建并开始分析')
}

async function loadActiveSagTask(taskId) {
  if (!taskId) {
    setAnalyzeTaskContext(null, 'create')
    return
  }

  try {
    const res = await zwavApi.sagTaskDetail(taskId)
    setAnalyzeTaskContext(res?.success ? (res.data || null) : null, 'append')
  } catch {
    setAnalyzeTaskContext(null, 'create')
  }
}

function stopSagTaskPolling() {
  if (state.analysis.taskPolling) clearInterval(state.analysis.taskPolling)
  state.analysis.taskPolling = null
}

function startSagTaskPolling() {
  stopSagTaskPolling()
}

function ensureSagTaskUi() {
  if (!document.getElementById('zwavSagTaskStyle')) {
    const style = document.createElement('style')
    style.id = 'zwavSagTaskStyle'
    style.textContent = `
      .task-summary-card{margin-bottom:14px;padding:14px 16px;border:1px solid #dcdfe6;border-radius:12px;background:linear-gradient(180deg,#f9fbff 0%,#ffffff 100%)}
      .task-summary-head{display:flex;align-items:flex-start;justify-content:space-between;gap:12px;margin-bottom:8px}
      .task-summary-title{font-size:15px;font-weight:600;color:#303133}
      .task-summary-sub{margin-top:4px;font-size:12px;color:#909399}
      .task-summary-body{color:#303133;line-height:1.8}
      .task-summary-foot{margin-top:8px;font-size:12px;color:#606266}
    `
    document.head.appendChild(style)
  }
}

*/
/*
function openAnalyzeModalForCreate() {
  state.analysis.page = 1
  state.analysis.selected.clear()
  byId('analysisKeyword').value = ''
  byId('analysisPageSize').value = String(state.analysis.pageSize)
  resetAnalyzeFormDefaults()
  setAnalyzeTaskContext(null, 'create')
  openDialog('analyzeModal')
  loadAnalyses()
}

async function openAnalyzeModalForAppend(task) {
  const targetTask = task?.id ? task : state.tasks.detailTask
  if (!targetTask?.id) {
    showAlert('warning', '请先选择一个暂降任务')
    return
  }
  if (targetTask.isClosed) {
    showAlert('warning', '当前任务已关闭，不能再追加录波文件')
    return
  }

  state.analysis.page = 1
  state.analysis.selected.clear()
  byId('analysisKeyword').value = ''
  byId('analysisPageSize').value = String(state.analysis.pageSize)
  resetAnalyzeFormDefaults()
  openDialog('analyzeModal')
  await loadActiveSagTask(targetTask.id)
  await loadAnalyses()
}

*/
async function loadTaskList(silent = false) {
  state.tasks.loading = true
  try {
    const statusRaw = String(byId('taskStatusFilter')?.value || '').trim()
    const closedRaw = String(byId('taskClosedFilter')?.value || '').trim()
    const params = {
      keyword: (byId('taskKeyword')?.value || '').trim() || undefined,
      status: statusRaw === '' ? undefined : Number(statusRaw),
      isClosed: closedRaw === '' ? undefined : closedRaw === 'true',
      page: state.tasks.page,
      pageSize: state.tasks.pageSize
    }
    const res = await zwavApi.sagTaskList(params)
    if (!res?.success) {
      if (!silent) showAlert('error', res?.message || '获取暂降任务列表失败')
      return
    }
    state.tasks.data = res.data?.data || []
    state.tasks.total = res.data?.total || 0
    renderTaskList()
  } catch (e) {
    if (!silent) showAlert('error', e.message || '获取暂降任务列表失败')
  } finally {
    state.tasks.loading = false
  }
}

function renderTaskList() {
  updatePagination(
    state.tasks.total,
    state.tasks.page,
    state.tasks.pageSize,
    'taskTotalText',
    'taskPageText',
    'taskPrev',
    'taskNext'
  )

  const rows = state.tasks.data.map((row, idx) => {
    const statusMeta = getSagTaskStatusMeta(row)
    return `
      <tr>
        <td class="cell-center">${idx + 1 + (state.tasks.page - 1) * state.tasks.pageSize}</td>
        <td>${escapeHtml(row.taskNo || '-')}</td>
        <td>${escapeHtml(row.taskName || '-')}</td>
        <td><span class="el-tag el-tag--${statusMeta.type}">${escapeHtml(statusMeta.text)}</span></td>
        <td class="cell-center">${Number(row.receivedFileCount || 0)}</td>
        <td class="cell-center">${Number(row.finishedFileCount || 0)}</td>
        <td class="cell-center">${Number(row.pendingFileCount || 0)}</td>
        <td>${escapeHtml(row.startParseTime ? formatDateTime(row.startParseTime) : '-')}</td>
        <td>${escapeHtml(row.finishParseTime ? formatDateTime(row.finishParseTime) : '-')}</td>
        <td class="cell-actions">
          <button class="el-button is-link action-task-detail" data-id="${escapeHtml(row.id)}">详情</button>
        </td>
      </tr>
    `
  }).join('')

  setHtml('taskTbody', rows)
}

async function loadTaskDetail(taskId, options = {}) {
  const { silent = false, reloadFiles = true } = options
  if (!taskId) return
  state.tasks.detailLoading = true
  state.tasks.detailTaskId = Number(taskId)
  try {
    const res = await zwavApi.sagTaskDetail(taskId)
    if (!res?.success) {
      if (!silent) showAlert('error', res?.message || '获取暂降任务详情失败')
      return
    }
    state.tasks.detailTask = res.data || null
    renderTaskDetail()
    if (reloadFiles) await loadTaskFiles(silent)
  } catch (e) {
    if (!silent) showAlert('error', e.message || '获取暂降任务详情失败')
  } finally {
    state.tasks.detailLoading = false
  }
}

function renderTaskDetail() {
  const task = state.tasks.detailTask
  const statusMeta = getSagTaskStatusMeta(task)
  setText('taskDetailTitle', task ? `暂降任务详情 - ${task.taskNo || task.id}` : '暂降任务详情')
  setText('taskDetailTaskNo', task ? `${task.taskNo || '-'} / ${task.taskName || '暂降分析任务'}` : '-')
  setText('taskDetailMeta', task ? getTaskMetaText(task) : '-')
  setText('taskDetailSummary', task?.summaryText || '-')
  setText(
    'taskDetailFoot',
    task
      ? `参考电压处理：${task.referenceType || 'Adaptive'}；参考电压值：${task.referenceVoltage ?? '-'}；暂降阈值：${task.sagThresholdPct ?? '-'}%。`
      : '-'
  )
  const statusTag = byId('taskDetailStatusTag')
  if (statusTag) {
    statusTag.className = `el-tag el-tag--${statusMeta.type}`
    statusTag.textContent = statusMeta.text
  }
  setText('taskReceivedCount', String(Number(task?.receivedFileCount || 0)))
  setText('taskFinishedCount', String(Number(task?.finishedFileCount || 0)))
  setText('taskFinishedSub', `成功 ${Number(task?.successFileCount || 0)} 个，失败 ${Number(task?.failedFileCount || 0)} 个`)
  setText('taskPendingCount', String(Number(task?.pendingFileCount || 0)))
  setText('taskEtaText', formatDurationHms(task?.estimatedRemainingMs))
  setText('taskProgressSub', `当前进度 ${Number(task?.progress || 0)}%`)
  const appendBtn = byId('btnTaskAppendFiles')
  if (appendBtn) appendBtn.disabled = !task || !!task.isClosed
  const closeBtn = byId('btnTaskCloseFromDetail')
  if (closeBtn) closeBtn.disabled = !task || !!task.isClosed
}

async function loadTaskFiles(silent = false) {
  const taskId = Number(state.tasks.detailTaskId || 0)
  if (!taskId) return
  try {
    const statusRaw = String(byId('taskFileStatusFilter')?.value || '').trim()
    const params = {
      keyword: (byId('taskFileKeyword')?.value || '').trim() || undefined,
      status: statusRaw === '' ? undefined : Number(statusRaw),
      page: state.tasks.detailPage,
      pageSize: state.tasks.detailPageSize
    }
    const res = await zwavApi.sagTaskEventList(taskId, params)
    if (!res?.success) {
      if (!silent) showAlert('error', res?.message || '获取任务文件列表失败')
      return
    }
    state.tasks.detailData = res.data?.data || []
    state.tasks.detailTotal = res.data?.total || 0
    renderTaskFiles()
  } catch (e) {
    if (!silent) showAlert('error', e.message || '获取任务文件列表失败')
  }
}

function renderTaskFiles() {
  updatePagination(
    state.tasks.detailTotal,
    state.tasks.detailPage,
    state.tasks.detailPageSize,
    'taskFileTotalText',
    'taskFilePageText',
    'taskFilePrev',
    'taskFileNext'
  )

  const rows = state.tasks.detailData.map((row, idx) => {
    const statusText = getAnalyzeStatusText(row.status)
    const statusType = getAnalyzeStatusType(row.status)
    return `
      <tr>
        <td class="cell-center">${idx + 1 + (state.tasks.detailPage - 1) * state.tasks.detailPageSize}</td>
        <td class="file-name-cell" title="${escapeHtml(row.originalName || '-')}">${escapeHtml(row.originalName || '-')}</td>
        <td><span class="el-tag el-tag--${statusType}">${escapeHtml(statusText)}</span></td>
        <td class="cell-center">${normalizeProgress(row.progress)}%</td>
        <td class="cell-center">${row.hasSag ? '是' : '否'}</td>
        <td>${escapeHtml(formatEventType(row.eventType))}</td>
        <td>${escapeHtml(row.worstPhase || '-')}</td>
        <td>${escapeHtml(row.startTime ? formatDateTime(row.startTime) : '-')}</td>
        <td>${escapeHtml(row.finishTime ? formatDateTime(row.finishTime) : '-')}</td>
        <td title="${escapeHtml(row.errorMessage || '-')}">${escapeHtml(row.errorMessage || '-')}</td>
      </tr>
    `
  }).join('')

  setHtml('taskFileTbody', rows)
}

function stopTaskPolling() {
  if (state.tasks.polling) clearInterval(state.tasks.polling)
  state.tasks.polling = null
}

function startTaskPolling() {
  if (state.tasks.polling) return
  state.tasks.polling = setInterval(async () => {
    const listOpen = isDialogOpen('taskListModal')
    const detailOpen = isDialogOpen('taskDetailModal')
    if (!listOpen && !detailOpen) {
      stopTaskPolling()
      return
    }
    if (detailOpen && state.tasks.detailTaskId) {
      await loadTaskDetail(state.tasks.detailTaskId, { silent: true, reloadFiles: true })
    }
    if (listOpen) {
      await loadTaskList(true)
    }
  }, 5000)
}

function computeRecoverThresholdPct(sagThresholdPct, hysteresisPct) {
  const sag = Number(sagThresholdPct)
  const h = Number(hysteresisPct)
  if (!Number.isFinite(sag) || !Number.isFinite(h)) return ''
  const v = Math.min(100, sag + h)
  return String(Math.max(sag, v))
}

function syncAnalyzeRecoverThresholdIfAuto() {
  if (!state.analyzeRecoverAuto) return
  const v = computeRecoverThresholdPct(byId('sagThresholdPct')?.value, byId('hysteresisPct')?.value)
  if (v === '') return
  const el = byId('recoverThresholdPct')
  if (el) el.value = v
}

function getSelectedAnalyzeReferenceMode() {
  return String(document.querySelector('input[name="referenceTypeRadio"]:checked')?.value || 'Adaptive').trim()
}

function updateAnalyzeReferenceModeUi() {
  const activeMode = getSelectedAnalyzeReferenceMode()
  document.querySelectorAll('[data-reference-mode]').forEach((el) => {
    el.classList.toggle('is-active', el.getAttribute('data-reference-mode') === activeMode)
  })

  const customWrap = byId('customReferenceWrap')
  if (customWrap) customWrap.classList.toggle('is-visible', activeMode === 'CustomVoltage')
}

function resolveAnalyzeReferenceSelection() {
  const raw = getSelectedAnalyzeReferenceMode()
  if (raw === 'LineVoltage') return { referenceType: 'LineVoltage', referenceVoltage: 100 }
  if (raw === 'Adaptive') return { referenceType: 'Adaptive', referenceVoltage: null }
  if (raw === 'CustomVoltage') {
    return {
      referenceType: 'CustomVoltage',
      referenceVoltage: Number(byId('customReferenceVoltage')?.value || 0)
    }
  }
  return { referenceType: 'PhaseVoltage', referenceVoltage: 57.74 }
}

function legacySyncAnalyzeReferenceUi() {
  const label = byId('referenceTypeLabel')
    || byId('referenceOptionGroup')?.closest('.search-item')?.querySelector('label')
  if (label) label.textContent = '\u53c2\u8003\u7535\u538b\u5904\u7406'

  const legacyLabel = label?.previousElementSibling
  if (legacyLabel && legacyLabel.tagName === 'LABEL') {
    legacyLabel.style.display = 'none'
  }

  updateAnalyzeReferenceModeUi()
}

async function legacyStartAnalyzeOld() {
  const ids = Array.from(state.analysis.selected)
  if (ids.length === 0) {
    showAlert('warning', '请先选择解析任务')
    return
  }

  const referenceSelection = resolveAnalyzeReferenceSelection()
  const sagThresholdPct = Number(byId('sagThresholdPct').value || 90)
  const interruptThresholdPct = Number(byId('interruptThresholdPct').value || 10)
  const hysteresisPct = Number(byId('hysteresisPct').value || 2)
  const recoverThresholdPct = Number(byId('recoverThresholdPct').value || 0)
  const minDurationMs = Number(byId('minDurationMs').value || 10)

  if (false) {
    showAlert('warning', '参考电压必须大于0')
    return
  }
  if (sagThresholdPct <= 0 || sagThresholdPct > 100) {
    showAlert('warning', '暂降阈值必须在0-100之间')
    return
  }
  if (interruptThresholdPct <= 0 || interruptThresholdPct > 100) {
    showAlert('warning', '中断阈值必须在0-100之间')
    return
  }
  if (hysteresisPct < 0 || hysteresisPct > 20) {
    showAlert('warning', '迟滞必须在0-20之间')
    return
  }
  if (recoverThresholdPct > 0) {
    if (recoverThresholdPct < sagThresholdPct || recoverThresholdPct > 100) {
      showAlert('warning', '恢复阈值必须在 暂降阈值~100 之间')
      return
    }
  }
  if (minDurationMs < 0) {
    showAlert('warning', '最小持续时间不能为负数')
    return
  }

  const body = {
    fileIds: [],
    analysisGuids: ids,
    referenceType: 'Declared',
    referenceVoltage,
    sagThresholdPct,
    recoverThresholdPct: recoverThresholdPct > 0 ? recoverThresholdPct : null,
    interruptThresholdPct,
    hysteresisPct,
    minDurationMs
  }

  try {
    const res = await zwavApi.sagAnalyze(body)
      if (res?.success) {
        const queuedCount = res.data?.queuedCount ?? res.data?.createdEventCount ?? 0
        const createdCount = queuedCount
        state.analysis.selected.clear()
        showAlert('success', `已加入分析队列：${queuedCount} 条，列表将自动刷新分析进度`)
        closeDialog('analyzeModal')
        state.page = 1
        await loadList()
        return
      showAlert('success', `已生成结果记录：${createdCount} 条，列表将自动刷新分析进度`)
      closeDialog('analyzeModal')
      state.page = 1
      await loadList()
    } else {
      showAlert('error', res?.message || '暂降分析失败')
    }
  } catch (e) {
    showAlert('error', e.message || '暂降分析失败')
  }
}

function syncAnalyzeReferenceUi() {
  const label = byId('referenceTypeLabel')
    || byId('referenceOptionGroup')?.closest('.search-item')?.querySelector('label')
  if (label) label.textContent = '\u53c2\u8003\u7535\u538b\u5904\u7406'

  const legacyLabel = label?.previousElementSibling
  if (legacyLabel && legacyLabel.tagName === 'LABEL') {
    legacyLabel.style.display = 'none'
  }

  updateAnalyzeReferenceModeUi()
}

/*
async function startAnalyze() {
  const ids = Array.from(state.analysis.selected)
  if (ids.length === 0) {
    showAlert('warning', '\u8bf7\u5148\u9009\u62e9\u5206\u6790\u4efb\u52a1')
    return
  }

  const referenceSelection = resolveAnalyzeReferenceSelection()
  const sagThresholdPct = Number(byId('sagThresholdPct').value || 90)
  const interruptThresholdPct = Number(byId('interruptThresholdPct').value || 10)
  const hysteresisPct = Number(byId('hysteresisPct').value || 2)
  const recoverThresholdPct = Number(byId('recoverThresholdPct').value || 0)
  const minDurationMs = Number(byId('minDurationMs').value || 10)

  if (referenceSelection.referenceType === 'CustomVoltage' && referenceSelection.referenceVoltage <= 0) {
    showAlert('warning', '\u81ea\u5b9a\u4e49\u53c2\u8003\u7535\u538b\u5fc5\u987b\u5927\u4e8e 0')
    return
  }
  if (sagThresholdPct <= 0 || sagThresholdPct > 100) {
    showAlert('warning', '\u6682\u964d\u9608\u503c\u5fc5\u987b\u5728 0-100 \u4e4b\u95f4')
    return
  }
  if (interruptThresholdPct <= 0 || interruptThresholdPct > 100) {
    showAlert('warning', '\u4e2d\u65ad\u9608\u503c\u5fc5\u987b\u5728 0-100 \u4e4b\u95f4')
    return
  }
  if (hysteresisPct < 0 || hysteresisPct > 20) {
    showAlert('warning', '\u8fdf\u6ede\u5fc5\u987b\u5728 0-20 \u4e4b\u95f4')
    return
  }
  if (recoverThresholdPct > 0 && (recoverThresholdPct < sagThresholdPct || recoverThresholdPct > 100)) {
    showAlert('warning', '\u6062\u590d\u9608\u503c\u5fc5\u987b\u5728\u6682\u964d\u9608\u503c\u5230 100 \u4e4b\u95f4')
    return
  }
  if (minDurationMs < 0) {
    showAlert('warning', '\u6700\u5c0f\u6301\u7eed\u65f6\u95f4\u4e0d\u80fd\u4e3a\u8d1f\u6570')
    return
  }

  const body = {
    taskId: state.analysis.taskContext?.id || null,
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
    if (res?.success) {
      const queuedCount = res.data?.queuedCount ?? res.data?.createdEventCount ?? 0
      const taskId = Number(res.data?.taskId || state.analysis.taskContext?.id || 0)
      state.analysis.selected.clear()
      closeDialog('analyzeModal')
      showAlert('success', `${state.analysis.mode === 'append' ? '已向任务追加录波文件' : '暂降任务已创建'}：新增 ${queuedCount} 条`)
      state.page = 1
      await loadList()
      if (isDialogOpen('taskListModal')) {
        await loadTaskList(true)
      }
      if (taskId > 0) {
        openDialog('taskDetailModal')
        state.tasks.detailPage = 1
        byId('taskFilePageSize').value = String(state.tasks.detailPageSize)
        await loadTaskDetail(taskId)
        startTaskPolling()
      }
      return
    }

    showAlert('error', res?.message || '\u6682\u964d\u5206\u6790\u5931\u8d25')
  } catch (e) {
    showAlert('error', e.message || '\u6682\u964d\u5206\u6790\u5931\u8d25')
  }
}

*/
async function loadRules() {
  const meta = getRuleTabMeta()
  const ruleState = getRuleListState()
  ruleState.loading = true
  try {
    const enabledRaw = String(byId('ruleEnabledFilter')?.value || '').trim()
    const params = {
      keyword: (byId('ruleKeyword').value || '').trim() || undefined,
      enabled: meta === RULE_TAB_META[RULE_TAB_PHASE] && enabledRaw !== '' ? enabledRaw : undefined,
      page: ruleState.page,
      pageSize: ruleState.pageSize
    }

    const res = await zwavApi[meta.listApi](params)
    if (!res?.success) {
      showAlert('error', res?.message || '获取词库失败')
      return
    }

    ruleState.data = res.data?.data || []
    ruleState.total = res.data?.total || 0
    renderRules()
  } catch (e) {
    showAlert('error', e.message || '获取词库失败')
  } finally {
    ruleState.loading = false
  }
}

function renderRules() {
  const meta = getRuleTabMeta()
  const ruleState = getRuleListState()
  updatePagination(
    ruleState.total,
    ruleState.page,
    ruleState.pageSize,
    'ruleTotalText',
    'rulePageText',
    'rulePrev',
    'ruleNext'
  )

  const tbody = ruleState.data.map((row, idx) => {
    const name = row.ruleName || '-'
    const valueText = row[meta.valueProp] || '-'
    const phaseTypeText = getSafePhaseTypeText(row.phaseType)
    const phaseValueText = formatPhaseValue(getDisplayPhaseValue(row))
    const enabledText = row.enabled === false ? '排除' : '启用'
    return `
      <tr>
        <td class="cell-center">${idx + 1 + (ruleState.page - 1) * ruleState.pageSize}</td>
        <td class="file-name-cell" title="${escapeHtml(name)}">${escapeHtml(name)}</td>
        <td class="cell-center">${escapeHtml(valueText)}</td>
        ${state.rules.activeTab === RULE_TAB_PHASE ? `<td class="cell-center">${escapeHtml(phaseTypeText)}</td>` : ''}
        ${state.rules.activeTab === RULE_TAB_PHASE ? `<td class="cell-center">${escapeHtml(phaseValueText)}</td>` : ''}
        ${state.rules.activeTab === RULE_TAB_PHASE ? `<td class="cell-center">${escapeHtml(enabledText)}</td>` : ''}
        <td class="cell-center">${escapeHtml(String(row.seqNo ?? 0))}</td>
        <td>${escapeHtml(row.crtTime ? formatDateTime(row.crtTime) : '-')}</td>
        <td class="cell-actions">
          <button class="el-button is-link action-edit-rule" data-id="${escapeHtml(row.id)}">编辑</button>
          <button class="el-button is-link action-del-rule" data-id="${escapeHtml(row.id)}" style="color: var(--el-color-danger)">删除</button>
        </td>
      </tr>
    `
  }).join('')

  setHtml('ruleTbody', tbody)
}

async function handleRuleDelete(id) {
  const meta = getRuleTabMeta()
  const confirmed = await showConfirmDialog('确认删除', meta.deleteConfirmText, '此操作不可恢复')
  if (!confirmed) return

  try {
    const res = await zwavApi[meta.deleteApi](id)
    if (res?.success) {
      showAlert('success', '删除成功')
      await loadRules()
    } else {
      showAlert('error', res?.message || '删除失败')
    }
  } catch (e) {
    showAlert('error', e.message || '删除失败')
  }
}

function computeLocalNextRuleSeqNo() {
  const items = Array.isArray(getRuleListState().data) ? getRuleListState().data : []
  let max = 0
  for (let i = 0; i < items.length; i++) {
    const v = Number(items[i]?.seqNo)
    if (Number.isFinite(v) && v > max) max = v
  }
  return max + 1
}

function openRuleEdit(row) {
  const tab = state.rules.activeTab
  const meta = getRuleTabMeta(tab)
  state.ruleEditing.isEdit = !!row
  state.ruleEditing.id = row ? Number(row.id) : 0
  state.ruleEditing.tab = tab
  setText('ruleEditTitle', row ? meta.editTitle : meta.addTitle)
  byId('ruleNameInput').value = row ? (row.ruleName || '') : ''
  byId('phaseNameSelect').value = row ? (row.phaseName || 'A') : 'A'
  byId('phaseTypeSelect').value = String(row ? (row.phaseType || getDefaultPhaseType(row.phaseName)) : getDefaultPhaseType(byId('phaseNameSelect').value))
  byId('phaseValueInput').value = row
    ? String(row.phaseValue ?? getDefaultPhaseValue(byId('phaseTypeSelect').value))
    : String(getDefaultPhaseValue(byId('phaseTypeSelect').value))
  byId('enabledInput').checked = row ? row.enabled !== false : true
  byId('groupNameInput').value = row ? (row.groupName || '') : ''
  byId('seqNoInput').value = row ? String(row.seqNo ?? 0) : String(computeLocalNextRuleSeqNo())
  syncRuleEditUi(tab)
  openDialog('channelRuleEditModal')
}

async function saveRule() {
  const tab = state.ruleEditing.tab || state.rules.activeTab
  const meta = getRuleTabMeta(tab)
  const ruleName = String(byId('ruleNameInput').value || '').trim()
  const phaseName = String(byId('phaseNameSelect').value || '').trim()
  const phaseType = Number(byId('phaseTypeSelect').value || 0)
  const phaseValue = Number(byId('phaseValueInput').value || 0)
  const enabled = !!byId('enabledInput').checked
  const groupName = String(byId('groupNameInput').value || '').trim()
  const seqNo = Number(byId('seqNoInput').value || 0)

  if (!ruleName) {
    showAlert('warning', '规则名称不能为空')
    return
  }
  if (tab === RULE_TAB_PHASE && !phaseName) {
    showAlert('warning', '相别不能为空')
    return
  }
  if (tab === RULE_TAB_PHASE && phaseType !== PHASE_TYPE_PHASE && phaseType !== PHASE_TYPE_LINE) {
    showAlert('warning', '鐢靛帇绫诲瀷鏃犳晥')
    return
  }
  if (tab === RULE_TAB_PHASE && (!Number.isFinite(phaseValue) || phaseValue <= 0)) {
    showAlert('warning', '鐩稿埆鐢靛帇鍊煎繀椤诲ぇ浜?0')
    return
  }
  if (tab === RULE_TAB_GROUP && !groupName) {
    showAlert('warning', '分组名称不能为空')
    return
  }
  if (seqNo < 0) {
    showAlert('warning', '排序号不能为负数')
    return
  }

  try {
    const body = tab === RULE_TAB_PHASE
      ? { ruleName, phaseName, phaseType, phaseValue, seqNo, enabled }
      : { ruleName, groupName, seqNo }

    let res
    if (state.ruleEditing.isEdit) {
      res = await zwavApi[meta.updateApi](state.ruleEditing.id, body)
    } else {
      res = await zwavApi[meta.createApi](body)
    }

    if (res?.success) {
      closeDialog('channelRuleEditModal')
      showAlert('success', state.ruleEditing.isEdit ? '更新成功' : '新增成功')
      await loadRules()
    } else {
      showAlert('error', res?.message || '保存失败')
    }
  } catch (e) {
    showAlert('error', e.message || '保存失败')
  }
}

function bindTableDelegation() {
  const tbody = byId('tbody')
  if (!tbody) return

  tbody.addEventListener('click', async (e) => {
    if (state.deleting) return
    const target = e.target
    const processBtn = target.closest?.('.action-process')
    const viewBtn = target.closest?.('.action-view')
    const delBtn = target.closest?.('.action-delete')

    if (processBtn) {
      const id = String(processBtn.getAttribute('data-id') || '').trim()
      if (id) openSagProcess(id)
      return
    }

    if (viewBtn) {
      const fileId = Number(viewBtn.getAttribute('data-fileid'))
      if (fileId) await viewOnline(fileId)
      return
    }

    if (delBtn) {
      const id = Number(delBtn.getAttribute('data-id'))
      if (id) await handleDelete(id)
      return
    }
  })

  tbody.addEventListener('change', (e) => {
    if (state.deleting) return
    const target = e.target
    if (!target.classList.contains('row-check')) return

    const id = Number(target.getAttribute('data-id'))
    if (target.checked) state.selected.add(id)
    else state.selected.delete(id)
    render()
  })
}

function bindAnalysisDelegation() {
  const tbody = byId('analysisTbody')
  if (!tbody) return

  tbody.addEventListener('change', (e) => {
    const target = e.target
    if (!target.classList.contains('analysis-check')) return

    const guid = String(target.getAttribute('data-guid') || '')
    if (target.checked) state.analysis.selected.add(guid)
    else state.analysis.selected.delete(guid)
    renderAnalyses()
  })
}

function bindTaskDelegation() {
  const tbody = byId('taskTbody')
  if (tbody) {
    tbody.addEventListener('click', async (e) => {
      const detailBtn = e.target.closest('.action-task-detail')
      if (!detailBtn) return
      const taskId = Number(detailBtn.getAttribute('data-id'))
      if (!taskId) return
      state.tasks.detailPage = 1
      byId('taskFilePageSize').value = String(state.tasks.detailPageSize)
      openDialog('taskDetailModal')
      await loadTaskDetail(taskId)
      startTaskPolling()
    })
  }
}

function bindRuleDelegation() {
  const tbody = byId('ruleTbody')
  if (!tbody) {
    console.warn('ruleTbody 未找到，规则表事件未绑定')
    return
  }

  tbody.addEventListener('click', async (e) => {
    const editBtn = e.target.closest('.action-edit-rule')
    const delBtn = e.target.closest('.action-del-rule')

    if (editBtn) {
      const id = Number(editBtn.getAttribute('data-id'))
      const row = getRuleListState().data.find(x => Number(x.id) === id)
      openRuleEdit(row)
      return
    }

    if (delBtn) {
      const id = Number(delBtn.getAttribute('data-id'))
      if (id) {
        await handleRuleDelete(id)
      }
      return
    }
  })
}

function bindEvents() {
  ensureSagTaskUi()

  on('btnOpenApiConfig', 'click', openApiModal)
  on('btnSaveApiBase', 'click', () => {
    setApiBaseUrl(byId('apiBaseInput').value || '')
    closeDialog('apiModal')
  })

  bindDialog('apiModal')
  bindDialog('analyzeModal')
  bindDialog('taskListModal')
  bindDialog('taskDetailModal')
  bindDialog('channelRuleModal')
  bindDialog('channelRuleEditModal')
  bindDeleteProgressEvents()

  bindTableDelegation()
  bindAnalysisDelegation()
  bindTaskDelegation()
  bindRuleDelegation()

  on('btnSearch', 'click', () => {
    state.page = 1
    loadList()
  })

  on('btnReset', 'click', () => {
    byId('keyword').value = ''
    byId('eventType').value = ''
    byId('phase').value = ''
    byId('fromDate').value = ''
    byId('toDate').value = ''
    state.page = 1
    loadList()
  })

  on('pageSize', 'change', () => {
    state.pageSize = Number(byId('pageSize').value || 10)
    state.page = 1
    loadList()
  })

  on('btnPrev', 'click', () => {
    if (state.deleting) return
    if (state.page > 1) state.page--
    loadList()
  })

  on('btnNext', 'click', () => {
    if (state.deleting) return
    const totalPages = Math.max(1, Math.ceil(state.total / state.pageSize))
    if (state.page < totalPages) state.page++
    loadList()
  })

  on('checkAll', 'change', () => {
    if (state.deleting) return
    const allIds = state.data.map(x => x.id).filter(x => x !== undefined && x !== null)
    if (byId('checkAll').checked) allIds.forEach(id => state.selected.add(id))
    else allIds.forEach(id => state.selected.delete(id))
    render()
  })

  on('btnBatchDelete', 'click', batchDelete)

  on('btnOpenAnalyze', 'click', () => {
    openLegacyAnalyzeModal()
  })

  on('btnOpenCreateTask', 'click', () => {
    openTaskWorkbenchWindow('create')
  })

  on('btnOpenTaskList', 'click', async () => {
    openTaskWorkbenchWindow('list')
  })

  on('btnTaskCreate', 'click', () => {
    openTaskWorkbenchWindow('create')
  })

  on('btnTaskSearch', 'click', () => {
    state.tasks.page = 1
    loadTaskList()
  })

  on('taskPageSize', 'change', () => {
    state.tasks.pageSize = Number(byId('taskPageSize').value || 10)
    state.tasks.page = 1
    loadTaskList()
  })

  on('taskPrev', 'click', () => {
    if (state.tasks.page > 1) state.tasks.page--
    loadTaskList()
  })

  on('taskNext', 'click', () => {
    const totalPages = Math.max(1, Math.ceil(state.tasks.total / state.tasks.pageSize))
    if (state.tasks.page < totalPages) state.tasks.page++
    loadTaskList()
  })

  on('btnAnalysisSearch', 'click', () => {
    state.analysis.page = 1
    loadAnalyses()
  })

  on('analysisPageSize', 'change', () => {
    state.analysis.pageSize = Number(byId('analysisPageSize').value || 10)
    state.analysis.page = 1
    loadAnalyses()
  })

  on('analysisPrev', 'click', () => {
    if (state.analysis.page > 1) state.analysis.page--
    loadAnalyses()
  })

  on('analysisNext', 'click', () => {
    const totalPages = Math.max(1, Math.ceil(state.analysis.total / state.analysis.pageSize))
    if (state.analysis.page < totalPages) state.analysis.page++
    loadAnalyses()
  })

  on('analysisCheckAll', 'change', () => {
    const allGuids = state.analysis.data.map(x => x.analysisGuid).filter(Boolean)
    if (byId('analysisCheckAll').checked) allGuids.forEach(g => state.analysis.selected.add(g))
    else allGuids.forEach(g => state.analysis.selected.delete(g))
    renderAnalyses()
  })

  on('btnTaskFileSearch', 'click', () => {
    state.tasks.detailPage = 1
    loadTaskFiles()
  })

  on('taskFilePageSize', 'change', () => {
    state.tasks.detailPageSize = Number(byId('taskFilePageSize').value || 10)
    state.tasks.detailPage = 1
    loadTaskFiles()
  })

  on('taskFilePrev', 'click', () => {
    if (state.tasks.detailPage > 1) state.tasks.detailPage--
    loadTaskFiles()
  })

  on('taskFileNext', 'click', () => {
    const totalPages = Math.max(1, Math.ceil(state.tasks.detailTotal / state.tasks.detailPageSize))
    if (state.tasks.detailPage < totalPages) state.tasks.detailPage++
    loadTaskFiles()
  })

  on('btnTaskAppendFiles', 'click', () => {
    openAnalyzeModalForAppend(state.tasks.detailTask)
  })

  on('btnTaskCloseFromDetail', 'click', async () => {
    const taskId = Number(state.tasks.detailTask?.id || 0)
    if (!taskId) {
      showAlert('warning', '当前没有可关闭的任务')
      return
    }

    try {
      const res = await zwavApi.sagCloseTask(taskId)
      if (!res?.success) {
        showAlert('error', res?.message || '关闭任务失败')
        return
      }
      showAlert('success', '任务已关闭')
      await loadTaskDetail(taskId)
      if (isDialogOpen('taskListModal')) {
        await loadTaskList(true)
      }
    } catch (e) {
      showAlert('error', e.message || '关闭任务失败')
    }
  })

  on('btnStartAnalyze', 'click', startAnalyze)
  on('btnCloseSagTask', 'click', async () => {
    const taskId = state.analysis.activeTask?.id
    if (!taskId) {
      showAlert('warning', '当前没有可关闭的任务')
      return
    }

    try {
      const res = await zwavApi.sagCloseTask(taskId)
      if (!res?.success) {
        showAlert('error', res?.message || '关闭任务失败')
        return
      }

      state.analysis.activeTask = null
      renderSagTaskSummary()
      applyTaskParamsToForm(null)
      showAlert('success', '当前任务已关闭，下次加入录波文件将自动新建任务')
    } catch (e) {
      showAlert('error', e.message || '关闭任务失败')
    }
  })

  on('sagThresholdPct', 'input', () => syncAnalyzeRecoverThresholdIfAuto())
  on('hysteresisPct', 'input', () => syncAnalyzeRecoverThresholdIfAuto())
  on('recoverThresholdPct', 'input', () => {
    state.analyzeRecoverAuto = false
  })
  document.querySelectorAll('input[name="referenceTypeRadio"]').forEach((el) => {
    el.addEventListener('change', () => {
      updateAnalyzeReferenceModeUi()
    })
  })

  on('btnOpenChannelRules', 'click', async () => {
    state.rules.activeTab = RULE_TAB_PHASE
    getRuleListState(RULE_TAB_PHASE).page = 1
    byId('ruleKeyword').value = ''
    byId('ruleEnabledFilter').value = ''
    byId('rulePageSize').value = String(getRuleListState(RULE_TAB_PHASE).pageSize)
    syncRuleTabUi()
    openDialog('channelRuleModal')
    await loadRules()
  })

  on('btnRuleTabPhase', 'click', async () => {
    await switchRuleTab(RULE_TAB_PHASE)
  })

  on('btnRuleTabGroup', 'click', async () => {
    await switchRuleTab(RULE_TAB_GROUP)
  })

  on('btnRuleSearch', 'click', () => {
    getRuleListState().page = 1
    loadRules()
  })

  on('ruleEnabledFilter', 'change', () => {
    if (state.rules.activeTab !== RULE_TAB_PHASE) return
    getRuleListState().page = 1
    loadRules()
  })

  on('rulePageSize', 'change', () => {
    const ruleState = getRuleListState()
    ruleState.pageSize = Number(byId('rulePageSize').value || 10)
    ruleState.page = 1
    loadRules()
  })

  on('rulePrev', 'click', () => {
    const ruleState = getRuleListState()
    if (ruleState.page > 1) ruleState.page--
    loadRules()
  })

  on('ruleNext', 'click', () => {
    const ruleState = getRuleListState()
    const totalPages = Math.max(1, Math.ceil(ruleState.total / ruleState.pageSize))
    if (ruleState.page < totalPages) ruleState.page++
    loadRules()
  })

  on('phaseNameSelect', 'change', () => {
    const nextType = getDefaultPhaseType(byId('phaseNameSelect').value)
    byId('phaseTypeSelect').value = String(nextType)
    syncPhaseRuleValueByType(true)
  })

  on('phaseTypeSelect', 'change', () => {
    syncPhaseRuleValueByType(true)
  })

  on('btnRuleAdd', 'click', () => openRuleEdit(null))
  on('btnSaveRule', 'click', saveRule)

  const apiBase = qs('apiBase')
  if (apiBase) setApiBaseUrl(apiBase)
}

function ensureSagTaskUi() {
  if (!document.getElementById('zwavSagTaskStyle')) {
    const style = document.createElement('style')
    style.id = 'zwavSagTaskStyle'
    style.textContent = `
      .task-summary-card{margin-bottom:14px;padding:14px 16px;border:1px solid #dcdfe6;border-radius:12px;background:linear-gradient(180deg,#f9fbff 0%,#ffffff 100%)}
      .task-summary-head{display:flex;align-items:flex-start;justify-content:space-between;gap:12px;margin-bottom:8px}
      .task-summary-title{font-size:15px;font-weight:600;color:#303133}
      .task-summary-sub{margin-top:4px;font-size:12px;color:#909399}
      .task-summary-body{color:#303133;line-height:1.8}
      .task-summary-foot{margin-top:8px;font-size:12px;color:#606266}
    `
    document.head.appendChild(style)
  }
}

function isDialogOpen(id) {
  return !!byId(id)?.classList.contains('is-open')
}

function stopSagTaskPolling() {
  if (state.analysis.taskPolling) clearInterval(state.analysis.taskPolling)
  state.analysis.taskPolling = null
}

function startSagTaskPolling() {
  stopSagTaskPolling()
}

function getSagTaskStatusMeta(task) {
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

function getTaskMetaText(task) {
  if (!task) return '当前会创建一个新的暂降分析任务，后续可以在任务详情中继续追加录波文件。'
  const taskName = task.taskName || '暂降分析任务'
  const statusText = task.isClosed ? '已关闭，不可再追加文件' : '未关闭，可继续追加文件'
  return `${task.taskNo || '-'} | ${taskName} | ${statusText}`
}

function resetAnalyzeFormDefaults() {
  const defaultReferenceRadio = document.querySelector('input[name="referenceTypeRadio"][value="Adaptive"]')
  if (defaultReferenceRadio) defaultReferenceRadio.checked = true
  const customReferenceInput = byId('customReferenceVoltage')
  if (customReferenceInput) customReferenceInput.value = ''
  byId('sagThresholdPct').value = '90'
  byId('interruptThresholdPct').value = '10'
  byId('hysteresisPct').value = '2'
  byId('minDurationMs').value = '10'
  state.analyzeRecoverAuto = true
  syncAnalyzeRecoverThresholdIfAuto()
  updateAnalyzeReferenceModeUi()
}

function setAnalyzeTaskContext(task, mode) {
  state.analysis.taskContext = task || null
  state.analysis.mode = mode || (task ? 'append' : 'legacy')
  renderSagTaskSummary()
  applyTaskParamsToForm(state.analysis.taskContext)
}

function renderSagTaskSummary() {
  const task = state.analysis.taskContext
  const mode = state.analysis.mode || 'legacy'
  const isLegacyMode = mode === 'legacy'
  const statusMeta = getSagTaskStatusMeta(task)
  const statusTag = byId('sagTaskStatusTag')
  const summaryCard = byId('sagTaskSummaryCard')

  if (summaryCard) {
    summaryCard.style.display = isLegacyMode ? 'none' : ''
  }

  if (statusTag) {
    statusTag.className = `el-tag el-tag--${statusMeta.type}`
    statusTag.textContent = task ? statusMeta.text : '新任务'
  }

  setText('analyzeModalTitle', task ? '向已有任务追加录波文件' : (isLegacyMode ? '新增暂降分析' : '创建暂降任务'))
  setText('sagTaskMetaText', getTaskMetaText(task))
  setText(
    'sagTaskSummaryText',
    task?.summaryText || '提交后会立即创建任务，并把当前勾选的录波文件加入该任务进行暂降分析。'
  )
  setText(
    'sagTaskFootText',
    task
      ? `当前进度 ${Number(task.progress || 0)}%，已完成 ${Number(task.finishedFileCount || 0)}/${Number(task.receivedFileCount || 0)}，预计剩余时间 ${formatDurationHms(task.estimatedRemainingMs)}。`
      : '任务创建后，只要没有被手动关闭，就可以在任务详情中继续追加录波文件。'
  )
  setText('btnStartAnalyze', mode === 'append' ? '追加并开始分析' : (isLegacyMode ? '开始分析' : '创建并开始分析'))
}

async function loadActiveSagTask(taskId) {
  if (!taskId) {
    setAnalyzeTaskContext(null, 'legacy')
    return
  }

  try {
    const res = await zwavApi.sagTaskDetail(taskId)
    setAnalyzeTaskContext(res?.success ? (res.data || null) : null, 'append')
  } catch {
    setAnalyzeTaskContext(null, 'legacy')
  }
}

function openLegacyAnalyzeModal() {
  state.analysis.page = 1
  state.analysis.selected.clear()
  byId('analysisKeyword').value = ''
  byId('analysisPageSize').value = String(state.analysis.pageSize)
  resetAnalyzeFormDefaults()
  setAnalyzeTaskContext(null, 'legacy')
  openDialog('analyzeModal')
  loadAnalyses()
}

async function openAnalyzeModalForCreate() {
  state.analysis.page = 1
  state.analysis.selected.clear()
  byId('analysisKeyword').value = ''
  byId('analysisPageSize').value = String(state.analysis.pageSize)
  resetAnalyzeFormDefaults()
  setAnalyzeTaskContext(null, 'taskCreate')
  openDialog('analyzeModal')

  try {
    const res = await zwavApi.sagActiveTask()
    const activeTask = res?.success ? (res.data || null) : null
    if (activeTask?.id && !activeTask.isClosed) {
      setAnalyzeTaskContext(activeTask, 'append')
    }
  } catch {
    setAnalyzeTaskContext(null, 'taskCreate')
  }

  await loadAnalyses()
}

async function openAnalyzeModalForAppend(task) {
  const targetTask = task?.id ? task : state.tasks.detailTask
  if (!targetTask?.id) {
    showAlert('warning', '请先选择一个暂降任务')
    return
  }
  if (targetTask.isClosed) {
    showAlert('warning', '当前任务已关闭，不能再追加录波文件')
    return
  }

  state.analysis.page = 1
  state.analysis.selected.clear()
  byId('analysisKeyword').value = ''
  byId('analysisPageSize').value = String(state.analysis.pageSize)
  resetAnalyzeFormDefaults()
  openDialog('analyzeModal')
  await loadActiveSagTask(targetTask.id)
  await loadAnalyses()
}

async function startAnalyze() {
  const ids = Array.from(state.analysis.selected)
  if (ids.length === 0) {
    showAlert('warning', '请先选择分析任务')
    return
  }

  const referenceSelection = resolveAnalyzeReferenceSelection()
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
  if (minDurationMs < 0) {
    showAlert('warning', '最小持续时间不能为负数')
    return
  }

  const mode = state.analysis.mode || 'legacy'
  const body = {
    taskId: mode === 'append' ? (state.analysis.taskContext?.id || null) : null,
    createTask: mode !== 'legacy',
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
      showAlert('error', res?.message || '暂降分析失败')
      return
    }

    const queuedCount = res.data?.queuedCount ?? res.data?.createdEventCount ?? 0
    const taskId = Number(res.data?.taskId || state.analysis.taskContext?.id || 0)
    state.analysis.selected.clear()
    closeDialog('analyzeModal')
    state.page = 1
    await loadList()

    if (mode === 'legacy') {
      showAlert('success', `已提交暂降分析：${queuedCount} 条`)
      return
    }

    showAlert('success', `${mode === 'append' ? '已向任务追加录波文件' : '暂降任务已创建'}：新增 ${queuedCount} 条`)
    if (isDialogOpen('taskListModal')) {
      await loadTaskList(true)
    }
    if (taskId > 0) {
      openDialog('taskDetailModal')
      state.tasks.detailPage = 1
      byId('taskFilePageSize').value = String(state.tasks.detailPageSize)
      await loadTaskDetail(taskId)
      startTaskPolling()
    }
  } catch (e) {
    showAlert('error', e.message || '暂降分析失败')
  }
}

function selectReferenceMode(mode, voltage) {
  const value = String(mode || 'Adaptive')
  const radio = document.querySelector(`input[name="referenceTypeRadio"][value="${value}"]`)
  if (radio) radio.checked = true
  const customInput = byId('customReferenceVoltage')
  if (customInput && Number.isFinite(Number(voltage)) && Number(voltage) > 0) {
    customInput.value = String(voltage)
  }
  updateAnalyzeReferenceModeUi()
}

function toggleAnalyzeParamsDisabled(disabled) {
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

function applyTaskParamsToForm(task) {
  if (!task) {
    toggleAnalyzeParamsDisabled(false)
    updateAnalyzeReferenceModeUi()
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
  toggleAnalyzeParamsDisabled(true)
}

async function init() {
  bindEvents()
  syncRuleTabUi()
  syncAnalyzeReferenceUi()
  startPolling()

  state.pageSize = Number(byId('pageSize').value || 10)
  const defaultReferenceRadio = document.querySelector('input[name="referenceTypeRadio"][value="Adaptive"]')
  if (defaultReferenceRadio) defaultReferenceRadio.checked = true
  byId('sagThresholdPct').value = '90'
  byId('interruptThresholdPct').value = '10'
  byId('hysteresisPct').value = '2'
  byId('minDurationMs').value = '10'

  await loadList()
}

init()
