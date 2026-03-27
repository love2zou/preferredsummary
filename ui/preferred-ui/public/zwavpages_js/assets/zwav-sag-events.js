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

const state = {
  loading: false,
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
    selected: new Set()
  },

  rules: {
    loading: false,
    page: 1,
    pageSize: 10,
    total: 0,
    data: []
  },

  ruleEditing: {
    id: 0,
    isEdit: false
  }
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

function on(id, event, handler) {
  const el = byId(id)
  if (!el) return
  el.addEventListener(event, handler)
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

function render() {
  updatePagination(state.total, state.page, state.pageSize, 'totalText', 'pageText', 'btnPrev', 'btnNext')
  byId('btnBatchDelete').disabled = state.selected.size === 0

  const checkAll = byId('checkAll')
  const allIds = state.data.map(x => x.id).filter(x => x !== undefined && x !== null)
  const checkedCount = allIds.filter(id => state.selected.has(id)).length
  checkAll.checked = allIds.length > 0 && checkedCount === allIds.length
  checkAll.indeterminate = checkedCount > 0 && checkedCount < allIds.length

  const tbody = state.data.map((row, idx) => {
    const id = row.id
    const checked = state.selected.has(id) ? 'checked' : ''
    const statusType = getAnalyzeStatusType(row.status)
    const statusText = getAnalyzeStatusText(row.status)
    const hasSag = !!row.hasSag
    const occur = row.occurTimeUtc ? formatDateTime(row.occurTimeUtc) : '-'
    const errorRaw = row.errorMessage ? String(row.errorMessage) : ''
    const errorText = errorRaw
      ? `<span class="error-text" title="${escapeHtml(errorRaw)}">${escapeHtml(errorRaw)}</span>`
      : '-'
    const eventTypeText = formatEventType(row.eventType)

    return `
      <tr>
        <td class="cell-center"><input type="checkbox" class="row-check" data-id="${escapeHtml(id)}" ${checked}></td>
        <td class="cell-center">${idx + 1 + (state.page - 1) * state.pageSize}</td>
        <td class="file-name-cell" title="${escapeHtml(row.originalName || '')}">${escapeHtml(row.originalName || '-')}</td>
        <td><span class="el-tag el-tag--${statusType}">${escapeHtml(statusText)}</span></td>
        <td class="cell-center">
          <span class="el-tag ${hasSag ? 'el-tag--danger' : 'el-tag--success'}">${hasSag ? '有' : '无'}</span>
        </td>
        <td>${escapeHtml(eventTypeText)}</td>
        <td class="cell-center">${escapeHtml(row.worstPhase || '-')}</td>
        <td>${escapeHtml(occur)}</td>
        <td>${escapeHtml(formatMs(row.durationMs))}</td>
        <td>${escapeHtml(formatPercent(row.sagPercent))}</td>
        <td>${escapeHtml(formatPercent(row.residualVoltagePct))}</td>
        <td class="file-name-cell">${errorText}</td>
        <td class="cell-actions">
          <button class="el-button is-link action-process" data-id="${escapeHtml(id)}">暂降分析</button>
          <button class="el-button is-link action-view" data-fileid="${escapeHtml(row.fileId)}">录波浏览</button>
          <button class="el-button is-link action-delete" data-id="${escapeHtml(id)}" style="color: var(--el-color-danger)">删除</button>
        </td>
      </tr>
    `
  }).join('')

  setHtml('tbody', tbody)
}

async function loadList() {
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
      showAlert('error', res?.message || '查询失败')
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
    showAlert('error', e.message || '查询失败')
  } finally {
    state.loading = false
  }
}

