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
          <el-button type="primary" size="small" :icon="Plus" @click="openFileSelectDialog">
            添加对比
          </el-button>
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
        <!-- 图例区域 -->
        <div class="seq-legend" v-if="loadedFileNames.size > 0">
          <div v-for="[guid, name] in loadedFileNames" :key="guid" class="legend-item">
            <span class="legend-dot" :style="{ backgroundColor: loadedFileColors.get(guid) }"></span>
            <span class="legend-text" :title="name">{{ name }}</span>
          </div>
        </div>

        <div class="seq-container" ref="seqContainerRef">
          <div class="left-info" :style="{ top: leftInfoTop + 'px' }">
            <div class="fault-time">{{ currentMainFaultTime }}</div>
            <div class="sub-info">故障发生时间</div>
          </div>
          <svg ref="seqSvgRef" class="seq-svg"></svg>
        </div>
      </div>
    </div>
  </el-dialog>

  <!-- 文件选择弹窗 -->
  <el-dialog
    v-model="fileSelectDialogVisible"
    title="选择录波文件"
    width="800px"
    append-to-body
    :close-on-click-modal="false"
  >
    <div class="file-select-header">
      <el-input
        v-model="fileSearchKeyword"
        placeholder="文件名"
        style="width: 200px"
        clearable
        @keyup.enter="fetchFileList"
      />
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

const updateVisible = (val: boolean) => {
  emit('update:visible', val)
}

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
const filePagination = reactive({
  page: 1,
  pageSize: 10,
  total: 0
})

// Caches（用 reactive Map，模板可响应）
const loadedTripInfos = reactive(new Map<string, any[]>())
const loadedFileNames = reactive(new Map<string, string>())
const loadedFileColors = reactive(new Map<string, string>())

// 颜色：尽量贴近截图的柔和配色（主蓝 + 紫 + 橙 + 绿 + 红）
const SERIES_COLORS = ['#1677ff', '#b56be6', '#f6a04b', '#52c41a', '#ff4d4f']

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

  // 已初始化则直接重绘
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

  const fileColor = SERIES_COLORS[0]
  loadedFileColors.set(guid, fileColor)

  const rootId = guid
  const tripNodes = tripInfo.map((item: any, index: number) => {
    return {
      label: formatTripNodeLabel(item),
      id: `${rootId}-trip-${index}`,
      disabled: true,
      color: fileColor
    }
  })

  seqTreeData.value = [
    {
      label: fileName,
      id: rootId,
      children: [{ label: `启动时间: ${startTime}`, id: `${rootId}-start-time`, disabled: true }, ...tripNodes]
    }
  ]

  defaultCheckedKeys.value = [rootId]

  await nextTick()
  requestAnimationFrame(() => {
    requestAnimationFrame(() => {
      renderSequenceDiagram()
    })
  })
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

      const tripNodes = tripInfo.map((item: any, index: number) => {
        return {
          label: formatTripNodeLabel(item),
          id: `${guid}-trip-${index}`,
          disabled: true,
          color: fileColor
        }
      })

      const newNode = {
        label: fileName,
        id: guid,
        children: [{ label: `启动时间: ${startTime}`, id: `${guid}-start-time`, disabled: true }, ...tripNodes]
      }
      seqTreeData.value = [...seqTreeData.value, newNode]

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
  ;(window as any)._seqRenderTimer = setTimeout(() => {
    renderSequenceDiagram()
  }, 80)
}

const formatTripNodeLabel = (item: any) => {
  const valText = item.value === '1' ? '动作' : item.value === '0' ? '复归' : item.value
  let timeStr = String(item.time || '').trim()
  const match = timeStr.match(/^([0-9.]+)/)
  if (match) {
    const num = parseFloat(match[1])
    timeStr = String(num)
  }
  return `${timeStr}ms ${item.name} ${valText}`
}

// =========================
// SVG 绘制
// =========================
const seqSvgRef = ref<SVGElement | null>(null)
const seqContainerRef = ref<HTMLElement | null>(null)
const leftInfoTop = ref(0)

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
  if (seqSvgRef.value) {
    while (seqSvgRef.value.firstChild) seqSvgRef.value.removeChild(seqSvgRef.value.firstChild)
  }
}

function setViewBox(W: number, H: number) {
  if (seqSvgRef.value) {
    seqSvgRef.value.setAttribute('viewBox', `0 0 ${W} ${H}`)
    ;(seqSvgRef.value as any).style.height = `${H}px`
  }
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
  let i = 0
  while (i < s.length) {
    out.push(s.slice(i, i + maxChars))
    i += maxChars
  }
  return out
}

