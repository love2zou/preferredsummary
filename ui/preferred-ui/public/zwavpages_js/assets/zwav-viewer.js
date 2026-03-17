import { bindDialog, byId, closeDialog, escapeHtml, formatDateTime, getApiBaseUrl, openDialog, parseFileNameFromContentDisposition, qs, setApiBaseUrl, setHtml, setText } from './common.js'
import { renderFileCheckboxList, renderSequence } from './sequence.js'
import { zwavApi } from './zwav-api.js'

const SERIES_COLORS = ['#1677ff', '#b56be6', '#f6a04b', '#52c41a', '#ff4d4f']

const state = {
  guid: '',
  loading: false,
  analogChannels: [],
  digitalChannels: [],
  selectedAnalog: new Set(),
  selectedDigital: new Set(),
  cfg: null,
  hdr: null,
  detail: null,
  chart: null,
  lastWaveData: null,
  markerLeftXPixel: null,
  markerRightXPixel: null,
  gridHeight: 50,
  searchFromSample: 0,
  searchToSample: 10000,
  searchLimit: 50000,
  searchDownSample: 1,
  loadedFiles: new Map(),
  fileColors: new Map(),
  checkedGuids: new Set(),
  pick: { page: 1, pageSize: 10, totalPages: 1, data: [] },
  seqZoom: 1,
  seqBaseSize: null
}

function on(id, event, handler) {
  const el = byId(id)
  if (!el) return
  el.addEventListener(event, handler)
}

function openApiModal() {
  byId('apiBaseInput').value = getApiBaseUrl()
  openDialog('apiModal')
}

async function init() {
  state.guid = String(qs('guid') || '').trim()
  if (!state.guid) {
    alert('缺少任务ID')
    return
  }
  setText('guidText', state.guid)

  bindEvents()
  initChart()
  await loadAll()
}

async function loadAll() {
  state.loading = true
  try {
    const [analogRes, digitalRes, cfgRes, hdrRes, detailRes] = await Promise.all([
      zwavApi.getChannels(state.guid, 'Analog', true),
      zwavApi.getChannels(state.guid, 'Digital', true),
      zwavApi.getCfg(state.guid, true),
      zwavApi.getHdr(state.guid).catch(() => ({ success: true, data: null })),
      zwavApi.getDetail(state.guid).catch(() => ({ success: true, data: null }))
    ])

    if (analogRes && analogRes.success) state.analogChannels = analogRes.data || []
    if (digitalRes && digitalRes.success) state.digitalChannels = digitalRes.data || []
    if (cfgRes && cfgRes.success) state.cfg = cfgRes.data || null
    if (hdrRes && hdrRes.success) state.hdr = hdrRes.data || null
    if (detailRes && detailRes.success) state.detail = detailRes.data || null

    const fileName = state.detail?.file?.originalName || ''
    setText('fileNameText', fileName ? `| ${fileName}` : '')

    if (state.analogChannels.length) state.analogChannels.slice(0, 3).forEach((c) => state.selectedAnalog.add(c.channelIndex))
    renderChannelLists()
    renderRightPanel()

    const hasTrip = Array.isArray(state.hdr?.tripInfoJSON) && state.hdr.tripInfoJSON.length > 0
    byId('btnSequence').disabled = !hasTrip

    if (state.selectedAnalog.size > 0 || state.selectedDigital.size > 0) {
      await fetchWaveData()
    }
  } catch (e) {
    alert(e.message || '初始化数据失败')
  } finally {
    state.loading = false
  }
}

function filterChannels(list, keyword) {
  const kw = String(keyword || '').trim().toLowerCase()
  if (!kw) return list
  return list.filter((c) => String(c.channelName || '').toLowerCase().includes(kw) || String(c.channelIndex).includes(kw))
}

function renderChannelLists() {
  const kw = byId('channelSearch').value || ''
  const analog = filterChannels(state.analogChannels, kw)
  const digital = filterChannels(state.digitalChannels, kw)

  setText('analogCount', String(state.selectedAnalog.size))
  setText('digitalCount', String(state.selectedDigital.size))

  const analogAllEl = byId('analogCheckAll')
  if (analogAllEl) {
    const checkedCount = analog.filter((c) => state.selectedAnalog.has(c.channelIndex)).length
    analogAllEl.checked = analog.length > 0 && checkedCount === analog.length
    analogAllEl.indeterminate = checkedCount > 0 && checkedCount < analog.length
  }
  const digitalAllEl = byId('digitalCheckAll')
  if (digitalAllEl) {
    const checkedCount = digital.filter((c) => state.selectedDigital.has(c.channelIndex)).length
    digitalAllEl.checked = digital.length > 0 && checkedCount === digital.length
    digitalAllEl.indeterminate = checkedCount > 0 && checkedCount < digital.length
  }

  byId('analogList').innerHTML = analog
    .map((c) => {
      const id = `a_${c.channelIndex}`
      const checked = state.selectedAnalog.has(c.channelIndex) ? 'checked' : ''
      const name = `${c.channelIndex}. ${c.channelName || ''}`
      return `<div class="channel-item"><input class="analog-check" type="checkbox" id="${id}" data-idx="${c.channelIndex}" ${checked}/><label for="${id}" title="${escapeHtml(name)}">${escapeHtml(name)}</label></div>`
    })
    .join('')

  byId('digitalList').innerHTML = digital
    .map((c) => {
      const id = `d_${c.channelIndex}`
      const checked = state.selectedDigital.has(c.channelIndex) ? 'checked' : ''
      const name = `${c.channelIndex}. ${c.channelName || ''}`
      return `<div class="channel-item"><input class="digital-check" type="checkbox" id="${id}" data-idx="${c.channelIndex}" ${checked}/><label for="${id}" title="${escapeHtml(name)}">${escapeHtml(name)}</label></div>`
    })
    .join('')

  byId('analogList').querySelectorAll('.analog-check').forEach((el) => {
    el.addEventListener('change', () => {
      const idx = Number(el.getAttribute('data-idx'))
      if (el.checked) state.selectedAnalog.add(idx)
      else state.selectedAnalog.delete(idx)
      setText('analogCount', String(state.selectedAnalog.size))
    })
  })

  byId('digitalList').querySelectorAll('.digital-check').forEach((el) => {
    el.addEventListener('change', () => {
      const idx = Number(el.getAttribute('data-idx'))
      if (el.checked) state.selectedDigital.add(idx)
      else state.selectedDigital.delete(idx)
      setText('digitalCount', String(state.selectedDigital.size))
    })
  })
}

