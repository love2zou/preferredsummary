import { bindDialog, byId, closeDialog, escapeHtml, formatDateTime, getApiBaseUrl, openDialog, qs, setApiBaseUrl, setHtml, setText } from './common.js'
import { zwavApi } from './zwav-api.js'

const RAW_COLORS = ['#1677ff', '#52c41a', '#faad14', '#ff4d4f', '#13c2c2', '#722ed1', '#eb2f96', '#2f54eb']
const RMS_COLORS = ['#722ed1', '#c41d7f', '#d48806', '#08979c', '#d4380d', '#237804', '#003a8c', '#597ef7']

const toleranceStandard = [
  [0, 90],
  [0.01, 90],
  [0.03, 80],
  [0.5, 70],
  [10, 80]
]

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
  rawChart: null,
  toleranceChart: null,
  lastWaveData: null,
  isFullScreen: false,
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
  setText('headerSub', name ? `事件ID ${id} | ${name}` : `事件ID ${id}`)
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
      const typeText = String(evt.eventType || '-')
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

  byId('sagTbody').querySelectorAll('.sag-check').forEach((el) => {
    el.addEventListener('change', () => {
      const key = String(el.getAttribute('data-key') || '')
      if (!key) return
      state.hasPickedSag = true
      if (el.checked) state.sagSelectedKeys.add(key)
      else state.sagSelectedKeys.delete(key)
      updateToleranceChart()
    })
  })

  updateToleranceChart()
}

function buildToleranceLegend(points) {
  const legend = []
  legend.push(`<span class="legend-item curve"><span class="legend-text">标准容忍度折线</span></span>`)
  for (const p of points) {
    legend.push(`<span class="legend-item dyn-point"><span class="legend-dot" style="background:${escapeHtml(p.color)}"></span><span class="legend-text">${escapeHtml(p.label)}</span></span>`)
  }
  setHtml('toleranceLegend', legend.join(''))
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
        value: [x, y]
      }
    })
    .filter(Boolean)

  buildToleranceLegend(points)

  const stdXMax = Math.max(1, ...toleranceStandard.map((p) => Number(p[0] || 0)))
  const pointXMax = Math.max(0, ...points.map((p) => Number(p.value?.[0] || 0)))
  const xMax = Math.max(1, stdXMax, pointXMax)

  const series = [
    {
      name: '标准容忍度折线',
      type: 'line',
      showSymbol: false,
      lineStyle: { width: 2, color: '#409eff' },
      data: toleranceStandard
    }
  ]
  for (const p of points) {
    series.push({
      name: p.label,
      type: 'scatter',
      symbolSize: 10,
      itemStyle: { color: p.color },
      label: { show: true, formatter: () => p.label, position: 'right', fontSize: 10 },
      data: [{ value: p.value }]
    })
  }

  state.toleranceChart.setOption({
    tooltip: { trigger: 'item' },
    grid: { left: 40, right: 20, top: 10, bottom: 40 },
    xAxis: { type: 'value', min: 0, max: xMax, name: '持续时间 (s)', nameGap: 30 },
    yAxis: { type: 'value', min: 0, max: 100, name: '残余电压 (%)' },
    series
  })
}

