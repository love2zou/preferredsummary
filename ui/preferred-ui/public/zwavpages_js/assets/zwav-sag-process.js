import { bindDialog, byId, closeDialog, downloadBlob, escapeHtml, getApiBaseUrl, openDialog, qs, setApiBaseUrl, setHtml, setText } from './common.js'
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
  [0, 50],
  [0.2, 50],
  [0.2, 70],
  [0.5, 70],
  [0.5, 80],
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
  totalSamples: 0,
  voltageChannels: [],
  selected: new Set(),
  hidden: new Set(),
  rmsHidden: new Set(),
  rawZoomApplied: false,
  rawZoomState: null,
  rawUserZoomed: false,
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
  searchFromSample: 1,
  searchToSample: 20000,
  searchLimit: 50000,
  searchDownSample: 1,
  params: {
    referenceVoltage: 0,
    sagThresholdPct: 90,
    recoverThresholdPct: null,
    interruptThresholdPct: 10,
    hysteresisPct: 2,
    minDurationMs: 10
  },
  recalcRecoverAuto: true
}

function on(id, event, handler) {
  const el = byId(id)
  if (!el) return
  el.addEventListener(event, handler)
}

function clamp(v, min, max) {
  return Math.max(min, Math.min(max, v))
}

function setInputValue(id, v) {
  const el = byId(id)
  if (!el) return
  el.value = String(v ?? '')
}