function toggleFullScreen() {
  const el = document.querySelector('.left-panel')
  if (!el) return
  if (!document.fullscreenElement) {
    el.requestFullscreen().catch(() => { })
  } else {
    document.exitFullscreen().catch(() => { })
  }
}

function renderRightPanel() {
  const cfg = state.cfg
  if (cfg) {
    const items = [
      ['厂站名', cfg.stationName],
      ['设备ID', cfg.deviceId],
      ['版本', cfg.revision],
      ['模拟量数', cfg.analogCount],
      ['开关量数', cfg.digitalCount],
      ['频率', cfg.frequencyHz ? `${cfg.frequencyHz} Hz` : '-'],
      ['时间倍率', cfg.timeMul ?? '-'],
      ['启动时间', cfg.startTimeRaw],
      ['触发时间', cfg.triggerTimeRaw],
      ['数据格式', cfg.formatType],
      ['数据类型', cfg.dataType]
    ]
    setHtml(
      'cfgSummary',
      items
        .map(([k, v]) => `<div class="info-item"><label>${escapeHtml(k)}：</label><span>${escapeHtml(v ?? '-')}</span></div>`)
        .join('')
    )
    byId('cfgText').textContent = cfg.fullCfgText || ''

    const sampleRateSection = byId('cfgSampleRateSection')
    const sampleRateTextEl = byId('sampleRateText')
    const sampleRateJson = cfg.sampleRateJson ?? cfg.sampleRateJSON ?? cfg.sampleRatesJson ?? cfg.sampleRatesJSON ?? null
    const sampleRateText = formatJson(sampleRateJson)
    if (sampleRateSection && sampleRateTextEl && sampleRateText) {
      sampleRateTextEl.textContent = sampleRateText
      sampleRateSection.style.display = ''
    } else if (sampleRateSection) {
      sampleRateSection.style.display = 'none'
      if (sampleRateTextEl) sampleRateTextEl.textContent = ''
    }
  } else {
    setHtml('cfgSummary', '<div class="zwav-small">暂无 CFG 信息</div>')
    byId('cfgText').textContent = ''
    const sampleRateSection = byId('cfgSampleRateSection')
    if (sampleRateSection) sampleRateSection.style.display = 'none'
    const sampleRateTextEl = byId('sampleRateText')
    if (sampleRateTextEl) sampleRateTextEl.textContent = ''
  }

  const hdr = state.hdr
  if (hdr) {
    renderHdrSections(hdr)
  } else {
    setHtml('hdrSummary', '<div class="zwav-small">暂无 HDR 信息</div>')
    const el = byId('hdrSections')
    if (el) el.innerHTML = '<div class="zwav-small">暂无 HDR 信息</div>'
  }
}

function normalizeTripInfo(t) {
  if (!t) return []
  if (Array.isArray(t)) return t
  if (typeof t === 'string') {
    try {
      const arr = JSON.parse(t)
      return Array.isArray(arr) ? arr : []
    } catch {
      return []
    }
  }
  return []
}

function normalizeJsonArray(t) {
  if (!t) return []
  if (Array.isArray(t)) return t
  if (typeof t === 'string') {
    try {
      const arr = JSON.parse(t)
      return Array.isArray(arr) ? arr : []
    } catch {
      return []
    }
  }
  return []
}

function formatJson(value) {
  if (!value) return ''
  try {
    const obj = typeof value === 'string' ? JSON.parse(value) : value
    return JSON.stringify(obj, null, 2)
  } catch {
    return typeof value === 'string' ? value : JSON.stringify(value, null, 2)
  }
}

function renderElTable({ columns, rows, emptyText }) {
  if (!rows || rows.length === 0) return `<div class="zwav-small">${escapeHtml(emptyText || '暂无数据')}</div>`
  const thead = `<thead><tr>${columns.map((c) => `<th style="${c.style || ''}">${escapeHtml(c.label)}</th>`).join('')}</tr></thead>`
  const tbody = `<tbody>${rows
    .map((r, i) => {
      return `<tr>${columns
        .map((c) => {
          const v = c.get ? c.get(r, i) : r[c.key]
          return `<td style="${c.tdStyle || ''}">${escapeHtml(v ?? '')}</td>`
        })
        .join('')}</tr>`
    })
    .join('')}</tbody>`
  return `<table class="el-table">${thead}${tbody}</table>`
}

