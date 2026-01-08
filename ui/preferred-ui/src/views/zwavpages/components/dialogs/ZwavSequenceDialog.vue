<template>
  <!-- 故障时序图浮窗 -->
  <el-dialog
    :model-value="visible"
    title="故障时序图"
    width="1400px"
    draggable
    append-to-body
    :close-on-click-modal="false"
    class="sequence-dialog"
    @update:model-value="updateVisible"
    @opened="renderSequenceDiagram"
  >
    <div class="seq-layout">
      <!-- 左侧树形结构 -->
      <div class="seq-sidebar">
        <div class="sidebar-actions">
          <el-button type="primary" size="small" :icon="Plus" @click="openFileSelectDialog">添加对比</el-button>
        </div>

        <el-tree
          ref="seqTreeRef"
          :data="seqTreeData"
          :props="{ label: 'label', children: 'children', class: customNodeClass }"
          show-checkbox
          check-strictly
          check-on-click-node
          node-key="id"
          default-expand-all
          highlight-current
          :default-checked-keys="defaultCheckedKeys"
          @check="handleTreeCheck"
        >
          <template #default="{ node, data }">
            <span class="custom-tree-node" :title="node.label">
              <span v-if="node.level > 1" class="dot-indicator" :style="{ backgroundColor: data.color || '#ccc' }"></span>
              {{ node.label }}
            </span>
          </template>
        </el-tree>
      </div>

      <!-- 右侧时序图 -->
      <div class="seq-content">
        <!-- 顶部工具条：图例 + 缩放控制 -->
        <div class="seq-topbar">
          <div class="seq-legend" v-if="loadedFileNames.size > 0">
            <div v-for="[guid, name] in loadedFileNames" :key="guid" class="legend-item">
              <span class="legend-dot" :style="{ backgroundColor: loadedFileColors.get(guid) }"></span>
              <span class="legend-text" :title="name">{{ name }}</span>
            </div>
          </div>

          <div class="seq-zoom-tools">
            <el-popover placement="bottom-end" :width="260" trigger="click">
              <template #reference>
                <el-button size="small" class="zoom-btn" type="primary" plain>
                  缩放倍数：{{ Math.round(zoomScale * 100) }}%
                </el-button>
              </template>

              <div class="zoom-pop">
                <div class="zoom-row">
                  <el-button size="small" @click="zoomOut">-</el-button>
                  <el-slider
                    v-model="zoomScale"
                    :min="0.4"
                    :max="3"
                    :step="0.1"
                    :show-tooltip="false"
                    @change="applyZoomCenter()"
                  />
                  <el-button size="small" @click="zoomIn">+</el-button>
                </div>
                <div class="zoom-actions">
                  <el-button size="small" @click="resetZoom">重置 100%</el-button>
                  <div class="zoom-hint">提示：滚轮缩放；按住滚轮拖拽平移</div>
                </div>
              </div>
            </el-popover>
          </div>
        </div>

        <!-- ✅ 缩放+平移容器 -->
        <div
          class="seq-container"
          ref="seqContainerRef"
          @wheel.passive="onWheelZoom"
          @mousedown="onMouseDown"
          @mousemove="onMouseMove"
          @mouseup="onMouseUp"
          @mouseleave="onMouseUp"
        >
          <!-- ✅ 左上角固定信息（不跟随鱼尾位置） -->
          <div class="canvas-info">
            <div class="fault-time">{{ currentMainFaultTime }}</div>
            <div class="sub-info">故障发生时间</div>
          </div>

          <!-- 缩放舞台：内部 stage 通过 transform 缩放 -->
          <div
            ref="seqStageRef"
            class="seq-stage"
            :class="{ panning: isPanning }"
            :style="{
              transform: `scale(${zoomScale})`,
              transformOrigin: '0 0'
            }"
          >
            <svg ref="seqSvgRef" class="seq-svg"></svg>
          </div>
        </div>
      </div>
    </div>
  </el-dialog>

  <!-- 文件选择弹窗 -->
  <el-dialog v-model="fileSelectDialogVisible" title="选择录波文件" width="800px" append-to-body :close-on-click-modal="false">
    <div class="file-select-header">
      <el-input v-model="fileSearchKeyword" placeholder="文件名" style="width: 200px" clearable @keyup.enter="fetchFileList" />
      <el-button type="primary" :icon="Search" @click="fetchFileList">查询</el-button>
    </div>

    <el-table
      v-loading="fileLoading"
      :data="fileTableData"
      border
      stripe
      height="400"
      @row-click="handleFileSelect"
      class="file-table"
    >
      <el-table-column prop="originalName" label="文件名" min-width="200" show-overflow-tooltip />
      <el-table-column prop="crtTime" label="创建时间" width="160" />
      <el-table-column label="操作" width="80">
        <template #default>
          <el-icon><Plus /></el-icon>
        </template>
      </el-table-column>
    </el-table>

    <div class="pagination-container">
      <el-pagination
        v-model:current-page="filePagination.page"
        v-model:page-size="filePagination.pageSize"
        :total="filePagination.total"
        layout="total, prev, pager, next"
        @current-change="fetchFileList"
      />
    </div>
  </el-dialog>
