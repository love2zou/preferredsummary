import { escapeHtml } from './common.js'

const NS = 'http://www.w3.org/2000/svg'
const LANES = [60, 100, 140]
const GAP = 22

function el(name, attrs = {}, text) {
  const n = document.createElementNS(NS, name)
  Object.entries(attrs).forEach(([k, v]) => n.setAttribute(k, String(v)))
  if (text != null) n.textContent = text
  return n
}

function clearSvg(svg) {
  while (svg.firstChild) svg.removeChild(svg.firstChild)
}

function setViewBox(svg, W, H) {
  svg.setAttribute('viewBox', `0 0 ${W} ${H}`)
  svg.style.width = `${W}px`
  svg.style.height = `${H}px`
}

function parseMs(str) {
  if (str == null) return NaN
  const s = String(str).trim()
  const m = s.match(/(\d+(\.\d+)?)/)
  return m ? Number(m[1]) : NaN
}

function splitTextByChars(text, maxChars) {
  const s = String(text ?? '').trim()
  if (!s) return ['']
  const out = []
  for (let i = 0; i < s.length; i += maxChars) out.push(s.slice(i, i + maxChars))
  return out
}

function appendWrappedText(parentG, x, y, anchor, fill, text, maxChars = 16, lineHeight = 16) {
  const lines = splitTextByChars(text, maxChars)
  const t = el('text', { x, y, fill, class: 'label-text', 'text-anchor': anchor, 'font-size': '12', 'font-weight': '700' })
  lines.forEach((ln, idx) => {
    const ts = el('tspan', { x, dy: idx === 0 ? '0' : String(lineHeight) }, ln)
    t.appendChild(ts)
  })
  parentG.appendChild(t)
}

function buildNodesFromTripInfoMap(selectedTripInfos, colorMap) {
  const map = new Map()
  for (const [guid, tripInfo] of selectedTripInfos) {
    const color = (colorMap && colorMap.get(guid)) || '#1677ff'
    for (const r of tripInfo || []) {
      const t = parseMs(r.time)
      if (!Number.isFinite(t)) continue
      if (!map.has(t)) map.set(t, { timeMs: t, labels: [] })
      const node = map.get(t)
      const valText = r.value === '1' ? '动作' : r.value === '0' ? '复归' : r.value
      node.labels.push({ text: `${r.name} ${valText}`, color, guid })
    }
  }

  const nodes = [...map.values()].sort((a, b) => a.timeMs - b.timeMs)
  let flip = false
  for (const n of nodes) {
    const side = flip ? 'bottom' : 'top'
    flip = !flip
    n.labels.forEach((x) => (x.side = side))
  }
  return nodes
}

function estimateGroupExtent(itemsCount, maxLines) {
  const dotsSpan = itemsCount <= 1 ? 0 : (itemsCount - 1) * GAP
  const textSpan = maxLines <= 1 ? 0 : (maxLines - 1) * 16
  return dotsSpan + textSpan + 20
}

function estimateVerticalNeeds(nodes) {
  let needTop = 0
  let needBot = 0
  nodes.forEach((node, idx) => {
    const arr = node.labels
    if (!arr || arr.length === 0) return
    const isTop = (arr[0].side || 'top') === 'top'
    const laneDist = LANES[idx % 3]
    const maxChars = arr.length > 1 ? 14 : 16
    const maxLines = Math.max(...arr.map((x) => splitTextByChars(x.text, maxChars).length))
    const extent = laneDist + estimateGroupExtent(arr.length, maxLines)
    if (isTop) needTop = Math.max(needTop, extent)
    else needBot = Math.max(needBot, extent)
  })
  return { needTop: Math.max(needTop, 120), needBot: Math.max(needBot, 120) }
}