function renderHdrSections(hdr) {
  const el = byId('hdrSections')
  if (!el) return

  const deviceInfo = normalizeJsonArray(hdr.deviceInfoJson)
  const faultInfo = normalizeJsonArray(hdr.faultInfoJson)
  const tripInfo = normalizeTripInfo(hdr.tripInfoJSON)
  const digitalStatus = normalizeJsonArray(hdr.digitalStatusJson)
  const digitalEvent = normalizeJsonArray(hdr.digitalEventJson)
  const settingValue = normalizeJsonArray(hdr.settingValueJson)
  const relayEna = normalizeJsonArray(hdr.relayEnaValueJSON)

  const sections = []

  if (deviceInfo.length) {
    sections.push({
      title: '设备信息',
      body:
        `<div class="hdr-section-body">` +
        renderElTable({
          columns: [
            { label: '序号', style: 'width:60px;text-align:center;', tdStyle: 'text-align:center;', get: (_r, i) => String(i + 1) },
            { label: '名称', style: 'width:140px;', key: 'name' },
            { label: '值', key: 'value' }
          ],
          rows: deviceInfo
        }) +
        `</div>`
    })
  }

  if (faultInfo.length) {
    sections.push({
      title: '故障信息',
      body:
        `<div class="hdr-section-body">` +
        renderElTable({
          columns: [
            { label: '序号', style: 'width:50px;text-align:center;', tdStyle: 'text-align:center;', get: (_r, i) => String(i + 1) },
            { label: '名称', style: 'width:160px;', key: 'name' },
            {
              label: '值',
              get: (r) => {
                const v = r.value ?? ''
                const u = r.unit ? ` ${r.unit}` : ''
                return `${v}${u}`
              }
            }
          ],
          rows: faultInfo
        }) +
        `</div>`
    })
  }

  if (tripInfo.length) {
    sections.push({
      title: '保护动作信息',
      body:
        `<div class="hdr-section-body">` +
        `<div class="info-list" style="margin-bottom:10px;">` +
        `<div class="info-item"><label>故障开始时间：</label><span>${escapeHtml(hdr.faultStartTime ?? '-')}</span></div>` +
        `<div class="info-item"><label>故障持续时间：</label><span>${escapeHtml(hdr.faultKeepingTime ?? '-')}</span></div>` +
        `</div>` +
        renderElTable({
          columns: [
            { label: '序号', style: 'width:50px;text-align:center;', tdStyle: 'text-align:center;', get: (_r, i) => String(i + 1) },
            { label: '时间', style: 'width:70px;', key: 'time' },
            { label: '相位', style: 'width:60px;text-align:center;', tdStyle: 'text-align:center;', key: 'phase' },
            {
              label: '保护动作',
              get: (r) => `${r.name ?? ''} ${r.value === '1' ? '动作' : r.value === '0' ? '复归' : r.value ?? ''}`
            }
          ],
          rows: tripInfo
        }) +
        `</div>`
    })
  }

  if (digitalStatus.length) {
    sections.push({
      title: '启动时切换状态',
      body:
        `<div class="hdr-section-body">` +
        renderElTable({
          columns: [
            { label: '序号', style: 'width:50px;text-align:center;', tdStyle: 'text-align:center;', get: (_r, i) => String(i + 1) },
            { label: '名称', style: 'width:180px;', key: 'name' },
            { label: '状态', style: 'width:100px;text-align:center;', tdStyle: 'text-align:center;', key: 'value' }
          ],
          rows: digitalStatus
        }) +
        `</div>`
    })
  }

  if (digitalEvent.length) {
    sections.push({
      title: '启动后变化信息',
      body:
        `<div class="hdr-section-body">` +
        renderElTable({
          columns: [
            { label: '序号', style: 'width:50px;text-align:center;', tdStyle: 'text-align:center;', get: (_r, i) => String(i + 1) },
            { label: '时间', style: 'width:70px;', key: 'time' },
            { label: '名称', style: 'width:120px;', key: 'name' },
            {
              label: '状态',
              get: (r) => {
                const v = r.value
                if (v === '1') return '0 => 1'
                if (v === '0') return '1 => 0'
                return v ?? ''
              }
            }
          ],
          rows: digitalEvent
        }) +
        `</div>`
    })
  }

  if (settingValue.length) {
    sections.push({
      title: '设备设置信息',
      body:
        `<div class="hdr-section-body">` +
        renderElTable({
          columns: [
            { label: '序号', style: 'width:50px;text-align:center;', tdStyle: 'text-align:center;', get: (_r, i) => String(i + 1) },
            { label: '名称', style: 'width:160px;', key: 'name' },
            {
              label: '值',
              get: (r) => {
                const v = r.value ?? ''
                const u = r.unit ? ` ${r.unit}` : ''
                return `${v}${u}`
              }
            }
          ],
          rows: settingValue
        }) +
        `</div>`
    })
  }

  if (relayEna.length) {
    sections.push({
      title: '继电保护“软压板”投入状态值',
      body:
        `<div class="hdr-section-body">` +
        renderElTable({
          columns: [
            { label: '序号', style: 'width:50px;text-align:center;', tdStyle: 'text-align:center;', get: (_r, i) => String(i + 1) },
            { label: '名称', style: 'width:180px;', key: 'name' },
            { label: '值', key: 'value' }
          ],
          rows: relayEna
        }) +
        `</div>`
    })
  }

  if (sections.length === 0) {
    el.innerHTML = '<div class="zwav-small">暂无 HDR 信息</div>'
    return
  }

  el.classList.add('hdr-sections')
  el.innerHTML = sections
    .map((s) => `<details class="hdr-section"><summary>${escapeHtml(s.title)}</summary>${s.body}</details>`)
    .join('')
}

function initChart() {
  const el = byId('zwavChart')
  state.chart = echarts.init(el)
  state.chart.setOption({
    tooltip: { trigger: 'axis', axisPointer: { type: 'cross' } },
    grid: { left: '3%', right: '4%', bottom: '3%', containLabel: true },
    xAxis: { type: 'category', boundaryGap: false, data: [] },
    yAxis: { type: 'value', scale: true },
    series: []
  })
  state.chart.on('datazoom', () => {
    refreshMarkerSummary()
    renderMarkerGraphics()
  })
  window.addEventListener('resize', () => {
    if (!state.chart) return
    state.chart.resize()
    refreshMarkerSummary()
    renderMarkerGraphics()
  })
}

function clamp(v, min, max) {
  return Math.max(min, Math.min(max, v))
}

function getGridRect() {
  if (!state.chart) return null
  const model = state.chart.getModel && state.chart.getModel()
  const grid0 = model && model.getComponent && model.getComponent('grid', 0)
  const rect = grid0 && grid0.coordinateSystem && grid0.coordinateSystem.getRect && grid0.coordinateSystem.getRect()
  return rect || null
}