function showAlert(type, text) {
  const msg = String(text || '')
  if (!msg) return
  alert(msg)
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

function formatAbsTimeFromOffsetMs(offsetMs) {
  const n = Number(offsetMs)
  if (!Number.isFinite(n)) return '-'
  const base = state.process?.waveStartTimeUtc
  const bd = base ? new Date(base) : null
  const bms = bd && !Number.isNaN(bd.getTime()) ? bd.getTime() : NaN
  if (!Number.isFinite(bms)) return `${n.toFixed(3)} ms`
  const d = new Date(bms + n)
  if (Number.isNaN(d.getTime())) return `${n.toFixed(3)} ms`
  const hh = String(d.getHours()).padStart(2, '0')
  const mi = String(d.getMinutes()).padStart(2, '0')
  const ss = String(d.getSeconds()).padStart(2, '0')
  const ms = String(d.getMilliseconds()).padStart(3, '0')
  return `${hh}:${mi}:${ss}.${ms}`
}

function getSagRowKey(evt) {
  const phase = String(evt?.phase || '')
  const startMs = Number(evt?.startMs ?? 0)
  const endMs = Number(evt?.endMs ?? 0)
  const kind = String(evt?.eventType || '')
  return `${phase}-${kind}-${startMs}-${endMs}`
}

function pickFocusEvent() {
  const events = Array.isArray(state.process?.computedEvents) ? state.process.computedEvents.slice() : []
  if (!events.length) return null

  const picked = events.filter((evt) => state.sagSelectedKeys.has(getSagRowKey(evt)))
  const source = picked.length ? picked : events

  return source
    .filter((evt) => Number.isFinite(Number(evt?.startMs)) && Number.isFinite(Number(evt?.endMs)))
    .sort((a, b) => {
      const magA = Number(a?.sagMagnitudePct ?? -Infinity)
      const magB = Number(b?.sagMagnitudePct ?? -Infinity)
      if (magB !== magA) return magB - magA

      const durA = Number(a?.durationMs ?? -Infinity)
      const durB = Number(b?.durationMs ?? -Infinity)
      if (durB !== durA) return durB - durA

      const resA = Number(a?.residualVoltagePct ?? Infinity)
      const resB = Number(b?.residualVoltagePct ?? Infinity)
      return resA - resB
    })[0] || null
}

function getDefaultZoomRangeMs(xTimes) {
  const xs = (xTimes || []).filter((x) => Number.isFinite(Number(x))).map((x) => Number(x))
  if (xs.length === 0) return null

  const minX = xs[0]
  const maxX = xs[xs.length - 1]
  if (!(maxX > minX)) return null

  const totalSpan = maxX - minX
  const focusEvt = pickFocusEvent()

  if (!focusEvt) {
    const defaultSpan = Math.min(totalSpan, Math.max(500, totalSpan * 0.2))
    return { start: minX, end: minX + defaultSpan }
  }

  const s = Number(focusEvt.startMs)
  const e = Number(focusEvt.endMs)
  if (!Number.isFinite(s) || !Number.isFinite(e) || e < s) return null

  const evtSpan = Math.max(1, e - s)
  const center = (s + e) / 2

  let windowSpan = Math.max(
    evtSpan * 12,
    400,
    totalSpan * 0.03
  )

  windowSpan = Math.min(windowSpan, Math.max(1500, totalSpan * 0.25), totalSpan)

  let start = center - windowSpan / 2
  let end = center + windowSpan / 2

  if (start < minX) {
    end += (minX - start)
    start = minX
  }
  if (end > maxX) {
    start -= (end - maxX)
    end = maxX
  }

  start = Math.max(minX, start)
  end = Math.min(maxX, end)

  if (!(end > start)) return { start: minX, end: maxX }
  return { start, end }
}

function getCurrentZoomRangeMs() {
  if (!state.rawChart) return state.rawZoomState || null

  try {
    const opt = state.rawChart.getOption()
    const dz = opt && opt.dataZoom
    if (!Array.isArray(dz) || dz.length === 0) return state.rawZoomState || null

    const first = dz.find((x) => Number.isFinite(Number(x?.startValue)) && Number.isFinite(Number(x?.endValue)))
    if (!first) return state.rawZoomState || null

    const start = Number(first.startValue)
    const end = Number(first.endValue)
    if (Number.isFinite(start) && Number.isFinite(end) && end > start) {
      return { start, end }
    }

    return state.rawZoomState || null
  } catch {
    return state.rawZoomState || null
  }
}

function saveRawZoomState() {
  const zoom = getCurrentZoomRangeMs()
  if (zoom && Number.isFinite(zoom.start) && Number.isFinite(zoom.end) && zoom.end > zoom.start) {
    state.rawZoomState = { ...zoom }
  }
}

function restoreRawZoom() {
  const data = state.lastWaveData
  if (!state.rawChart || !data || !Array.isArray(data.rows) || data.rows.length === 0) return

  const times = data.rows.map((r) => Number(r?.timeMs)).filter((x) => Number.isFinite(x))
  if (times.length === 0) return

  const minX = times[0]
  const maxX = times[times.length - 1]
  if (!(maxX > minX)) return

  const opt = state.rawChart.getOption()
  const xAxisLen = Array.isArray(opt?.xAxis) ? opt.xAxis.length : 1
  const xAxisIndex = Array.from({ length: Math.max(1, xAxisLen) }, (_v, i) => i)

  state.rawZoomApplied = true
  state.rawUserZoomed = false
  state.rawZoomState = { start: minX, end: maxX }

  state.rawChart.setOption({
    dataZoom: [
      {
        id: 'rawInsideZoom',
        type: 'inside',
        xAxisIndex,
        filterMode: 'none',
        startValue: minX,
        endValue: maxX
      },
      {
        id: 'rawSliderZoom',
        type: 'slider',
        xAxisIndex,
        filterMode: 'none',
        height: 20,
        bottom: 22,
        startValue: minX,
        endValue: maxX
      }
    ]
  }, false)

  scheduleMarkerRender()
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

function getGridRect0() {
  if (!state.rawChart) return null
  const model = state.rawChart.getModel?.()
  const grid0 = model && model.getComponent && model.getComponent('grid', 0)
  const rect = grid0 && grid0.coordinateSystem && grid0.coordinateSystem.getRect && grid0.coordinateSystem.getRect()
  return rect || null
}

function getGridRects() {
  if (!state.rawChart) return []
  const model = state.rawChart.getModel?.()
  if (!model) return []
  const rects = []
  if (model.getComponent) {
    for (let i = 0; i < 64; i++) {
      const g = model.getComponent('grid', i)
      if (!g) break
      const r = g.coordinateSystem && g.coordinateSystem.getRect && g.coordinateSystem.getRect()
      if (r) rects.push(r)
    }
  }
  if (rects.length) return rects
  const r0 = getGridRect0()
  return r0 ? [r0] : []
}

function getAllGridYRange() {
  const rects = getGridRects()
  if (!rects.length) {
    if (!state.rawChart) return null
    return { minY: 0, maxY: state.rawChart.getHeight() }
  }
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
  if (!rects.length) {
    if (!state.rawChart) return null
    return { minX: 0, maxX: state.rawChart.getWidth() }
  }
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
  const lx = Number(state.markerLeftXPixel)
  const rx = Number(state.markerRightXPixel)
  if (state.markerLeftXPixel === null || !Number.isFinite(lx) || lx < xr.minX || lx > xr.maxX) {
    state.markerLeftXPixel = xr.minX + width * 0.3
  }
  if (state.markerRightXPixel === null || !Number.isFinite(rx) || rx < xr.minX || rx > xr.maxX) {
    state.markerRightXPixel = xr.minX + width * 0.7
  }
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
    const footer = byId('markerFooter')
    const textEl = byId('markerFooterText')
    if (footer) footer.style.display = 'none'
    if (textEl) textEl.textContent = ''
    return
  }

  const leftWave = getWaveAtXPixel(lx)
  const rightWave = getWaveAtXPixel(rx)
  if (!leftWave || !rightWave) {
    const rawSub = byId('rawSub')
    if (rawSub && state.rawSubBaseText) rawSub.textContent = state.rawSubBaseText
    const footer = byId('markerFooter')
    const textEl = byId('markerFooterText')
    if (footer) footer.style.display = 'none'
    if (textEl) textEl.textContent = ''
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
  if (rawSub && state.rawSubBaseText) rawSub.textContent = state.rawSubBaseText

  const footer = byId('markerFooter')
  const textEl = byId('markerFooterText')
  if (footer) footer.style.display = ''
  if (textEl) textEl.textContent = summary
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
  const chartH = state.rawChart.getHeight ? state.rawChart.getHeight() : yr.maxY
  const yPad = 18
  const y1 = clamp(yr.minY - yPad, 0, chartH)
  const y2 = clamp(yr.maxY + yPad, 0, chartH)

  state.rawChart.setOption(
    {
      graphic: [
        {
          id: 'markerLeftLine',
          type: 'line',
          z: 100,
          position: [0, 0],
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
          z: 100,
          position: [0, 0],
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
      const rawName = String(c.channelName || '')
      const cleanName = rawName.replace(/[\(\（\[].*[\)\）\]]\s*$/, '').trim()
      const title = `${c.channelIndex}. ${cleanName}`
      return `<div class="channel-item" data-idx="${escapeHtml(c.channelIndex)}" ${hidden}>
        <input class="analog-check" type="checkbox" id="${id}" data-idx="${escapeHtml(c.channelIndex)}" ${checked}/>
        <label for="${id}">${escapeHtml(title)}</label>
      </div>`
    })
    .join('')
  setHtml('analogList', html)

  const listEl = byId('analogList')
  listEl.querySelectorAll('.analog-check').forEach((el) => {
    el.addEventListener('change', () => {
      const idx = Number(el.getAttribute('data-idx'))
      if (el.checked) state.selected.add(idx)
      else state.selected.delete(idx)
      setText('analogCount', String(state.selected.size))
      setTimeout(() => renderChannelList(), 0)
    })
  })

  listEl.querySelectorAll('.channel-item').forEach((el) => {
    el.addEventListener('click', (e) => {
      const t = e.target
      if (t && (t.tagName === 'INPUT' || t.closest?.('input'))) return
      if (t && (t.tagName === 'LABEL' || t.closest?.('label'))) return
      const idx = Number(el.getAttribute('data-idx'))
      const input = el.querySelector('input.analog-check')
      if (!Number.isFinite(idx) || !input) return
      input.checked = !input.checked
      if (input.checked) state.selected.add(idx)
      else state.selected.delete(idx)
      setText('analogCount', String(state.selected.size))
      setTimeout(() => renderChannelList(), 0)
    })
  })
}

function buildHorizontalLineData(times, y) {
  return (times || []).map((t) => [Number(t), y])
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
  const phaseRank = (p) => {
    const x = String(p || '').trim().toUpperCase()
    if (x === 'A') return 0
    if (x === 'B') return 1
    if (x === 'C') return 2
    if (x === 'AB') return 3
    if (x === 'BC') return 4
    if (x === 'CA') return 5
    return 9
  }

  const rows = (state.process?.computedEvents || []).slice().sort((a, b) => {
    const pa = phaseRank(a?.phase)
    const pb = phaseRank(b?.phase)
    if (pa !== pb) return pa - pb

    const ca = String(a?.channelName || '').trim()
    const cb = String(b?.channelName || '').trim()
    const cc = ca.localeCompare(cb, 'zh-Hans-CN-u-co-pinyin')
    if (cc !== 0) return cc

    return Number(a?.startMs) - Number(b?.startMs)
  })
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
      const chText = evt.channelName ? String(evt.channelName) : '-'
      return `
        <tr>
          <td class="cell-center"><input type="checkbox" class="sag-check" data-key="${escapeHtml(key)}" ${checked}></td>
          <td class="cell-center">${idx + 1}</td>
          <td title="${escapeHtml(chText)}">${escapeHtml(chText)}</td>
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

  const pointSeries = points.map((x) => {
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
      appendToBody: true,
      confine: false,
      extraCssText: 'z-index:99999;',
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
    legend: { show: false },
    graphic: [
      {
        id: 'tolerance-split-line',
        type: 'line',
        silent: true,
        z: 30,
        shape: { x1: 0, y1: 0, x2: 0, y2: 0 },
        style: { stroke: '#909399', lineWidth: 1, lineDash: [4, 4] }
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
      axisTick: { show: true, length: 4 },
      axisLine: { show: true },
      minorTick: { show: false },
      splitLine: { show: true, lineStyle: { type: 'solid', opacity: 0.15 } }
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
      splitLine: { show: true, lineStyle: { type: 'solid', opacity: 0.35 } }
    },
    series: [
      ...toleranceSeries,
      ...pointSeries
    ]
  }, true)

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

function getVisibleSelectedChannels(wave) {
  return Array.from(state.selected)
    .filter((x) => (wave?.channels || []).includes(x))
}

function buildRmsSeriesMap() {
  const map = new Map()
  const list = Array.isArray(state.process?.rmsPoints) ? state.process.rmsPoints : []
  for (const p of list) {
    const idx = Number(p?.channelIndex)
    const t = Number(p?.timeMs)
    const rms = Number(p?.rms)
    if (!Number.isFinite(idx) || !Number.isFinite(t) || !Number.isFinite(rms)) continue
    const arr = map.get(idx) || []
    arr.push([t, rms])
    map.set(idx, arr)
  }
  for (const arr of map.values()) {
    arr.sort((a, b) => a[0] - b[0])
  }
  return map
}

function renderRawLegend() {
  const wave = state.lastWaveData
  const selected = getVisibleSelectedChannels(wave)
  const items = []
  selected.forEach((channelIndex, i) => {
    const ch = state.voltageChannels.find((c) => Number(c.channelIndex) === Number(channelIndex))
    const label = ch ? `${ch.channelIndex}. ${ch.channelName || ''}` : `CH${channelIndex}`
    const hiddenCls = state.hidden.has(channelIndex) ? 'style="opacity:0.45"' : ''
    items.push(`<span class="raw-legend-item" data-kind="raw" data-idx="${escapeHtml(channelIndex)}" ${hiddenCls}><span class="legend-line" style="background:${RAW_COLORS[i % RAW_COLORS.length]}"></span><span class="legend-text">${escapeHtml(label)}</span></span>`)
  })
  selected.forEach((channelIndex, i) => {
    const ch = state.voltageChannels.find((c) => Number(c.channelIndex) === Number(channelIndex))
    const base = ch ? `${ch.channelIndex}. ${ch.channelName || ''}` : `CH${channelIndex}`
    const label = `${base} RMS`
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
      saveRawZoomState()
      const idx = Number(el.getAttribute('data-idx'))
      if (state.hidden.has(idx)) state.hidden.delete(idx)
      else state.hidden.add(idx)
      renderRawLegend()
      updateRawChart()
    })
  })

  byId('rawLegend').querySelectorAll('.raw-legend-item[data-kind="rms"]').forEach((el) => {
    el.addEventListener('click', () => {
      saveRawZoomState()
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

  const selectedAll = getVisibleSelectedChannels(wave)
  const selected = selectedAll.filter((x) => !state.hidden.has(x))
  const chartEl = byId('rawChart')

  if (selected.length === 0) {
    state.rawChart.setOption({ series: [], xAxis: [], yAxis: [], grid: [] }, true)
    renderRawLegend()
    initMarkersIfNeeded()
    refreshMarkerSummary()
    renderMarkerGraphics()
    return
  }

  const rows = wave.rows
  const xTimes = rows.map((r) => Number(r.timeMs))
  const gridGap = 20
  const bottomPad = 60
  const baseHeight = selected.length * (140 + gridGap) + bottomPad
  const desiredHeight = state.isFullScreen
    ? clamp(baseHeight, 420, 20000)
    : clamp(baseHeight, 420, 2400)

  if (chartEl) chartEl.style.height = `${desiredHeight}px`
  const gridHeight = Math.max(140, Math.floor((desiredHeight - gridGap * (selected.length - 1) - bottomPad) / selected.length))

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
  const rmsMap = buildRmsSeriesMap()

  selected.forEach((channelIndex, i) => {
    const top = topBase + i * (gridHeight + gridGap)
    grids.push({ left: 100, right: 20, top, height: gridHeight - 18, containLabel: true })

    xAxis.push({
      type: 'value',
      gridIndex: i,
      position: 'top',
      axisLabel: {
        show: i === 0,
        fontSize: 10,
        margin: i === 0 ? 23 : 8,
        formatter: (v) => formatAbsTimeFromOffsetMs(v)
      },
      axisTick: { show: i === 0 },
      axisLine: { show: i === 0 },
      scale: true
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
    const raw = dataIndex >= 0
      ? rows.map((r) => {
        const t = Number(r.timeMs)
        const v = r.analog?.[dataIndex]
        const vv = Number(v)
        return [t, Number.isFinite(vv) ? vv : null]
      })
      : rows.map((r) => [Number(r.timeMs), null])

    const rawColor = RAW_COLORS[colorIndex % RAW_COLORS.length]
    const rmsColor = RMS_COLORS[colorIndex % RMS_COLORS.length]

    const ch = state.voltageChannels.find((c) => Number(c.channelIndex) === Number(channelIndex))
    const phase = String(ch?.phase || '').toUpperCase()
    const name = ch ? `${ch.channelIndex}. ${ch.channelName || ''}` : `CH${channelIndex}`
    yAxis[i].name = name

    const rawSeriesIndex = series.length
    series.push({
      id: `raw-${channelIndex}`,
      name,
      type: 'line',
      xAxisIndex: i,
      yAxisIndex: i,
      data: raw,
      showSymbol: false,
      connectNulls: false,
      lineStyle: { width: 1, color: rawColor, opacity: 0.9 },
      itemStyle: { color: rawColor }
    })

    if (!state.rmsHidden.has(channelIndex)) {
      const rmsData = rmsMap.get(Number(channelIndex)) || []
      if (rmsData.length > 0) {
        series.push({
          id: `rms-${channelIndex}`,
          name: `${name} RMS`,
          type: 'line',
          xAxisIndex: i,
          yAxisIndex: i,
          showSymbol: false,
          connectNulls: false,
          data: rmsData,
          lineStyle: { width: 2, color: rmsColor, opacity: 0.95 },
          itemStyle: { color: rmsColor, opacity: 0.95 }
        })
      }
    }

    const baseSeriesIndex = rawSeriesIndex

    if (refV > 0) {
      const sagY = (refV * sagPct) / 100
      const recoverY = (refV * recoverPct) / 100
      const interruptY = (refV * interruptPct) / 100
      series.push({
        id: `sag-threshold-${channelIndex}`,
        name: `暂降阈值-${channelIndex}`,
        type: 'line',
        xAxisIndex: i,
        yAxisIndex: i,
        showSymbol: false,
        lineStyle: { type: 'dashed', width: 1, color: '#1677ff' },
        data: buildHorizontalLineData(xTimes, sagY)
      })
      series.push({
        id: `recover-threshold-${channelIndex}`,
        name: `恢复阈值-${channelIndex}`,
        type: 'line',
        xAxisIndex: i,
        yAxisIndex: i,
        showSymbol: false,
        lineStyle: { type: 'dashed', width: 1, color: '#52c41a' },
        data: buildHorizontalLineData(xTimes, recoverY)
      })
      series.push({
        id: `interrupt-threshold-${channelIndex}`,
        name: `中断阈值-${channelIndex}`,
        type: 'line',
        xAxisIndex: i,
        yAxisIndex: i,
        showSymbol: false,
        lineStyle: { type: 'dashed', width: 1, color: '#fa8c16' },
        data: buildHorizontalLineData(xTimes, interruptY)
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
      markAreas.push([{ xAxis: xTimes[si] }, { xAxis: xTimes[ei] }])
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
        xAxis: xTimes[idx2],
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

  const currentZoom = getCurrentZoomRangeMs()
  const defaultZoom = getDefaultZoomRangeMs(xTimes)
  const zoom = currentZoom || state.rawZoomState || (!state.rawZoomApplied ? defaultZoom : null)

  if (!state.rawZoomApplied && zoom) {
    state.rawZoomApplied = true
    state.rawZoomState = { ...zoom }
  }

  const xAxisIndices = selected.map((_x, i) => i)

  state.rawChart.setOption({
    animation: false,
    grid: grids,
    xAxis,
    yAxis,
    tooltip: {
      trigger: 'axis',
      confine: true,
      axisPointer: { type: 'cross', snap: false },
      formatter: (params) => {
        const list = Array.isArray(params) ? params : []
        if (!list.length) return ''
        const axisValue = Number(list[0]?.axisValue)
        const title = Number.isFinite(axisValue) ? formatAbsTimeFromOffsetMs(axisValue) : '-'

        const lines = [`<div><b>${escapeHtml(title)}</b></div>`]
        for (const p of list) {
          const name = String(p?.seriesName || '')
          if (!name) continue
          if (name.includes('阈值-')) continue

          const rawVal = Array.isArray(p?.data) ? p.data[1] : p?.data
          const v = Number(rawVal)
          const valText = Number.isFinite(v) ? v.toFixed(3) : '-'
          const color = typeof p?.color === 'string' ? p.color : '#909399'
          lines.push(
            `<div><span style="display:inline-block;width:10px;height:10px;border-radius:2px;background:${escapeHtml(color)};margin-right:6px;"></span>${escapeHtml(name)}: ${escapeHtml(valText)}</div>`
          )
        }
        return lines.join('')
      }
    },
    dataZoom: [
      {
        id: 'rawInsideZoom',
        type: 'inside',
        xAxisIndex: xAxisIndices,
        filterMode: 'none',
        startValue: zoom ? zoom.start : undefined,
        endValue: zoom ? zoom.end : undefined
      },
      {
        id: 'rawSliderZoom',
        type: 'slider',
        xAxisIndex: xAxisIndices,
        filterMode: 'none',
        height: 20,
        bottom: 22,
        startValue: zoom ? zoom.start : undefined,
        endValue: zoom ? zoom.end : undefined
      }
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
  lines.push('本报告基于事件识别结果与RMS曲线（窗口：1周波，更新：半周波）。')
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
  const highlight = (line) => {
    const esc = escapeHtml(String(line ?? ''))
    const idx = esc.indexOf('：')
    const withKey = idx > 0 ? `<span class="report-key">${esc.slice(0, idx + 1)}</span>${esc.slice(idx + 1)}` : esc
    return withKey.replace(/(\d+(?:\.\d+)?)(\s*(?:ms|s|V|%|条|周波))?/g, (_m, n, unit) => {
      const txt = `${n}${unit || ''}`
      return `<span class="report-hl">${txt}</span>`
    })
  }
  setHtml('reportBody', lines.map((x) => `<div class="report-line">${highlight(x)}</div>`).join(''))
}

function initCharts() {
  state.rawChart = echarts.init(byId('rawChart'))
  state.toleranceChart = echarts.init(byId('toleranceChart'))

  const zr = state.rawChart.getZr && state.rawChart.getZr()
  if (zr) {
    zr.on('click', (e) => {
      const x = Number(e?.offsetX ?? e?.event?.offsetX)
      if (!Number.isFinite(x)) return
      state.markerLeftXPixel = x
      scheduleMarkerRender()
    })
    zr.on('contextmenu', (e) => {
      try { e?.event?.preventDefault?.() } catch { }
      const x = Number(e?.offsetX ?? e?.event?.offsetX)
      if (!Number.isFinite(x)) return
      state.markerRightXPixel = x
      scheduleMarkerRender()
    })
  }

  state.rawChart.on('datazoom', () => {
    state.rawUserZoomed = true
    saveRawZoomState()
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
  state.rawZoomApplied = false

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
  state.params.recoverThresholdPct = (res.data?.event?.recoverThresholdPct !== undefined && res.data?.event?.recoverThresholdPct !== null)
    ? Number(res.data.event.recoverThresholdPct)
    : null
  state.params.minDurationMs = Number(res.data?.event?.minDurationMs ?? 10) || 10
  renderHeader()

  state.totalSamples = 0
  if (state.analysisGuid) {
    try {
      const detailRes = await zwavApi.getDetail(state.analysisGuid)
      if (detailRes && detailRes.success && detailRes.data) {
        const total = Number(detailRes.data.totalRecords ?? 0)
        if (Number.isFinite(total) && total > 0) {
          state.totalSamples = total
          state.searchFromSample = 1
          state.searchToSample = total
        }
      }
    } catch { }
  }

  if (state.totalSamples <= 0) {
    if (res.data.suggestedFromSample !== undefined && res.data.suggestedFromSample !== null) {
      state.searchFromSample = Math.max(1, Number(res.data.suggestedFromSample))
    }
    if (res.data.suggestedToSample !== undefined && res.data.suggestedToSample !== null) {
      state.searchToSample = Math.min(50000, Number(res.data.suggestedToSample))
    }
  }

  setInputValue('fromSample', state.searchFromSample)
  setInputValue('toSample', state.searchToSample)
  setInputValue('limit', state.searchLimit)
  setInputValue('downSample', state.searchDownSample)

  const present = new Set(state.voltageChannels.map((x) => Number(x.channelIndex)))
  state.selected.clear()
  state.hidden.clear()
  state.rmsHidden.clear()

  if (prevSelected.length > 0) {
    prevSelected.forEach((x) => {
      const v = Number(x)
      if (present.has(v)) state.selected.add(v)
    })
  } else {
    state.voltageChannels.forEach((c) => {
      const v = Number(c.channelIndex)
      if (present.has(v)) state.selected.add(v)
    })
  }
  prevHidden.forEach((x) => {
    const v = Number(x)
    if (present.has(v)) state.hidden.add(v)
  })
  prevRmsHidden.forEach((x) => {
    const v = Number(x)
    if (present.has(v)) state.rmsHidden.add(v)
  })

  if (prevRmsHidden.size === 0 && state.rmsHidden.size === 0) {
    state.voltageChannels.forEach((c) => {
      const v = Number(c.channelIndex)
      if (present.has(v)) state.rmsHidden.add(v)
    })
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

  const from = Math.max(1, Number(state.searchFromSample || 1))
  const to = Math.max(from, Number(state.searchToSample || 0) || state.totalSamples || from)
  const take = Math.max(1, Math.min(50000, Number(state.searchLimit || 50000)))
  const downSample = Math.max(1, Number(state.searchDownSample || 1))

  const allRows = []
  let waveChannels = null
  let cursor = from

  while (cursor <= to) {
    const chunkTo = Math.min(to, cursor + take - 1)
    const params = {
      channels: selected.join(','),
      fromSample: cursor,
      toSample: chunkTo,
      limit: take,
      downSample
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

    if (!waveChannels) waveChannels = Array.isArray(res.data.channels) ? res.data.channels.slice() : []
    const rows = Array.isArray(res.data.rows) ? res.data.rows : []
    if (!rows.length) {
      cursor = chunkTo + 1
      continue
    }

    const timeMul = Number(state.process?.timeMul ?? 0.001)
    rows.forEach((r) => {
      const sampleNo = Number(r?.sampleNo)
      const timeRaw = Number(r?.timeRaw)
      let timeMs = Number(r?.timeMs)
      if ((!Number.isFinite(timeMs) || timeMs === 0) && Number.isFinite(timeRaw) && timeRaw !== 0 && Number.isFinite(timeMul) && timeMul > 0) {
        timeMs = timeRaw * timeMul
      }
      allRows.push({
        sampleNo: Number.isFinite(sampleNo) ? sampleNo : 0,
        timeRaw: Number.isFinite(timeRaw) ? timeRaw : 0,
        timeMs: Number.isFinite(timeMs) ? timeMs : 0,
        analog: r?.analog,
        digital: r?.digital
      })
    })

    const lastSampleNo = Number(rows[rows.length - 1]?.sampleNo)
    if (Number.isFinite(lastSampleNo) && lastSampleNo >= cursor) cursor = lastSampleNo + 1
    else cursor = chunkTo + 1
  }

  allRows.sort((a, b) => Number(a.sampleNo) - Number(b.sampleNo))
  state.lastWaveData = {
    fromSample: from,
    toSample: to,
    downSample,
    channels: waveChannels || [],
    digitals: [],
    rows: allRows
  }
  state.rawZoomApplied = false
  state.rawUserZoomed = false
  state.rawZoomState = null
  resetMarkers()
  updateRawChart()
}

async function reloadAll() {
  const ok = await loadProcess()
  if (ok) await fetchWaveData()
}

function setRecalcConfirmValue(id, v) {
  const el = byId(id)
  if (el) el.value = String(v ?? '')
}

function computeRecoverThresholdPct(sagThresholdPct, hysteresisPct) {
  const sag = Number(sagThresholdPct)
  const h = Number(hysteresisPct)
  if (!Number.isFinite(sag) || !Number.isFinite(h)) return ''
  return String(Math.max(sag, Math.min(100, sag + h)))
}

function syncRecalcRecoverThresholdIfAuto() {
  if (!state.recalcRecoverAuto) return
  const sag = byId('recalcSagThresholdPct')?.value
  const hyst = byId('recalcHysteresisPct')?.value
  const v = computeRecoverThresholdPct(sag, hyst)
  if (v !== '') setRecalcConfirmValue('recalcRecoverThresholdPct', v)
}

function openRecalcConfirm() {
  state.recalcRecoverAuto = true
  setRecalcConfirmValue('recalcReferenceVoltage', state.params.referenceVoltage ?? 0)
  setRecalcConfirmValue('recalcMinDurationMs', state.params.minDurationMs ?? 10)
  setRecalcConfirmValue('recalcSagThresholdPct', state.params.sagThresholdPct ?? 90)
  setRecalcConfirmValue('recalcInterruptThresholdPct', state.params.interruptThresholdPct ?? 10)
  setRecalcConfirmValue('recalcHysteresisPct', state.params.hysteresisPct ?? 2)
  const recover = state.params.recoverThresholdPct
  setRecalcConfirmValue(
    'recalcRecoverThresholdPct',
    Number.isFinite(Number(recover)) && Number(recover) > 0
      ? recover
      : computeRecoverThresholdPct(state.params.sagThresholdPct, state.params.hysteresisPct)
  )
  openDialog('recalcConfirmModal')
}

async function confirmRecalc() {
  closeDialog('recalcConfirmModal')
  state.params.referenceVoltage = Number(byId('recalcReferenceVoltage')?.value || state.params.referenceVoltage || 0)
  state.params.minDurationMs = Number(byId('recalcMinDurationMs')?.value || state.params.minDurationMs || 10)
  state.params.sagThresholdPct = Number(byId('recalcSagThresholdPct')?.value || state.params.sagThresholdPct || 90)
  state.params.interruptThresholdPct = Number(byId('recalcInterruptThresholdPct')?.value || state.params.interruptThresholdPct || 10)
  state.params.hysteresisPct = Number(byId('recalcHysteresisPct')?.value || state.params.hysteresisPct || 2)
  const recoverVal = Number(byId('recalcRecoverThresholdPct')?.value || 0)
  state.params.recoverThresholdPct = Number.isFinite(recoverVal) && recoverVal > 0 ? recoverVal : null
  await previewProcess()
  await fetchWaveData()
}

async function previewProcess() {
  setHtml('reportBody', '<div class="report-line">正在重新计算暂降事件...</div>')

  try {
    const body = {
      referenceVoltage: state.params.referenceVoltage,
      sagThresholdPct: state.params.sagThresholdPct,
      recoverThresholdPct: state.params.recoverThresholdPct,
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
      state.searchFromSample = Math.max(1, Number(res.data.suggestedFromSample))
      byId('fromSample').value = String(state.searchFromSample)
    }
    if (res.data.suggestedToSample !== undefined && res.data.suggestedToSample !== null) {
      state.searchToSample = Math.min(50000, Number(res.data.suggestedToSample))
      byId('toSample').value = String(state.searchToSample)
    }

    state.sagSelectedKeys.clear()
    renderSagTable()
    renderReport()
    updateRawChart()

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
  state.searchFromSample = Math.max(1, Number(byId('fromSample').value || 1))
  state.searchToSample = Math.min(50000, Math.max(state.searchFromSample, Number(byId('toSample').value || 20000)))
  state.searchLimit = Math.min(50000, Math.max(100, Number(byId('limit').value || 50000)))
  state.searchDownSample = Math.max(1, Number(byId('downSample').value || 1))
  byId('fromSample').value = String(state.searchFromSample)
  byId('toSample').value = String(state.searchToSample)
  byId('limit').value = String(state.searchLimit)
  byId('downSample').value = String(state.searchDownSample)
  closeDialog('waveModal')
  fetchWaveData()
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
      scroll.style.overflowY = 'auto'
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

async function exportSamples() {
  try {
    const wave = state.lastWaveData
    const selected = Array.from(state.selected || []).map((x) => Number(x)).filter((x) => Number.isFinite(x))
    if (!selected.length) {
      alert('请至少选择一个通道')
      return
    }

    const needFetch = !wave || !Array.isArray(wave.channels) || selected.some((x) => !wave.channels.includes(x))
    if (needFetch) {
      await fetchWaveData()
    }

    const w = state.lastWaveData
    if (!w || !Array.isArray(w.rows) || w.rows.length === 0) {
      alert('没有可导出的采样点数据')
      return
    }

    const sel = selected.filter((x) => (w.channels || []).includes(x))
    if (!sel.length) {
      alert('所选通道没有采样数据')
      return
    }

    const nameMap = new Map()
      ; (state.voltageChannels || []).forEach((c) => {
        nameMap.set(Number(c.channelIndex), String(c.channelName || ''))
      })

    const csvCell = (v) => {
      const s = v === null || v === undefined ? '' : String(v)
      if (/[",\n\r]/.test(s)) return `"${s.replace(/"/g, '""')}"`
      return s
    }

    const header = ['采样点号', '时间(ms)', '时间'].concat(sel.map((idx) => {
      const nm = nameMap.get(Number(idx)) || ''
      return nm ? `${idx}. ${nm}` : `CH${idx}`
    }))
    const lines = [header.map(csvCell).join(',')]

    for (const r of w.rows) {
      const sampleNo = r?.sampleNo ?? ''
      const timeMs = r?.timeMs ?? ''
      const absTime = formatAbsTimeFromOffsetMs(timeMs)
      const analog = Array.isArray(r?.analog) ? r.analog : []
      const row = [sampleNo, timeMs, absTime]
      for (const idx of sel) {
        const dataIndex = (w.channels || []).indexOf(idx)
        const v = dataIndex >= 0 ? analog[dataIndex] : ''
        row.push(v === null || v === undefined ? '' : v)
      }
      lines.push(row.map(csvCell).join(','))
    }

    const text = '\ufeff' + lines.join('\n')
    const blob = new Blob([text], { type: 'text/csv;charset=utf-8;' })
    const base = String(state.process?.event?.originalName || `event-${state.eventId}`)
    const from = Number(w.fromSample ?? '')
    const to = Number(w.toSample ?? '')
    const range = Number.isFinite(from) && Number.isFinite(to) ? `-${from}-${to}` : ''
    downloadBlob(blob, `${base}-Samples${range}.csv`)
  } catch (e) {
    alert(e.message || '导出失败')
  }
}

