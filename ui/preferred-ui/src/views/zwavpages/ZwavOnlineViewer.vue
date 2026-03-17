<template>
  <div class="viewer-container" v-loading="loading">
    <div class="header">
      <div class="title">
        <h2>录波在线浏览</h2>
        <span class="file-name" v-if="detailData?.file">{{ detailData.file.originalName }}</span>
        <span class="sub-title" v-if="cfgData">({{ cfgData.stationName }} - {{ cfgData.deviceId }})</span>
      </div>
    </div>

    <div class="content">
      <!-- 左侧波形显示 -->
      <div class="left-panel">
        <ZwavToolbar
          :wave-loading="waveLoading"
          :has-stats="channelStats.length > 0"
          :can-open-sequence="canOpenSequence"
          :is-full-screen="isFullScreen"
          @fetch-wave-data="fetchWaveData"
          @handle-export="handleExport"
          @open-axis-settings="showAxisSettingDialog = true"
          @open-stats-dialog="showStatsDialog = true"
          @open-sequence-dialog="showSequenceDialog = true"
          @toggle-full-screen="toggleFullScreen"
        />

        <div class="main-body">
          <ZwavChannelSidebar
            :analog-channels="analogChannels"
            :digital-channels="digitalChannels"
            v-model:selected-channels="selectedChannels"
            v-model:selected-digital-channels="selectedDigitalChannels"
          />

          <!-- 图表区域 -->
          <div class="chart-wrapper">
            <div class="chart-scroll-container">
              <div ref="chartRef" class="chart-content"></div>
            </div>
            <div class="marker-footer" v-if="markerSummary.visible">
              <el-tooltip placement="top" effect="dark">
                <template #content>
                  <div v-for="line in markerDetailLines" :key="line">{{ line }}</div>
                </template>
                <span class="marker-footer-text">
                  LeftLine：[{{ markerSummary.leftXText }}][第{{ markerSummary.leftPointNo }}个点]
                  |
                  RightLine：[{{ markerSummary.rightXText }}][第{{ markerSummary.rightPointNo }}个点]
                  |
                  RightLine - LeftLine：{{ markerSummary.deltaXText }}
                </span>
              </el-tooltip>
              <el-button size="small" link @click="resetMarkers">重置</el-button>
            </div>
          </div>
        </div>

        <ZwavAxisSettingDialog
          v-model:visible="showAxisSettingDialog"
          :grid-height="gridHeightSetting"
          :search-params="{ fromSample: searchFromSample, toSample: searchToSample, limit: searchLimit, downSample: searchDownSample }"
          @apply="handleAxisSettingsApply"
        />

        <ZwavChannelStatsDialog
          v-model:visible="showStatsDialog"
          :stats="channelStats"
        />

        <ZwavSequenceDialog
          v-model:visible="showSequenceDialog"
          :current-analysis-guid="analysisGuid"
          :detail-data="detailData"
          :hdr-data="hdrData"
          :trip-info-array="tripInfoArray"
        />
      </div>

      <!-- 右侧信息显示 -->
      <ZwavRightPanel
        :cfg-data="cfgData"
        :hdr-data="hdrData"
        :trip-info-array="tripInfoArray"
      />
    </div>
  </div>
</template>

<script setup lang="ts">
import { zwavService, type AnalysisDetailDto, type CfgDto, type ChannelDto, type HdrDto, type WaveDataPageDto } from '@/services/zwavService'
import * as echarts from 'echarts'
import { ElMessage, ElMessageBox } from 'element-plus'
import { computed, onMounted, onUnmounted, ref } from 'vue'
import { useRoute } from 'vue-router'

// Components
import ZwavChannelSidebar from './components/ZwavChannelSidebar.vue'
import ZwavRightPanel from './components/ZwavRightPanel.vue'
import ZwavToolbar from './components/ZwavToolbar.vue'
import ZwavAxisSettingDialog from './components/dialogs/ZwavAxisSettingDialog.vue'
import ZwavChannelStatsDialog from './components/dialogs/ZwavChannelStatsDialog.vue'
import ZwavSequenceDialog from './components/dialogs/ZwavSequenceDialog.vue'

const route = useRoute()
const analysisGuid = route.params.guid as string

const loading = ref(false)
const waveLoading = ref(false)
const chartRef = ref<HTMLElement>()
let myChart: echarts.ECharts | null = null