async function batchDelete() {
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

async function handleDelete(id) {
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

async function startAnalyze() {
  const ids = Array.from(state.analysis.selected)
  if (ids.length === 0) {
    showAlert('warning', '请先选择解析任务')
    return
  }

  const referenceVoltage = Number(byId('referenceVoltage').value || 0)
  const sagThresholdPct = Number(byId('sagThresholdPct').value || 90)
  const interruptThresholdPct = Number(byId('interruptThresholdPct').value || 10)
  const hysteresisPct = Number(byId('hysteresisPct').value || 2)
  const minDurationMs = Number(byId('minDurationMs').value || 10)

  if (referenceVoltage <= 0) {
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
  if (minDurationMs < 0) {
    showAlert('warning', '最小持续时间不能为负数')
    return
  }

  const body = {
    fileIds: [],
    analysisGuids: ids,
    referenceType: 'Config',
    referenceVoltage,
    sagThresholdPct,
    interruptThresholdPct,
    hysteresisPct,
    minDurationMs
  }

  try {
    const res = await zwavApi.sagAnalyze(body)
    if (res?.success) {
      const createdCount = res.data?.createdEventCount || 0
      showAlert('success', `已生成结果记录：${createdCount} 条`)
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

async function loadRules() {
  state.rules.loading = true
  try {
    const params = {
      keyword: (byId('ruleKeyword').value || '').trim() || undefined,
      page: state.rules.page,
      pageSize: state.rules.pageSize
    }

    const res = await zwavApi.sagChannelRuleList(params)
    if (!res?.success) {
      showAlert('error', res?.message || '获取词库失败')
      return
    }

    state.rules.data = res.data?.data || []
    state.rules.total = res.data?.total || 0
    renderRules()
  } catch (e) {
    showAlert('error', e.message || '获取词库失败')
  } finally {
    state.rules.loading = false
  }
}

function renderRules() {
  updatePagination(
    state.rules.total,
    state.rules.page,
    state.rules.pageSize,
    'ruleTotalText',
    'rulePageText',
    'rulePrev',
    'ruleNext'
  )

  const tbody = state.rules.data.map((row, idx) => {
    const name = row.ruleName || '-'
    return `
      <tr>
        <td class="cell-center">${idx + 1 + (state.rules.page - 1) * state.rules.pageSize}</td>
        <td class="file-name-cell" title="${escapeHtml(name)}">${escapeHtml(name)}</td>
        <td class="cell-center">${escapeHtml(row.phaseName || '-')}</td>
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
  const confirmed = await showConfirmDialog('确认删除', '确定要删除该规则吗？', '此操作不可恢复')
  if (!confirmed) return

  try {
    const res = await zwavApi.sagChannelRuleDelete(id)
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
  const items = Array.isArray(state.rules.data) ? state.rules.data : []
  let max = 0
  for (let i = 0; i < items.length; i++) {
    const v = Number(items[i]?.seqNo)
    if (Number.isFinite(v) && v > max) max = v
  }
  return max + 1
}

function openRuleEdit(row) {
  state.ruleEditing.isEdit = !!row
  state.ruleEditing.id = row ? Number(row.id) : 0
  setText('ruleEditTitle', row ? '编辑规则' : '新增规则')
  byId('ruleNameInput').value = row ? (row.ruleName || '') : ''
  byId('phaseNameSelect').value = row ? (row.phaseName || 'A') : 'A'
  byId('seqNoInput').value = row ? String(row.seqNo ?? 0) : String(computeLocalNextRuleSeqNo())
  openDialog('channelRuleEditModal')
}

async function saveRule() {
  const ruleName = String(byId('ruleNameInput').value || '').trim()
  const phaseName = String(byId('phaseNameSelect').value || '').trim()
  const seqNo = Number(byId('seqNoInput').value || 0)

  if (!ruleName) {
    showAlert('warning', '规则名称不能为空')
    return
  }
  if (!phaseName) {
    showAlert('warning', '相别不能为空')
    return
  }
  if (seqNo < 0) {
    showAlert('warning', '排序号不能为负数')
    return
  }

  try {
    let res
    if (state.ruleEditing.isEdit) {
      res = await zwavApi.sagChannelRuleUpdate(state.ruleEditing.id, { ruleName, phaseName, seqNo })
    } else {
      res = await zwavApi.sagChannelRuleCreate({ ruleName, phaseName, seqNo })
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

function bindRuleDelegation() {
  const tbody = byId('ruleTbody')
  if (!tbody) {
    console.warn('ruleTbody 未找到，规则表事件未绑定')
    return
  }

  console.log('规则表事件绑定成功', tbody)

  tbody.addEventListener('click', async (e) => {
    console.log('规则表点击事件触发', e.target)

    const editBtn = e.target.closest('.action-edit-rule')
    const delBtn = e.target.closest('.action-del-rule')

    if (editBtn) {
      const id = Number(editBtn.getAttribute('data-id'))
      console.log('点击编辑规则，id=', id)

      const row = state.rules.data.find(x => Number(x.id) === id)
      openRuleEdit(row)
      return
    }

    if (delBtn) {
      const id = Number(delBtn.getAttribute('data-id'))
      console.log('点击删除规则，id=', id)

      if (id) {
        await handleRuleDelete(id)
      } else {
        console.warn('删除按钮未取到 data-id')
      }
      return
    }

    console.log('点击了规则表，但不是操作按钮')
  })
}

function bindEvents() {
  on('btnOpenApiConfig', 'click', openApiModal)
  on('btnSaveApiBase', 'click', () => {
    setApiBaseUrl(byId('apiBaseInput').value || '')
    closeDialog('apiModal')
  })

  bindDialog('apiModal')
  bindDialog('analyzeModal')
  bindDialog('channelRuleModal')
  bindDialog('channelRuleEditModal')

  bindTableDelegation()
  bindAnalysisDelegation()
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
    if (state.page > 1) state.page--
    loadList()
  })

  on('btnNext', 'click', () => {
    const totalPages = Math.max(1, Math.ceil(state.total / state.pageSize))
    if (state.page < totalPages) state.page++
    loadList()
  })

  on('checkAll', 'change', () => {
    const allIds = state.data.map(x => x.id).filter(x => x !== undefined && x !== null)
    if (byId('checkAll').checked) allIds.forEach(id => state.selected.add(id))
    else allIds.forEach(id => state.selected.delete(id))
    render()
  })

  on('btnBatchDelete', 'click', batchDelete)

  on('btnOpenAnalyze', 'click', async () => {
    state.analysis.page = 1
    state.analysis.selected.clear()
    byId('analysisKeyword').value = ''
    byId('analysisPageSize').value = String(state.analysis.pageSize)
    openDialog('analyzeModal')
    await loadAnalyses()
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

  on('btnStartAnalyze', 'click', startAnalyze)

  on('btnOpenChannelRules', 'click', async () => {
    state.rules.page = 1
    byId('ruleKeyword').value = ''
    byId('rulePageSize').value = String(state.rules.pageSize)
    openDialog('channelRuleModal')
    await loadRules()
  })

  on('btnRuleSearch', 'click', () => {
    state.rules.page = 1
    loadRules()
  })

  on('rulePageSize', 'change', () => {
    state.rules.pageSize = Number(byId('rulePageSize').value || 10)
    state.rules.page = 1
    loadRules()
  })

  on('rulePrev', 'click', () => {
    if (state.rules.page > 1) state.rules.page--
    loadRules()
  })

  on('ruleNext', 'click', () => {
    const totalPages = Math.max(1, Math.ceil(state.rules.total / state.rules.pageSize))
    if (state.rules.page < totalPages) state.rules.page++
    loadRules()
  })

  on('btnRuleAdd', 'click', () => openRuleEdit(null))
  on('btnSaveRule', 'click', saveRule)

  const apiBase = qs('apiBase')
  if (apiBase) setApiBaseUrl(apiBase)
}

async function init() {
  bindEvents()

  state.pageSize = Number(byId('pageSize').value || 10)
  byId('referenceVoltage').value = '57.74'
  byId('sagThresholdPct').value = '90'
  byId('interruptThresholdPct').value = '10'
  byId('hysteresisPct').value = '2'
  byId('minDurationMs').value = '10'

  await loadList()
}

init()