</template>

<script setup lang="ts">
import { zwavService, type ZwavFileAnalysis } from '@/services/zwavService';
import { Plus, Search } from '@element-plus/icons-vue';
import { ElMessage } from 'element-plus';
import { nextTick, reactive, ref, watch } from 'vue';

const props = defineProps<{
  visible: boolean
  currentAnalysisGuid: string
  detailData: any
  hdrData: any
  tripInfoArray: any[]
}>()

const emit = defineEmits<{
  (e: 'update:visible', val: boolean): void
}>()

const updateVisible = (val: boolean) => emit('update:visible', val)

const customNodeClass = (_data: any, node: any) => {
  if (node.level > 1) return 'is-leaf-node'
  return null
}

// =========================
// Data
// =========================
const seqTreeData = ref<any[]>([])
const seqTreeRef = ref()
const defaultCheckedKeys = ref<string[]>([])
const currentMainFaultTime = ref('')

// File Selection
const fileSelectDialogVisible = ref(false)
const fileSearchKeyword = ref('')
const fileLoading = ref(false)
const fileTableData = ref<ZwavFileAnalysis[]>([])
const filePagination = reactive({ page: 1, pageSize: 10, total: 0 })

// Caches（用 reactive Map，模板可响应）
const loadedTripInfos = reactive(new Map<string, any[]>())
const loadedFileNames = reactive(new Map<string, string>())
const loadedFileColors = reactive(new Map<string, string>())
const SERIES_COLORS = ['#1677ff', '#b56be6', '#f6a04b', '#52c41a', '#ff4d4f']

// =========================
// Zoom + Pan
// =========================
const zoomScale = ref(1)
const seqStageRef = ref<HTMLElement | null>(null)
const seqContainerRef = ref<HTMLElement | null>(null)

const clamp = (v: number, min: number, max: number) => Math.min(max, Math.max(min, v))

const applyZoomCenter = () => {
  // 如需以鼠标点为中心缩放，可再加 scroll 修正；目前保持简单稳定
}

const zoomIn = () => {
  zoomScale.value = clamp(Number((zoomScale.value + 0.1).toFixed(2)), 0.4, 3)
  applyZoomCenter()
}
const zoomOut = () => {
  zoomScale.value = clamp(Number((zoomScale.value - 0.1).toFixed(2)), 0.4, 3)
  applyZoomCenter()
}
const resetZoom = () => {
  zoomScale.value = 1
  applyZoomCenter()
}

// 滚轮缩放（默认不阻断滚动；若你希望滚轮只缩放，去掉 .passive 并 preventDefault）
const onWheelZoom = (e: WheelEvent) => {
  // 按住滚轮拖拽平移时，避免滚轮的滚动触发缩放抖动
  if (isPanning.value) return

  const dir = e.deltaY < 0 ? 1 : -1
  const next = clamp(Number((zoomScale.value + dir * 0.08).toFixed(2)), 0.4, 3)
  if (next !== zoomScale.value) {
    zoomScale.value = next
    applyZoomCenter()
  }
}

