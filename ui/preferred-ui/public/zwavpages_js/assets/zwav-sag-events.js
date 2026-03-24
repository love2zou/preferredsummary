import { bindDialog, byId, closeDialog, escapeHtml, formatDateTime, getApiBaseUrl, openDialog, qs, setApiBaseUrl, setHtml, setText } from './common.js'
import { zwavApi } from './zwav-api.js'

const state = {
  loading: false,
  selected: new Set(),
  page: 1,
  pageSize: 10,
  total: 0,
  data: [],
  analysis: { loading: false, page: 1, pageSize: 10, total: 0, data: [], selected: new Set() },
  rules: { loading: false, page: 1, pageSize: 10, total: 0, data: [] },
  ruleEditing: { id: 0, isEdit: false }
}

function on(id, event, handler) {
  const el = byId(id)
  if (!el) return
  el.addEventListener(event, handler)
}

function getStatusType(status) {
  if (status === 0) return 'warning'
  if (status === 1) return 'primary'
  if (status === 2) return 'success'
  if (status === 3) return 'danger'
  return 'info'
}

function getStatusText(status) {
  if (status === 0) return '待处理'
  if (status === 1) return '处理中'
  if (status === 2) return '成功'
  if (status === 3) return '失败'
  return '-'
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

function getDateRangeParams() {
  const fromDate = String(byId('fromDate').value || '').trim()
  const toDate = String(byId('toDate').value || '').trim()
  const params = {}
  if (fromDate) params.fromUtc = new Date(fromDate).toISOString()
  if (toDate) {
    const end = new Date(toDate)
    end.setDate(end.getDate() + 1)
    params.toUtc = end.toISOString()
  }
  return params
}

function render() {
  const totalPages = Math.max(1, Math.ceil(state.total / state.pageSize))
  setText('totalText', `共 ${state.total} 条`)
  setText('pageText', `${state.page}/${totalPages}`)
  byId('btnPrev').disabled = state.page <= 1
  byId('btnNext').disabled = state.page >= totalPages
  byId('btnBatchDelete').disabled = state.selected.size === 0

  const checkAll = byId('checkAll')
  const allIds = state.data.map((x) => x.id).filter((x) => x !== undefined && x !== null)
  const checkedCount = allIds.filter((id) => state.selected.has(id)).length
  checkAll.checked = allIds.length > 0 && checkedCount === allIds.length
  checkAll.indeterminate = checkedCount > 0 && checkedCount < allIds.length

  const tbody = state.data
    .map((row, idx) => {
      const id = row.id
      const checked = state.selected.has(id) ? 'checked' : ''
      const statusType = getStatusType(row.status)
      const statusText = getStatusText(row.status)
      const hasSag = !!row.hasSag
      const occur = row.occurTimeUtc ? formatDateTime(row.occurTimeUtc) : '-'
      const errorText = row.errorMessage ? escapeHtml(String(row.errorMessage)) : '-'
      return `
        <tr>
          <td class="cell-center"><input type="checkbox" class="row-check" data-id="${escapeHtml(id)}" ${checked}></td>
          <td class="cell-center">${idx + 1 + (state.page - 1) * state.pageSize}</td>
          <td class="file-name-cell" title="${escapeHtml(row.originalName || '')}">${escapeHtml(row.originalName || '-')}</td>
          <td><span class="el-tag el-tag--${statusType}">${escapeHtml(statusText)}</span></td>
          <td class="cell-center">
            <span class="el-tag ${hasSag ? 'el-tag--danger' : 'el-tag--success'}">${hasSag ? '有' : '无'}</span>
          </td>
          <td>${escapeHtml(row.eventType || '-')}</td>
          <td class="cell-center">${escapeHtml(row.worstPhase || '-')}</td>
          <td>${escapeHtml(occur)}</td>
          <td>${escapeHtml(formatMs(row.durationMs))}</td>
          <td>${escapeHtml(formatPercent(row.sagPercent))}</td>
          <td>${escapeHtml(formatPercent(row.residualVoltagePct))}</td>
          <td class="file-name-cell" title="${errorText}">${errorText}</td>
          <td class="cell-actions">
            <button class="el-button is-link action-process" data-id="${escapeHtml(id)}">暂降分析</button>
            <button class="el-button is-link action-view" data-fileid="${escapeHtml(row.fileId)}">录波浏览</button>
            <button class="el-button is-link action-delete" data-id="${escapeHtml(id)}" style="color: var(--el-color-danger)">删除</button>
          </td>
        </tr>
      `
    })
    .join('')
  setHtml('tbody', tbody)

  byId('tbody').querySelectorAll('.row-check').forEach((el) => {
    el.addEventListener('change', () => {
      const id = Number(el.getAttribute('data-id'))
      if (el.checked) state.selected.add(id)
      else state.selected.delete(id)
      render()
    })
  })

  byId('tbody').querySelectorAll('.action-process').forEach((el) => {
    el.addEventListener('click', () => {
      const id = String(el.getAttribute('data-id') || '').trim()
      if (!id) return
      const u = new URL('./ZwavSagProcess.html', window.location.href)
      u.searchParams.set('id', id)
      window.open(u.toString(), '_blank')
    })
  })

  byId('tbody').querySelectorAll('.action-view').forEach((el) => {
    el.addEventListener('click', async () => {
      const fileId = Number(el.getAttribute('data-fileid'))
      if (!fileId) return
      try {
        const r = await zwavApi.createAnalysis(fileId, false)
        const guid = r && r.data && r.data.analysisGuid
        if (!r || !r.success || !guid) {
          alert((r && r.message) || '获取解析任务失败')
          return
        }
        const u = new URL('./ZwavOnlineViewer.html', window.location.href)
        u.searchParams.set('guid', guid)
        window.open(u.toString(), '_blank')
      } catch (e) {
        alert(e.message || '打开在线浏览失败')
      }
    })
  })

  byId('tbody').querySelectorAll('.action-delete').forEach((el) => {
    el.addEventListener('click', async () => {
      const id = Number(el.getAttribute('data-id'))
      if (!id) return
      if (!confirm('确定要删除该事件吗？')) return
      try {
        const res = await zwavApi.sagDelete(id)
        if (res && res.success) {
          state.selected.delete(id)
          await loadList()
        } else {
          alert((res && res.message) || '删除失败')
        }
      } catch (e) {
        alert(e.message || '删除失败')
      }
    })
  })
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
      alert((res && res.message) || '查询失败')
      return
    }
    state.data = (res.data && res.data.data) || []
    state.total = (res.data && res.data.total) || 0
    const presentIds = new Set(state.data.map((x) => x.id))
    Array.from(state.selected).forEach((id) => {
      if (!presentIds.has(id)) state.selected.delete(id)
    })
    render()
  } catch (e) {
    alert(e.message || '查询失败')
  } finally {
    state.loading = false
  }
}