// Data
const analogChannels = ref<ChannelDto[]>([])
const digitalChannels = ref<ChannelDto[]>([])
const selectedChannels = ref<number[]>([]) // Analog selection
const selectedDigitalChannels = ref<number[]>([]) // Digital selection
const cfgData = ref<CfgDto | null>(null)
const hdrData = ref<HdrDto | null>(null)
const detailData = ref<AnalysisDetailDto | null>(null)
const channelStats = ref<{ name: string; max: number; min: number }[]>([])

// Dialog States
const showStatsDialog = ref(false)
const showSequenceDialog = ref(false)
const showAxisSettingDialog = ref(false)
const isFullScreen = ref(false)

// Settings
const gridHeightSetting = ref(50)
const searchFromSample = ref(0)
const searchToSample = ref(10000)
const searchLimit = ref(50000)
const searchDownSample = ref(1)

const lastWaveData = ref<WaveDataPageDto | null>(null)

const markerLeftXPixel = ref<number | null>(null)
const markerRightXPixel = ref<number | null>(null)
const markerDetailLines = ref<string[]>([])
const markerSummary = ref({
  visible: false,
  leftXText: '-',
  rightXText: '-',
  leftPointNo: 0,
  rightPointNo: 0,
  deltaXText: '-'
})

const clamp = (v: number, min: number, max: number) => Math.max(min, Math.min(max, v))

const getGridRect = () => {
  if (!myChart) return null
  const model: any = (myChart as any).getModel?.()
  const grid0 = model?.getComponent?.('grid', 0)
  const rect = grid0?.coordinateSystem?.getRect?.()
  return rect || null
}

const getAllGridYRange = () => {
  if (!myChart) return { minY: 0, maxY: 0 }
  const model: any = (myChart as any).getModel?.()
  const grids: any[] = model?.getComponents?.('grid') || []
  let minY = Infinity
  let maxY = -Infinity
  for (let i = 0; i < grids.length; i++) {
    const rect = grids[i]?.coordinateSystem?.getRect?.()
    if (!rect) continue
    minY = Math.min(minY, rect.y)
    maxY = Math.max(maxY, rect.y + rect.height)
  }
  if (!Number.isFinite(minY) || !Number.isFinite(maxY)) return { minY: 0, maxY: myChart.getHeight() }
  return { minY, maxY }
}

const initMarkersIfNeeded = () => {
  if (!myChart) return
  const rect = getGridRect()
  if (!rect) return
  if (markerLeftXPixel.value === null) markerLeftXPixel.value = rect.x + rect.width * 0.25
  if (markerRightXPixel.value === null) markerRightXPixel.value = rect.x + rect.width * 0.75
}