// ✅ 按住滚轮（中键）拖拽平移：通过控制容器 scrollLeft/scrollTop 实现
const isPanning = ref(false)
const panStart = reactive({
  x: 0,
  y: 0,
  scrollLeft: 0,
  scrollTop: 0
})

const onMouseDown = (e: MouseEvent) => {
  // 中键：button===1
  if (e.button !== 1) return
  if (!seqContainerRef.value) return

  isPanning.value = true
  panStart.x = e.clientX
  panStart.y = e.clientY
  panStart.scrollLeft = seqContainerRef.value.scrollLeft
  panStart.scrollTop = seqContainerRef.value.scrollTop

  // 防止中键默认行为（部分浏览器会出现自动滚动图标）
  e.preventDefault()
}

const onMouseMove = (e: MouseEvent) => {
  if (!isPanning.value) return
  if (!seqContainerRef.value) return

  const dx = e.clientX - panStart.x
  const dy = e.clientY - panStart.y

  // 拖拽方向与滚动方向相反：向右拖 => 画布向右 => scrollLeft 减小
  seqContainerRef.value.scrollLeft = panStart.scrollLeft - dx
  seqContainerRef.value.scrollTop = panStart.scrollTop - dy
}

const onMouseUp = () => {
  if (!isPanning.value) return
  isPanning.value = false
}

// =========================
// Init when open
// =========================
watch(
  () => props.visible,
  (val) => {
    if (val) initSequenceDialog()
  }
)

const initSequenceDialog = async () => {
  if (!props.hdrData || !props.tripInfoArray || props.tripInfoArray.length === 0) return

  zoomScale.value = 1
  isPanning.value = false

  if (loadedTripInfos.has(props.currentAnalysisGuid)) {
    await nextTick()
    requestAnimationFrame(() => renderSequenceDiagram())
    return
  }

  const guid = props.currentAnalysisGuid
  const fileName = props.detailData?.file?.originalName || '当前文件'
  const startTime = props.hdrData.faultStartTime || '未知时间'
  currentMainFaultTime.value = startTime

  const tripInfo = props.tripInfoArray

  loadedTripInfos.clear()
  loadedFileNames.clear()
  loadedFileColors.clear()

  loadedTripInfos.set(guid, tripInfo)
  loadedFileNames.set(guid, fileName)
  loadedFileColors.set(guid, SERIES_COLORS[0])

  const rootId = guid
  const tripNodes = tripInfo.map((item: any, index: number) => ({
    label: formatTripNodeLabel(item),
    id: `${rootId}-trip-${index}`,
    disabled: true,
    color: SERIES_COLORS[0]
  }))

  seqTreeData.value = [
    {
      label: fileName,
      id: rootId,
      children: [{ label: `启动时间: ${startTime}`, id: `${rootId}-start-time`, disabled: true }, ...tripNodes]
    }
  ]

  defaultCheckedKeys.value = [rootId]

  await nextTick()
  requestAnimationFrame(() => requestAnimationFrame(() => renderSequenceDiagram()))
}

// =========================
// File picker
// =========================
const openFileSelectDialog = () => {
  fileSelectDialogVisible.value = true
  fetchFileList()
}

const fetchFileList = async () => {
  fileLoading.value = true
  try {
    const params = {
      page: filePagination.page,
      pageSize: filePagination.pageSize,
      keyword: fileSearchKeyword.value,
      status: 'Completed'
    }
    const res: any = await zwavService.getList(params)
    if (res.success) {
      fileTableData.value = res.data.data.filter((f: any) => f.analysisGuid !== props.currentAnalysisGuid)
      filePagination.total = res.data.total
    }
  } catch (err) {
    console.error(err)
  } finally {
    fileLoading.value = false
  }
}