function getAllGridYRange() {
  if (!state.chart) return { minY: 0, maxY: 0 }
  const model = state.chart.getModel && state.chart.getModel()
  const grids = (model && model.getComponents && model.getComponents('grid')) || []
  let minY = Infinity
  let maxY = -Infinity
  for (let i = 0; i < grids.length; i++) {
    const rect = grids[i] && grids[i].coordinateSystem && grids[i].coordinateSystem.getRect && grids[i].coordinateSystem.getRect()
    if (!rect) continue
    minY = Math.min(minY, rect.y)
    maxY = Math.max(maxY, rect.y + rect.height)
  }
  if (!Number.isFinite(minY) || !Number.isFinite(maxY)) return { minY: 0, maxY: state.chart.getHeight() }
  return { minY, maxY }
}

function initMarkersIfNeeded() {
  const rect = getGridRect()
  if (!rect) return
  if (state.markerLeftXPixel === null) state.markerLeftXPixel = rect.x + rect.width * 0.25
  if (state.markerRightXPixel === null) state.markerRightXPixel = rect.x + rect.width * 0.75
}

function getWaveAtXPixel(xPixel) {
  const data = state.lastWaveData
  if (!state.chart || !data || !data.rows || data.rows.length === 0) return null
  const rect = getGridRect()
  if (rect) xPixel = clamp(xPixel, rect.x, rect.x + rect.width)

  const times = data.rows.map((r) => Number(r.timeMs))
  const n = times.length
  const opt = state.chart.getOption ? state.chart.getOption() : null
  const dz = opt && Array.isArray(opt.dataZoom) ? opt.dataZoom[0] : null
  const startPct = Number((dz && dz.start) ?? 0)
  const endPct = Number((dz && dz.end) ?? 100)
  const startIdx = (clamp(startPct, 0, 100) / 100) * (n - 1)
  const endIdx = (clamp(endPct, 0, 100) / 100) * (n - 1)
  const width = rect && rect.width > 0 ? rect.width : 1
  const ratio = rect ? clamp((xPixel - rect.x) / width, 0, 1) : 0
  const idxFloat = startIdx + ratio * (endIdx - startIdx)

  const i0 = clamp(Math.floor(idxFloat), 0, data.rows.length - 1)
  const i1 = clamp(i0 + 1, 0, data.rows.length - 1)
  const frac = i1 === i0 ? 0 : clamp(idxFloat - i0, 0, 1)
  const t0 = times[i0]
  const t1 = times[i1]
  const timeMs = t0 + (t1 - t0) * frac
  return { i0, i1, frac, timeMs }
}

function setMarkerFooterVisible(visible) {
  const el = byId('markerFooter')
  if (!el) return
  el.style.display = visible ? '' : 'none'
}

function refreshMarkerSummary() {
  const data = state.lastWaveData
  if (!state.chart || !data || !data.rows || data.rows.length === 0) {
    setMarkerFooterVisible(false)
    return
  }

  initMarkersIfNeeded()
  if (state.markerLeftXPixel === null || state.markerRightXPixel === null) {
    setMarkerFooterVisible(false)
    return
  }

  const left = getWaveAtXPixel(state.markerLeftXPixel)
  const right = getWaveAtXPixel(state.markerRightXPixel)
  if (!left || !right) {
    setMarkerFooterVisible(false)
    return
  }

  const leftMs = left.timeMs
  const rightMs = right.timeMs
  const leftNearest = left.frac >= 0.5 ? left.i1 : left.i0
  const rightNearest = right.frac >= 0.5 ? right.i1 : right.i0

  const leftXText = `${Number(leftMs).toFixed(3)}ms`
  const rightXText = `${Number(rightMs).toFixed(3)}ms`
  const deltaXText = `${Math.abs(Number(rightMs) - Number(leftMs)).toFixed(3)}ms`

  const text = `LeftLine：[${leftXText}][第${leftNearest + 1}个点] | RightLine：[${rightXText}][第${rightNearest + 1}个点] | RightLine - LeftLine：${deltaXText}`
  const tEl = byId('markerFooterText')
  if (tEl) tEl.textContent = text
  setMarkerFooterVisible(true)
}

function updateMarkerByPixelX(side, xPixel) {
  const rect = getGridRect()
  if (rect) xPixel = clamp(xPixel, rect.x, rect.x + rect.width)
  if (side === 'left') state.markerLeftXPixel = xPixel
  else state.markerRightXPixel = xPixel
  refreshMarkerSummary()
}

