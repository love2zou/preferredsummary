import { bindDialog, byId, closeDialog, escapeHtml, getApiBaseUrl, openDialog, qs, setApiBaseUrl, setHtml, setText } from './common.js'
import { zwavApi } from './zwav-api.js'

const RAW_COLORS = ['#1677ff', '#52c41a', '#faad14', '#ff4d4f', '#13c2c2', '#722ed1', '#eb2f96', '#2f54eb']
const RMS_COLORS = ['#722ed1', '#c41d7f', '#d48806', '#08979c', '#d4380d', '#237804', '#003a8c', '#597ef7']

const toleranceStandardLegacy = [
  [0, 0],
  [0.05, 0],
  [0.05, 50],
  [0.2, 50],
  [0.2, 70],
  [0.5, 70],
  [0.5, 80],
  [1, 80],
  [10, 80]
]

const toleranceStandard = [
  [0, 0],
  [0.2, 0],
  [0.2, 50],
  [0.5, 50],
  [0.5, 70],
  [1, 70],
  [1, 80],
  [10, 80]
]

const TOLERANCE_X_TOTAL_SECONDS = 10
const TOLERANCE_X_FIRST_SEG_SECONDS = 1
const TOLERANCE_X_FIRST_SEG_RATIO = 0.75

const state = {
  eventId: 0,
  process: null,
  analysisGuid: '',
  voltageChannels: [],
  selected: new Set(),
  hidden: new Set(),
  rmsHidden: new Set(),
  hasPickedSag: false,
  sagSelectedKeys: new Set(),
  toleranceHidden: { legacy: false, current: false },
  rawChart: null,
  toleranceChart: null,
  lastWaveData: null,
  isFullScreen: false,
  markerLeftXPixel: null,
  markerRightXPixel: null,
  rawSubBaseText: '',
  markerRenderRetry: 0,
  searchFromSample: 0,
  searchToSample: 10000,
  searchLimit: 20000,
  searchDownSample: 1,
  rmsWindowCycles: 1,
  rmsHopCycles: 0.5,
  params: {
    referenceVoltage: 0,
    sagThresholdPct: 90,
    interruptThresholdPct: 10,
    hysteresisPct: 2,
    minDurationMs: 10
  }
}

function on(id, event, handler) {
  const el = byId(id)
  if (!el) return
  el.addEventListener(event, handler)
}

function clamp(v, min, max) {
  return Math.max(min, Math.min(max, v))
}

let markerRenderRaf = 0
function scheduleMarkerRender() {
  if (!state.rawChart) return
  if (markerRenderRaf) cancelAnimationFrame(markerRenderRaf)
  markerRenderRaf = requestAnimationFrame(() => {
    markerRenderRaf = requestAnimationFrame(() => {
      markerRenderRaf = 0
      initMarkersIfNeeded()
      refreshMarkerSummary()
      renderMarkerGraphics()
    })
  })
}

function formatMs(v) {
  const n = Number(v)
  if (!Number.isFinite(n)) return '-'
  return n.toFixed(0)
}

function formatPercent(v) {
  const n = Number(v)
  if (!Number.isFinite(n)) return '-'
  return n.toFixed(2)
}

function formatUtcMs(str) {
  if (!str) return '-'
  const d = new Date(str)
  if (Number.isNaN(d.getTime())) return String(str)
  const yyyy = d.getFullYear()
  const mm = String(d.getMonth() + 1).padStart(2, '0')
  const dd = String(d.getDate()).padStart(2, '0')
  const hh = String(d.getHours()).padStart(2, '0')
  const mi = String(d.getMinutes()).padStart(2, '0')
  const ss = String(d.getSeconds()).padStart(2, '0')
  const ms = String(d.getMilliseconds()).padStart(3, '0')
  return `${yyyy}-${mm}-${dd} ${hh}:${mi}:${ss}.${ms}`
}

function getRefVoltage() {
  const v = Number(state.params.referenceVoltage || state.process?.event?.referenceVoltage)
  if (Number.isFinite(v) && v > 0) return v
  const p = state.process?.phases || []
  const pv = Number(p[0]?.referenceVoltage)
  if (Number.isFinite(pv) && pv > 0) return pv
  return 0
}

function renderHeader() {
  const name = state.process?.event?.originalName || ''
  const id = state.process?.event?.id || state.eventId
  setText('fileName', name || '-')
  setText('headerSub', `事件ID: ${id}`)
}

function getGridRects() {
  if (!state.rawChart) return []
  const model = state.rawChart.getModel?.()
  if (!model) return []
  const grids = model.getComponents?.('grid') || []
  const rects = []
  for (let i = 0; i < grids.length; i++) {
    const cs = grids[i]?.coordinateSystem
    const r = cs?.getRect?.()
    if (r) rects.push(r)
  }
  return rects
}

function getAllGridYRange() {
  const rects = getGridRects()
  if (!rects.length) return null
  let minY = rects[0].y
  let maxY = rects[0].y + rects[0].height
  for (let i = 1; i < rects.length; i++) {
    minY = Math.min(minY, rects[i].y)
    maxY = Math.max(maxY, rects[i].y + rects[i].height)
  }
  return { minY, maxY }
}

function getGridXRange() {
  const rects = getGridRects()
  if (!rects.length) return null
  let minX = rects[0].x
  let maxX = rects[0].x + rects[0].width
  for (let i = 1; i < rects.length; i++) {
    minX = Math.min(minX, rects[i].x)
    maxX = Math.max(maxX, rects[i].x + rects[i].width)
  }
  return { minX, maxX }
}

function initMarkersIfNeeded() {
  const xr = getGridXRange()
  if (!xr) return
  const width = xr.maxX - xr.minX
  if (state.markerLeftXPixel === null) state.markerLeftXPixel = xr.minX + width * 0.3
  if (state.markerRightXPixel === null) state.markerRightXPixel = xr.minX + width * 0.7
}

function getWaveAtXPixel(xPixel) {
  const data = state.lastWaveData
  if (!state.rawChart || !data || !Array.isArray(data.rows) || data.rows.length === 0) return null
  const rects = getGridRects()
  const rect = rects[0]
  if (!rect) return null

  const val = state.rawChart.convertFromPixel({ xAxisIndex: 0 }, [xPixel, rect.y])
  const targetMs = Array.isArray(val) ? Number(val[0]) : Number(val)
  const times = data.rows.map((r) => Number(r?.timeMs))
  if (!times.length) return null

  let nearestIdx = 0
  if (Number.isFinite(targetMs)) {
    nearestIdx = findNearestIndex(times, targetMs)
  } else {
    const xr = getGridXRange()
    if (!xr) return null
    const w = xr.maxX - xr.minX
    if (!(w > 0)) return null
    const ratio = clamp((Number(xPixel) - xr.minX) / w, 0, 1)
    nearestIdx = clamp(Math.round(ratio * (times.length - 1)), 0, times.length - 1)
  }
  const timeMs = Number(times[nearestIdx])
  if (!Number.isFinite(timeMs)) return null
  return { timeMs, nearestIdx }
}

function refreshMarkerSummary() {
  const lx = state.markerLeftXPixel
  const rx = state.markerRightXPixel
  if (lx === null || rx === null) {
    const rawSub = byId('rawSub')
    if (rawSub && state.rawSubBaseText) rawSub.textContent = state.rawSubBaseText
    return
  }

  const leftWave = getWaveAtXPixel(lx)
  const rightWave = getWaveAtXPixel(rx)
  if (!leftWave || !rightWave) {
    const rawSub = byId('rawSub')
    if (rawSub && state.rawSubBaseText) rawSub.textContent = state.rawSubBaseText
    return
  }

  const leftMs = Number(leftWave.timeMs)
  const rightMs = Number(rightWave.timeMs)
  const delta = Math.abs(rightMs - leftMs)

  const summary =
    `LeftLine：[${leftMs.toFixed(3)}ms][第${leftWave.nearestIdx + 1}个点] | ` +
    `RightLine：[${rightMs.toFixed(3)}ms][第${rightWave.nearestIdx + 1}个点] | ` +
    `RightLine - LeftLine：${delta.toFixed(3)}ms`

  const rawSub = byId('rawSub')
  if (rawSub && state.rawSubBaseText) rawSub.textContent = `${state.rawSubBaseText} | ${summary}`

  const footer = byId('markerFooter')
  const textEl = byId('markerFooterText')
  if (footer && textEl) {
    footer.style.display = ''
    textEl.textContent = summary
  }
}

function updateMarkerByPixelX(side, xPixel) {
  const xr = getGridXRange()
  if (!xr) return
  const xx = clamp(Number(xPixel), xr.minX, xr.maxX)
  if (side === 'left') state.markerLeftXPixel = xx
  else state.markerRightXPixel = xx
  refreshMarkerSummary()
}