const handleFileSelect = async (row: ZwavFileAnalysis) => {
  if (loadedTripInfos.has(row.analysisGuid)) {
    ElMessage.info('该文件已在列表中')
    return
  }

  try {
    const res: any = await zwavService.getHdr(row.analysisGuid)
    if (res.success && res.data && res.data.tripInfoJSON) {
      const guid = row.analysisGuid
      const fileName = row.originalName
      const startTime = res.data.faultStartTime

      let tripInfo: any[] = []
      const t = res.data.tripInfoJSON
      if (Array.isArray(t)) tripInfo = t
      else if (typeof t === 'string') {
        try {
          tripInfo = JSON.parse(t)
        } catch {
          tripInfo = []
        }
      }

      if (!Array.isArray(tripInfo) || tripInfo.length === 0) {
        ElMessage.warning('该文件没有保护动作信息')
        return
      }

      loadedTripInfos.set(guid, tripInfo)
      loadedFileNames.set(guid, fileName)

      const colorIndex = loadedTripInfos.size % SERIES_COLORS.length
      const fileColor = SERIES_COLORS[colorIndex] || SERIES_COLORS[0]
      loadedFileColors.set(guid, fileColor)

      const tripNodes = tripInfo.map((item: any, index: number) => ({
        label: formatTripNodeLabel(item),
        id: `${guid}-trip-${index}`,
        disabled: true,
        color: fileColor
      }))

      seqTreeData.value = [
        ...seqTreeData.value,
        {
          label: fileName,
          id: guid,
          children: [{ label: `启动时间: ${startTime}`, id: `${guid}-start-time`, disabled: true }, ...tripNodes]
        }
      ]

      defaultCheckedKeys.value = [...defaultCheckedKeys.value, guid]
      nextTick(() => {
        seqTreeRef.value?.setCheckedKeys(defaultCheckedKeys.value)
        renderSequenceDiagram()
      })

      ElMessage.success('添加成功')
    } else {
      ElMessage.warning('该文件没有保护动作信息')
    }
  } catch (err) {
    console.error(err)
    ElMessage.error('加载文件信息失败')
  }
}

const handleTreeCheck = () => {
  if ((window as any)._seqRenderTimer) clearTimeout((window as any)._seqRenderTimer)
  ;(window as any)._seqRenderTimer = setTimeout(() => renderSequenceDiagram(), 80)
}

const formatTripNodeLabel = (item: any) => {
  const valText = item.value === '1' ? '动作' : item.value === '0' ? '复归' : item.value
  let timeStr = String(item.time || '').trim()
  const match = timeStr.match(/^([0-9.]+)/)
  if (match) timeStr = String(parseFloat(match[1]))
  return `${timeStr}ms ${item.name} ${valText}`
}

// =========================
// SVG 绘制
// =========================
const seqSvgRef = ref<SVGElement | null>(null)

const NS = 'http://www.w3.org/2000/svg'
const LANES = [60, 100, 140]
const GAP = 22

function el(name: string, attrs: any = {}, text?: string) {
  const n = document.createElementNS(NS, name)
  for (const [k, v] of Object.entries(attrs)) n.setAttribute(k, String(v))
  if (text != null) n.textContent = text
  return n
}

function clearSvg() {
  if (!seqSvgRef.value) return
  while (seqSvgRef.value.firstChild) seqSvgRef.value.removeChild(seqSvgRef.value.firstChild)
}

function setViewBox(W: number, H: number) {
  if (!seqSvgRef.value) return
  seqSvgRef.value.setAttribute('viewBox', `0 0 ${W} ${H}`)
  ;(seqSvgRef.value as any).style.height = `${H}px`
  ;(seqSvgRef.value as any).style.width = `${W}px`
}

function parseMs(str: any) {
  if (str == null) return NaN
  const s = String(str).trim()
  const m = s.match(/(\d+(\.\d+)?)/)
  return m ? Number(m[1]) : NaN
}

function splitTextByChars(text: string, maxChars: number) {
  const s = String(text ?? '').trim()
  if (!s) return ['']
  const out: string[] = []
  for (let i = 0; i < s.length; i += maxChars) out.push(s.slice(i, i + maxChars))
  return out
}