function computeXPositionsAdaptiveByTime(times, x0, minGap = 70, extraSpan = 110, maxMs = 5000) {
  const n = times.length
  if (n === 0) return []
  if (n === 1) return [x0]
  const sorted = [...times].filter((t) => Number.isFinite(t)).sort((a, b) => a - b)
  if (sorted.length <= 1) return times.map(() => x0)

  const deltas = []
  for (let i = 1; i < sorted.length; i++) deltas.push(Math.max(0, sorted[i] - sorted[i - 1]))
  const weights = deltas.map((d) => Math.sqrt(Math.min(1, d / maxMs)))
  const segs = weights.map((w) => minGap + extraSpan * w)

  const xsSorted = new Array(sorted.length)
  xsSorted[0] = x0
  for (let i = 1; i < sorted.length; i++) xsSorted[i] = xsSorted[i - 1] + segs[i - 1]

  const map = new Map()
  for (let i = 0; i < sorted.length; i++) map.set(sorted[i], xsSorted[i])
  return times.map((t) => map.get(t) ?? x0)
}

function drawLabelGroup(svg, cx, axisY, items, isTop, idx) {
  const dir = isTop ? -1 : 1
  const laneDist = LANES[idx % 3]
  const anchorX = cx + laneDist
  const anchorY = axisY + dir * laneDist

  const g = el('g', {})
  svg.appendChild(g)

  const groupColor = (items[0] && items[0].color) || '#1677ff'

  g.appendChild(
    el('line', {
      x1: cx,
      y1: axisY,
      x2: anchorX,
      y2: anchorY,
      stroke: groupColor,
      'stroke-width': 2,
      'stroke-linecap': 'round',
      opacity: 0.45
    })
  )

  const ys = []
  for (let i = 0; i < items.length; i++) ys.push(anchorY + dir * GAP * i)

  if (items.length > 1) {
    g.appendChild(
      el('line', {
        x1: anchorX,
        y1: Math.min(...ys),
        x2: anchorX,
        y2: Math.max(...ys),
        stroke: groupColor,
        'stroke-width': 3,
        'stroke-linecap': 'round',
        opacity: 0.55
      })
    )
  }

  for (let i = 0; i < items.length; i++) {
    const y = ys[i]
    const itemColor = items[i].color || groupColor
    g.appendChild(el('circle', { cx: anchorX, cy: y, r: 4, fill: itemColor, opacity: 0.95 }))
    const textX = anchorX + 10
    appendWrappedText(g, textX, y + 4, 'start', itemColor, items[i].text, 18, 16)
  }
}

export function renderSequence(svg, { selectedTripInfos, fileColorMap, width }) {
  clearSvg(svg)

  const nodes = buildNodesFromTripInfoMap(selectedTripInfos, fileColorMap)
  const times = nodes.map((n) => n.timeMs)

  if (times.length === 0) {
    setViewBox(svg, 1400, 320)
    svg.appendChild(el('text', { x: 60, y: 100, fill: '#909399', 'font-size': '14', 'font-weight': '700' }, '暂无保护动作信息'))
    return { width: 1400, height: 320 }
  }

  const minGap = 70
  const extraSpan = 110
  const maxMs = 5000

  const xLeftPad = 60
  const xRightPad = 220
  const axisExtra = 120

  const xsTemp = computeXPositionsAdaptiveByTime(times, xLeftPad + 40, minGap, extraSpan, maxMs)
  const contentEndX = xsTemp.length ? Math.max(...xsTemp) : xLeftPad + 400
  const requiredW = Math.ceil(contentEndX + xRightPad + axisExtra)
  const W = Math.max(1400, Number(width || 1400), requiredW)

  const { needTop, needBot } = estimateVerticalNeeds(nodes)
  const topPad = 80
  const botPad = 80
  const axisY = topPad + needTop
  const H = axisY + needBot + botPad

  setViewBox(svg, W, H)

  const axisStart = xLeftPad
  const axisEnd = W - xRightPad

  svg.appendChild(
    el('line', { x1: axisStart, y1: axisY, x2: axisEnd, y2: axisY, stroke: '#cfe6ff', 'stroke-width': 10, 'stroke-linecap': 'round' })
  )

  const headLen = 46
  const headHalf = 18
  const tipX = axisEnd + headLen
  svg.appendChild(el('polygon', { points: `${tipX},${axisY} ${axisEnd},${axisY - headHalf} ${axisEnd},${axisY + headHalf}`, fill: '#9dc5f8', opacity: 0.95 }))
  svg.appendChild(el('text', { x: tipX + 14, y: axisY + 6, fill: '#6b7280', 'font-weight': '700', 'font-size': '12' }, '时间/ms'))

  const xs = computeXPositionsAdaptiveByTime(times, axisStart + 50, minGap, extraSpan, maxMs)
  nodes.forEach((node, idx) => {
    const cx = xs[idx]
    svg.appendChild(el('circle', { cx, cy: axisY, r: 18, fill: '#1677ff', opacity: 0.92 }))
    svg.appendChild(el('circle', { cx, cy: axisY, r: 22, fill: 'none', stroke: '#93c5fd', 'stroke-width': 6, opacity: 0.22 }))
    svg.appendChild(el('text', { x: cx, y: axisY + 5, 'text-anchor': 'middle', fill: '#fff', 'font-size': '14', 'font-weight': '800' }, String(node.timeMs)))

    const arr = node.labels
    if (arr && arr.length) {
      const isTop = (arr[0].side || 'top') === 'top'
      drawLabelGroup(svg, cx, axisY, arr, isTop, idx)
    }
  })
  return { width: W, height: H }
}