async function batchDelete() {
  const ids = Array.from(state.selected)
  if (!ids.length) return
  if (!confirm('确定要批量删除选中的事件吗？')) return
  let ok = 0
  let fail = 0
  for (const id of ids) {
    try {
      const res = await zwavApi.sagDelete(id)
      if (res && res.success) {
        ok++
      } else {
        fail++
      }
    } catch {
      fail++
    }
  }
  alert(`删除完成：成功 ${ok} 条，失败 ${fail} 条`)
  state.selected.clear()
  await loadList()
}

function openApiModal() {
  byId('apiBaseInput').value = getApiBaseUrl()
  openDialog('apiModal')
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
    if (!res || !res.success) {
      alert((res && res.message) || '获取解析任务失败')
      return
    }
    state.analysis.data = (res.data && res.data.data) || []
    state.analysis.total = (res.data && res.data.total) || 0
    renderAnalyses()
  } catch (e) {
    alert(e.message || '获取解析任务失败')
  } finally {
    state.analysis.loading = false
  }
}

function renderAnalyses() {
  const totalPages = Math.max(1, Math.ceil(state.analysis.total / state.analysis.pageSize))
  setText('analysisTotalText', `共 ${state.analysis.total} 条`)
  setText('analysisPageText', `${state.analysis.page}/${totalPages}`)
  byId('analysisPrev').disabled = state.analysis.page <= 1
  byId('analysisNext').disabled = state.analysis.page >= totalPages

  const allGuids = state.analysis.data.map((x) => x.analysisGuid).filter(Boolean)
  const checkedCount = allGuids.filter((g) => state.analysis.selected.has(g)).length
  const checkAll = byId('analysisCheckAll')
  checkAll.checked = allGuids.length > 0 && checkedCount === allGuids.length
  checkAll.indeterminate = checkedCount > 0 && checkedCount < allGuids.length

  const tbody = state.analysis.data
    .map((row, idx) => {
      const guid = row.analysisGuid
      const checked = state.analysis.selected.has(guid) ? 'checked' : ''
      const name = row.originalName || '-'
      const status = row.status || '-'
      const fileSize = row.fileSize !== undefined && row.fileSize !== null ? String(row.fileSize) : '-'
      return `
        <tr>
          <td class="cell-center"><input type="checkbox" class="analysis-check" data-guid="${escapeHtml(guid)}" ${checked}></td>
          <td class="cell-center">${idx + 1 + (state.analysis.page - 1) * state.analysis.pageSize}</td>
          <td class="file-name-cell" title="${escapeHtml(name)}">${escapeHtml(name)}</td>
          <td>${escapeHtml(status)}</td>
          <td>${escapeHtml(fileSize)}</td>
          <td>${escapeHtml(row.crtTime ? formatDateTime(row.crtTime) : '-')}</td>
        </tr>
      `
    })
    .join('')
  setHtml('analysisTbody', tbody)

  byId('analysisTbody').querySelectorAll('.analysis-check').forEach((el) => {
    el.addEventListener('change', () => {
      const guid = String(el.getAttribute('data-guid') || '')
      if (el.checked) state.analysis.selected.add(guid)
      else state.analysis.selected.delete(guid)
      renderAnalyses()
    })
  })
}