function appendWrappedText(
  parentG: Element,
  x: number,
  y: number,
  anchor: string,
  fill: string,
  text: string,
  maxChars = 16,
  lineHeight = 16
) {
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
      const txt = `${r.name} ${valText}`
      node.labels.push({ text: txt, color, guid })
    }
  }

  const nodes = [...map.values()].sort((a, b) => a.timeMs - b.timeMs)

  // 交替上下（更像截图）
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
    if (!arr || !arr.length) return

    const isTop = (arr[0].side || 'top') === 'top'
    const lane = idx % 3
    const laneDist = LANES[lane]

    const maxChars = arr.length > 1 ? 14 : 16
    const maxLines = Math.max(...arr.map((x: any) => splitTextByChars(x.text, maxChars).length))
    const extent = laneDist + estimateGroupExtent(arr.length, maxLines)

    if (isTop) needTop = Math.max(needTop, extent)
    else needBot = Math.max(needBot, extent)
  })

  needTop = Math.max(needTop, 120)
  needBot = Math.max(needBot, 120)
  return { needTop, needBot }
}

/**
 * ✅ 0~5000ms 自适应 X：相邻点至少 minGap，
 *    时间差越大，额外间距越大；并且点多时自动扩展画布宽度
 */
function computeXPositionsAdaptiveByTime(
  times: number[],
  x0: number,
  minGap = 70,
  extraSpan = 110,
  maxMs = 5000
) {
  const n = times.length
  if (n === 0) return []
  if (n === 1) return [x0]

  const sorted = [...times].filter((t) => Number.isFinite(t)).sort((a, b) => a - b)
  if (sorted.length === 0) return new Array(times.length).fill(x0)
  if (sorted.length === 1) {
    const m = new Map<number, number>([[sorted[0], x0]])
    return times.map((t) => m.get(t) ?? x0)
  }

  const deltas: number[] = []
  for (let i = 1; i < sorted.length; i++) {
    deltas.push(Math.max(0, sorted[i] - sorted[i - 1]))
  }

  // 非线性：sqrt(0~1)，小差更紧、大差更松
  const weights = deltas.map((d) => {
    const r = Math.min(1, d / maxMs)
    return Math.sqrt(r)
  })

  const segs = weights.map((w) => minGap + extraSpan * w)

  const xsSorted: number[] = new Array(sorted.length)
  xsSorted[0] = x0
  for (let i = 1; i < sorted.length; i++) xsSorted[i] = xsSorted[i - 1] + segs[i - 1]

  const map = new Map<number, number>()
  for (let i = 0; i < sorted.length; i++) map.set(sorted[i], xsSorted[i])

  return times.map((t) => map.get(t) ?? x0)
}