function renderMarkerGraphics() {
  const data = state.lastWaveData
  if (!state.chart || !data || !data.rows || data.rows.length === 0) {
    state.chart && state.chart.setOption({ graphic: [] }, false)
    setMarkerFooterVisible(false)
    return
  }

  initMarkersIfNeeded()
  if (state.markerLeftXPixel === null || state.markerRightXPixel === null) return

  const rect = getGridRect()
  const minX = rect ? rect.x : 0
  const maxX = rect ? rect.x + rect.width : state.chart.getWidth()
  const yr = getAllGridYRange()
  const y1 = yr.minY
  const y2 = yr.maxY

  const lx = clamp(Number(state.markerLeftXPixel), minX, maxX)
  const rx = clamp(Number(state.markerRightXPixel), minX, maxX)

  state.chart.setOption(
    {
      graphic: [
        {
          id: 'markerLeftLine',
          type: 'line',
          draggable: true,
          cursor: 'ew-resize',
          shape: { x1: lx, y1, x2: lx, y2 },
          style: { stroke: '#E6A23C', lineWidth: 1 },
          ondrag: function () {
            const rect2 = getGridRect()
            const minX2 = rect2 ? rect2.x : 0
            const maxX2 = rect2 ? rect2.x + rect2.width : (state.chart ? state.chart.getWidth() : 0)
            const baseX = Number(this.shape && this.shape.x1) || 0
            const posX = Number(this.position && this.position[0]) || 0
            let currentX = baseX + posX
            currentX = clamp(currentX, minX2, maxX2)
            if (this.position) {
              this.position[0] = currentX - baseX
              this.position[1] = 0
            }
            updateMarkerByPixelX('left', currentX)
          },
          ondragend: function () {
            const rect2 = getGridRect()
            const minX2 = rect2 ? rect2.x : 0
            const maxX2 = rect2 ? rect2.x + rect2.width : (state.chart ? state.chart.getWidth() : 0)
            const baseX = Number(this.shape && this.shape.x1) || 0
            const posX = Number(this.position && this.position[0]) || 0
            let currentX = baseX + posX
            currentX = clamp(currentX, minX2, maxX2)
            this.shape.x1 = currentX
            this.shape.x2 = currentX
            if (this.position) {
              this.position[0] = 0
              this.position[1] = 0
            }
            updateMarkerByPixelX('left', currentX)
            renderMarkerGraphics()
          }
        },
        {
          id: 'markerRightLine',
          type: 'line',
          draggable: true,
          cursor: 'ew-resize',
          shape: { x1: rx, y1, x2: rx, y2 },
          style: { stroke: '#409EFF', lineWidth: 1 },
          ondrag: function () {
            const rect2 = getGridRect()
            const minX2 = rect2 ? rect2.x : 0
            const maxX2 = rect2 ? rect2.x + rect2.width : (state.chart ? state.chart.getWidth() : 0)
            const baseX = Number(this.shape && this.shape.x1) || 0
            const posX = Number(this.position && this.position[0]) || 0
            let currentX = baseX + posX
            currentX = clamp(currentX, minX2, maxX2)
            if (this.position) {
              this.position[0] = currentX - baseX
              this.position[1] = 0
            }
            updateMarkerByPixelX('right', currentX)
          },
          ondragend: function () {
            const rect2 = getGridRect()
            const minX2 = rect2 ? rect2.x : 0
            const maxX2 = rect2 ? rect2.x + rect2.width : (state.chart ? state.chart.getWidth() : 0)
            const baseX = Number(this.shape && this.shape.x1) || 0
            const posX = Number(this.position && this.position[0]) || 0
            let currentX = baseX + posX
            currentX = clamp(currentX, minX2, maxX2)
            this.shape.x1 = currentX
            this.shape.x2 = currentX
            if (this.position) {
              this.position[0] = 0
              this.position[1] = 0
            }
            updateMarkerByPixelX('right', currentX)
            renderMarkerGraphics()
          }
        }
      ]
    },
    false
  )
}

function resetMarkers() {
  state.markerLeftXPixel = null
  state.markerRightXPixel = null
  initMarkersIfNeeded()
  refreshMarkerSummary()
  renderMarkerGraphics()
}

async function fetchWaveData() {
  if (state.selectedAnalog.size === 0 && state.selectedDigital.size === 0) {
    alert('请至少选择一个通道')
    return
  }

  state.chart.showLoading()
  try {
    const params = {
      channels: Array.from(state.selectedAnalog).join(','),
      digitals: Array.from(state.selectedDigital).join(','),
      fromSample: state.searchFromSample,
      toSample: state.searchToSample,
      limit: state.searchLimit,
      downSample: state.searchDownSample
    }
    const res = await zwavApi.getWaveData(state.guid, params)
    if (res && res.success && res.data) {
      state.lastWaveData = res.data
      updateChart(res.data)
      byId('btnStats').disabled = false
    } else {
      alert((res && res.message) || '获取波形数据失败')
    }
  } catch (e) {
    alert(e.message || '获取波形数据异常')
  } finally {
    state.chart.hideLoading()
  }
}