async function startAnalyze() {
  const ids = Array.from(state.analysis.selected)
  if (ids.length === 0) {
    alert('请先选择解析任务')
    return
  }
  const referenceVoltage = Number(byId('referenceVoltage').value || 0)
  const sagThresholdPct = Number(byId('sagThresholdPct').value || 90)
  const interruptThresholdPct = Number(byId('interruptThresholdPct').value || 10)
  const hysteresisPct = Number(byId('hysteresisPct').value || 2)
  const minDurationMs = Number(byId('minDurationMs').value || 10)
  const body = {
    fileIds: [],
    analysisGuids: ids,
    referenceType: 'Config',
    referenceVoltage,
    sagThresholdPct,
    interruptThresholdPct,
    hysteresisPct,
    minDurationMs,
  }
  try {
    const res = await zwavApi.sagAnalyze(body)
    if (res && res.success) {
      alert(`已生成结果记录：${(res.data && res.data.createdEventCount) || 0} 条`)
      closeDialog('analyzeModal')
      state.page = 1
      await loadList()
    } else {
      alert((res && res.message) || '暂降分析失败')
    }
  } catch (e) {
    alert(e.message || '暂降分析失败')
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
    if (!res || !res.success) {
      alert((res && res.message) || '获取词库失败')
      return
    }
    state.rules.data = (res.data && res.data.data) || []
    state.rules.total = (res.data && res.data.total) || 0
    renderRules()
  } catch (e) {
    alert(e.message || '获取词库失败')
  } finally {
    state.rules.loading = false
  }
}