export function renderFileCheckboxList(container, { files, checkedGuids, fileColorMap, onChange }) {
  container.innerHTML = files
    .map((f) => {
      const checked = checkedGuids.has(f.guid) ? 'checked' : ''
      const color = (fileColorMap && fileColorMap.get(f.guid)) || '#1677ff'
      const trip = Array.isArray(f.trip) ? f.trip : []
      const list = trip
        .map((r) => {
          const t = parseMs(r.time)
          if (!Number.isFinite(t)) return null
          const valText = r.value === '1' ? '动作' : r.value === '0' ? '复归' : (r.value ?? '')
          const text = `${r.name ?? ''} ${valText}`.trim()
          return { t, text }
        })
        .filter(Boolean)
        .sort((a, b) => a.t - b.t)

      const maxItems = 200
      const shown = list.slice(0, maxItems)
      const startTime = f.startTime || '-'
      const startNode = `<li class="seq-tree-item is-start"><span class="seq-tree-item__dot" style="background:${escapeHtml(
        color
      )}"></span><span class="seq-tree-item__time">启动时间</span><span class="seq-tree-item__text" title="${escapeHtml(
        String(startTime)
      )}">${escapeHtml(String(startTime))}</span></li>`
      const children = shown.length
        ? shown
          .map((it) => {
            return `<li class="seq-tree-item">
              <span class="seq-tree-item__dot" style="background:${escapeHtml(color)}"></span>
              <span class="seq-tree-item__time">${escapeHtml(String(it.t))}ms</span>
              <span class="seq-tree-item__text" title="${escapeHtml(it.text)}">${escapeHtml(it.text)}</span>
            </li>`
          })
          .join('')
        : `<li class="seq-tree-item is-empty"><span class="seq-tree-item__text">暂无动作</span></li>`

      const open = checked ? 'open' : ''
      return `<details class="seq-tree-node" ${open}>
        <summary class="seq-tree-summary">
          <span class="seq-tree-arrow"></span>
          <input class="seq-check" type="checkbox" data-guid="${escapeHtml(f.guid)}" ${checked}/>
          <span class="seq-tree-dot" style="background:${escapeHtml(color)}"></span>
          <span class="seq-tree-name" title="${escapeHtml(f.name)}">${escapeHtml(f.name)}</span>
        </summary>
        <ul class="seq-tree-children">${startNode}${children}</ul>
      </details>`
    })
    .join('')

  container.querySelectorAll('.seq-check').forEach((el2) => {
    el2.addEventListener('change', () => {
      if (typeof onChange === 'function') onChange()
    })
  })
}