function openExportSamplesConfirm() {
  const selected = Array.from(state.selected || []).map((x) => Number(x)).filter((x) => Number.isFinite(x))
  if (!selected.length) {
    alert('请至少选择一个通道')
    return
  }

  const nameMap = new Map()
    ; (state.voltageChannels || []).forEach((c) => {
      nameMap.set(Number(c.channelIndex), String(c.channelName || ''))
    })

  const list = selected
    .slice()
    .sort((a, b) => a - b)
    .map((idx) => {
      const nm = nameMap.get(Number(idx)) || ''
      return nm ? `${idx}. ${nm}` : `CH${idx}`
    })

  const from = state.searchFromSample
  const to = state.searchToSample
  const body = `
    <div style="margin-bottom:8px;">将导出当前所选通道的采样点数据，确定继续吗？</div>
    <div style="color: var(--el-text-color-secondary); font-size:12px; line-height:18px;">
      <div>通道数：${list.length}</div>
      <div>采样点范围：${escapeHtml(String(from))} ~ ${escapeHtml(String(to))}</div>
    </div>
    <div style="margin-top:10px; max-height: 220px; overflow:auto; padding:8px 10px; border:1px solid var(--el-border-color); border-radius:6px; background: var(--el-fill-color-light); font-size:12px; line-height:18px;">
      ${list.map((x) => `<div>${escapeHtml(x)}</div>`).join('')}
    </div>
  `
  setHtml('exportSamplesConfirmBody', body)
  openDialog('exportSamplesConfirmModal')
}