function renderRules() {
  const totalPages = Math.max(1, Math.ceil(state.rules.total / state.rules.pageSize))
  setText('ruleTotalText', `共 ${state.rules.total} 条`)
  setText('rulePageText', `${state.rules.page}/${totalPages}`)
  byId('rulePrev').disabled = state.rules.page <= 1
  byId('ruleNext').disabled = state.rules.page >= totalPages

  const tbody = state.rules.data
    .map((row, idx) => {
      const name = row.ruleName || '-'
      return `
        <tr>
          <td class="cell-center">${idx + 1 + (state.rules.page - 1) * state.rules.pageSize}</td>
          <td class="file-name-cell" title="${escapeHtml(name)}">${escapeHtml(name)}</td>
          <td class="cell-center">${escapeHtml(row.phaseName || '-')}</td>
          <td class="cell-center">${escapeHtml(String(row.seqNo ?? 0))}</td>
          <td>${escapeHtml(row.crtTime ? formatDateTime(row.crtTime) : '-')}</td>
          <td class="cell-actions">
            <button class="el-button is-link action-edit" data-id="${escapeHtml(row.id)}">编辑</button>
            <button class="el-button is-link action-del" data-id="${escapeHtml(row.id)}" style="color: var(--el-color-danger)">删除</button>
          </td>
        </tr>
      `
    })
    .join('')
  setHtml('ruleTbody', tbody)

  byId('ruleTbody').querySelectorAll('.action-edit').forEach((el) => {
    el.addEventListener('click', () => {
      const id = Number(el.getAttribute('data-id'))
      const row = state.rules.data.find((x) => Number(x.id) === id)
      openRuleEdit(row)
    })
  })
  byId('ruleTbody').querySelectorAll('.action-del').forEach((el) => {
    el.addEventListener('click', async () => {
      const id = Number(el.getAttribute('data-id'))
      if (!id) return
      if (!confirm('确定删除该规则吗？')) return
      try {
        const res = await zwavApi.sagChannelRuleDelete(id)
        if (res && res.success) {
          await loadRules()
        } else {
          alert((res && res.message) || '删除失败')
        }
      } catch (e) {
        alert(e.message || '删除失败')
      }
    })
  })
}

function openRuleEdit(row) {
  state.ruleEditing.isEdit = !!row
  state.ruleEditing.id = row ? Number(row.id) : 0
  setText('ruleEditTitle', row ? '编辑规则' : '新增规则')
  byId('ruleNameInput').value = row ? (row.ruleName || '') : ''
  byId('phaseNameSelect').value = row ? (row.phaseName || 'A') : 'A'
  byId('seqNoInput').value = row ? String(row.seqNo ?? 0) : '0'
  openDialog('channelRuleEditModal')
}

async function saveRule() {
  const ruleName = String(byId('ruleNameInput').value || '').trim()
  const phaseName = String(byId('phaseNameSelect').value || '').trim()
  const seqNo = Number(byId('seqNoInput').value || 0)
  if (!ruleName) {
    alert('规则名称不能为空')
    return
  }
  if (!phaseName) {
    alert('相别不能为空')
    return
  }
  try {
    let res
    if (state.ruleEditing.isEdit) {
      res = await zwavApi.sagChannelRuleUpdate(state.ruleEditing.id, { ruleName, phaseName, seqNo })
    } else {
      res = await zwavApi.sagChannelRuleCreate({ ruleName, phaseName, seqNo })
    }
    if (res && res.success) {
      closeDialog('channelRuleEditModal')
      await loadRules()
    } else {
      alert((res && res.message) || '保存失败')
    }
  } catch (e) {
    alert(e.message || '保存失败')
  }
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
    const allIds = state.data.map((x) => x.id).filter((x) => x !== undefined && x !== null)
    if (byId('checkAll').checked) allIds.forEach((id) => state.selected.add(id))
    else allIds.forEach((id) => state.selected.delete(id))
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
    const allGuids = state.analysis.data.map((x) => x.analysisGuid).filter(Boolean)
    if (byId('analysisCheckAll').checked) allGuids.forEach((g) => state.analysis.selected.add(g))
    else allGuids.forEach((g) => state.analysis.selected.delete(g))
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