function renderRawLegend() {
  const selected = Array.from(state.selected)
  const items = []
  selected.forEach((idx, i) => {
    const ch = state.voltageChannels.find((c) => Number(c.channelIndex) === Number(idx))
    const phase = String(ch?.phase || '').toUpperCase()
    const label = `${phase ? `${phase}相` : `CH${idx}`} 原始`
    const hiddenCls = state.hidden.has(idx) ? 'style="opacity:0.45"' : ''
    items.push(`<span class="raw-legend-item" data-kind="raw" data-idx="${escapeHtml(idx)}" ${hiddenCls}><span class="legend-line" style="background:${RAW_COLORS[i % RAW_COLORS.length]}"></span><span class="legend-text">${escapeHtml(label)}</span></span>`)
  })
  selected.forEach((idx, i) => {
    const ch = state.voltageChannels.find((c) => Number(c.channelIndex) === Number(idx))
    const phase = String(ch?.phase || '').toUpperCase()
    const label = `${phase ? `${phase}相` : `CH${idx}`} RMS`
    const hiddenCls = state.rmsHidden.has(idx) ? 'style="opacity:0.45"' : ''
    items.push(`<span class="raw-legend-item" data-kind="rms" data-idx="${escapeHtml(idx)}" ${hiddenCls}><span class="legend-line rms" style="background:${RMS_COLORS[i % RMS_COLORS.length]}"></span><span class="legend-text">${escapeHtml(label)}</span></span>`)
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

  const selected = Array.from(state.selected).filter((x) => (wave.channels || []).includes(x)).slice(0, 12)
  if (selected.length === 0) return

  const rows = wave.rows
  const xTimes = rows.map((r) => Number(r.timeMs))
  const xData = xTimes.map((t) => Number(t).toFixed(3))
  const gridHeight = Math.max(80, Math.floor(420 / selected.length))
  const freqHz = Number(state.process?.frequencyHz)
  const rmsWindowN = getRmsWindowN(xTimes, freqHz, state.rmsWindowCycles)
  const rmsHopN = Math.max(1, Math.round(rmsWindowN * (Number(state.rmsHopCycles) / Math.max(0.0001, Number(state.rmsWindowCycles) || 1))))

  const grids = []
  const xAxis = []
  const yAxis = []
  const series = []

  const topBase = 10
  const totalHeight = selected.length * gridHeight
  const refV = getRefVoltage()
  const sagPct = Number(state.params.sagThresholdPct ?? 90)
  const interruptPct = Number(state.params.interruptThresholdPct ?? 10)
  const hysteresisPct = Number(state.params.hysteresisPct ?? 2)
  const recoverPct = sagPct + hysteresisPct

  selected.forEach((channelIndex, i) => {
    const top = topBase + i * gridHeight
    grids.push({ left: 60, right: 20, top, height: gridHeight - 18 })
    xAxis.push({
      type: 'category',
      gridIndex: i,
      data: xData,
      boundaryGap: false,
      axisLabel: { fontSize: 10, formatter: (v) => `${v}` }
    })
    yAxis.push({
      type: 'value',
      gridIndex: i,
      axisLabel: { fontSize: 10 },
      scale: true
    })

    const dataIndex = (wave.channels || []).indexOf(channelIndex)
    const raw = dataIndex >= 0 ? rows.map((r) => (state.hidden.has(channelIndex) ? null : (r.analog?.[dataIndex] ?? null))) : rows.map(() => null)
    const rawColor = RAW_COLORS[i % RAW_COLORS.length]
    const rmsColor = RMS_COLORS[i % RMS_COLORS.length]

    const ch = state.voltageChannels.find((c) => Number(c.channelIndex) === Number(channelIndex))
    const phase = String(ch?.phase || '').toUpperCase()
    const name = ch ? `${ch.channelIndex}. ${ch.channelName || ''}` : `CH${channelIndex}`

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

    if (rmsWindowN > 0 && !state.rmsHidden.has(channelIndex) && !state.hidden.has(channelIndex)) {
      const rmsData = computeRollingRms(raw, rmsWindowN, rmsHopN)
      series.push({
        name: `${phase || channelIndex} RMS`,
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

    const baseSeriesIndex = series.length - 1

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
        itemStyle: { color: 'rgba(255, 173, 20, 0.18)' },
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
    animation: false,
    grid: grids,
    xAxis,
    yAxis,
    dataZoom: [
      { type: 'inside', xAxisIndex: selected.map((_, i) => i) },
      { type: 'slider', xAxisIndex: selected.map((_, i) => i), bottom: 0 }
    ],
    series
  })

  setText(
    'rawSub',
    `参考电压：${refV ? refV.toFixed(2) : '-'} V，暂降阈值：${formatPercent(sagPct)}%，中断阈值：${formatPercent(interruptPct)}%，迟滞：${formatPercent(hysteresisPct)}%，最小持续：${formatMs(state.params.minDurationMs)} ms`
  )
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
  return lines
}

function renderReport() {
  const lines = buildReportLines()
  setHtml('reportBody', lines.map((x) => `<div class="report-line">${escapeHtml(x)}</div>`).join(''))
}

function initCharts() {
  state.rawChart = echarts.init(byId('rawChart'))
  state.toleranceChart = echarts.init(byId('toleranceChart'))
  window.addEventListener('resize', () => {
    state.rawChart && state.rawChart.resize()
    state.toleranceChart && state.toleranceChart.resize()
  })
}

async function loadProcess() {
  const res = await zwavApi.sagGetProcess(state.eventId)
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

  const byPhase = (p) => state.voltageChannels.find((c) => String(c.phase || '').toUpperCase() === p)
  const initPick = [byPhase('A'), byPhase('B'), byPhase('C')].filter(Boolean).map((x) => x.channelIndex)
  if (initPick.length) initPick.forEach((x) => state.selected.add(x))
  if (!state.selected.size) state.voltageChannels.slice(0, 3).forEach((x) => state.selected.add(x.channelIndex))

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
  const res = await zwavApi.getWaveData(state.analysisGuid, params)
  if (!res || !res.success || !res.data) {
    alert((res && res.message) || '获取波形失败')
    return
  }
  state.lastWaveData = res.data
  updateRawChart()
}

async function previewProcess() {
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

    renderSagTable()
    renderReport()
    updateRawChart()
    alert('预览成功')
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

function toggleFullScreen() {
  state.isFullScreen = !state.isFullScreen
  const wrap = byId('rawWrap')
  if (state.isFullScreen) wrap.classList.add('is-window-fullscreen')
  else wrap.classList.remove('is-window-fullscreen')
  byId('reportCard').style.display = state.isFullScreen ? 'none' : ''
  setTimeout(() => {
    state.rawChart && state.rawChart.resize()
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

  on('btnFetch', 'click', fetchWaveData)
  on('btnPreview', 'click', previewProcess)
  on('btnWaveSetting', 'click', () => openDialog('waveModal'))
  on('btnApplyWave', 'click', applyWaveSettings)
  on('btnFullScreen', 'click', toggleFullScreen)
  on('btnExportRms', 'click', exportRms)
  on('btnEventDetail', 'click', showEventDetail)

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
  const ok = await loadProcess()
  if (ok) await fetchWaveData()
}

init()