function renderMarkerGraphics() {
  const xr = getGridXRange()
  const yr = getAllGridYRange()
  if (!state.rawChart || !xr || !yr) {
    if (state.markerRenderRetry < 3) {
      state.markerRenderRetry++
      setTimeout(() => scheduleMarkerRender(), 60)
    }
    return
  }
  state.markerRenderRetry = 0
  if (state.markerLeftXPixel === null || state.markerRightXPixel === null) return

  const lx = clamp(Number(state.markerLeftXPixel), xr.minX, xr.maxX)
  const rx = clamp(Number(state.markerRightXPixel), xr.minX, xr.maxX)
  const y1 = yr.minY
  const y2 = yr.maxY

  state.rawChart.setOption(
    {
      graphic: [
        {
          id: 'markerLeftLine',
          type: 'line',
          zlevel: 10,
          z: 100,
          draggable: true,
          cursor: 'ew-resize',
          shape: { x1: lx, y1, x2: lx, y2 },
          style: { stroke: '#E6A23C', lineWidth: 1 },
          ondrag: function () {
            const xr2 = getGridXRange()
            const minX2 = xr2 ? xr2.minX : 0
            const maxX2 = xr2 ? xr2.maxX : state.rawChart?.getWidth?.() || 0
            const baseX = Number(this.shape?.x1) || 0
            const posX = Number(this.position?.[0]) || 0
            let currentX = baseX + posX
            currentX = clamp(currentX, minX2, maxX2)
            if (this.position) {
              this.position[0] = currentX - baseX
              this.position[1] = 0
            }
            updateMarkerByPixelX('left', currentX)
          },
          ondragend: function () {
            const xr2 = getGridXRange()
            const minX2 = xr2 ? xr2.minX : 0
            const maxX2 = xr2 ? xr2.maxX : state.rawChart?.getWidth?.() || 0
            const baseX = Number(this.shape?.x1) || 0
            const posX = Number(this.position?.[0]) || 0
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
          zlevel: 10,
          z: 100,
          draggable: true,
          cursor: 'ew-resize',
          shape: { x1: rx, y1, x2: rx, y2 },
          style: { stroke: '#409EFF', lineWidth: 1 },
          ondrag: function () {
            const xr2 = getGridXRange()
            const minX2 = xr2 ? xr2.minX : 0
            const maxX2 = xr2 ? xr2.maxX : state.rawChart?.getWidth?.() || 0
            const baseX = Number(this.shape?.x1) || 0
            const posX = Number(this.position?.[0]) || 0
            let currentX = baseX + posX
            currentX = clamp(currentX, minX2, maxX2)
            if (this.position) {
              this.position[0] = currentX - baseX
              this.position[1] = 0
            }
            updateMarkerByPixelX('right', currentX)
          },
          ondragend: function () {
            const xr2 = getGridXRange()
            const minX2 = xr2 ? xr2.minX : 0
            const maxX2 = xr2 ? xr2.maxX : state.rawChart?.getWidth?.() || 0
            const baseX = Number(this.shape?.x1) || 0
            const posX = Number(this.position?.[0]) || 0
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
  state.rawChart && state.rawChart.setOption({ graphic: [] }, false)
  scheduleMarkerRender()
}

function filterChannels(list, keyword) {
  const kw = String(keyword || '').trim().toLowerCase()
  if (!kw) return list
  return list.filter((c) => {
    const name = String(c.channelName || '').toLowerCase()
    const idx = String(c.channelIndex ?? '')
    const phase = String(c.phase || '').toLowerCase()
    return name.includes(kw) || idx.includes(kw) || phase.includes(kw)
  })
}

function renderChannelList() {
  const kw = byId('channelSearch').value || ''
  const list = filterChannels(state.voltageChannels, kw)

  setText('analogCount', String(state.selected.size))
  const allEl = byId('analogCheckAll')
  if (allEl) {
    const checkedCount = list.filter((c) => state.selected.has(c.channelIndex)).length
    allEl.checked = list.length > 0 && checkedCount === list.length
    allEl.indeterminate = checkedCount > 0 && checkedCount < list.length
  }

  const html = list
    .map((c) => {
      const id = `a_${c.channelIndex}`
      const checked = state.selected.has(c.channelIndex) ? 'checked' : ''
      const hidden = state.hidden.has(c.channelIndex) ? 'style="opacity:0.45"' : ''
      const title = `${c.channelIndex}. ${c.channelName || ''}`
      const phase = String(c.phase || '').toUpperCase()
      return `<div class="channel-item" ${hidden}>
        <input class="analog-check" type="checkbox" id="${id}" data-idx="${escapeHtml(c.channelIndex)}" ${checked}/>
        <label for="${id}" title="${escapeHtml(title)}">${escapeHtml(title)}${phase ? ` <span style="color:var(--el-text-color-secondary);font-size:12px;">(${escapeHtml(phase)})</span>` : ''}</label>
      </div>`
    })
    .join('')
  setHtml('analogList', html)

  byId('analogList').querySelectorAll('.analog-check').forEach((el) => {
    el.addEventListener('change', () => {
      const idx = Number(el.getAttribute('data-idx'))
      if (el.checked) state.selected.add(idx)
      else state.selected.delete(idx)
      setText('analogCount', String(state.selected.size))
    })
  })
}

function buildToleranceCurve(xLen, y) {
  const arr = new Array(xLen)
  for (let i = 0; i < xLen; i++) arr[i] = y
  return arr
}

function findNearestIndex(times, target) {
  if (!times.length) return 0
  let lo = 0
  let hi = times.length - 1
  while (lo <= hi) {
    const mid = (lo + hi) >> 1
    const v = times[mid]
    if (v === target) return mid
    if (v < target) lo = mid + 1
    else hi = mid - 1
  }
  if (lo <= 0) return 0
  if (lo >= times.length) return times.length - 1
  const a = times[lo - 1]
  const b = times[lo]
  return Math.abs(a - target) <= Math.abs(b - target) ? lo - 1 : lo
}

function getRmsWindowN(times, freqHz, windowCycles) {
  if (!Array.isArray(times) || times.length < 2) return 0
  const dt = (times[times.length - 1] - times[0]) / Math.max(1, times.length - 1)
  if (!Number.isFinite(dt) || dt <= 0) return 0
  const f = Number.isFinite(freqHz) && freqHz > 0 ? freqHz : 50
  const periodMs = 1000 / f
  const cycles = Number.isFinite(windowCycles) && windowCycles > 0 ? windowCycles : 1
  const n = Math.round((periodMs / dt) * cycles)
  return clamp(n, 3, 5000)
}

function computeRollingRms(values, windowN, hopN) {
  const n = Array.isArray(values) ? values.length : 0
  if (n === 0 || windowN <= 0) return []

  const sumSq = new Array(n + 1).fill(0)
  const cnt = new Array(n + 1).fill(0)
  for (let i = 0; i < n; i++) {
    const v = Number(values[i])
    const ok = Number.isFinite(v)
    sumSq[i + 1] = sumSq[i] + (ok ? v * v : 0)
    cnt[i + 1] = cnt[i] + (ok ? 1 : 0)
  }

  const out = new Array(n).fill(null)
  const hop = clamp(Math.round(hopN || 0), 1, windowN)
  for (let i = windowN - 1; i < n; i += hop) {
    const s = sumSq[i + 1] - sumSq[i + 1 - windowN]
    const c = cnt[i + 1] - cnt[i + 1 - windowN]
    if (c < Math.max(3, Math.floor(windowN * 0.9))) {
      out[i] = null
      continue
    }
    out[i] = Math.sqrt(s / c)
  }
  return out
}

function getDefaultSagRows() {
  const list = (state.process?.computedEvents || []).slice()
  let maxMag = -Infinity
  for (const evt of list) {
    const mag = Number(evt?.sagMagnitudePct)
    if (!Number.isFinite(mag)) continue
    if (mag > maxMag) maxMag = mag
  }
  if (!Number.isFinite(maxMag)) return []
  return list.filter((evt) => Number(evt?.sagMagnitudePct) === maxMag)
}

function getSagRowKey(evt) {
  const phase = String(evt?.phase || '')
  const startMs = Number(evt?.startMs ?? 0)
  const endMs = Number(evt?.endMs ?? 0)
  const kind = String(evt?.eventType || '')
  return `${phase}-${kind}-${startMs}-${endMs}`
}

function mapToleranceX(seconds) {
  const x = Number(seconds)
  if (!Number.isFinite(x) || x <= 0) return 0

  const total = TOLERANCE_X_TOTAL_SECONDS
  const firstSeg = TOLERANCE_X_FIRST_SEG_SECONDS
  const firstRatio = TOLERANCE_X_FIRST_SEG_RATIO

  if (x <= firstSeg) {
    return (x / firstSeg) * firstRatio
  }

  const tail = Math.min(x, total) - firstSeg
  const tailTotal = total - firstSeg
  if (tailTotal <= 0) return firstRatio

  return firstRatio + (tail / tailTotal) * (1 - firstRatio)
}

function mapToleranceLineData(list) {
  return (list || []).map((item) => {
    const x = Number(item?.[0])
    const y = Number(item?.[1])
    return [mapToleranceX(x), y, x]
  })
}

function mapTolerancePointData(point) {
  const rawX = Number(point?.value?.[0])
  const y = Number(point?.value?.[1])
  return {
    ...point,
    rawDurationSec: rawX,
    value: [mapToleranceX(rawX), y]
  }
}

function buildToleranceNormalAreaData(lineData) {
  if (!Array.isArray(lineData) || lineData.length === 0) return []

  const topY = 100
  const polygon = []

  for (const item of lineData) {
    const x = Number(item?.[0])
    if (!Number.isFinite(x)) continue
    polygon.push([x, topY])
  }

  for (let i = lineData.length - 1; i >= 0; i--) {
    const item = lineData[i]
    const x = Number(item?.[0])
    const y = Number(item?.[1])
    if (!Number.isFinite(x) || !Number.isFinite(y)) continue
    polygon.push([x, y])
  }

  return polygon
}

function formatToleranceXAxisLabel(v) {
  const n = Number(v)
  if (!Number.isFinite(n)) return ''
  if (Math.abs(n - Math.round(n)) < 1e-9) return `${Math.round(n)}`
  return `${n.toFixed(2).replace(/\.?0+$/, '')}`
}

function buildToleranceXAxisTicks() {
  const ticks = [0, 0.05, 0.1, 0.2, 0.5, 1, 2, 5, 10]
  return ticks.map((sec) => ({
    raw: sec,
    pos: mapToleranceX(sec),
    label: formatToleranceXAxisLabel(sec)
  }))
}

function findNearestToleranceTick(axisValue, ticks) {
  let best = null
  let bestDiff = Infinity

  for (const tick of ticks) {
    const diff = Math.abs(Number(tick.pos) - axisValue)
    if (diff < bestDiff) {
      best = tick
      bestDiff = diff
    }
  }

  return best && bestDiff <= 0.032 ? best : null
}

function renderSagTable() {
  const rows = (state.process?.computedEvents || []).slice().sort((a, b) => Number(a.startMs) - Number(b.startMs))
  setText('sagCountText', `共 ${rows.length} 条`)

  if (!state.hasPickedSag && state.sagSelectedKeys.size === 0) {
    getDefaultSagRows().forEach((x) => state.sagSelectedKeys.add(getSagRowKey(x)))
  }

  const tbody = rows
    .map((evt, idx) => {
      const key = getSagRowKey(evt)
      const checked = state.sagSelectedKeys.has(key) ? 'checked' : ''
      const occur = evt.occurTimeUtc ? formatUtcMs(evt.occurTimeUtc) : (evt.startTimeUtc ? formatUtcMs(evt.startTimeUtc) : '-')
      const typeText = evt.eventType === 'Interruption' ? '中断' : evt.eventType === 'Sag' ? '暂降' : String(evt.eventType || '-')
      return `
        <tr>
          <td class="cell-center"><input type="checkbox" class="sag-check" data-key="${escapeHtml(key)}" ${checked}></td>
          <td class="cell-center">${idx + 1}</td>
          <td class="cell-center">${escapeHtml(String(evt.phase || '-'))}</td>
          <td class="cell-center">${escapeHtml(typeText)}</td>
          <td>${escapeHtml(occur)}</td>
          <td>${escapeHtml(formatMs(evt.durationMs))}</td>
          <td>${escapeHtml(formatPercent(evt.sagMagnitudePct))}</td>
          <td>${escapeHtml(formatPercent(evt.residualVoltagePct))}</td>
        </tr>
      `
    })
    .join('')
  setHtml('sagTbody', tbody)

  const allKeys = rows.map((evt) => getSagRowKey(evt))
  initSagCheckAll(allKeys)

  byId('sagTbody').querySelectorAll('.sag-check').forEach((el) => {
    el.addEventListener('change', () => {
      const key = String(el.getAttribute('data-key') || '')
      if (!key) return
      state.hasPickedSag = true
      if (el.checked) state.sagSelectedKeys.add(key)
      else state.sagSelectedKeys.delete(key)
      updateSagCheckAll(allKeys)
      updateToleranceChart()
    })
  })

  updateToleranceChart()
}

function initSagCheckAll(allKeys) {
  const el = byId('sagCheckAll')
  if (!el) return
  updateSagCheckAll(allKeys)
  el.onchange = () => {
    state.hasPickedSag = true
    if (el.checked) allKeys.forEach((k) => state.sagSelectedKeys.add(k))
    else state.sagSelectedKeys.clear()
    renderSagTable()
  }
}

function updateSagCheckAll(allKeys) {
  const el = byId('sagCheckAll')
  if (!el) return
  const total = allKeys.length
  if (total === 0) {
    el.checked = false
    el.indeterminate = false
    return
  }
  let checked = 0
  for (let i = 0; i < allKeys.length; i++) {
    if (state.sagSelectedKeys.has(allKeys[i])) checked++
  }
  el.checked = checked > 0 && checked === total
  el.indeterminate = checked > 0 && checked < total
}

function buildToleranceLegend(points) {
  const legend = []
  const legacyHidden = state.toleranceHidden?.legacy
  const currentHidden = state.toleranceHidden?.current

  legend.push(
    `<span class="legend-item curve-legacy ${legacyHidden ? 'is-hidden' : ''}" data-curve="legacy"><span class="legend-line-demo legacy"></span><span class="legend-text">旧版容忍度标准</span></span>`
  )
  legend.push(
    `<span class="legend-item curve-current ${currentHidden ? 'is-hidden' : ''}" data-curve="current"><span class="legend-line-demo current"></span><span class="legend-text">新版容忍度标准</span></span>`
  )
  for (const p of points) {
    legend.push(`<span class="legend-item dyn-point"><span class="legend-dot" style="background:${escapeHtml(p.color)}"></span><span class="legend-text">${escapeHtml(p.label)}</span></span>`)
  }
  setHtml('toleranceLegend', legend.join(''))

  byId('toleranceLegend').querySelectorAll('.legend-item[data-curve]').forEach((el) => {
    el.addEventListener('click', () => {
      const c = String(el.getAttribute('data-curve') || '')
      if (c === 'legacy') state.toleranceHidden.legacy = !state.toleranceHidden.legacy
      if (c === 'current') state.toleranceHidden.current = !state.toleranceHidden.current
      updateToleranceChart()
    })
  })
}

function updateToleranceChart() {
  if (!state.toleranceChart) return
  const all = (state.process?.computedEvents || []).slice()
  const selected = all.filter((evt) => state.sagSelectedKeys.has(getSagRowKey(evt)))
  const list = selected.length > 0 ? selected : getDefaultSagRows()
  const points = list
    .map((evt, idx) => {
      const x = Number(evt.durationMs) / 1000
      const y = Number(evt.residualVoltagePct)
      if (!Number.isFinite(x) || !Number.isFinite(y) || x < 0) return null
      const phase = String(evt.phase || '-')
      return {
        key: getSagRowKey(evt),
        label: `${phase}相 ${x.toFixed(3)}s ${y.toFixed(2)}%`,
        color: RAW_COLORS[idx % RAW_COLORS.length],
        value: [x, y],
        phase,
        durationMs: evt.durationMs,
        durationSec: x,
        occurTimeText: evt.occurTimeUtc ? formatUtcMs(evt.occurTimeUtc) : (evt.startTimeUtc ? formatUtcMs(evt.startTimeUtc) : '-'),
        eventTypeText: evt.eventType === 'Interruption' ? '中断' : evt.eventType === 'Sag' ? '暂降' : evt.eventType || '-',
        residualVoltage: evt.residualVoltage,
        residualVoltagePct: evt.residualVoltagePct,
        sagMagnitudePct: evt.sagMagnitudePct
      }
    })
    .filter(Boolean)

  buildToleranceLegend(points)

  const xTicks = buildToleranceXAxisTicks()
  const mappedToleranceStandardLine = mapToleranceLineData(toleranceStandard)
  const mappedToleranceStandardLineLegacy = mapToleranceLineData(toleranceStandardLegacy)
  const currentNormalAreaData = buildToleranceNormalAreaData(mappedToleranceStandardLine)
  const legacyNormalAreaData = buildToleranceNormalAreaData(mappedToleranceStandardLineLegacy)

  const pointSeries = points.map((x, idx) => {
    const mappedPoint = mapTolerancePointData(x)
    return {
      name: x.label,
      type: 'scatter',
      showInLegend: false,
      symbol: 'circle',
      symbolSize: 9,
      data: [mappedPoint],
      z: 20,
      itemStyle: {
        color: x.color,
        borderColor: '#ffffff',
        borderWidth: 1.5,
        shadowBlur: 6,
        shadowColor: 'rgba(0,0,0,0.20)'
      },
      emphasis: {
        scale: true,
        itemStyle: {
          color: x.color,
          borderColor: '#ffffff',
          borderWidth: 2,
          shadowBlur: 10,
          shadowColor: 'rgba(0,0,0,0.28)'
        }
      },
      label: { show: false }
    }
  })

  const toleranceSeries = []

  // Legacy standard
  if (!state.toleranceHidden?.legacy) {
    toleranceSeries.push(
      {
        name: '旧版正常范围',
        type: 'custom',
        silent: true,
        z: 1,
        renderItem: (params, api) => {
          const pts = legacyNormalAreaData
            .map((item) => {
              const x = api.value(0, item)
              const y = api.value(1, item)
              return api.coord([x, y])
            })
            .filter((p) => Array.isArray(p) && p.length >= 2)

          if (!pts.length) return null

          return {
            type: 'polygon',
            shape: { points: pts },
            style: {
              fill: 'rgba(0, 0, 0, 0.05)'
            }
          }
        },
        data: legacyNormalAreaData
      },
      {
        name: '旧版容忍度标准',
        type: 'line',
        data: mappedToleranceStandardLineLegacy,
        showSymbol: true,
        symbol: 'circle',
        symbolSize: 6,
        smooth: false,
        z: 3,
        lineStyle: {
          width: 1,
          color: '#000000',
          type: 'solid'
        },
        itemStyle: { color: '#000000', opacity: 0 },
        emphasis: {
          scale: true,
          itemStyle: {
            color: '#000000',
            opacity: 1,
            borderColor: '#ffffff',
            borderWidth: 2,
            shadowBlur: 10,
            shadowColor: 'rgba(0,0,0,0.28)'
          }
        }
      }
    )
  }

  // Current standard
  if (!state.toleranceHidden?.current) {
    toleranceSeries.push(
      {
        name: '新版正常范围',
        type: 'custom',
        silent: true,
        z: 2,
        renderItem: (params, api) => {
          const pts = currentNormalAreaData
            .map((item) => {
              const x = api.value(0, item)
              const y = api.value(1, item)
              return api.coord([x, y])
            })
            .filter((p) => Array.isArray(p) && p.length >= 2)

          if (!pts.length) return null

          return {
            type: 'polygon',
            shape: { points: pts },
            style: {
              fill: 'rgba(22, 119, 255, 0.08)'
            }
          }
        },
        data: currentNormalAreaData
      },
      {
        name: '新版容忍度标准',
        type: 'line',
        data: mappedToleranceStandardLine,
        showSymbol: true,
        symbol: 'circle',
        symbolSize: 6,
        smooth: false,
        z: 5,
        lineStyle: {
          width: 1,
          color: '#1677ff',
          type: 'solid'
        },
        itemStyle: { color: '#1677ff', opacity: 0 },
        emphasis: {
          scale: true,
          itemStyle: {
            color: '#1677ff',
            opacity: 1,
            borderColor: '#ffffff',
            borderWidth: 2,
            shadowBlur: 10,
            shadowColor: 'rgba(0,0,0,0.28)'
          }
        }
      }
    )
  }

  // 1-second split line
  toleranceSeries.push({
    name: '1秒分界线',
    type: 'line',
    silent: true,
    showSymbol: false,
    z: 25,
    data: [
      [mapToleranceX(1), 0],
      [mapToleranceX(1), 100]
    ],
    lineStyle: {
      width: 1,
      color: '#909399',
      type: 'dashed'
    }
  })

  state.toleranceChart.setOption({
    tooltip: {
      trigger: 'item',
      formatter: (p) => {
        const seriesName = String(p?.seriesName || '')
        const d = p?.data

        if (seriesName.includes('标准')) {
          const value = Array.isArray(d) ? d : Array.isArray(p?.value) ? p.value : []
          const rawX = Number(value?.[2] ?? value?.[0])
          const y = Number(value?.[1])

          return [
            `<div><b>${seriesName}</b></div>`,
            `<div>持续时间：${Number.isFinite(rawX) ? rawX.toFixed(3) : '-'} s</div>`,
            `<div>残余电压：${Number.isFinite(y) ? y.toFixed(3) : '-'} %</div>`
          ].join('')
        }

        const obj = (d && typeof d === 'object' && !Array.isArray(d) ? d : {})
        const rawX = Number(obj?.rawDurationSec)
        const value = (obj?.value || [])
        const y = Number(value[1])

        return [
          `<div><b>${escapeHtml(`${obj.phase || '-'}相 ${obj.eventTypeText || '-'}`)}</b></div>`,
          `<div>发生时间：${escapeHtml(obj.occurTimeText || '-')}</div>`,
          `<div>持续时间：${Number.isFinite(rawX) ? rawX.toFixed(3) : '-'} s</div>`,
          `<div>暂降幅值：${Number.isFinite(Number(obj.sagMagnitudePct)) ? Number(obj.sagMagnitudePct).toFixed(2) : '-'} %</div>`,
          `<div>残余电压：${Number.isFinite(y) ? y.toFixed(2) : '-'} %</div>`
        ].join('')
      }
    },
    grid: { left: 32, right: 64, top: 42, bottom: 28, containLabel: true },
    legend: {
      show: false
    },
    graphic: [
      {
        id: 'tolerance-split-line',
        type: 'line',
        silent: true,
        z: 30,
        shape: {
          x1: 0,
          y1: 0,
          x2: 0,
          y2: 0
        },
        style: {
          stroke: '#909399',
          lineWidth: 1,
          lineDash: [4, 4]
        }
      },
      {
        id: 'tolerance-text-left',
        type: 'text',
        silent: true,
        z: 31,
        style: {
          text: '0~1s 重点观察区',
          fill: '#606266',
          font: '12px sans-serif',
          textAlign: 'center'
        },
        left: '28%',
        top: 14
      },
      {
        id: 'tolerance-text-right',
        type: 'text',
        silent: true,
        z: 31,
        style: {
          text: '1~10s 压缩显示区',
          fill: '#909399',
          font: '12px sans-serif',
          textAlign: 'center'
        },
        left: '78%',
        top: 14
      }
    ],
    xAxis: {
      type: 'value',
      name: '持续时间(s)',
      nameLocation: 'end',
      nameGap: 5,
      nameTextStyle: { align: 'left', padding: [0, 0, 0, 0] },
      min: 0,
      max: 1,
      interval: 0.05,
      axisLabel: {
        fontSize: 10,
        margin: 10,
        formatter: (v) => {
          const n = Number(v)
          const hit = findNearestToleranceTick(n, xTicks)
          return hit ? hit.label : ''
        }
      },
      axisTick: {
        show: true,
        length: 4
      },
      axisLine: { show: true },
      minorTick: { show: false },
      splitLine: {
        show: true,
        lineStyle: { type: 'solid', opacity: 0.15 }
      }
    },
    yAxis: {
      type: 'value',
      name: '残余电压 (%)',
      nameLocation: 'middle',
      nameRotate: 90,
      nameGap: 32,
      min: 0,
      max: 100,
      interval: 10,
      axisLabel: {
        fontSize: 10,
        margin: 8,
        formatter: (v) => {
          const n = Number(v)
          if (!Number.isFinite(n)) return String(v)
          return String(Math.round(n))
        }
      },
      minorTick: { show: true, splitNumber: 4 },
      minorSplitLine: { show: true, lineStyle: { opacity: 0.12 } },
      splitLine: {
        show: true,
        lineStyle: { type: 'solid', opacity: 0.35 }
      }
    },
    series: [
      ...toleranceSeries,
      ...pointSeries
    ]
  }, true)

  // Update split line position
  setTimeout(() => {
    if (!state.toleranceChart) return
    const grid = state.toleranceChart.convertToPixel({ xAxisIndex: 0, yAxisIndex: 0 }, [mapToleranceX(1), 100])
    const base = state.toleranceChart.convertToPixel({ xAxisIndex: 0, yAxisIndex: 0 }, [mapToleranceX(1), 0])

    if (Array.isArray(grid) && grid.length >= 2 && Array.isArray(base) && base.length >= 2) {
      state.toleranceChart.setOption({
        graphic: [
          {
            id: 'tolerance-split-line',
            shape: {
              x1: grid[0],
              y1: grid[1],
              x2: base[0],
              y2: base[1]
            }
          }
        ]
      })
    }
  }, 100)
}

function renderRawLegend() {
  const wave = state.lastWaveData
  const selected = Array.from(state.selected).filter((x) => (wave?.channels || []).includes(x)).slice(0, 12)
  const items = []
  selected.forEach((channelIndex, i) => {
    const ch = state.voltageChannels.find((c) => Number(c.channelIndex) === Number(channelIndex))
    const label = ch ? `${ch.channelIndex}. ${ch.channelName || ''}` : `CH${channelIndex}`
    const hiddenCls = state.hidden.has(channelIndex) ? 'style="opacity:0.45"' : ''
    items.push(`<span class="raw-legend-item" data-kind="raw" data-idx="${escapeHtml(channelIndex)}" ${hiddenCls}><span class="legend-line" style="background:${RAW_COLORS[i % RAW_COLORS.length]}"></span><span class="legend-text">${escapeHtml(label)}</span></span>`)
  })
  selected.forEach((channelIndex, i) => {
    const ch = state.voltageChannels.find((c) => Number(c.channelIndex) === Number(channelIndex))
    const phase = String(ch?.phase || '').toUpperCase()
    const label = `${phase ? `${phase}相` : `CH${channelIndex}`} RMS`
    const hiddenCls = state.rmsHidden.has(channelIndex) ? 'style="opacity:0.45"' : ''
    items.push(`<span class="raw-legend-item" data-kind="rms" data-idx="${escapeHtml(channelIndex)}" ${hiddenCls}><span class="legend-line rms" style="background:${RMS_COLORS[i % RMS_COLORS.length]}"></span><span class="legend-text">${escapeHtml(label)}</span></span>`)
  })
  items.push(`<span class="raw-legend-item"><span class="legend-dash dash-sag"></span><span class="legend-text">暂降阈值</span></span>`)
  items.push(`<span class="raw-legend-item"><span class="legend-dash dash-recover"></span><span class="legend-text">恢复阈值</span></span>`)
  items.push(`<span class="raw-legend-item"><span class="legend-dash dash-interrupt"></span><span class="legend-text">中断阈值</span></span>`)
  items.push(`<span class="raw-legend-item"><span class="legend-area"></span><span class="legend-text">暂降区间</span></span>`)
  items.push(`<span class="raw-legend-item"><span class="legend-vline"></span><span class="legend-text">开始/结束</span></span>`)
  setHtml('rawLegend', items.join(''))

  byId('rawLegend').querySelectorAll('.raw-legend-item[data-kind="raw"]').forEach((el) => {
    el.addEventListener('click', () => {
      const idx = Number(el.getAttribute('data-idx'))
      if (state.hidden.has(idx)) state.hidden.delete(idx)
      else state.hidden.add(idx)
      renderRawLegend()
      updateRawChart()
    })
  })
  byId('rawLegend').querySelectorAll('.raw-legend-item[data-kind="rms"]').forEach((el) => {
    el.addEventListener('click', () => {
      const idx = Number(el.getAttribute('data-idx'))
      if (state.rmsHidden.has(idx)) state.rmsHidden.delete(idx)
      else state.rmsHidden.add(idx)
      renderRawLegend()
      updateRawChart()
    })
  })
}

function updateRawChart() {
  if (!state.rawChart) return
  const wave = state.lastWaveData
  if (!wave || !wave.rows || !wave.rows.length) return

  const selectedAll = Array.from(state.selected).filter((x) => (wave.channels || []).includes(x)).slice(0, 12)
  const selected = selectedAll.filter((x) => !state.hidden.has(x))
  if (selected.length === 0) {
    state.rawChart.setOption({ series: [], xAxis: [], yAxis: [], grid: [] }, true)
    initMarkersIfNeeded()
    refreshMarkerSummary()
    renderMarkerGraphics()
    renderRawLegend()
    return
  }

  const rows = wave.rows
  const xTimes = rows.map((r) => Number(r.timeMs))
  const xData = xTimes.map((t) => Number(t).toFixed(3))
  const chartEl = byId('rawChart')
  const gridGap = 20
  const scroll = byId('rawWrap')?.querySelector('.chart-scroll-container')
  const fullAvail = state.isFullScreen && scroll ? Number(scroll.clientHeight) : NaN
  const desiredHeight = Number.isFinite(fullAvail) && fullAvail > 120
    ? fullAvail
    : clamp(selected.length * (140 + gridGap) + 40, 420, 2400)
  if (chartEl) chartEl.style.height = `${desiredHeight}px`
  const gridHeight = Math.max(140, Math.floor((desiredHeight - gridGap * (selected.length - 1) - 40) / selected.length))
  const freqHz = Number(state.process?.frequencyHz)
  const rmsWindowN = getRmsWindowN(xTimes, freqHz, state.rmsWindowCycles)
  const rmsHopN = Math.max(1, Math.round(rmsWindowN * (Number(state.rmsHopCycles) / Math.max(0.0001, Number(state.rmsWindowCycles) || 1))))

  const grids = []
  const xAxis = []
  const yAxis = []
  const series = []

  const topBase = 20
  const refV = getRefVoltage()
  const sagPct = Number(state.params.sagThresholdPct ?? 90)
  const interruptPct = Number(state.params.interruptThresholdPct ?? 10)
  const hysteresisPct = Number(state.params.hysteresisPct ?? 2)
  const recoverPct = sagPct + hysteresisPct

  selected.forEach((channelIndex, i) => {
    const top = topBase + i * (gridHeight + gridGap)
    grids.push({ left: 100, right: 20, top, height: gridHeight - 18, containLabel: true })
    xAxis.push({
      type: 'category',
      gridIndex: i,
      data: xData,
      boundaryGap: false,
      position: 'top',
      axisLabel: { show: i === 0, fontSize: 10, margin: i === 0 ? 23 : 8, formatter: (v) => `${v}` },
      axisTick: { show: i === 0 },
      axisLine: { show: i === 0 }
    })
    yAxis.push({
      type: 'value',
      gridIndex: i,
      name: '',
      nameLocation: 'middle',
      nameRotate: 90,
      nameGap: 55,
      nameTextStyle: { fontSize: 12, color: '#606266' },
      axisLabel: { show: false },
      axisTick: { show: false },
      scale: true
    })

    const colorIndex = Math.max(0, selectedAll.indexOf(channelIndex))
    const dataIndex = (wave.channels || []).indexOf(channelIndex)
    const raw = dataIndex >= 0 ? rows.map((r) => (r.analog?.[dataIndex] ?? null)) : rows.map(() => null)
    const rawColor = RAW_COLORS[colorIndex % RAW_COLORS.length]
    const rmsColor = RMS_COLORS[colorIndex % RMS_COLORS.length]

    const ch = state.voltageChannels.find((c) => Number(c.channelIndex) === Number(channelIndex))
    const phase = String(ch?.phase || '').toUpperCase()
    const name = ch ? `${ch.channelIndex}. ${ch.channelName || ''}` : `CH${channelIndex}`
    yAxis[i].name = name

    const rawSeriesIndex = series.length
    series.push({
      name,
      type: 'line',
      xAxisIndex: i,
      yAxisIndex: i,
      data: raw,
      showSymbol: false,
      lineStyle: { width: 1, color: rawColor, opacity: 0.9 },
      itemStyle: { color: rawColor }
    })

    if (rmsWindowN > 0 && !state.rmsHidden.has(channelIndex)) {
      const rmsData = computeRollingRms(raw, rmsWindowN, rmsHopN)
      series.push({
        name: `${name} RMS`,
        type: 'line',
        xAxisIndex: i,
        yAxisIndex: i,
        showSymbol: false,
        connectNulls: true,
        data: rmsData,
        lineStyle: { width: 2, color: rmsColor, opacity: 0.95 },
        itemStyle: { color: rmsColor, opacity: 0.95 }
      })
    }

    const baseSeriesIndex = rawSeriesIndex

    if (refV > 0) {
      const sagY = (refV * sagPct) / 100
      const recoverY = (refV * recoverPct) / 100
      const interruptY = (refV * interruptPct) / 100
      series.push({
        name: `暂降阈值-${channelIndex}`,
        type: 'line',
        xAxisIndex: i,
        yAxisIndex: i,
        showSymbol: false,
        lineStyle: { type: 'dashed', width: 1, color: '#1677ff' },
        data: buildToleranceCurve(xData.length, sagY)
      })
      series.push({
        name: `恢复阈值-${channelIndex}`,
        type: 'line',
        xAxisIndex: i,
        yAxisIndex: i,
        showSymbol: false,
        lineStyle: { type: 'dashed', width: 1, color: '#52c41a' },
        data: buildToleranceCurve(xData.length, recoverY)
      })
      series.push({
        name: `中断阈值-${channelIndex}`,
        type: 'line',
        xAxisIndex: i,
        yAxisIndex: i,
        showSymbol: false,
        lineStyle: { type: 'dashed', width: 1, color: '#fa8c16' },
        data: buildToleranceCurve(xData.length, interruptY)
      })
    }

    const markAreas = []
    for (const evt of state.process?.computedEvents || []) {
      const evtPhase = String(evt?.phase || '').toUpperCase()
      if (evtPhase && phase && evtPhase !== phase) continue
      const s = Number(evt?.startMs)
      const e = Number(evt?.endMs)
      if (!Number.isFinite(s) || !Number.isFinite(e)) continue
      const si = findNearestIndex(xTimes, s)
      const ei = findNearestIndex(xTimes, e)
      markAreas.push([{ xAxis: xData[si] }, { xAxis: xData[ei] }])
    }
    if (markAreas.length) {
      series[baseSeriesIndex].markArea = {
        silent: true,
        itemStyle: { color: '#fff1f1' },
        data: markAreas
      }
    }

    const lines = []
    for (const m of state.process?.markers || []) {
      const mp = String(m?.phase || '').toUpperCase()
      if (mp && phase && mp !== phase) continue
      const t = Number(m?.timeMs)
      if (!Number.isFinite(t)) continue
      const idx2 = findNearestIndex(xTimes, t)
      lines.push({
        xAxis: xData[idx2],
        name: String(m?.label || m?.kind || ''),
        label: { show: true, formatter: String(m?.label || ''), fontSize: 10 }
      })
    }
    if (lines.length) {
      series[baseSeriesIndex].markLine = {
        symbol: ['none', 'none'],
        lineStyle: { type: 'dashed', color: 'rgba(0,0,0,0.35)' },
        data: lines
      }
    }
  })

  state.rawChart.setOption({
    tooltip: {
      trigger: 'axis',
      confine: true,
      axisPointer: { type: 'cross', snap: true },
      formatter: (params) => {
        const list = Array.isArray(params) ? params : []
        if (!list.length) return ''
        const axisValue = list[0]?.axisValue
        const title = axisValue !== undefined && axisValue !== null ? `${String(axisValue)} ms` : '-'

        const lines = [`<div><b>${escapeHtml(title)}</b></div>`]
        for (const p of list) {
          const name = String(p?.seriesName || '')
          if (!name) continue
          if (name.includes('阈值-')) continue
          const v = Number(p?.data)
          const valText = Number.isFinite(v) ? v.toFixed(3) : '-'
          const color = typeof p?.color === 'string' ? p.color : '#909399'
          lines.push(
            `<div><span style="display:inline-block;width:10px;height:10px;border-radius:2px;background:${escapeHtml(color)};margin-right:6px;"></span>${escapeHtml(name)}: ${escapeHtml(valText)}</div>`
          )
        }
        return lines.join('')
      }
    },
    animation: false,
    grid: grids,
    xAxis,
    yAxis,
    dataZoom: [
      { type: 'inside', xAxisIndex: selected.map((_, i) => i) },
      { type: 'slider', xAxisIndex: selected.map((_, i) => i), bottom: 15 }
    ],
    series
  }, true)
  state.rawChart.resize()
  scheduleMarkerRender()

  setText(
    'rawSub',
    `参考电压：${refV ? refV.toFixed(2) : '-'} V，暂降阈值：${formatPercent(sagPct)}%，中断阈值：${formatPercent(interruptPct)}%，迟滞：${formatPercent(hysteresisPct)}%，最小持续：${formatMs(state.params.minDurationMs)} ms`
  )
  state.rawSubBaseText = byId('rawSub')?.textContent || ''
  refreshMarkerSummary()
  renderRawLegend()
}

function buildReportLines() {
  const rows = (state.process?.computedEvents || []).slice()
  const lines = []
  const refV = getRefVoltage()
  lines.push(`本报告基于事件识别结果与RMS曲线（窗口：${state.rmsWindowCycles}周波，更新：${state.rmsHopCycles}周波）。`)
  lines.push(`参考电压：${refV ? refV.toFixed(2) : '-'} V；暂降阈值：${formatPercent(state.params.sagThresholdPct)}%；中断阈值：${formatPercent(state.params.interruptThresholdPct)}%；迟滞：${formatPercent(state.params.hysteresisPct)}%；最小持续：${formatMs(state.params.minDurationMs)} ms。`)

  if (rows.length === 0) {
    lines.push('当前未识别到暂降/中断事件。')
    return lines
  }

  const normPhase = (p) => String(p || '').toUpperCase()
  const isSag = (e) => String(e?.eventType || '').toLowerCase() === 'sag'
  const isInterrupt = (e) => String(e?.eventType || '').toLowerCase() === 'interruption'
  const phaseSet = new Set(rows.map((x) => normPhase(x?.phase)).filter(Boolean))
  const phases = Array.from(phaseSet)
  const isThree = ['A', 'B', 'C'].every((p) => phaseSet.has(p))

  const total = rows.length
  const sagCount = rows.filter(isSag).length
  const intCount = rows.filter(isInterrupt).length
  lines.push(`识别事件：共 ${total} 条（暂降 ${sagCount} 条，中断 ${intCount} 条）。`)

  const overallMaxMag = rows.reduce((m, x) => {
    const v = Number(x?.sagMagnitudePct)
    return Number.isFinite(v) ? Math.max(m, v) : m
  }, -Infinity)
  const overallMinResidual = rows.reduce((m, x) => {
    const v = Number(x?.residualVoltagePct)
    return Number.isFinite(v) ? Math.min(m, v) : m
  }, Infinity)
  const overallMaxDur = rows.reduce((m, x) => {
    const v = Number(x?.durationMs)
    return Number.isFinite(v) ? Math.max(m, v) : m
  }, -Infinity)

  if (Number.isFinite(overallMaxMag)) {
    const worstRows = rows.filter((x) => Number(x?.sagMagnitudePct) === overallMaxMag)
    const worstPhases = Array.from(new Set(worstRows.map((x) => normPhase(x?.phase)).filter(Boolean)))
    lines.push(`最大暂降幅值：${formatPercent(overallMaxMag)}%（相别：${worstPhases.join('/') || '-'}）。`)
  }
  if (Number.isFinite(overallMinResidual)) lines.push(`最低残余电压：${formatPercent(overallMinResidual)}%。`)
  if (Number.isFinite(overallMaxDur)) lines.push(`最长持续时间：${formatMs(overallMaxDur)} ms。`)

  const perPhase = phases.map((p) => {
    const list = rows.filter((x) => normPhase(x?.phase) === p)
    const pMaxMag = list.reduce((m, x) => {
      const v = Number(x?.sagMagnitudePct)
      return Number.isFinite(v) ? Math.max(m, v) : m
    }, -Infinity)
    const pMinResidual = list.reduce((m, x) => {
      const v = Number(x?.residualVoltagePct)
      return Number.isFinite(v) ? Math.min(m, v) : m
    }, Infinity)
    const pMaxDur = list.reduce((m, x) => {
      const v = Number(x?.durationMs)
      return Number.isFinite(v) ? Math.max(m, v) : m
    }, -Infinity)
    return {
      phase: p,
      total: list.length,
      sag: list.filter(isSag).length,
      inter: list.filter(isInterrupt).length,
      pMaxMag,
      pMinResidual,
      pMaxDur,
      worstEvt: Number.isFinite(pMaxMag)
        ? list
          .filter((x) => Number(x?.sagMagnitudePct) === pMaxMag)
          .slice()
          .sort((a, b) => Number(b?.durationMs ?? 0) - Number(a?.durationMs ?? 0))[0]
        : null
    }
  })

  if (isThree) {
    lines.push('三相对比：')
    for (const p of ['A', 'B', 'C']) {
      const item = perPhase.find((x) => x.phase === p)
      if (!item) continue
      const parts = []
      parts.push(`${p}相 ${item.total}条（暂降${item.sag}/中断${item.inter}）`)
      if (Number.isFinite(item.pMaxMag)) parts.push(`最大暂降${formatPercent(item.pMaxMag)}%`)
      if (Number.isFinite(item.pMinResidual)) parts.push(`最低残余${formatPercent(item.pMinResidual)}%`)
      if (Number.isFinite(item.pMaxDur)) parts.push(`最长${formatMs(item.pMaxDur)}ms`)
      lines.push(`- ${parts.join('，')}`)
    }
  } else if (perPhase.length) {
    const item = perPhase[0]
    lines.push(`单相分析（${item.phase || '-'}相）：`)
    lines.push(`- 事件分布：共 ${item.total} 条（暂降 ${item.sag} 条，中断 ${item.inter} 条）。`)
    if (Number.isFinite(item.pMaxMag)) lines.push(`- 最严重幅值：${formatPercent(item.pMaxMag)}%。`)
    if (Number.isFinite(item.pMinResidual)) lines.push(`- 最低残余电压：${formatPercent(item.pMinResidual)}%。`)
    if (Number.isFinite(item.pMaxDur)) lines.push(`- 最长持续：${formatMs(item.pMaxDur)} ms。`)
    if (item.worstEvt) {
      const t = item.worstEvt.occurTimeUtc ? formatUtcMs(item.worstEvt.occurTimeUtc) : '-'
      lines.push(`- 最严重事件时间：${t}；持续 ${formatMs(item.worstEvt.durationMs)} ms；幅值 ${formatPercent(item.worstEvt.sagMagnitudePct)}%。`)
    }
  }

  lines.push('解读建议：优先查看最严重相的RMS下降与恢复过程，并结合容忍度曲线判断是否越界。')

  // Tolerance curve analysis
  if (rows.length > 0) {
    const toleranceAnalysis = analyzeToleranceCurve(rows)
    if (toleranceAnalysis) {
      lines.push('')
      lines.push('容忍度曲线分析：')
      lines.push(`- ${toleranceAnalysis.summary}`)
      if (toleranceAnalysis.details.length > 0) {
        toleranceAnalysis.details.forEach(detail => {
          lines.push(`  • ${detail}`)
        })
      }
    }
  }

  return lines
}

function getCurveBoundaryY(curve, x) {
  const xx = Number(x)
  if (!Number.isFinite(xx)) return null

  const points = Array.isArray(curve) ? curve : []
  if (points.length === 0) return null

  const candidates = []

  for (let i = 0; i < points.length; i++) {
    const px = Number(points[i]?.[0])
    const py = Number(points[i]?.[1])
    if (Number.isFinite(px) && Number.isFinite(py) && px === xx) candidates.push(py)
  }

  for (let i = 0; i < points.length - 1; i++) {
    const x1 = Number(points[i]?.[0])
    const y1 = Number(points[i]?.[1])
    const x2 = Number(points[i + 1]?.[0])
    const y2 = Number(points[i + 1]?.[1])
    if (!Number.isFinite(x1) || !Number.isFinite(y1) || !Number.isFinite(x2) || !Number.isFinite(y2)) continue

    if (x1 === x2) {
      if (xx === x1) candidates.push(Math.max(y1, y2))
      continue
    }

    const minX = Math.min(x1, x2)
    const maxX = Math.max(x1, x2)
    if (xx < minX || xx > maxX) continue

    const t = (xx - x1) / (x2 - x1)
    candidates.push(y1 + (y2 - y1) * t)
  }

  if (candidates.length) {
    let best = candidates[0]
    for (let i = 1; i < candidates.length; i++) best = Math.max(best, candidates[i])
    return best
  }

  let minPoint = null
  let maxPoint = null
  for (let i = 0; i < points.length; i++) {
    const px = Number(points[i]?.[0])
    const py = Number(points[i]?.[1])
    if (!Number.isFinite(px) || !Number.isFinite(py)) continue
    if (!minPoint || px < minPoint.x) minPoint = { x: px, y: py }
    if (!maxPoint || px > maxPoint.x) maxPoint = { x: px, y: py }
  }
  if (!minPoint || !maxPoint) return null
  if (xx <= minPoint.x) return minPoint.y
  if (xx >= maxPoint.x) return maxPoint.y
  return null
}

function analyzeToleranceCurve(events) {
  if (!Array.isArray(events) || events.length === 0) return null

  const pointsInNormal = events.filter((evt) => {
    const durationSec = Number(evt.durationMs) / 1000
    const voltagePct = Number(evt.residualVoltagePct)
    const boundary = getCurveBoundaryY(toleranceStandard, durationSec)
    if (!Number.isFinite(voltagePct) || !Number.isFinite(boundary)) return false
    return voltagePct >= boundary
  })

  const pointsInLegacy = events.filter((evt) => {
    const durationSec = Number(evt.durationMs) / 1000
    const voltagePct = Number(evt.residualVoltagePct)
    const boundary = getCurveBoundaryY(toleranceStandardLegacy, durationSec)
    if (!Number.isFinite(voltagePct) || !Number.isFinite(boundary)) return false
    return voltagePct >= boundary
  })

  const summary = []
  const details = []

  if (pointsInNormal.length === events.length) {
    summary.push('所有事件均在新版容忍度曲线范围内')
  } else if (pointsInNormal.length > 0) {
    summary.push(`${pointsInNormal.length}/${events.length} 条事件在新版容忍度曲线范围内`)
    const outsideEvents = events.filter(evt => !pointsInNormal.includes(evt))
    if (outsideEvents.length > 0) {
      const worstOutside = outsideEvents.reduce((a, b) =>
        Number(a.residualVoltagePct) < Number(b.residualVoltagePct) ? a : b
      )
      details.push(`越界最严重：${worstOutside.phase || '-'}相 ${(Number(worstOutside.durationMs) / 1000).toFixed(3)}s ${Number(worstOutside.residualVoltagePct).toFixed(2)}%`)
    }
  } else {
    summary.push('所有事件均超出新版容忍度曲线范围')
  }

  if (pointsInLegacy.length !== pointsInNormal.length) {
    if (pointsInLegacy.length > pointsInNormal.length) {
      summary.push(`旧版标准更宽松，多包含 ${pointsInLegacy.length - pointsInNormal.length} 条事件`)
    } else {
      summary.push(`新版标准更宽松，多包含 ${pointsInNormal.length - pointsInLegacy.length} 条事件`)
    }
  }

  // Duration-based analysis
  const shortEvents = events.filter(evt => Number(evt.durationMs) <= 1000)
  const longEvents = events.filter(evt => Number(evt.durationMs) > 1000)

  if (shortEvents.length > 0 && longEvents.length > 0) {
    details.push(`短时事件（≤1s）：${shortEvents.length} 条，长时事件（>1s）：${longEvents.length} 条`)
  } else if (shortEvents.length > 0) {
    details.push(`主要为短时事件（≤1s）：${shortEvents.length} 条`)
  } else if (longEvents.length > 0) {
    details.push(`主要为长时事件（>1s）：${longEvents.length} 条`)
  }

  return {
    summary: summary.join('；'),
    details
  }
}

function renderReport() {
  const lines = buildReportLines()
  setHtml('reportBody', lines.map((x) => `<div class="report-line">${escapeHtml(x)}</div>`).join(''))
}

function initCharts() {
  state.rawChart = echarts.init(byId('rawChart'))
  state.toleranceChart = echarts.init(byId('toleranceChart'))
  state.rawChart.on('click', (params) => {
    const x = Number(params?.event?.offsetX)
    if (!Number.isFinite(x)) return
    if (state.markerLeftXPixel === null) state.markerLeftXPixel = x
    else if (state.markerRightXPixel === null) state.markerRightXPixel = x
    else state.markerRightXPixel = x
    scheduleMarkerRender()
  })
  window.addEventListener('resize', () => {
    state.rawChart && state.rawChart.resize()
    state.toleranceChart && state.toleranceChart.resize()
    scheduleMarkerRender()
  })
  scheduleMarkerRender()
}

async function loadProcess() {
  const prevSelected = Array.from(state.selected)
  const prevHidden = new Set(Array.from(state.hidden))
  const prevRmsHidden = new Set(Array.from(state.rmsHidden))

  let res = null
  try {
    res = await zwavApi.sagGetProcess(state.eventId)
  } catch (e) {
    const msg = e && typeof e === 'object' && 'message' in e ? String(e.message || '') : String(e || '')
    alert(msg ? `获取过程失败：${msg}` : '获取过程失败')
    return false
  }
  if (!res || !res.success || !res.data) {
    alert((res && res.message) || '获取过程失败')
    return false
  }
  state.process = res.data
  state.analysisGuid = res.data.analysisGuid || ''
  state.voltageChannels = res.data.voltageChannels || []
  state.params.referenceVoltage = Number(res.data?.event?.referenceVoltage ?? 0) || 0
  state.params.sagThresholdPct = Number(res.data?.event?.sagThresholdPct ?? 90) || 90
  state.params.interruptThresholdPct = Number(res.data?.event?.interruptThresholdPct ?? 10) || 10
  state.params.hysteresisPct = Number(res.data?.event?.hysteresisPct ?? 2) || 2
  state.params.minDurationMs = Number(res.data?.event?.minDurationMs ?? 10) || 10
  renderHeader()

  if (res.data.suggestedFromSample !== undefined && res.data.suggestedFromSample !== null) state.searchFromSample = Number(res.data.suggestedFromSample)
  if (res.data.suggestedToSample !== undefined && res.data.suggestedToSample !== null) state.searchToSample = Number(res.data.suggestedToSample)

  byId('fromSample').value = String(state.searchFromSample)
  byId('toSample').value = String(state.searchToSample)
  byId('limit').value = String(state.searchLimit)
  byId('downSample').value = String(state.searchDownSample)
  byId('rmsWindowCycles').value = String(state.rmsWindowCycles)
  byId('rmsHopCycles').value = String(state.rmsHopCycles)
  byId('referenceVoltage').value = String(state.params.referenceVoltage || '')
  byId('sagThresholdPct').value = String(state.params.sagThresholdPct)
  byId('interruptThresholdPct').value = String(state.params.interruptThresholdPct)
  byId('hysteresisPct').value = String(state.params.hysteresisPct)
  byId('minDurationMs').value = String(state.params.minDurationMs)

  const present = new Set(state.voltageChannels.map((x) => Number(x.channelIndex)))
  state.selected.clear()
  state.hidden.clear()
  state.rmsHidden.clear()

  prevSelected.forEach((x) => {
    const v = Number(x)
    if (present.has(v)) state.selected.add(v)
  })
  prevHidden.forEach((x) => {
    const v = Number(x)
    if (present.has(v)) state.hidden.add(v)
  })
  prevRmsHidden.forEach((x) => {
    const v = Number(x)
    if (present.has(v)) state.rmsHidden.add(v)
  })

  if (!state.selected.size) {
    const byPhase = (p) => state.voltageChannels.find((c) => String(c.phase || '').toUpperCase() === p)
    const initPick = [byPhase('A'), byPhase('B'), byPhase('C')].filter(Boolean).map((x) => x.channelIndex)
    if (initPick.length) initPick.forEach((x) => state.selected.add(x))
    if (!state.selected.size) state.voltageChannels.slice(0, 3).forEach((x) => state.selected.add(x.channelIndex))
  }

  if (prevRmsHidden.size === 0 && state.rmsHidden.size === 0) {
    Array.from(state.selected).forEach((x) => state.rmsHidden.add(Number(x)))
  }

  renderChannelList()
  renderSagTable()
  renderReport()
  return true
}

async function fetchWaveData() {
  if (!state.analysisGuid) return
  const selected = Array.from(state.selected)
  if (!selected.length) {
    alert('请至少选择一个通道')
    return
  }
  const params = {
    channels: selected.join(','),
    fromSample: state.searchFromSample,
    toSample: state.searchToSample,
    limit: state.searchLimit,
    downSample: state.searchDownSample
  }
  let res = null
  try {
    res = await zwavApi.getWaveData(state.analysisGuid, params)
  } catch (e) {
    const msg = e && typeof e === 'object' && 'message' in e ? String(e.message || '') : String(e || '')
    alert(msg ? `获取波形失败：${msg}` : '获取波形失败')
    return
  }
  if (!res || !res.success || !res.data) {
    alert((res && res.message) || '获取波形失败')
    return
  }
  state.lastWaveData = res.data
  resetMarkers()
  updateRawChart()
}

async function reloadAll() {
  const ok = await loadProcess()
  if (ok) await fetchWaveData()
}

async function previewProcess() {
  // Show loading state
  setHtml('reportBody', '<div class="report-line">正在重新计算暂降事件...</div>')

  try {
    const body = {
      referenceVoltage: state.params.referenceVoltage,
      sagThresholdPct: state.params.sagThresholdPct,
      interruptThresholdPct: state.params.interruptThresholdPct,
      hysteresisPct: state.params.hysteresisPct,
      minDurationMs: state.params.minDurationMs
    }

    const res = await zwavApi.sagPreviewProcess(state.eventId, body)
    if (!res || !res.success || !res.data) {
      alert((res && res.message) || '预览失败')
      return
    }

    if (state.process) {
      state.process.rmsPoints = res.data.rmsPoints || []
      state.process.computedEvents = res.data.computedEvents || []
      state.process.markers = res.data.markers || []
    }

    if (res.data.suggestedFromSample !== undefined && res.data.suggestedFromSample !== null) {
      state.searchFromSample = Number(res.data.suggestedFromSample)
      byId('fromSample').value = String(state.searchFromSample)
    }
    if (res.data.suggestedToSample !== undefined && res.data.suggestedToSample !== null) {
      state.searchToSample = Number(res.data.suggestedToSample)
      byId('toSample').value = String(state.searchToSample)
    }

    // Clear selection and re-render
    state.sagSelectedKeys.clear()
    renderSagTable()
    renderReport()
    updateRawChart()

    // Show success message
    showAlert('success', '暂降事件重新计算完成')
  } catch (e) {
    alert(e.message || '预览失败')
  }
}

function openApiModal() {
  byId('apiBaseInput').value = getApiBaseUrl()
  openDialog('apiModal')
}

function applyWaveSettings() {
  state.params.referenceVoltage = Number(byId('referenceVoltage').value || 0)
  state.params.sagThresholdPct = Number(byId('sagThresholdPct').value || 90)
  state.params.interruptThresholdPct = Number(byId('interruptThresholdPct').value || 10)
  state.params.hysteresisPct = Number(byId('hysteresisPct').value || 2)
  state.params.minDurationMs = Number(byId('minDurationMs').value || 10)
  state.searchFromSample = Number(byId('fromSample').value || 0)
  state.searchToSample = Number(byId('toSample').value || 0)
  state.searchLimit = Number(byId('limit').value || 20000)
  state.searchDownSample = Number(byId('downSample').value || 1)
  state.rmsWindowCycles = Number(byId('rmsWindowCycles').value || 1)
  state.rmsHopCycles = Number(byId('rmsHopCycles').value || 0.5)
  closeDialog('waveModal')
  updateRawChart()
}

function getRawViewportHeight() {
  const wrap = byId('rawWrap')
  if (!wrap) return 420
  const header = wrap.querySelector('.sag-card-header')
  const legend = byId('rawLegend')
  const footer = byId('markerFooter')
  const extra = 24
  const total = wrap.clientHeight
  const h = (header?.offsetHeight || 0) + (legend?.offsetHeight || 0) + (footer?.offsetHeight || 0) + extra
  const avail = total - h
  if (Number.isFinite(avail) && avail > 120) return avail
  return 420
}

function toggleFullScreen() {
  state.isFullScreen = !state.isFullScreen
  const wrap = byId('rawWrap')
  if (state.isFullScreen) wrap.classList.add('is-window-fullscreen')
  else wrap.classList.remove('is-window-fullscreen')
  byId('reportCard').style.display = state.isFullScreen ? 'none' : ''
  const sidebar = document.querySelector('.channel-sidebar')
  if (sidebar) sidebar.style.display = state.isFullScreen ? 'none' : ''
  const bottom = document.querySelector('.sag-bottom-row')
  if (bottom) bottom.style.display = state.isFullScreen ? 'none' : ''
  document.body.style.overflow = state.isFullScreen ? 'hidden' : ''

  const scroll = wrap?.querySelector('.chart-scroll-container')
  if (scroll) {
    if (state.isFullScreen) {
      scroll.style.height = `${Math.max(200, Math.floor(getRawViewportHeight()))}px`
      scroll.style.overflowY = 'hidden'
    } else {
      scroll.style.height = ''
      scroll.style.overflowY = ''
    }
  }

  setTimeout(() => {
    state.rawChart && state.rawChart.resize()
    updateRawChart()
  }, 0)
}

function onKeyDown(e) {
  if (e.key !== 'Escape') return
  if (!state.isFullScreen) return
  toggleFullScreen()
}

async function exportRms() {
  try {
    const res = await zwavApi.sagGetProcess(state.eventId)
    if (!res || !res.success || !res.data) {
      alert((res && res.message) || '导出失败')
      return
    }
    const rows = res.data.rmsPoints || []
    if (!rows.length) {
      alert('没有可导出的RMS数据')
      return
    }

    const groups = new Map()
    rows.forEach((r) => {
      const phase = String(r?.phase ?? '')
      const channelIndex = String(r?.channelIndex ?? '')
      const key = `${phase}#${channelIndex}`
      const list = groups.get(key) || []
      list.push(r)
      groups.set(key, list)
    })

    const filtered = []
    Array.from(groups.values()).forEach((list) => {
      list.sort((a, b) => Number(a?.sampleNo ?? 0) - Number(b?.sampleNo ?? 0))
      for (let i = 1; i < list.length; i++) filtered.push(list[i])
    })
    filtered.sort((a, b) => Number(a?.timeMs ?? 0) - Number(b?.timeMs ?? 0))

    if (!filtered.length) {
      alert('没有可导出的RMS数据')
      return
    }

    const header = ['序号', '时间(ms)', '采样点号', '相别', '通道号', '均方根值', '均方根(%)', '参考电压']
    const lines = [header.join(',')]
    filtered.forEach((r) => {
      lines.push([
        r?.seqNo ?? '',
        r?.timeMs ?? '',
        r?.sampleNo ?? '',
        r?.phase ?? '',
        r?.channelIndex ?? '',
        r?.rms ?? '',
        r?.rmsPct ?? '',
        r?.referenceVoltage ?? ''
      ].join(','))
    })

    const text = '\ufeff' + lines.join('\n')
    const blob = new Blob([text], { type: 'text/csv;charset=utf-8;' })
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    const name = String(res.data?.event?.originalName || `event-${state.eventId}`)
    a.href = url
    a.download = `${name}-RMS.csv`
    a.click()
    URL.revokeObjectURL(url)
  } catch (e) {
    alert(e.message || '导出失败')
  }
}

function fallbackCopyText(text) {
  const ta = document.createElement('textarea')
  ta.value = text
  ta.setAttribute('readonly', 'readonly')
  ta.style.position = 'fixed'
  ta.style.left = '-9999px'
  ta.style.top = '0'
  document.body.appendChild(ta)
  ta.select()
  const ok = document.execCommand('copy')
  document.body.removeChild(ta)
  return ok
}

async function copyReport() {
  const body = byId('reportBody')
  if (!body) return
  const lines = Array.from(body.querySelectorAll('.report-line'))
    .map((el) => String(el.textContent || '').trim())
    .filter(Boolean)
  const text = lines.join('\n')
  if (!text) {
    alert('分析报告为空')
    return
  }

  try {
    if (navigator.clipboard?.writeText) {
      await navigator.clipboard.writeText(text)
      alert('已复制')
      return
    }
  } catch { }

  const ok = fallbackCopyText(text)
  alert(ok ? '已复制' : '复制失败')
}

function openExportConfirm() {
  openDialog('exportConfirmModal')
}

async function confirmExportRms() {
  closeDialog('exportConfirmModal')
  await exportRms()
}

function showEventDetail() {
  const evt = state.process?.event || null
  const phases = state.process?.phases || []
  if (!evt) {
    alert('无事件详情')
    return
  }

  const items = [
    ['事件ID', evt.id ?? '-'],
    ['录波文件ID', evt.fileId ?? '-'],
    ['录波文件名', evt.originalName || '-'],
    ['状态', evt.status ?? '-'],
    ['是否暂降', evt.hasSag ? '有' : '无'],
    ['事件类型', evt.eventType || '-'],
    ['事件数', evt.eventCount ?? '-'],
    ['耗时(ms)', evt.costMs ?? '-'],
    ['错误信息', evt.errorMessage || '-'],
    ['最严重相', evt.worstPhase || '-'],
    ['触发相', evt.triggerPhase || '-'],
    ['终止相', evt.endPhase || '-'],
    ['开始时间', evt.startTimeUtc ? formatUtcMs(evt.startTimeUtc) : '-'],
    ['结束时间', evt.endTimeUtc ? formatUtcMs(evt.endTimeUtc) : '-'],
    ['发生时间', evt.occurTimeUtc ? formatUtcMs(evt.occurTimeUtc) : '-'],
    ['持续时间(ms)', formatMs(evt.durationMs)],
    ['参考电压', evt.referenceVoltage ?? '-'],
    ['残余电压(V)', evt.residualVoltage ?? '-'],
    ['残余电压(%)', formatPercent(evt.residualVoltagePct)],
    ['暂降幅值(%)', formatPercent(evt.sagPercent)]
  ]

  const baseInfo = `
    <div style="font-weight:700; color: var(--el-text-color-primary); margin-bottom:10px;">基本信息</div>
    <div style="display:grid; grid-template-columns: 180px 1fr 180px 1fr; gap: 8px 12px;">
      ${items
      .map(([k, v]) => `<div style="color: var(--el-text-color-secondary);">${escapeHtml(k)}：</div><div style="min-width:0; word-break:break-all;">${escapeHtml(String(v ?? '-'))}</div>`)
      .join('')}
    </div>
  `

  const phaseTable = `
    <div style="font-weight:700; color: var(--el-text-color-primary); margin: 16px 0 10px;">各相明细</div>
    <table class="el-table">
      <thead>
        <tr>
          <th style="width:70px">相别</th>
          <th style="width:200px">开始时间</th>
          <th style="width:200px">结束时间</th>
          <th style="width:120px">持续(ms)</th>
          <th style="width:120px">残余(V)</th>
          <th style="width:120px">残余(%)</th>
          <th style="width:120px">暂降(%)</th>
          <th style="width:80px">触发相</th>
          <th style="width:80px">终止相</th>
          <th style="width:90px">最严重相</th>
        </tr>
      </thead>
      <tbody>
        ${phases
      .map((p) => {
        return `<tr>
              <td class="cell-center">${escapeHtml(p.phase || '-')}</td>
              <td>${escapeHtml(p.startTimeUtc ? formatUtcMs(p.startTimeUtc) : '-')}</td>
              <td>${escapeHtml(p.endTimeUtc ? formatUtcMs(p.endTimeUtc) : '-')}</td>
              <td>${escapeHtml(formatMs(p.durationMs))}</td>
              <td>${escapeHtml(String(p.residualVoltage ?? '-'))}</td>
              <td>${escapeHtml(formatPercent(p.residualVoltagePct))}</td>
              <td>${escapeHtml(formatPercent(p.sagPercent))}</td>
              <td class="cell-center">${p.isTriggerPhase ? '是' : '否'}</td>
              <td class="cell-center">${p.isEndPhase ? '是' : '否'}</td>
              <td class="cell-center">${p.isWorstPhase ? '是' : '否'}</td>
            </tr>`
      })
      .join('')}
      </tbody>
    </table>
  `

  setHtml('eventDetailBody', baseInfo + phaseTable)
  openDialog('eventModal')
}

function bindEvents() {
  on('btnOpenApiConfig', 'click', openApiModal)
  on('btnSaveApiBase', 'click', () => {
    setApiBaseUrl(byId('apiBaseInput').value || '')
    closeDialog('apiModal')
  })

  on('btnFetch', 'click', reloadAll)
  on('btnPreview', 'click', previewProcess)
  on('btnWaveSetting', 'click', () => openDialog('waveModal'))
  on('btnApplyWave', 'click', applyWaveSettings)
  on('btnFullScreen', 'click', toggleFullScreen)
  on('btnExportRms', 'click', openExportConfirm)
  on('btnEventDetail', 'click', showEventDetail)
  on('btnResetMarkers', 'click', resetMarkers)
  on('btnCopyReport', 'click', copyReport)
  on('btnConfirmExportRms', 'click', confirmExportRms)

  on('channelSearch', 'input', renderChannelList)
  on('analogCheckAll', 'change', () => {
    const kw = byId('channelSearch').value || ''
    const list = filterChannels(state.voltageChannels, kw)
    if (byId('analogCheckAll').checked) list.forEach((c) => state.selected.add(c.channelIndex))
    else list.forEach((c) => state.selected.delete(c.channelIndex))
    renderChannelList()
  })

  bindDialog('apiModal')
  bindDialog('waveModal')
  bindDialog('eventModal')
  bindDialog('exportConfirmModal')

  window.addEventListener('keydown', onKeyDown)

  const apiBase = qs('apiBase')
  if (apiBase) setApiBaseUrl(apiBase)
}

async function init() {
  state.eventId = Number(qs('id') || 0)
  if (!state.eventId) {
    alert('缺少事件ID')
    return
  }
  bindEvents()
  initCharts()
  try {
    const ok = await loadProcess()
    if (ok) await fetchWaveData()
  } catch (e) {
    const msg = e && typeof e === 'object' && 'message' in e ? String(e.message || '') : String(e || '')
    alert(msg ? `初始化失败：${msg}` : '初始化失败')
  }
}

init()