async function confirmExportSamples() {
  closeDialog('exportSamplesConfirmModal')
  await exportSamples()
}

async function exportRms() {
  try {
    const rows = Array.isArray(state.process?.rmsPoints) ? state.process.rmsPoints.slice() : []
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
      for (let i = 0; i < list.length; i++) filtered.push(list[i])
    })
    filtered.sort((a, b) => Number(a?.timeMs ?? 0) - Number(b?.timeMs ?? 0))

    if (!filtered.length) {
      alert('没有可导出的RMS数据')
      return
    }

    const nameMap = new Map()
      ; (state.voltageChannels || []).forEach((c) => {
        nameMap.set(Number(c.channelIndex), String(c.channelName || ''))
      })

    const csvCell = (v) => {
      const s = v === null || v === undefined ? '' : String(v)
      if (/[",\n\r]/.test(s)) return `"${s.replace(/"/g, '""')}"`
      return s
    }

    const header = ['序号', '时间(ms)', '采样点号', '相别', '通道名称', '均方根值', '均方根(%)', '参考电压']
    const lines = [header.join(',')]
    filtered.forEach((r) => {
      const idx = Number(r?.channelIndex)
      const chName = nameMap.get(idx) || ''
      lines.push([
        csvCell(r?.seqNo ?? ''),
        csvCell(r?.timeMs ?? ''),
        csvCell(r?.sampleNo ?? ''),
        csvCell(r?.phase ?? ''),
        csvCell(chName || (r?.channelIndex ?? '')),
        csvCell(r?.rms ?? ''),
        csvCell(r?.rmsPct ?? ''),
        csvCell(r?.referenceVoltage ?? '')
      ].join(','))
    })

    const text = '\ufeff' + lines.join('\n')
    const blob = new Blob([text], { type: 'text/csv;charset=utf-8;' })
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    const name = String(state.process?.event?.originalName || `event-${state.eventId}`)
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

  on('btnRecalc', 'click', openRecalcConfirm)
  on('btnLoadWave', 'click', fetchWaveData)
  on('btnWaveSetting', 'click', () => openDialog('waveModal'))
  on('btnApplyWave', 'click', applyWaveSettings)
  on('btnRestoreZoom', 'click', restoreRawZoom)
  on('btnFullScreen', 'click', toggleFullScreen)
  on('rawChart', 'dblclick', toggleFullScreen)
  on('rawChart', 'contextmenu', (e) => {
    try { e.preventDefault() } catch { }
  })
  on('btnExportSamples', 'click', openExportSamplesConfirm)
  on('btnExportRms', 'click', openExportConfirm)
  on('btnEventDetail', 'click', showEventDetail)
  on('btnResetMarkers', 'click', resetMarkers)
  on('btnCopyReport', 'click', copyReport)
  on('btnConfirmExportRms', 'click', confirmExportRms)
  on('btnConfirmExportSamples', 'click', confirmExportSamples)

  const recalcMask = byId('recalcConfirmModal')
  if (recalcMask) {
    recalcMask.addEventListener('click', (e) => {
      const t = e.target
      if (!t) return
      if (t.id === 'btnConfirmRecalc' || (t.closest && t.closest('#btnConfirmRecalc'))) {
        confirmRecalc()
      }
    })
  }

  on('recalcSagThresholdPct', 'input', () => syncRecalcRecoverThresholdIfAuto())
  on('recalcHysteresisPct', 'input', () => syncRecalcRecoverThresholdIfAuto())
  on('recalcRecoverThresholdPct', 'input', () => {
    state.recalcRecoverAuto = false
  })

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
  bindDialog('exportSamplesConfirmModal')
  bindDialog('recalcConfirmModal')

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