function updateChart(data) {
  if (!state.chart) return
  const xAxisData = (data.rows || []).map((r) => r.timeMs)
  const stats = []
  const digitalRawMap = new Map()
  const seriesItems = []

  const analogSel = Array.from(state.selectedAnalog)
  const digitalSel = Array.from(state.selectedDigital)

  analogSel.forEach((chIdx) => {
    const chInfo = state.analogChannels.find((c) => c.channelIndex === chIdx)
    const name = chInfo ? `${chInfo.channelIndex}. ${chInfo.channelName}` : `${chIdx}. CH${chIdx}`
    const unit = (chInfo && chInfo.unit) || 'A'
    const dataIndex = data.channels ? data.channels.indexOf(chIdx) : -1
    if (dataIndex === -1) return
    const channelData = data.rows.map((r) => r.analog[dataIndex])
    let max = -Infinity
    let min = Infinity
    for (const v of channelData) {
      if (v > max) max = v
      if (v < min) min = v
    }
    stats.push({ name, max, min })
    seriesItems.push({ name, isDigital: false, unit, analogData: channelData })
  })

  digitalSel.forEach((chIdx) => {
    const chInfo = state.digitalChannels.find((c) => c.channelIndex === chIdx)
    const name = chInfo ? `${chInfo.channelIndex}. ${chInfo.channelName}` : `D${chIdx}`
    const dataIndex = data.digitals ? data.digitals.indexOf(chIdx) : -1
    if (dataIndex === -1) return
    const raw = data.rows.map((r) => {
      const v = r.digital && r.digital[dataIndex] !== undefined ? r.digital[dataIndex] : 0
      return v === 1 ? 1 : 0
    })
    digitalRawMap.set(name, raw)
    const line0 = raw.map((v) => (v === 0 ? 0 : null))
    const area1 = raw.map((v) => (v === 1 ? 1 : null))
    let max = 0
    let min = 1
    for (const v of raw) {
      if (v > max) max = v
      if (v < min) min = v
    }
    stats.push({ name, max, min })
    seriesItems.push({ name, isDigital: true, digitalLine0: line0, digitalArea1: area1 })
  })

  state.channelStats = stats

  const count = seriesItems.length
  const grids = []
  const xAxes = []
  const yAxes = []
  const series = []

  const topMargin = 40
  const bottomMargin = 20
  const gap = 10
  const gridHeight = state.gridHeight

  const totalChartHeight = topMargin + count * (gridHeight + gap) + bottomMargin
  byId('zwavChart').style.height = `${totalChartHeight}px`
  state.chart.resize()

  const DIGITAL_GREEN = '#67C23A'
  const DIGITAL_RED_AREA = 'rgba(245,108,108,0.35)'

  const tooltipFormatter = (params) => {
    if (!params || params.length === 0) return ''
    const xVal = params[0].axisValue
    const dataIndex = params[0].dataIndex
    let html = `<div>时间(ms): ${escapeHtml(xVal)}</div>`
    const seen = new Set()

    for (const p of params) {
      const sName = p.seriesName || ''
      const baseName = sName.endsWith('-0') || sName.endsWith('-1') ? sName.slice(0, -2) : sName
      if (seen.has(baseName)) continue
      seen.add(baseName)

      if (sName.endsWith('-0') || sName.endsWith('-1')) {
        const rawArr = digitalRawMap.get(baseName)
        if (rawArr && dataIndex >= 0 && dataIndex < rawArr.length) {
          const st = rawArr[dataIndex] === 1 ? 1 : 0
          html += `<div>${escapeHtml(baseName)}: ${st === 1 ? '1(投入)' : '0(复归)'}</div>`
        } else {
          html += `<div>${escapeHtml(baseName)}: -</div>`
        }
        continue
      }

      const val = p.value
      if (val === null || val === undefined) continue
      html += `<div style="color:${p.color}">${escapeHtml(baseName)}: ${Number(val).toFixed(4)}</div>`
    }

    return html
  }

  seriesItems.forEach((item, idx) => {
    const top = topMargin + idx * (gridHeight + gap)
    grids.push({ left: 160, right: '4%', top, height: gridHeight, containLabel: false })
    const isFirst = idx === 0
    xAxes.push({
      type: 'category',
      boundaryGap: false,
      data: xAxisData,
      gridIndex: idx,
      position: 'top',
      axisLabel: { show: isFirst },
      axisTick: { show: isFirst },
      axisLine: { show: isFirst },
      splitLine: { show: true, lineStyle: { type: 'dashed', opacity: 0.5 } }
    })
    yAxes.push({
      type: 'value',
      scale: true,
      gridIndex: idx,
      name: item.name,
      nameLocation: 'middle',
      nameGap: 5,
      nameRotate: 0,
      min: item.isDigital ? -0.1 : undefined,
      max: item.isDigital ? 1.1 : undefined,
      interval: item.isDigital ? 1 : undefined,
      nameTextStyle: { align: 'right', color: '#333', fontWeight: 'bold', fontSize: 12, width: 130, overflow: 'break', lineHeight: 14 },
      axisLabel: { show: false },
      axisLine: { show: true, lineStyle: { color: '#ccc' } },
      splitLine: { show: true, lineStyle: { type: 'dashed', opacity: 0.5 } }
    })

    if (!item.isDigital) {
      series.push({
        name: item.name,
        type: 'line',
        xAxisIndex: idx,
        yAxisIndex: idx,
        symbol: 'none',
        sampling: 'lttb',
        data: item.analogData || [],
        markLine: { symbol: 'none', silent: true, data: [{ yAxis: 0 }], lineStyle: { color: '#ccc', type: 'solid', width: 1 } }
      })
      return
    }

    series.push({
      name: `${item.name}-0`,
      type: 'line',
      xAxisIndex: idx,
      yAxisIndex: idx,
      data: item.digitalLine0 || [],
      step: 'start',
      symbol: 'none',
      lineStyle: { color: DIGITAL_GREEN, width: 2 },
      tooltip: { show: true },
      emphasis: { disabled: true }
    })
    series.push({
      name: `${item.name}-1`,
      type: 'line',
      xAxisIndex: idx,
      yAxisIndex: idx,
      data: item.digitalArea1 || [],
      step: 'start',
      symbol: 'none',
      lineStyle: { width: 0 },
      areaStyle: { color: DIGITAL_RED_AREA },
      tooltip: { show: false },
      emphasis: { disabled: true }
    })
  })

  state.chart.setOption(
    {
      tooltip: { trigger: 'axis', axisPointer: { type: 'cross', link: { xAxisIndex: 'all' } }, formatter: tooltipFormatter },
      axisPointer: { link: { xAxisIndex: 'all' } },
      grid: grids,
      xAxis: xAxes,
      yAxis: yAxes,
      dataZoom: [{ type: 'inside', xAxisIndex: xAxes.map((_, i) => i), start: 0, end: 100 }],
      series
    },
    true
  )

  refreshMarkerSummary()
  renderMarkerGraphics()
}

function openAxisModal() {
  byId('gridHeight').value = String(state.gridHeight)
  byId('fromSample').value = String(state.searchFromSample)
  byId('toSample').value = String(state.searchToSample)
  byId('limit').value = String(state.searchLimit)
  byId('downSample').value = String(state.searchDownSample)
  openDialog('axisModal')
}

function applyAxisSettings() {
  state.gridHeight = Number(byId('gridHeight').value || 50)
  state.searchFromSample = Number(byId('fromSample').value || 0)
  state.searchToSample = Number(byId('toSample').value || 10000)
  state.searchLimit = Number(byId('limit').value || 50000)
  state.searchDownSample = Math.max(1, Number(byId('downSample').value || 1))
  closeDialog('axisModal')
  fetchWaveData()
}

function openStatsModal() {
  const stats = state.channelStats || []
  if (!stats.length) return
  byId('statsBody').innerHTML = `<table class="el-table">
    <thead><tr><th style="width:50px">序号</th><th>通道名称</th><th style="width:120px">最大值</th><th style="width:120px">最小值</th></tr></thead>
    <tbody>
      ${stats
      .map((s, i) => `<tr><td>${i + 1}</td><td>${escapeHtml(s.name)}</td><td style="color: var(--el-color-danger)">${Number(s.max).toFixed(3)}</td><td style="color: var(--el-color-primary)">${Number(s.min).toFixed(3)}</td></tr>`)
      .join('')}
    </tbody></table>`
  openDialog('statsModal')
}