const getWaveAtXPixel = (xPixel: number) => {
  const data = lastWaveData.value
  if (!myChart || !data || !data.rows || data.rows.length === 0) return null
  const rect = getGridRect()
  if (rect) xPixel = clamp(xPixel, rect.x, rect.x + rect.width)

  const times = data.rows.map((r) => Number(r.timeMs))
  const n = times.length
  const opt: any = myChart.getOption()
  const dz = Array.isArray(opt?.dataZoom) ? opt.dataZoom[0] : null
  const startPct = Number(dz?.start ?? 0)
  const endPct = Number(dz?.end ?? 100)
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

const computeMarkerDetails = (data: WaveDataPageDto, leftIdx: number, rightIdx: number) => {
  const leftRow = data.rows[leftIdx]
  const rightRow = data.rows[rightIdx]
  const lines: string[] = []

  selectedChannels.value.forEach((chIdx) => {
    const chInfo = analogChannels.value.find((c) => c.channelIndex === chIdx)
    const name = chInfo ? `${chInfo.channelIndex}. ${chInfo.channelName}` : `${chIdx}. CH${chIdx}`
    const dataIndex = data.channels ? data.channels.indexOf(chIdx) : -1
    if (dataIndex === -1) return
    const lv = leftRow.analog?.[dataIndex]
    const rv = rightRow.analog?.[dataIndex]
    const ltxt = typeof lv === 'number' && Number.isFinite(lv) ? Number(lv).toFixed(6) : '-'
    const rtxt = typeof rv === 'number' && Number.isFinite(rv) ? Number(rv).toFixed(6) : '-'
    lines.push(`${name}: L=${ltxt}, R=${rtxt}`)
  })

  selectedDigitalChannels.value.forEach((chIdx) => {
    const chInfo = digitalChannels.value.find((c) => c.channelIndex === chIdx)
    const name = chInfo ? `${chInfo.channelIndex}. ${chInfo.channelName}` : `D${chIdx}`
    const dataIndex = data.digitals ? data.digitals.indexOf(chIdx) : -1
    if (dataIndex === -1) return
    const lv = leftRow.digital?.[dataIndex] === 1 ? '1' : '0'
    const rv = rightRow.digital?.[dataIndex] === 1 ? '1' : '0'
    lines.push(`${name}: L=${lv}, R=${rv}`)
  })

  return lines
}

const refreshMarkerSummary = () => {
  const data = lastWaveData.value
  if (!data || !data.rows || data.rows.length === 0) {
    markerSummary.value.visible = false
    markerDetailLines.value = []
    return
  }

  initMarkersIfNeeded()
  if (markerLeftXPixel.value === null || markerRightXPixel.value === null) return

  const leftWave = getWaveAtXPixel(markerLeftXPixel.value)
  const rightWave = getWaveAtXPixel(markerRightXPixel.value)
  if (!leftWave || !rightWave) return

  const leftMs = leftWave.timeMs
  const rightMs = rightWave.timeMs
  const leftNearestIdx = leftWave.frac >= 0.5 ? leftWave.i1 : leftWave.i0
  const rightNearestIdx = rightWave.frac >= 0.5 ? rightWave.i1 : rightWave.i0

  const leftXText = `${Number(leftMs).toFixed(3)}ms`
  const rightXText = `${Number(rightMs).toFixed(3)}ms`
  const deltaXText = `${Math.abs(Number(rightMs) - Number(leftMs)).toFixed(3)}ms`

  markerDetailLines.value = computeMarkerDetails(data, leftWave.i0, rightWave.i0)
  markerSummary.value = {
    visible: true,
    leftXText,
    rightXText,
    leftPointNo: leftNearestIdx + 1,
    rightPointNo: rightNearestIdx + 1,
    deltaXText
  }
}

const updateMarkerByPixelX = (side: 'left' | 'right', xPixel: number) => {
  if (!myChart) return
  const rect = getGridRect()
  if (rect) xPixel = clamp(xPixel, rect.x, rect.x + rect.width)
  if (side === 'left') markerLeftXPixel.value = xPixel
  else markerRightXPixel.value = xPixel
  refreshMarkerSummary()
}

const renderMarkerGraphics = () => {
  const data = lastWaveData.value
  if (!myChart || !data || !data.rows || data.rows.length === 0) return
  initMarkersIfNeeded()
  if (markerLeftXPixel.value === null || markerRightXPixel.value === null) return

  const rect = getGridRect()
  const minX = rect ? rect.x : 0
  const maxX = rect ? rect.x + rect.width : myChart.getWidth()
  const yr = getAllGridYRange()
  const y1 = yr.minY
  const y2 = yr.maxY

  const lx = clamp(Number(markerLeftXPixel.value), minX, maxX)
  const rx = clamp(Number(markerRightXPixel.value), minX, maxX)

  myChart.setOption(
    {
      graphic: [
        {
          id: 'markerLeftLine',
          type: 'line',
          draggable: true,
          cursor: 'ew-resize',
          shape: { x1: lx, y1, x2: lx, y2 },
          style: { stroke: '#E6A23C', lineWidth: 1 },
          ondrag: function (this: any) {
            const rect2 = getGridRect()
            const minX2 = rect2 ? rect2.x : 0
            const maxX2 = rect2 ? rect2.x + rect2.width : myChart?.getWidth?.() || 0
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
          ondragend: function (this: any) {
            const rect2 = getGridRect()
            const minX2 = rect2 ? rect2.x : 0
            const maxX2 = rect2 ? rect2.x + rect2.width : myChart?.getWidth?.() || 0
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
          draggable: true,
          cursor: 'ew-resize',
          shape: { x1: rx, y1, x2: rx, y2 },
          style: { stroke: '#409EFF', lineWidth: 1 },
          ondrag: function (this: any) {
            const rect2 = getGridRect()
            const minX2 = rect2 ? rect2.x : 0
            const maxX2 = rect2 ? rect2.x + rect2.width : myChart?.getWidth?.() || 0
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
          ondragend: function (this: any) {
            const rect2 = getGridRect()
            const minX2 = rect2 ? rect2.x : 0
            const maxX2 = rect2 ? rect2.x + rect2.width : myChart?.getWidth?.() || 0
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

const resetMarkers = () => {
  markerLeftXPixel.value = null
  markerRightXPixel.value = null
  initMarkersIfNeeded()
  refreshMarkerSummary()
  renderMarkerGraphics()
}

/** Trip Info Parsing */
const tripInfoArray = computed<any[]>(() => {
  const t: any = hdrData.value?.tripInfoJSON
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
})

/** Can Open Sequence Logic */
const canOpenSequence = computed(() => tripInfoArray.value.length > 0)

const handleAxisSettingsApply = (height: number, params: any) => {
  gridHeightSetting.value = height
  searchFromSample.value = params.fromSample
  searchToSample.value = params.toSample
  searchLimit.value = params.limit
  searchDownSample.value = params.downSample
  fetchWaveData()
}

const toggleFullScreen = () => {
  const el = document.querySelector('.left-panel') as any
  if (!el) return

  if (!document.fullscreenElement) {
    el.requestFullscreen().catch((err: any) => {
      ElMessage.error(`无法进入全屏模式: ${err.message}`)
    })
  } else {
    document.exitFullscreen()
  }
}

const onFullScreenChange = () => {
  isFullScreen.value = !!document.fullscreenElement
}

onMounted(async () => {
  document.addEventListener('fullscreenchange', onFullScreenChange)

  if (!analysisGuid) {
    ElMessage.error('缺少任务ID')
    return
  }

  loading.value = true
  try {
    const [analogRes, digitalRes, cfgRes, hdrRes, detailRes] = await Promise.all([
      zwavService.getChannels(analysisGuid, 'Analog', true),
      zwavService.getChannels(analysisGuid, 'Digital', true),
      zwavService.getCfg(analysisGuid, true),
      zwavService.getHdr(analysisGuid).catch(() => ({ success: true, data: null } as any)),
      zwavService.getDetail(analysisGuid).catch(() => ({ success: true, data: null } as any))
    ])

    if (analogRes.success) {
      analogChannels.value = analogRes.data || []
      selectedChannels.value = analogChannels.value.slice(0, 3).map((c) => c.channelIndex)
    }
    if (digitalRes.success) {
      digitalChannels.value = digitalRes.data || []
    }
    if (cfgRes.success) cfgData.value = cfgRes.data
    if ((hdrRes as any)?.success) hdrData.value = (hdrRes as any).data
    if ((detailRes as any)?.success) detailData.value = (detailRes as any).data

    initChart()
    await fetchWaveData()
  } catch (err) {
    console.error(err)
    ElMessage.error('初始化数据失败')
  } finally {
    loading.value = false
  }

  window.addEventListener('resize', handleResize)
})

onUnmounted(() => {
  myChart?.dispose()
  window.removeEventListener('resize', handleResize)
  document.removeEventListener('fullscreenchange', onFullScreenChange)
})

const handleResize = () => {
  myChart?.resize()
  refreshMarkerSummary()
  renderMarkerGraphics()
}

const initChart = () => {
  if (!chartRef.value) return
  myChart = echarts.init(chartRef.value)
  myChart.setOption({
    tooltip: {
      trigger: 'axis',
      confine: true,
      axisPointer: { type: 'cross' }
    },
    grid: { left: '3%', right: '4%', bottom: '3%', containLabel: true },
    xAxis: { type: 'category', boundaryGap: false, data: [] },
    yAxis: { type: 'value', scale: true },
    series: []
  })
}

const fetchWaveData = async () => {
  if (selectedChannels.value.length === 0 && selectedDigitalChannels.value.length === 0) {
    ElMessage.warning('请至少选择一个通道')
    return
  }

  myChart?.showLoading()
  waveLoading.value = true
  try {
    const channelsStr = selectedChannels.value.join(',')
    const digitalsStr = selectedDigitalChannels.value.join(',')

    const res: any = await zwavService.getWaveData(analysisGuid, {
      channels: channelsStr,
      digitals: digitalsStr,
      fromSample: searchFromSample.value,
      toSample: searchToSample.value,
      limit: searchLimit.value,
      downSample: searchDownSample.value
    })

    if (res.success && res.data) {
      lastWaveData.value = res.data
      updateChart(res.data)
    } else {
      ElMessage.error(res.message || '获取波形数据失败')
    }
  } catch (err) {
    console.error(err)
    ElMessage.error('获取波形数据异常')
  } finally {
    myChart?.hideLoading()
    waveLoading.value = false
  }
}

const handleExport = () => {
  ElMessageBox.confirm('确定要导出当前分析的波形数据吗？文件可能较大，请耐心等待。', '导出确认', {
    confirmButtonText: '确定',
    cancelButtonText: '取消',
    type: 'warning'
  })
    .then(async () => {
      try {
        loading.value = true
        const res = await zwavService.exportWaveData(analysisGuid)
        const blob = new Blob([res.data], { type: 'text/csv;charset=utf-8;' })
        const link = document.createElement('a')
        const url = window.URL.createObjectURL(blob)
        link.href = url
        const contentDisposition = (res as any).headers?.['content-disposition']
        let fileName = 'wave_data.csv'
        if (contentDisposition) {
          const fileNameMatch = contentDisposition.match(/filename[^;=\n]*=((['"]).*?\2|[^;\n]*)/)
          if (fileNameMatch != null && fileNameMatch[1]) fileName = fileNameMatch[1].replace(/['"]/g, '')
        }
        if (fileName === 'wave_data.csv' && detailData.value?.file?.originalName) {
          fileName = `${detailData.value.file.originalName}.csv`
        }

        link.setAttribute('download', fileName)
        document.body.appendChild(link)
        link.click()
        document.body.removeChild(link)
        window.URL.revokeObjectURL(url)

        ElMessage.success('导出成功')
      } catch (err) {
        console.error(err)
        ElMessage.error('导出失败')
      } finally {
        loading.value = false
      }
    })
    .catch(() => {})
}

const updateChart = (data: WaveDataPageDto) => {
  if (!myChart || !chartRef.value) return
  lastWaveData.value = data

  const xAxisData = data.rows.map((r) => r.timeMs)
  const stats: { name: string; max: number; min: number }[] = []

  type SeriesItem = {
    name: string
    isDigital: boolean
    unit?: string
    analogData?: number[]
    digitalLine0?: Array<number | null>
    digitalArea1?: Array<number | null>
  }

  const seriesItems: SeriesItem[] = []

  // ---------- Analog ----------
  selectedChannels.value.forEach((chIdx) => {
    const chInfo = analogChannels.value.find((c) => c.channelIndex === chIdx)
    const name = chInfo ? `${chInfo.channelIndex}. ${chInfo.channelName}` : `${chIdx}. CH${chIdx}`
    const unit = chInfo?.unit || 'A'

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

  // ---------- Digital ----------
  selectedDigitalChannels.value.forEach((chIdx) => {
    const chInfo = digitalChannels.value.find((c) => c.channelIndex === chIdx)
    const name = chInfo ? `${chInfo.channelIndex}. ${chInfo.channelName}` : `D${chIdx}`

    const dataIndex = data.digitals ? data.digitals.indexOf(chIdx) : -1
    if (dataIndex === -1) return

    const raw = data.rows.map((r) => {
      const v = r.digital && r.digital[dataIndex] !== undefined ? r.digital[dataIndex] : 0
      return v === 1 ? 1 : 0
    })

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

  channelStats.value = stats

  // ---------- 动态 Grid ----------
  const count = seriesItems.length
  const grids: any[] = []
  const xAxes: any[] = []
  const yAxes: any[] = []
  const series: any[] = []

  const topMargin = 40
  const bottomMargin = 20
  const gap = 10
  const gridHeight = gridHeightSetting.value

  const totalChartHeight = topMargin + count * (gridHeight + gap) + bottomMargin
  chartRef.value.style.height = `${totalChartHeight}px`
  myChart.resize()

  const DIGITAL_GREEN = '#67C23A'
  const DIGITAL_RED_AREA = 'rgba(245,108,108,0.35)'

  const tooltipFormatter = (params: any[]) => {
    if (!params || params.length === 0) return ''

    const xVal = params[0].axisValue
    const xText =
      typeof xVal === 'number' && Number.isFinite(xVal) ? `${Number(xVal).toFixed(3)}ms` : `${xVal}`

    let html = `<div>时间(ms): ${xText}</div>`
    const seen = new Set<string>()

    for (const p of params) {
      const sName: string = p.seriesName || ''
      const baseName = sName.endsWith('-0') || sName.endsWith('-1') ? sName.slice(0, -2) : sName

      if (seen.has(baseName)) continue

      if (sName.endsWith('-0') || sName.endsWith('-1')) {
        const related = params.filter((x) => (x.seriesName || '').startsWith(baseName))
        const hasOne = related.some((x) => Number(x.value) === 1)
        html += `<div>${baseName}: ${hasOne ? '1(投入)' : '0(复归)'}</div>`
        seen.add(baseName)
        continue
      }

      const val = p.value
      if (val === null || val === undefined) {
        seen.add(baseName)
        continue
      }
      const num = Number(val)
      const vText = Number.isFinite(num) ? num.toFixed(4) : `${val}`
      html += `<div style="color:${p.color}">${baseName}: ${vText}</div>`
      seen.add(baseName)
    }

    return html
  }

  seriesItems.forEach((item, idx) => {
    const top = topMargin + idx * (gridHeight + gap)

    grids.push({
      left: 160,
      right: '4%',
      top,
      height: gridHeight,
      containLabel: false
    })

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
      nameTextStyle: {
        align: 'right',
        color: '#333',
        fontWeight: 'bold',
        fontSize: 12,
        width: 130,
        overflow: 'break',
        lineHeight: 14
      },
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
        markLine: {
          symbol: 'none',
          silent: true,
          data: [{ yAxis: 0 }],
          lineStyle: { color: '#ccc', type: 'solid', width: 1 }
        }
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
      emphasis: { disabled: true }
    })
  })

  const option: any = {
    tooltip: {
      trigger: 'axis',
      confine: true,
      axisPointer: {
        type: 'cross',
        link: { xAxisIndex: 'all' }
      },
      formatter: tooltipFormatter
    },
    axisPointer: { link: { xAxisIndex: 'all' } },
    grid: grids,
    xAxis: xAxes,
    yAxis: yAxes,
    dataZoom: [
      {
        type: 'inside',
        xAxisIndex: xAxes.map((_: any, i: number) => i),
        start: 0,
        end: 100
      }
    ],
    series
  }

  myChart.setOption(option, true)
  refreshMarkerSummary()
  renderMarkerGraphics()
}
</script>

<style scoped>
.viewer-container {
  display: flex;
  flex-direction: column;
  height: 100vh;
  background-color: #f5f7fa;
}

.header {
  background-color: #fff;
  padding: 15px 20px;
  border-bottom: 1px solid #dcdfe6;
  box-shadow: 0 1px 4px rgba(0, 0, 0, 0.1);
  z-index: 10;
}

.title {
  display: flex;
  align-items: center;
}

.title h2 {
  margin: 0;
  font-size: 18px;
  color: #303133;
}

.file-name {
  margin-left: 20px;
  color: #303133;
  font-size: 16px;
  font-weight: bold;
}

.sub-title {
  margin-left: 10px;
  color: #909399;
  font-size: 14px;
}

.content {
  flex: 1;
  display: flex;
  overflow: hidden;
  padding: 10px;
  gap: 10px;
  background-color: #f5f7fa;
}

.left-panel {
  flex: 4;
  background-color: #fff;
  border-radius: 4px;
  display: flex;
  flex-direction: column;
  padding: 0;
  box-shadow: 0 2px 12px 0 rgba(0, 0, 0, 0.1);
  overflow: hidden;
}

.main-body {
  flex: 1;
  display: flex;
  flex-direction: row;
  overflow: hidden;
}

.chart-wrapper {
  flex: 1;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  padding: 10px;
  background-color: #fff;
}

.chart-scroll-container {
  flex: 1;
  width: 100%;
  overflow-y: auto;
  position: relative;
}

.chart-content {
  width: 100%;
}

.marker-footer {
  margin-top: 8px;
  padding: 2px 8px;
  background: #f5f7fa;
  border: 1px solid #ebeef5;
  border-radius: 4px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 10px;
}

.marker-footer-text {
  font-size: 12px;
  line-height: 16px;
  color: #606266;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  flex: 1;
  min-width: 0;
}
</style>