function appendWrappedText(parentG: Element, x: number, y: number, anchor: string, fill: string, text: string, maxChars = 16, lineHeight = 16) {
  const lines = splitTextByChars(text, maxChars)
  const t = el('text', { x, y, fill, class: 'label-text', 'text-anchor': anchor, 'font-size': '12', 'font-weight': '700' })
  lines.forEach((ln, idx) => {
    const ts = el('tspan', { x, dy: idx === 0 ? '0' : String(lineHeight) }, ln)
    t.appendChild(ts)
  })
  parentG.appendChild(t)
}

function buildNodesFromTripInfoMap(selectedTripInfos: Map<string, any[]>) {
  const map = new Map<number, any>()
  for (const [guid, tripInfo] of selectedTripInfos) {
    const color = loadedFileColors.get(guid) || '#1677ff'
    for (const r of tripInfo) {
      const t = parseMs(r.time)
      if (!Number.isFinite(t)) continue
      if (!map.has(t)) map.set(t, { timeMs: t, labels: [] as any[] })
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
    n.labels.forEach((x: any) => (x.side = side))
  }
  return nodes
}

function estimateGroupExtent(itemsCount: number, maxLines: number) {
  const dotsSpan = itemsCount <= 1 ? 0 : (itemsCount - 1) * GAP
  const textSpan = maxLines <= 1 ? 0 : (maxLines - 1) * 16
  return dotsSpan + textSpan + 20
}

function estimateVerticalNeeds(nodes: any[]) {
  let needTop = 0
  let needBot = 0
  nodes.forEach((node, idx) => {
    const arr = node.labels
    if (!arr?.length) return
    const isTop = (arr[0].side || 'top') === 'top'
    const laneDist = LANES[idx % 3]
    const maxChars = arr.length > 1 ? 14 : 16
    const maxLines = Math.max(...arr.map((x: any) => splitTextByChars(x.text, maxChars).length))
    const extent = laneDist + estimateGroupExtent(arr.length, maxLines)
    if (isTop) needTop = Math.max(needTop, extent)
    else needBot = Math.max(needBot, extent)
  })
  return { needTop: Math.max(needTop, 120), needBot: Math.max(needBot, 120) }
}

// 0~5000ms 自适应 X
function computeXPositionsAdaptiveByTime(times: number[], x0: number, minGap = 70, extraSpan = 110, maxMs = 5000) {
  const n = times.length
  if (n === 0) return []
  if (n === 1) return [x0]
  const sorted = [...times].filter((t) => Number.isFinite(t)).sort((a, b) => a - b)
  if (sorted.length <= 1) return times.map(() => x0)

  const deltas: number[] = []
  for (let i = 1; i < sorted.length; i++) deltas.push(Math.max(0, sorted[i] - sorted[i - 1]))
  const weights = deltas.map((d) => Math.sqrt(Math.min(1, d / maxMs)))
  const segs = weights.map((w) => minGap + extraSpan * w)

  const xsSorted: number[] = new Array(sorted.length)
  xsSorted[0] = x0
  for (let i = 1; i < sorted.length; i++) xsSorted[i] = xsSorted[i - 1] + segs[i - 1]

  const map = new Map<number, number>()
  for (let i = 0; i < sorted.length; i++) map.set(sorted[i], xsSorted[i])
  return times.map((t) => map.get(t) ?? x0)
}

function drawLabelGroup(cx: number, axisY: number, items: any[], isTop: boolean, idx: number) {
  if (!seqSvgRef.value) return

  const dir = isTop ? -1 : 1
  const laneDist = LANES[idx % 3]

  // 45°：dx=dy，统一朝右倾斜
  const anchorX = cx + laneDist
  const anchorY = axisY + dir * laneDist

  const g = el('g', {})
  seqSvgRef.value.appendChild(g)

  const groupColor = items[0]?.color || '#1677ff'

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

  const ys: number[] = []
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

const renderSequenceDiagram = () => {
  if (!seqSvgRef.value || !seqContainerRef.value) return

  const checkedKeys = seqTreeRef.value?.getCheckedKeys() || defaultCheckedKeys.value
  const selectedGuids = checkedKeys.filter((k: string) => loadedTripInfos.has(k))

  clearSvg()

  if (selectedGuids.length === 0) {
    setViewBox(1400, 320)
    seqSvgRef.value.appendChild(el('text', { x: 60, y: 100, fill: '#909399', 'font-size': '14', 'font-weight': '700' }, '请在左侧勾选要显示的文件'))
    return
  }

  const selectedMap = new Map<string, any[]>()
  for (const guid of selectedGuids) {
    const info = loadedTripInfos.get(guid)
    if (info) selectedMap.set(guid, info)
  }

  setTimeout(() => {
    if (!seqContainerRef.value || !seqSvgRef.value) return

    const nodes = buildNodesFromTripInfoMap(selectedMap)
    const times = nodes.map((n: any) => n.timeMs)

    const minGap = 70
    const extraSpan = 110
    const maxMs = 5000

    const xLeftPad = 60
    const xRightPad = 220
    const axisExtra = 120

    const xsTemp = computeXPositionsAdaptiveByTime(times, xLeftPad + 40, minGap, extraSpan, maxMs)
    const contentEndX = xsTemp.length ? Math.max(...xsTemp) : xLeftPad + 400
    const requiredW = Math.ceil(contentEndX + xRightPad + axisExtra)

    const box = seqContainerRef.value.getBoundingClientRect()
    const W = Math.max(1400, Math.floor(box.width), requiredW)

    const { needTop, needBot } = estimateVerticalNeeds(nodes)
    const topPad = 80
    const botPad = 80
    const axisY = topPad + needTop
    const H = axisY + needBot + botPad

    setViewBox(W, H)

    const axisStart = xLeftPad
    const axisEnd = W - xRightPad

    // 主轴
    seqSvgRef.value!.appendChild(
      el('line', {
        x1: axisStart,
        y1: axisY,
        x2: axisEnd,
        y2: axisY,
        stroke: '#cfe6ff',
        'stroke-width': 10,
        'stroke-linecap': 'round'
      })
    )

    // 箭头
    const headLen = 46
    const headHalf = 18
    const tipX = axisEnd + headLen
    seqSvgRef.value!.appendChild(
      el('polygon', {
        points: `${tipX},${axisY} ${axisEnd},${axisY - headHalf} ${axisEnd},${axisY + headHalf}`,
        fill: '#9dc5f8',
        opacity: 0.95
      })
    )

    // 单位
    seqSvgRef.value!.appendChild(
      el('text', { x: tipX + 14, y: axisY + 6, fill: '#6b7280', 'font-weight': '700', 'font-size': '12' }, '时间/ms')
    )

    const xs = computeXPositionsAdaptiveByTime(times, axisStart + 50, minGap, extraSpan, maxMs)

    nodes.forEach((node: any, idx: number) => {
      const cx = xs[idx]
      seqSvgRef.value!.appendChild(el('circle', { cx, cy: axisY, r: 18, fill: '#1677ff', opacity: 0.92 }))
      seqSvgRef.value!.appendChild(el('circle', { cx, cy: axisY, r: 22, fill: 'none', stroke: '#93c5fd', 'stroke-width': 6, opacity: 0.22 }))

      seqSvgRef.value!.appendChild(
        el('text', { x: cx, y: axisY + 5, 'text-anchor': 'middle', fill: '#fff', 'font-size': '14', 'font-weight': '800' }, String(node.timeMs))
      )

      const arr = node.labels
      if (arr?.length) {
        const isTop = (arr[0].side || 'top') === 'top'
        drawLabelGroup(cx, axisY, arr, isTop, idx)
      }
    })
  }, 60)
}
</script>

<style scoped>
.seq-layout {
  display: flex;
  height: 600px;
  border: 1px solid #e5e7eb;
  border-radius: 10px;
  overflow: hidden;
  background: #fff;
}

.seq-sidebar {
  width: 260px;
  background: linear-gradient(180deg, #fcfcfc 0%, #f9fafb 100%);
  border-right: 1px solid #e5e7eb;
  display: flex;
  flex-direction: column;
  padding: 12px;
}

.sidebar-actions {
  margin-bottom: 10px;
  display: flex;
  justify-content: flex-end;
}
.sidebar-actions .el-button {
  width: 100%;
}

.seq-sidebar .el-tree {
  flex: 1;
  overflow-y: auto;
  background: transparent;
}

.seq-content {
  flex: 1;
  background-color: #fff;
  overflow: hidden;
  position: relative;
  display: flex;
  flex-direction: column;
}

/* 顶栏：图例 + 缩放按钮 */
.seq-topbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  padding: 10px 12px;
  border-bottom: 1px solid #eef2f7;
  background: #ffffff;
}

.seq-legend {
  display: flex;
  flex-wrap: wrap;
  gap: 14px;
  min-width: 0;
}

.legend-item {
  display: flex;
  align-items: center;
  font-size: 12px;
  color: #374151;
  max-width: 420px;
}

.legend-dot {
  width: 10px;
  height: 10px;
  border-radius: 999px;
  margin-right: 8px;
  box-shadow: 0 0 0 4px rgba(22, 119, 255, 0.12);
}

.legend-text {
  max-width: 360px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  font-weight: 700;
}

.seq-zoom-tools {
  flex-shrink: 0;
}
.zoom-btn {
  font-weight: 700;
}

.zoom-pop {
  padding: 6px 2px;
}
.zoom-row {
  display: flex;
  align-items: center;
  gap: 10px;
}
.zoom-actions {
  margin-top: 10px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
}
.zoom-hint {
  font-size: 12px;
  color: #6b7280;
  white-space: nowrap;
}

/* 主容器：滚动 + 平移 */
.seq-container {
  position: relative;
  width: 100%;
  height: 100%;
  background: #ffffff;
  overflow: auto;
  /* 平移体验：用户按住中键时，提示可拖拽 */
  cursor: default;
}
.seq-container:active {
  cursor: default;
}

/* 左上角固定信息 */
.canvas-info {
  position: sticky; /* 跟随滚动条保持在左上角视口（容器内） */
  top: 12px;
  left: 12px;
  z-index: 20;
  width: fit-content;
  padding: 8px 10px;
  background: rgba(255, 255, 255, 0.92);
  border: 1px solid rgba(229, 231, 235, 0.9);
  border-radius: 10px;
  box-shadow: 0 8px 24px rgba(17, 24, 39, 0.08);
  backdrop-filter: blur(6px);
}

.fault-time {
  font-size: 14px;
  font-weight: 900;
  color: #111827;
  white-space: nowrap;
}

.sub-info {
  font-size: 12px;
  color: #6b7280;
  margin-top: 4px;
  font-weight: 700;
}

/* 缩放舞台 */
.seq-stage {
  display: inline-block;
  will-change: transform;
}

/* 平移状态：按住滚轮拖拽 */
.seq-stage.panning,
.seq-container .panning {
  cursor: grabbing;
}
.seq-container.panning {
  cursor: grabbing;
}

.seq-svg {
  display: block;
  min-width: 1400px;
}

.sequence-dialog :deep(.el-dialog__body) {
  padding: 20px;
  background-color: #f8fafc;
}

:deep(.is-leaf-node .el-checkbox) {
  display: none;
}

.custom-tree-node {
  font-size: 13px;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  display: inline-block;
  width: 100%;
}

.dot-indicator {
  display: inline-block;
  width: 8px;
  height: 8px;
  border-radius: 50%;
  margin-right: 8px;
  vertical-align: middle;
}

.file-select-header {
  display: flex;
  gap: 10px;
  margin-bottom: 20px;
}
.file-table {
  margin-bottom: 20px;
}
.pagination-container {
  display: flex;
  justify-content: flex-end;
}
</style>