async function handleExport() {
  try {
    const res = await zwavApi.exportWaveData(state.guid, true)
    const nameFromHeader = parseFileNameFromContentDisposition(res.contentDisposition)
    const name = nameFromHeader || ((state.detail?.file?.originalName || 'wave_data') + '.csv')
    const url = window.URL.createObjectURL(res.blob)
    const a = document.createElement('a')
    a.href = url
    a.download = name
    document.body.appendChild(a)
    a.click()
    document.body.removeChild(a)
    window.URL.revokeObjectURL(url)
  } catch (e) {
    alert(e.message || '导出失败')
  }
}

function openSequenceModal() {
  const trip = normalizeTripInfo(state.hdr?.tripInfoJSON)
  if (!trip.length) return

  const name = state.detail?.file?.originalName || '当前文件'
  const guid = state.guid
  state.loadedFiles.clear()
  state.fileColors.clear()
  state.checkedGuids.clear()
  state.seqZoom = 1
  state.seqBaseSize = null
  const startTime = state.cfg?.startTimeRaw || '-'

  state.loadedFiles.set(guid, { guid, name, startTime, trip })
  state.fileColors.set(guid, SERIES_COLORS[0])
  state.checkedGuids.add(guid)

  byId('seqFaultTime').textContent = state.hdr?.faultStartTime || '-'
  renderSequenceUi()
  openDialog('seqModal')

  const canvas = byId('seqCanvas')
  if (canvas) {
    canvas.scrollLeft = 0
    canvas.scrollTop = 0
  }
}

function renderSequenceUi() {
  const files = Array.from(state.loadedFiles.values())
  renderFileCheckboxList(byId('seqFileList'), {
    files,
    checkedGuids: state.checkedGuids,
    fileColorMap: state.fileColors,
    onChange: () => {
      state.checkedGuids.clear()
      byId('seqFileList')
        .querySelectorAll('.seq-check')
        .forEach((el) => {
          if (el.checked) state.checkedGuids.add(el.getAttribute('data-guid'))
        })
      renderSeqLegend()
      drawSequence()
    }
  })
  renderSeqLegend()
  drawSequence()
}

function renderSeqLegend() {
  const el = byId('seqLegend')
  if (!el) return
  const chips = []
  for (const guid of state.checkedGuids) {
    const f = state.loadedFiles.get(guid)
    if (!f) continue
    const color = state.fileColors.get(guid) || SERIES_COLORS[0]
    chips.push(
      `<div class="seq-chip" title="${escapeHtml(f.name)}"><span class="seq-chip__dot" style="background:${escapeHtml(
        color
      )}"></span><span class="seq-chip__text">${escapeHtml(f.name)}</span></div>`
    )
  }
  el.innerHTML = chips.join('')
}

function drawSequence() {
  const svg = byId('seqSvg')
  const selectedMap = new Map()
  for (const guid of state.checkedGuids) {
    const f = state.loadedFiles.get(guid)
    if (f) selectedMap.set(guid, f.trip)
  }
  const width = byId('seqSvg').parentElement.getBoundingClientRect().width
  const rendered = renderSequence(svg, { selectedTripInfos: selectedMap, fileColorMap: state.fileColors, width })
  state.seqBaseSize = rendered || null

  const zoomEl = byId('seqZoomText')
  applySeqZoom()
  if (zoomEl) zoomEl.textContent = `${Math.round(state.seqZoom * 100)}%`
}

function applySeqZoom() {
  const svg = byId('seqSvg')
  if (!svg || !state.seqBaseSize) return
  const baseW = Number(state.seqBaseSize.width) || 0
  const baseH = Number(state.seqBaseSize.height) || 0
  if (!baseW || !baseH) return
  svg.style.width = `${Math.round(baseW * state.seqZoom)}px`
  svg.style.height = `${Math.round(baseH * state.seqZoom)}px`
}

function setSeqZoom(nextZoom, { anchorClientX, anchorClientY } = {}) {
  const canvas = byId('seqCanvas')
  if (!canvas) return
  const prevZoom = state.seqZoom
  const z = Math.max(0.2, Math.min(3, nextZoom))
  if (Math.abs(z - prevZoom) < 0.0001) return

  const rect = canvas.getBoundingClientRect()
  const ax = anchorClientX != null ? anchorClientX - rect.left : rect.width / 2
  const ay = anchorClientY != null ? anchorClientY - rect.top : rect.height / 2

  const contentX = (canvas.scrollLeft + ax) / prevZoom
  const contentY = (canvas.scrollTop + ay) / prevZoom

  state.seqZoom = z
  applySeqZoom()
  canvas.scrollLeft = Math.max(0, contentX * z - ax)
  canvas.scrollTop = Math.max(0, contentY * z - ay)

  const zoomEl = byId('seqZoomText')
  if (zoomEl) zoomEl.textContent = `${Math.round(state.seqZoom * 100)}%`
}

function bindSeqCanvasInteractions() {
  const canvas = byId('seqCanvas')
  if (!canvas) return

  canvas.addEventListener(
    'wheel',
    (e) => {
      const seq = byId('seqModal')
      if (!seq || !seq.classList.contains('is-open')) return
      if (!state.seqBaseSize) return
      e.preventDefault()
      const factor = Math.exp(-e.deltaY * 0.0012)
      setSeqZoom(state.seqZoom * factor, { anchorClientX: e.clientX, anchorClientY: e.clientY })
    },
    { passive: false }
  )

  let dragging = false
  let startX = 0
  let startY = 0
  let startLeft = 0
  let startTop = 0

  canvas.addEventListener('pointerdown', (e) => {
    const seq = byId('seqModal')
    if (!seq || !seq.classList.contains('is-open')) return
    if (e.button !== 0) return
    dragging = true
    startX = e.clientX
    startY = e.clientY
    startLeft = canvas.scrollLeft
    startTop = canvas.scrollTop
    canvas.classList.add('is-grabbing')
    canvas.setPointerCapture(e.pointerId)
  })

  canvas.addEventListener('pointermove', (e) => {
    if (!dragging) return
    canvas.scrollLeft = startLeft - (e.clientX - startX)
    canvas.scrollTop = startTop - (e.clientY - startY)
  })

  function stopDrag(e) {
    if (!dragging) return
    dragging = false
    canvas.classList.remove('is-grabbing')
    if (e && e.pointerId != null) {
      try {
        canvas.releasePointerCapture(e.pointerId)
      } catch {
      }
    }
  }

  canvas.addEventListener('pointerup', stopDrag)
  canvas.addEventListener('pointercancel', stopDrag)
  canvas.addEventListener('pointerleave', stopDrag)
}