function drawLabelGroup(cx: number, axisY: number, items: any[], isTop: boolean, _color: string, idx: number) {
  if (!seqSvgRef.value) return

  const dir = isTop ? -1 : 1
  const lane = idx % 3
  const laneDist = LANES[lane]

  // ✅ 45° 斜线：dx = dy（都朝右倾斜，更接近你截图一致性）
  const anchorX = cx + laneDist
  const anchorY = axisY + dir * laneDist

  const g = el('g', {})
  seqSvgRef.value.appendChild(g)

  // 斜线：颜色取该“组”的第一个颜色（更贴近截图：同一组一个主题色）
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

  // 多条同一时刻：竖线串起来（截图里那种“点点竖排”）
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

    // 小点
    g.appendChild(
      el('circle', {
        cx: anchorX,
        cy: y,
        r: 4,
        fill: itemColor,
        opacity: 0.95
      })
    )

    // 文本：统一向右摆放（符合你说“同一个方向”）
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
    seqSvgRef.value.appendChild(
      el('text', { x: 60, y: 100, fill: '#909399', 'font-size': '14', 'font-weight': '700' }, '请在左侧勾选要显示的文件')
    )
    leftInfoTop.value = 160
    return
  }

  const selectedMap = new Map<string, any[]>()
  for (const guid of selectedGuids) {
    const info = loadedTripInfos.get(guid)
    if (info) selectedMap.set(guid, info)
  }

  setTimeout(() => {
    if (!seqContainerRef.value || !seqSvgRef.value) return

    const box = seqContainerRef.value.getBoundingClientRect()

    const nodes = buildNodesFromTripInfoMap(selectedMap)
    const times = nodes.map((n: any) => n.timeMs)

    // ✅ 0~5000ms 参数（你给的范围）
    const minGap = 70
    const extraSpan = 110
    const maxMs = 5000

    // ✅ 留白与截图更一致
    const xLeftPad = 260
    const xRightPad = 220
    const axisExtra = 120

    // 先估算“需要的内容宽度”，点多就扩宽，避免挤
    const xsTemp = computeXPositionsAdaptiveByTime(times, xLeftPad + 40, minGap, extraSpan, maxMs)
    const contentEndX = xsTemp.length ? Math.max(...xsTemp) : xLeftPad + 400
    const requiredW = Math.ceil(contentEndX + xRightPad + axisExtra)

    // ✅ 最终宽度：容器宽度不足就扩展（横向滚动）
    const W = Math.max(1400, Math.floor(box.width), requiredW)

    const { needTop, needBot } = estimateVerticalNeeds(nodes)
    const topPad = 80
    const botPad = 80
    const axisY = topPad + needTop
    leftInfoTop.value = axisY
    const H = axisY + needBot + botPad

    setViewBox(W, H)

    const axisStart = xLeftPad
    const axisEnd = W - xRightPad

    // 主轴：淡蓝粗线（接近截图）
    const axisColor = '#cfe6ff'
    const axisStroke = 10
    seqSvgRef.value!.appendChild(
      el('line', {
        x1: axisStart,
        y1: axisY,
        x2: axisEnd,
        y2: axisY,
        stroke: axisColor,
        'stroke-width': axisStroke,
        'stroke-linecap': 'round'
      })
    )

    // 箭头：淡蓝填充
    const headLen = 46
    const headHalf = 18
    const tipX = axisEnd + headLen
    const headColor = '#9dc5f8'
    seqSvgRef.value!.appendChild(
      el('polygon', {
        points: `${tipX},${axisY} ${axisEnd},${axisY - headHalf} ${axisEnd},${axisY + headHalf}`,
        fill: headColor,
        opacity: 0.95
      })
    )

    // 右侧单位
    seqSvgRef.value!.appendChild(
      el(
        'text',
        {
          x: tipX + 14,
          y: axisY + 6,
          fill: '#6b7280',
          'font-weight': '700',
          'font-size': '12'
        },
        '时间/ms'
      )
    )

    // ✅ X 位置：按 0~5000ms 自适应（最关键）
    const xs = computeXPositionsAdaptiveByTime(times, axisStart + 50, minGap, extraSpan, maxMs)

    // 圆点：蓝色实心圆 + 白字时间（贴近截图）
    nodes.forEach((node: any, idx: number) => {
      const cx = xs[idx]

      // 该时间点如果有多文件合并，圆点固定蓝色（截图圆点统一蓝）
      const dotFill = '#1677ff'

      // 圆点
      seqSvgRef.value!.appendChild(
        el('circle', {
          cx,
          cy: axisY,
          r: 18,
          fill: dotFill,
          opacity: 0.92
        })
      )

      // 圆点外发光（淡）
      seqSvgRef.value!.appendChild(
        el('circle', {
          cx,
          cy: axisY,
          r: 22,
          fill: 'none',
          stroke: '#93c5fd',
          'stroke-width': 6,
          opacity: 0.22
        })
      )

      // 圆点内白字（时间）
      seqSvgRef.value!.appendChild(
        el(
          'text',
          {
            x: cx,
            y: axisY + 5,
            'text-anchor': 'middle',
            fill: '#ffffff',
            'font-size': '14',
            'font-weight': '800'
          },
          String(node.timeMs)
        )
      )

      // label 组：交替上下，45°，颜色跟随文件
      const arr = node.labels
      if (arr && arr.length) {
        const isTop = (arr[0].side || 'top') === 'top'
        drawLabelGroup(cx, axisY, arr, isTop, arr[0]?.color || '#1677ff', idx)
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

/* 图例：贴近截图的“顶栏图例” */
.seq-legend {
  display: flex;
  flex-wrap: wrap;
  gap: 14px;
  padding: 12px 18px;
  border-bottom: 1px solid #eef2f7;
  background: #ffffff;
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

.seq-container {
  position: relative;
  width: 100%;
  height: 100%;
  background: #ffffff;
  overflow: auto;
}

/* svg 默认宽度，实际会根据计算扩宽 */
.seq-svg {
  display: block;
  min-width: 1400px;
}

/* 左侧故障发生时间：更贴近截图样式 */
.left-info {
  position: absolute;
  left: 22px;
  transform: translateY(-50%);
  pointer-events: none;
  z-index: 10;
  padding-left: 10px;
  border-left: 4px solid rgba(22, 119, 255, 0.35);
}

.fault-time {
  font-size: 16px;
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

.sequence-dialog :deep(.el-dialog__body) {
  padding: 20px;
  background-color: #f8fafc;
}

/* 树叶子节点：不显示 checkbox */
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

/* 叶子节点前的颜色点 */
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