async function openFilePicker() {
  state.pick.page = 1
  await fetchPickList()
  openDialog('filePickModal')
}

async function fetchPickList() {
  const keyword = byId('pickKeyword').value || ''
  const params = { page: state.pick.page, pageSize: state.pick.pageSize, keyword, status: 'Completed' }
  const res = await zwavApi.getList(params)
  if (res && res.success) {
    state.pick.data = (res.data && res.data.data) || []
    state.pick.totalPages = (res.data && res.data.totalPages) || 1
  }
  renderPickTable()
}

function renderPickTable() {
  const rows = state.pick.data.filter((f) => f.analysisGuid !== state.guid)
  byId('pickTbody').innerHTML = rows
    .map((r) => `<tr><td title="${escapeHtml(r.originalName)}">${escapeHtml(r.originalName)}</td><td>${escapeHtml(formatDateTime(r.crtTime))}</td><td><button class="el-button is-link pick-add" data-guid="${escapeHtml(r.analysisGuid)}" data-name="${escapeHtml(r.originalName)}">添加</button></td></tr>`)
    .join('')
}

async function addCompareFile(guid, name) {
  if (state.loadedFiles.has(guid)) {
    alert('该文件已在列表中')
    return
  }
  const [hdrRes, cfgRes] = await Promise.all([
    zwavApi.getHdr(guid),
    zwavApi.getCfg(guid, false).catch(() => ({ success: true, data: null }))
  ])
  if (!hdrRes || !hdrRes.success || !hdrRes.data || !hdrRes.data.tripInfoJSON) {
    alert('该文件没有保护动作信息')
    return
  }
  const trip = normalizeTripInfo(hdrRes.data.tripInfoJSON)
  if (!trip.length) {
    alert('该文件没有保护动作信息')
    return
  }
  const startTime = cfgRes?.data?.startTimeRaw || '-'
  const color = SERIES_COLORS[state.loadedFiles.size % SERIES_COLORS.length] || SERIES_COLORS[0]
  state.loadedFiles.set(guid, { guid, name, startTime, trip })
  state.fileColors.set(guid, color)
  state.checkedGuids.add(guid)
  renderSequenceUi()
}

function bindEvents() {
  bindDialog('apiModal')
  bindDialog('axisModal')
  bindDialog('statsModal')
  bindDialog('seqModal')
  bindDialog('filePickModal')

  on('btnOpenApiConfig', 'click', openApiModal)
  on('btnSaveApiBase', 'click', () => {
    const v = byId('apiBaseInput').value
    setApiBaseUrl(v)
    closeDialog('apiModal')
    loadAll()
  })

  on('channelSearch', 'input', renderChannelLists)

  on('analogCheckAll', 'change', (e) => {
    const checked = !!e.target.checked
    const kw = byId('channelSearch').value
    const list = filterChannels(state.analogChannels, kw)
    list.forEach((c) => {
      if (checked) state.selectedAnalog.add(c.channelIndex)
      else state.selectedAnalog.delete(c.channelIndex)
    })
    renderChannelLists()
  })
  on('digitalCheckAll', 'change', (e) => {
    const checked = !!e.target.checked
    const kw = byId('channelSearch').value
    const list = filterChannels(state.digitalChannels, kw)
    list.forEach((c) => {
      if (checked) state.selectedDigital.add(c.channelIndex)
      else state.selectedDigital.delete(c.channelIndex)
    })
    renderChannelLists()
  })

  on('btnFetch', 'click', fetchWaveData)
  on('btnAxis', 'click', openAxisModal)
  on('btnApplyAxis', 'click', applyAxisSettings)
  on('btnStats', 'click', openStatsModal)
  on('btnExport', 'click', handleExport)
  on('btnSequence', 'click', openSequenceModal)
  on('btnFullScreen', 'click', toggleFullScreen)
  on('btnMarkerReset', 'click', resetMarkers)

  on('btnSeqAdd', 'click', openFilePicker)
  on('btnPickSearch', 'click', () => {
    state.pick.page = 1
    fetchPickList()
  })
  on('btnPickPrev', 'click', () => {
    if (state.pick.page <= 1) return
    state.pick.page--
    fetchPickList()
  })
  on('btnPickNext', 'click', () => {
    if (state.pick.page >= state.pick.totalPages) return
    state.pick.page++
    fetchPickList()
  })

  byId('pickTbody').addEventListener('click', async (e) => {
    const t = e.target
    if (!t.classList.contains('pick-add')) return
    const guid = t.getAttribute('data-guid')
    const name = t.getAttribute('data-name')
    try {
      await addCompareFile(guid, name)
      closeDialog('filePickModal')
    } catch (err) {
      alert(err.message || '添加失败')
    }
  })

  bindSeqCanvasInteractions()

  window.addEventListener('resize', () => {
    const seq = byId('seqModal')
    if (seq && seq.classList.contains('is-open')) {
      drawSequence()
    }
  })
}

init()

function bindTabs() {
  const items = Array.from(document.querySelectorAll('.el-tabs__item'))
  if (items.length === 0) return
  items.forEach((it) => {
    it.addEventListener('click', () => {
      items.forEach((x) => x.classList.remove('is-active'))
      it.classList.add('is-active')
      const tab = it.getAttribute('data-tab')
      const cfg = byId('tabCfg')
      const hdr = byId('tabHdr')
      if (!cfg || !hdr) return
      if (tab === 'hdr') {
        cfg.classList.remove('is-active')
        hdr.classList.add('is-active')
      } else {
        hdr.classList.remove('is-active')
        cfg.classList.add('is-active')
      }
    })
  })
}

bindTabs()
