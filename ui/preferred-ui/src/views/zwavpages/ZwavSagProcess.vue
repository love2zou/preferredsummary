<template>
  <div class="viewer-container" v-loading="loading">
    <div class="header">
      <div class="title">
        <h2>暂降分析过程</h2>
        <span class="file-name">{{ process?.event?.originalName || '-' }}</span>
        <span class="sub-title">事件ID: {{ eventId }}</span>
      </div>
      <div class="header-actions">
        <el-button size="small" @click="reload" :loading="loading">
          <el-icon><Refresh /></el-icon>
          刷新
        </el-button>
        <el-button size="small" @click="showWaveSettingDialog = true">
          <el-icon><Setting /></el-icon>
          波形设置
        </el-button>
        <el-button size="small" type="primary" :loading="previewing" @click="preview">
          预览计算
        </el-button>
      </div>
    </div>

    <div class="content">
      <!-- 左侧：通道区（基本不动） -->
      <div class="left-panel">
        <ZwavChannelSidebar
          :analog-channels="analogChannels"
          :digital-channels="digitalChannels"
          :show-digital="false"
          v-model:selected-channels="selectedChannels"
          v-model:selected-digital-channels="selectedDigitalChannels"
        />
      </div>

      <!-- 右侧：波形 + 容忍度曲线 + 暂降清单 -->
      <div class="right-panel">
        <div class="charts-row">
          <div
            ref="rawCardWrapperRef"
            class="raw-card-wrapper"
            :class="{ 'is-window-fullscreen': isRawFullScreen }"
          >
            <el-card shadow="never" class="chart-card raw-card">
              <template #header>
                <div class="card-header">
                  <div class="card-header-left">
                    <span>原始波形</span>
                    <span class="card-sub">
                      参考电压：{{ getRefVoltageText() }} V，
                      暂降阈值：{{ formatPercent(params.sagThresholdPct) }}%，
                      中断阈值：{{ formatPercent(params.interruptThresholdPct) }}%，
                      迟滞：{{ formatPercent(params.hysteresisPct) }}%，
                      最小持续：{{ formatMs(params.minDurationMs) }} ms
                    </span>
                  </div>
                  <div class="card-header-right">
                    <el-tooltip content="事件详情" placement="top">
                      <el-button link class="icon-btn" @click="showEventDetailDialog = true">
                        <el-icon><Tickets /></el-icon>
                      </el-button>
                    </el-tooltip>
                    <el-tooltip :content="isRawFullScreen ? '退出全屏' : '全屏'" placement="top">
                      <el-button link class="icon-btn" @click="toggleRawFullScreen">
                        <el-icon><FullScreen /></el-icon>
                      </el-button>
                    </el-tooltip>
                  </div>
                </div>
              </template>

              <div class="raw-legend">
                <div class="raw-legend-channels">
                  <span
                    v-for="c in rawLegendChannels"
                    :key="c.key"
                    class="raw-legend-item"
                    :class="{ 'is-hidden': c.hidden }"
                    @click.stop.prevent="toggleLegendChannel(c.key)"
                  >
                    <span class="legend-line" :style="{ background: c.color }"></span>
                    <span class="legend-text">{{ c.label }}</span>
                  </span>
                </div>
                <div class="raw-legend-meta">
                  <span class="raw-legend-item">
                    <span class="legend-dash dash-sag"></span>
                    <span class="legend-text">暂降阈值</span>
                  </span>
                  <span class="raw-legend-item">
                    <span class="legend-dash dash-recover"></span>
                    <span class="legend-text">恢复阈值</span>
                  </span>
                  <span class="raw-legend-item">
                    <span class="legend-dash dash-interrupt"></span>
                    <span class="legend-text">中断阈值</span>
                  </span>
                  <span class="raw-legend-item">
                    <span class="legend-area"></span>
                    <span class="legend-text">暂降区间</span>
                  </span>
                  <span class="raw-legend-item">
                    <span class="legend-vline"></span>
                    <span class="legend-text">开始/结束</span>
                  </span>
                  <span class="raw-legend-item">
                    <span class="legend-point"></span>
                    <span class="legend-text">关键点</span>
                  </span>
                </div>
              </div>

              <div class="chart-scroll-container">
                <div ref="rawChartRef" class="raw-chart-content" :style="{ height: `${rawChartHeight}px` }"></div>
              </div>

              <div class="marker-footer" v-if="markerSummary.visible">
                <el-tooltip placement="top" effect="dark">
                  <template #content>
                    <div v-for="line in markerDetailLines" :key="line">{{ line }}</div>
                  </template>
                  <span class="marker-footer-text">
                    LeftLine：[{{ markerSummary.leftXText }}][第{{ markerSummary.leftPointNo }}个点] |
                    RightLine：[{{ markerSummary.rightXText }}][第{{ markerSummary.rightPointNo }}个点] |
                    RightLine - LeftLine：{{ markerSummary.deltaXText }}
                  </span>
                </el-tooltip>
                <el-button size="small" link @click="resetMarkers">重置</el-button>
              </div>
            </el-card>
          </div>
        </div>

        <div class="bottom-row" :style="{ height: `${bottomRowHeight}px` }">
          <el-card shadow="never" class="chart-card tolerance-card">
            <template #header>
              <div class="card-header">
                <span>容忍度曲线</span>
                <span class="card-sub">清单勾选后打点</span>
              </div>
            </template>

            <div class="tolerance-legend">
              <span class="legend-item curve">标准容忍度折线</span>
              <span v-for="x in tolerancePointLegends" :key="x.key" class="legend-item dyn-point">
                <span class="legend-dot" :style="{ background: x.color }"></span>
                <span class="legend-text">{{ x.label }}</span>
              </span>
            </div>

            <div ref="toleranceChartRef" class="tolerance-chart-content"></div>
          </el-card>

          <el-card shadow="never" class="list-card">
            <template #header>
              <div class="card-header">
                <span>电压暂降清单</span>
                <span class="card-sub">共 {{ sagListRows.length }} 条</span>
              </div>
            </template>

            <div class="list-table-wrap">
              <el-table
                ref="sagTableRef"
                :data="sagListRows"
                border
                stripe
                size="small"
                height="100%"
                :row-key="getSagRowKey"
                @selection-change="handleSagSelectionChange"
              >
                <el-table-column type="selection" width="50" align="center" reserve-selection />
                <el-table-column type="index" label="序号" width="50" align="center" />
                <el-table-column prop="phase" label="相别" width="70" align="center" />
                <el-table-column prop="eventTypeText" label="事件类型" width="100" align="center" />
                <el-table-column prop="occurTimeText" label="发生时间" min-width="180" />
                <el-table-column prop="durationMs" label="持续时间(ms)" width="120" align="left">
                  <template #default="{ row }">{{ formatMs(row.durationMs) }}</template>
                </el-table-column>
                <el-table-column prop="sagMagnitudePct" label="暂降幅值（%）" width="100" align="left">
                  <template #default="{ row }">{{ formatPercent(row.sagMagnitudePct) }}</template>
                </el-table-column>
                <el-table-column prop="residualVoltagePct" label="残余电压(%)" width="100" align="left">
                  <template #default="{ row }">{{ formatPercent(row.residualVoltagePct) }}</template>
                </el-table-column>
              </el-table>
            </div>
          </el-card>
        </div>
      </div>
    </div>

    <!-- 波形设置 -->
    <el-dialog v-model="showWaveSettingDialog" title="波形设置" width="720px">
      <el-form label-width="120px" size="small">
        <el-row :gutter="16">
          <el-col :span="12">
            <el-form-item label="参考电压(V)">
              <div class="param-field">
                <el-input-number v-model="params.referenceVoltage" :min="0" :step="0.01" :precision="2" />
                <div class="param-desc">用于将百分比阈值换算为电压；一般填写额定相电压/线电压对应值。</div>
              </div>
            </el-form-item>
          </el-col>

          <el-col :span="12">
            <el-form-item label="暂降阈值(%)">
              <div class="param-field">
                <el-input-number v-model="params.sagThresholdPct" :min="0" :max="100" :step="1" />
                <div class="param-desc">当电压低于参考电压的该百分比时，判定进入暂降。</div>
              </div>
            </el-form-item>
          </el-col>

          <el-col :span="12">
            <el-form-item label="中断阈值(%)">
              <div class="param-field">
                <el-input-number v-model="params.interruptThresholdPct" :min="0" :max="100" :step="1" />
                <div class="param-desc">当电压低于该百分比时，判定为中断（更严重）。</div>
              </div>
            </el-form-item>
          </el-col>

          <el-col :span="12">
            <el-form-item label="迟滞(%)">
              <div class="param-field">
                <el-input-number v-model="params.hysteresisPct" :min="0" :max="30" :step="0.5" />
                <div class="param-desc">用于抑制阈值附近抖动：恢复阈值 = 暂降阈值 + 迟滞。</div>
              </div>
            </el-form-item>
          </el-col>

          <el-col :span="12">
            <el-form-item label="最小持续(ms)">
              <div class="param-field">
                <el-input-number v-model="params.minDurationMs" :min="0" :step="1" />
                <div class="param-desc">事件持续时间小于该值的暂降/中断将被忽略。</div>
              </div>
            </el-form-item>
          </el-col>

          <el-col :span="12">
            <el-form-item label="波形高度(px)">
              <div class="param-field">
                <el-input-number v-model="gridHeightSetting" :min="80" :max="260" :step="10" />
                <div class="param-desc">原始波形区域的显示高度，数值越大越“高”。</div>
              </div>
            </el-form-item>
          </el-col>

          <el-col :span="12">
            <el-form-item label="FromSample(起始点)">
              <div class="param-field">
                <el-input-number v-model="searchFromSample" :min="0" :step="1" />
                <div class="param-desc">波形拉取起始采样点（包含），用于快速定位局部区间。</div>
              </div>
            </el-form-item>
          </el-col>

          <el-col :span="12">
            <el-form-item label="ToSample(结束点)">
              <div class="param-field">
                <el-input-number v-model="searchToSample" :min="0" :step="1" />
                <div class="param-desc">波形拉取结束采样点（包含）；与起始点配合控制范围。</div>
              </div>
            </el-form-item>
          </el-col>

          <el-col :span="12">
            <el-form-item label="Limit(最大点数)">
              <div class="param-field">
                <el-input-number v-model="searchLimit" :min="1000" :max="50000" :step="1000" />
                <div class="param-desc">单次请求最多返回点数；范围越大加载越慢但细节更完整。</div>
              </div>
            </el-form-item>
          </el-col>

          <el-col :span="12">
            <el-form-item label="DownSample(抽点)">
              <div class="param-field">
                <el-input-number v-model="searchDownSample" :min="1" :step="1" />
                <div class="param-desc">下采样倍率：1 表示不抽点（取全部点）；2 表示每 2 点取 1 点。</div>
              </div>
            </el-form-item>
          </el-col>
        </el-row>
      </el-form>

      <template #footer>
        <el-button @click="showWaveSettingDialog = false">关闭</el-button>
        <el-button type="primary" :loading="waveLoading" @click="applyWaveSettings">应用并刷新</el-button>
      </template>
    </el-dialog>

    <el-dialog v-model="showEventDetailDialog" title="事件详情" width="980px">
      <div v-loading="loading">
        <el-descriptions title="基本信息" :column="2" border>
          <el-descriptions-item label="事件ID">{{ process?.event?.id ?? '-' }}</el-descriptions-item>
          <el-descriptions-item label="录波文件ID">{{ process?.event?.fileId ?? '-' }}</el-descriptions-item>

          <el-descriptions-item label="录波文件名">{{ process?.event?.originalName || '-' }}</el-descriptions-item>
          <el-descriptions-item label="状态">{{ getAnalyzeStatusText(process?.event?.status) }}</el-descriptions-item>

          <el-descriptions-item label="是否暂降">{{ process?.event?.hasSag ? '有' : '无' }}</el-descriptions-item>
          <el-descriptions-item label="事件类型">{{ formatEventType(process?.event?.eventType) }}</el-descriptions-item>

          <el-descriptions-item label="事件数">{{ process?.event?.eventCount ?? '-' }}</el-descriptions-item>
          <el-descriptions-item label="耗时(ms)">{{ process?.event?.costMs ?? '-' }}</el-descriptions-item>

          <el-descriptions-item label="错误信息">{{ process?.event?.errorMessage || '-' }}</el-descriptions-item>
          <el-descriptions-item label="最严重相">{{ process?.event?.worstPhase || '-' }}</el-descriptions-item>

          <el-descriptions-item label="触发相">{{ process?.event?.triggerPhase || '-' }}</el-descriptions-item>
          <el-descriptions-item label="终止相">{{ process?.event?.endPhase || '-' }}</el-descriptions-item>

          <el-descriptions-item label="开始时间">{{ formatUtc(process?.event?.startTimeUtc) }}</el-descriptions-item>
          <el-descriptions-item label="结束时间">{{ formatUtc(process?.event?.endTimeUtc) }}</el-descriptions-item>

          <el-descriptions-item label="发生时间">{{ formatUtc(process?.event?.occurTimeUtc) }}</el-descriptions-item>
          <el-descriptions-item label="持续时间(ms)">{{ formatMs(process?.event?.durationMs as any) }}</el-descriptions-item>

          <el-descriptions-item label="参考电压">{{ formatV(process?.event?.referenceVoltage as any) }}</el-descriptions-item>
          <el-descriptions-item label="残余电压(V)">{{ formatV(process?.event?.residualVoltage as any) }}</el-descriptions-item>

          <el-descriptions-item label="残余电压(%)">{{ formatPercent(process?.event?.residualVoltagePct as any) }}</el-descriptions-item>
          <el-descriptions-item label="暂降幅值(%)">{{ formatPercent(process?.event?.sagPercent as any) }}</el-descriptions-item>
        </el-descriptions>

        <div class="phase-section">
          <h3>各相明细</h3>
          <el-table :data="process?.phases || []" border stripe style="width: 100%">
            <el-table-column prop="phase" label="相别" width="80" />
            <el-table-column prop="startTimeUtc" label="开始时间" width="180">
              <template #default="{ row }">{{ formatUtc(row.startTimeUtc) }}</template>
            </el-table-column>
            <el-table-column prop="endTimeUtc" label="结束时间" width="180">
              <template #default="{ row }">{{ formatUtc(row.endTimeUtc) }}</template>
            </el-table-column>
            <el-table-column prop="durationMs" label="持续时间(ms)" width="130" align="right">
              <template #default="{ row }">{{ formatMs(row.durationMs) }}</template>
            </el-table-column>
            <el-table-column prop="residualVoltage" label="残余电压(V)" width="120" align="right">
              <template #default="{ row }">{{ formatV(row.residualVoltage) }}</template>
            </el-table-column>
            <el-table-column prop="residualVoltagePct" label="残余电压(%)" width="130" align="right">
              <template #default="{ row }">{{ formatPercent(row.residualVoltagePct) }}</template>
            </el-table-column>
            <el-table-column prop="sagPercent" label="暂降幅值(%)" width="120" align="right">
              <template #default="{ row }">{{ formatPercent(row.sagPercent) }}</template>
            </el-table-column>
            <el-table-column prop="isTriggerPhase" label="触发相" width="90" align="center">
              <template #default="{ row }">{{ row.isTriggerPhase ? '是' : '否' }}</template>
            </el-table-column>
            <el-table-column prop="isEndPhase" label="终止相" width="90" align="center">
              <template #default="{ row }">{{ row.isEndPhase ? '是' : '否' }}</template>
            </el-table-column>
            <el-table-column prop="isWorstPhase" label="最严重相" width="100" align="center">
              <template #default="{ row }">{{ row.isWorstPhase ? '是' : '否' }}</template>
            </el-table-column>
          </el-table>
        </div>
      </div>
      <template #footer>
        <el-button @click="showEventDetailDialog = false">关闭</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import {
  zwavSagService,
  type ZwavSagComputedEventDto,
  type ZwavSagMarkerDto,
  type ZwavSagProcessDto
} from '@/services/zwavSagService'
import { zwavService, type ChannelDto, type WaveDataPageDto } from '@/services/zwavService'
import { FullScreen, Refresh, Setting, Tickets } from '@element-plus/icons-vue'
import * as echarts from 'echarts'
import { ElMessage } from 'element-plus'
import { computed, nextTick, onMounted, onUnmounted, reactive, ref, watch } from 'vue'
import { useRoute } from 'vue-router'
import ZwavChannelSidebar from './components/ZwavChannelSidebar.vue'

const route = useRoute()
const eventId = Number(route.params.id)

const loading = ref(false)
const previewing = ref(false)
const waveLoading = ref(false)
const isRawFullScreen = ref(false)

const process = ref<ZwavSagProcessDto | null>(null)
const computedEvents = ref<ZwavSagComputedEventDto[]>([])
const markers = ref<ZwavSagMarkerDto[]>([])

const analogChannels = ref<ChannelDto[]>([])
const digitalChannels = ref<ChannelDto[]>([])
const selectedChannels = ref<number[]>([])
const selectedDigitalChannels = ref<number[]>([])

const lastWaveData = ref<WaveDataPageDto | null>(null)

const params = reactive({
  referenceVoltage: 57.74 as number,
  sagThresholdPct: 90,
  interruptThresholdPct: 10,
  hysteresisPct: 2,
  minDurationMs: 10
})

const showWaveSettingDialog = ref(false)
const showEventDetailDialog = ref(false)
const gridHeightSetting = ref(150)
const searchFromSample = ref(0)
const searchToSample = ref(10000)
const searchLimit = ref(20000)
const searchDownSample = ref(1)

const rawChartRef = ref<HTMLElement | null>(null)
const toleranceChartRef = ref<HTMLElement | null>(null)
const rawCardWrapperRef = ref<HTMLElement | null>(null)

let rawChart: echarts.ECharts | null = null
let toleranceChart: echarts.ECharts | null = null

const rawChartHeight = ref(900)
const analysisGuid = computed(() => process.value?.analysisGuid || '')

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

const formatMs = (v?: number | null) => {
  if (v === undefined || v === null || !Number.isFinite(Number(v))) return '-'
  return Number(v).toFixed(3)
}

const formatPercent = (v?: number | null) => {
  if (v === undefined || v === null || !Number.isFinite(Number(v))) return '-'
  return Number(v).toFixed(3)
}

const formatUtc = (s?: string | null) => {
  if (!s) return '-'
  const d = new Date(s)
  if (Number.isNaN(d.getTime())) return s
  return d.toISOString().replace('T', ' ').slice(0, 19)
}

const formatEventType = (type?: string | null) => {
  if (!type) return '-'
  if (type === 'Normal') return '正常 (Normal)'
  if (type === 'Sag') return '暂降 (Sag)'
  if (type === 'Interruption') return '中断 (Interruption)'
  return type
}

const getAnalyzeStatusText = (status?: number) => {
  if (status === 0) return '待处理'
  if (status === 1) return '处理中'
  if (status === 2) return '成功'
  if (status === 3) return '失败'
  return '-'
}

const formatV = (v?: number | null) => {
  if (v === undefined || v === null || !Number.isFinite(Number(v))) return '-'
  return Number(v).toFixed(3)
}

const clamp = (v: number, min: number, max: number) => Math.max(min, Math.min(max, v))

const toggleRawFullScreen = () => {
  isRawFullScreen.value = !isRawFullScreen.value
  nextTick(() => handleResize())
}

const onKeyDown = (e: KeyboardEvent) => {
  if (e.key !== 'Escape') return
  if (!isRawFullScreen.value) return
  isRawFullScreen.value = false
  nextTick(() => handleResize())
}

const getRefVoltage = () => {
  const local = Number(params.referenceVoltage)
  if (Number.isFinite(local) && local > 0) return local

  const evtRef = Number((process.value as any)?.event?.referenceVoltage)
  if (Number.isFinite(evtRef) && evtRef > 0) return evtRef

  const pointRef = Number(
    (process.value as any)?.rmsPoints?.find((x: any) => Number(x?.referenceVoltage) > 0)?.referenceVoltage
  )
  if (Number.isFinite(pointRef) && pointRef > 0) return pointRef

  return null
}

const getRefVoltageText = () => {
  const refV = getRefVoltage()
  return refV ? refV.toFixed(2) : '-'
}

const RAW_LINE_COLORS = ['#5470C6', '#91CC75', '#FAC858', '#EE6666', '#73C0DE', '#3BA272', '#FC8452', '#9A60B4', '#EA7CCC']

const windowHeight = ref<number>(window.innerHeight)
const hiddenChannelIndices = ref<number[]>([])
const selectedSagRows = ref<any[]>([])
const sagTableRef = ref<any>(null)
const hasUserPickedSagRows = ref(false)
const isApplyingDefaultSagSelection = ref(false)

const defaultSagRowsForTolerance = computed(() => {
  const list = (computedEvents.value || []).slice()
  let maxMag = -Infinity
  for (const evt of list) {
    const mag = Number((evt as any)?.sagMagnitudePct ?? (evt as any)?.sagPercent)
    if (!Number.isFinite(mag)) continue
    if (mag > maxMag) maxMag = mag
  }
  if (!Number.isFinite(maxMag)) return []
  return list.filter((evt: any) => {
    const mag = Number(evt?.sagMagnitudePct ?? evt?.sagPercent)
    return Number.isFinite(mag) && Math.abs(mag - maxMag) < 1e-9
  })
})

const tolerancePointLegends = computed(() => {
  const picked = selectedSagRows.value || []
  const list = hasUserPickedSagRows.value ? picked : picked.length > 0 ? picked : defaultSagRowsForTolerance.value
  return list
    .map((evt: any, idx: number) => {
      const durationMs = Number(evt?.durationMs)
      const x = durationMs / 1000
      const y = Number(evt?.residualVoltagePct)
      if (!Number.isFinite(x) || !Number.isFinite(y) || x < 0) return null

      const phase = String(evt?.phase || '-')
      const mag = Number(evt?.sagMagnitudePct ?? evt?.sagPercent)
      if (!Number.isFinite(mag)) return null
      const magText = formatPercent(mag)
      const label = `${phase}-${magText}%`
      const key = getSagRowKey(evt)
      const color = RAW_LINE_COLORS[idx % RAW_LINE_COLORS.length]

      return {
        key,
        label,
        color,
        point: {
          name: label,
          value: [x, y],
          phase: evt.phase,
          eventTypeText: evt.eventTypeText,
          occurTimeText: evt.occurTimeText,
          residualVoltage: evt.residualVoltage,
          residualVoltagePct: evt.residualVoltagePct,
          sagMagnitudePct: evt.sagMagnitudePct
        }
      }
    })
    .filter(Boolean) as any[]
})

const rawLegendChannels = computed(() => {
  const wave: any = lastWaveData.value
  const selected = selectedChannels.value
    .filter((x) => (wave?.channels || []).includes(x))
    .slice(0, 12)

  return selected.map((channelIndex, idx) => {
    const ch = analogChannels.value.find((c) => c.channelIndex === channelIndex)
    const label = ch ? `${ch.channelIndex}. ${ch.channelName}` : `CH${channelIndex}`
    return {
      key: channelIndex,
      label,
      color: RAW_LINE_COLORS[idx % RAW_LINE_COLORS.length],
      hidden: hiddenChannelIndices.value.includes(channelIndex)
    }
  })
})

const toggleLegendChannel = (channelIndex: number) => {
  const list = hiddenChannelIndices.value.slice()
  const i = list.indexOf(channelIndex)
  if (i >= 0) list.splice(i, 1)
  else list.push(channelIndex)
  hiddenChannelIndices.value = list
  updateRawChart()
}

const getSagRowKey = (row: any) => {
  const phase = String(row?.phase || '')
  const startMs = Number(row?.startMs ?? row?.startTimeMs ?? 0)
  const endMs = Number(row?.endMs ?? row?.endTimeMs ?? 0)
  const kind = String(row?.eventType || '')
  return `${phase}-${kind}-${startMs}-${endMs}`
}

const handleSagSelectionChange = (val: any[]) => {
  if (!isApplyingDefaultSagSelection.value) hasUserPickedSagRows.value = true
  selectedSagRows.value = val || []
  updateToleranceChart()
}

const getDefaultSagRowsForTable = () => {
  const list = (sagListRows.value || []).slice()
  let maxMag = -Infinity
  for (const evt of list) {
    const mag = Number((evt as any)?.sagMagnitudePct ?? (evt as any)?.sagPercent)
    if (!Number.isFinite(mag)) continue
    if (mag > maxMag) maxMag = mag
  }
  if (!Number.isFinite(maxMag)) return []
  return list.filter((evt: any) => {
    const mag = Number(evt?.sagMagnitudePct ?? evt?.sagPercent)
    return Number.isFinite(mag) && Math.abs(mag - maxMag) < 1e-9
  })
}

const applyDefaultSagSelectionIfNeeded = async () => {
  if (hasUserPickedSagRows.value) return
  const table = sagTableRef.value
  if (!table) return
  await nextTick()
  if ((selectedSagRows.value || []).length > 0) return

  const defaults = getDefaultSagRowsForTable()
  if (defaults.length === 0) return

  isApplyingDefaultSagSelection.value = true
  try {
    table.clearSelection?.()
    for (const row of defaults) {
      table.toggleRowSelection?.(row, true)
    }
  } finally {
    isApplyingDefaultSagSelection.value = false
  }
}

const formatUtcMs = (s?: string | null) => {
  if (!s) return '-'
  const d = new Date(s)
  if (Number.isNaN(d.getTime())) return s
  const pad = (n: number, len = 2) => String(n).padStart(len, '0')
  const yyyy = d.getFullYear()
  const mm = pad(d.getMonth() + 1)
  const dd = pad(d.getDate())
  const hh = pad(d.getHours())
  const mi = pad(d.getMinutes())
  const ss = pad(d.getSeconds())
  const ms = pad(d.getMilliseconds(), 3)
  return `${yyyy}-${mm}-${dd} ${hh}:${mi}:${ss}.${ms}`
}

const enhancedComputedEvents = computed(() => {
  const refV = getRefVoltage()
  return (computedEvents.value || [])
    .slice()
    .sort((a: any, b: any) => Number(a?.startMs ?? 0) - Number(b?.startMs ?? 0))
    .map((evt: any) => {
      const phase = String(evt?.phase || '')
      const channel = analogChannels.value.find(
        (c) => String(c.phase || '').toUpperCase() === phase.toUpperCase()
      )
      const residualPct = Number(evt?.residualVoltagePct)
      const residualVoltage =
        Number.isFinite(residualPct) && refV ? (refV * residualPct) / 100 : null

      return {
        ...evt,
        eventTypeText:
          evt?.eventType === 'Interruption'
            ? '中断'
            : evt?.eventType === 'Sag'
              ? '暂降'
              : evt?.eventType || '-',
        channelText: channel ? `${channel.channelIndex}. ${channel.channelName}` : '-',
        residualVoltage
      }
    })
})

const sagListRows = computed(() => {
  const phaseOrder = (p: string) => {
    const u = String(p || '').toUpperCase()
    if (u === 'A') return 0
    if (u === 'B') return 1
    if (u === 'C') return 2
    return 99
  }

  return enhancedComputedEvents.value
    .slice()
    .sort((a: any, b: any) => {
      const pa = phaseOrder(a?.phase)
      const pb = phaseOrder(b?.phase)
      if (pa !== pb) return pa - pb
      return Number(a?.startMs ?? 0) - Number(b?.startMs ?? 0)
    })
    .map((row: any) => ({
      ...row,
      occurTimeText: formatUtcMs(row?.occurTimeUtc || row?.startTimeUtc || null)
    }))
})

watch(
  sagListRows,
  async () => {
    await applyDefaultSagSelectionIfNeeded()
  },
  { flush: 'post' }
)

const bottomRowHeight = computed(() => Math.max(260, Math.floor(windowHeight.value * 0.32)))

const getGridRect = () => {
  if (!rawChart) return null
  const model: any = (rawChart as any).getModel?.()
  const grid0 = model?.getComponent?.('grid', 0)
  const rect = grid0?.coordinateSystem?.getRect?.()
  return rect || null
}

const getAllGridYRange = () => {
  if (!rawChart) return { minY: 0, maxY: 0 }
  const model: any = (rawChart as any).getModel?.()
  const grids: any[] = model?.getComponents?.('grid') || []
  let minY = Infinity
  let maxY = -Infinity

  for (let i = 0; i < grids.length; i++) {
    const rect = grids[i]?.coordinateSystem?.getRect?.()
    if (!rect) continue
    minY = Math.min(minY, rect.y)
    maxY = Math.max(maxY, rect.y + rect.height)
  }

  if (!Number.isFinite(minY) || !Number.isFinite(maxY)) {
    return { minY: 0, maxY: rawChart.getHeight() }
  }

  return { minY, maxY }
}

const initMarkersIfNeeded = () => {
  if (!rawChart) return
  const rect = getGridRect()
  if (!rect) return
  if (markerLeftXPixel.value === null) markerLeftXPixel.value = rect.x + rect.width * 0.25
  if (markerRightXPixel.value === null) markerRightXPixel.value = rect.x + rect.width * 0.75
}

const getWaveAtXPixel = (xPixel: number) => {
  const data = lastWaveData.value
  if (!rawChart || !data || !data.rows || data.rows.length === 0) return null

  const rect = getGridRect()
  if (rect) xPixel = clamp(xPixel, rect.x, rect.x + rect.width)

  const times = data.rows.map((r: any) => Number(r.timeMs))
  const n = times.length
  const opt: any = rawChart.getOption()
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

  markerDetailLines.value = computeMarkerDetails(data, leftWave.i0, rightWave.i0)
  markerSummary.value = {
    visible: true,
    leftXText: `${Number(leftMs).toFixed(3)}ms`,
    rightXText: `${Number(rightMs).toFixed(3)}ms`,
    leftPointNo: leftNearestIdx + 1,
    rightPointNo: rightNearestIdx + 1,
    deltaXText: `${Math.abs(Number(rightMs) - Number(leftMs)).toFixed(3)}ms`
  }
}

const updateMarkerByPixelX = (side: 'left' | 'right', xPixel: number) => {
  if (!rawChart) return
  const rect = getGridRect()
  if (rect) xPixel = clamp(xPixel, rect.x, rect.x + rect.width)
  if (side === 'left') markerLeftXPixel.value = xPixel
  else markerRightXPixel.value = xPixel
  refreshMarkerSummary()
}

const renderMarkerGraphics = () => {
  const data = lastWaveData.value
  if (!rawChart || !data || !data.rows || data.rows.length === 0) return

  initMarkersIfNeeded()
  if (markerLeftXPixel.value === null || markerRightXPixel.value === null) return

  const rect = getGridRect()
  const minX = rect ? rect.x : 0
  const maxX = rect ? rect.x + rect.width : rawChart.getWidth()
  const yr = getAllGridYRange()
  const y1 = yr.minY
  const y2 = yr.maxY

  const lx = clamp(Number(markerLeftXPixel.value), minX, maxX)
  const rx = clamp(Number(markerRightXPixel.value), minX, maxX)

  rawChart.setOption(
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
            const maxX2 = rect2 ? rect2.x + rect2.width : rawChart?.getWidth?.() || 0
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
            const maxX2 = rect2 ? rect2.x + rect2.width : rawChart?.getWidth?.() || 0
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
            const maxX2 = rect2 ? rect2.x + rect2.width : rawChart?.getWidth?.() || 0
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
            const maxX2 = rect2 ? rect2.x + rect2.width : rawChart?.getWidth?.() || 0
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

const findNearestIndex = (times: number[], target: number) => {
  if (times.length === 0) return 0
  if (target <= times[0]) return 0
  if (target >= times[times.length - 1]) return times.length - 1

  let l = 0
  let r = times.length - 1
  while (l <= r) {
    const m = (l + r) >> 1
    const v = times[m]
    if (v === target) return m
    if (v < target) l = m + 1
    else r = m - 1
  }

  const i1 = clamp(l, 0, times.length - 1)
  const i0 = clamp(i1 - 1, 0, times.length - 1)
  return Math.abs(times[i1] - target) < Math.abs(times[i0] - target) ? i1 : i0
}

const buildToleranceCurve = (xLen: number, y: number) => new Array<number>(xLen).fill(y)

/**
 * 原始波形
 */
const updateRawChart = () => {
  if (!rawChart) return

  const wave = lastWaveData.value
  if (!wave || !wave.rows || wave.rows.length === 0) {
    rawChart.setOption({ series: [], xAxis: [], yAxis: [], grid: [] }, true)
    markerSummary.value.visible = false
    return
  }

  const rows = wave.rows
  const xTimes = rows.map((r: any) => Number(r.timeMs))
  const xData = xTimes.map((t: number) => Number(t).toFixed(3))

  const selectedAll = selectedChannels.value
    .filter((x) => (wave.channels || []).includes(x))
    .slice(0, 12)

  const selected = selectedAll.filter((x) => !hiddenChannelIndices.value.includes(x))

  const grids: any[] = []
  const xAxis: any[] = []
  const yAxis: any[] = []
  const series: any[] = []

  const gap = 14
  const topBase = 14
  const left = 160
  const right = 30
  const heightPer = gridHeightSetting.value

  rawChartHeight.value = topBase + selected.length * (heightPer + gap) + 80

  for (let i = 0; i < selected.length; i++) {
    const top = topBase + i * (heightPer + gap)
    grids.push({ left, right, top, height: heightPer, containLabel: false })

    xAxis.push({
      type: 'category',
      boundaryGap: false,
      gridIndex: i,
      data: xData,
      axisLabel: { show: i === selected.length - 1 }
    })

    const channelIndex = selected[i]
    const colorIndex = Math.max(0, selectedAll.indexOf(channelIndex))
    const info = analogChannels.value.find((c) => c.channelIndex === channelIndex)
    const phase = info?.phase ? String(info.phase).toUpperCase() : ''

    yAxis.push({
      type: 'value',
      scale: true,
      gridIndex: i,
      name: info ? `${info.channelIndex}. ${info.channelName}` : `CH${channelIndex}`,
      nameLocation: 'middle',
      nameGap: 5,
      nameRotate: 0,
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

    const dataIndex = wave.channels.indexOf(channelIndex)
    const data = dataIndex >= 0 ? rows.map((r: any) => r.analog?.[dataIndex] ?? null) : rows.map(() => null)
    const lineColor = RAW_LINE_COLORS[colorIndex % RAW_LINE_COLORS.length]

    series.push({
      name: info ? `${info.channelIndex}. ${info.channelName}` : `CH${channelIndex}`,
      type: 'line',
      xAxisIndex: i,
      yAxisIndex: i,
      showSymbol: false,
      data,
      lineStyle: { width: 1, color: lineColor },
      itemStyle: { color: lineColor }
    })

    const baseSeriesIndex = series.length - 1
    const refV = getRefVoltage()
    const sagPct = Number(params.sagThresholdPct)
    const recoverPct = Number(params.sagThresholdPct) + Number(params.hysteresisPct)
    const interruptPct = Number(params.interruptThresholdPct)

    if (refV && Number.isFinite(sagPct)) {
      series.push({
        name: `${phase || channelIndex} 暂降阈值`,
        type: 'line',
        xAxisIndex: i,
        yAxisIndex: i,
        showSymbol: false,
        data: buildToleranceCurve(xData.length, refV * (sagPct / 100)),
        lineStyle: { width: 1, type: 'dashed', color: '#1677ff' }
      })
    }

    if (refV && Number.isFinite(recoverPct)) {
      series.push({
        name: `${phase || channelIndex} 恢复阈值`,
        type: 'line',
        xAxisIndex: i,
        yAxisIndex: i,
        showSymbol: false,
        data: buildToleranceCurve(xData.length, refV * (recoverPct / 100)),
        lineStyle: { width: 1, type: 'dashed', color: '#13c2c2' }
      })
    }

    if (refV && Number.isFinite(interruptPct)) {
      series.push({
        name: `${phase || channelIndex} 中断阈值`,
        type: 'line',
        xAxisIndex: i,
        yAxisIndex: i,
        showSymbol: false,
        data: buildToleranceCurve(xData.length, refV * (interruptPct / 100)),
        lineStyle: { width: 1, type: 'dashed', color: '#fa8c16' }
      })
    }

    const markAreas: any[] = []
    for (const evt of computedEvents.value || []) {
      const evtPhase = String((evt as any)?.phase || '').toUpperCase()
      if (evtPhase && phase && evtPhase !== phase) continue

      const sIdx = findNearestIndex(xTimes, Number((evt as any)?.startMs ?? 0))
      const eIdx = findNearestIndex(xTimes, Number((evt as any)?.endMs ?? 0))
      markAreas.push([{ xAxis: xData[sIdx] }, { xAxis: xData[eIdx] }])
    }

    if (markAreas.length > 0) {
      series[baseSeriesIndex].markArea = {
        itemStyle: { color: 'rgba(255, 77, 79, 0.08)' },
        data: markAreas
      }
    }

    const vLines: any[] = []
    for (const m of markers.value || []) {
      const t = Number((m as any)?.timeMs)
      if (!Number.isFinite(t)) continue

      const kind = String((m as any)?.kind || '')
      const mPhase = String((m as any)?.phase || '').toUpperCase()
      const isGlobal = !mPhase

      if (!isGlobal && mPhase && phase && mPhase !== phase) continue
      if (!isGlobal && !mPhase) continue

      const idx = findNearestIndex(xTimes, t)
      vLines.push({
        xAxis: xData[idx],
        name: kind,
        label: { formatter: (m as any)?.label || kind, rotate: 90 }
      })
    }

    if (vLines.length > 0) {
      series[baseSeriesIndex].markLine = {
        symbol: 'none',
        lineStyle: { color: '#ff4d4f', width: 1 },
        label: { show: true, position: 'insideEndTop' },
        data: vLines
      }
    }

    const dots: any[] = []
    for (const evt of computedEvents.value || []) {
      const evtPhase = String((evt as any)?.phase || '').toUpperCase()
      if (evtPhase && phase && evtPhase !== phase) continue

      const minMs = Number((evt as any)?.minTimeMs ?? (evt as any)?.startMs)
      if (!Number.isFinite(minMs)) continue

      const idx = findNearestIndex(xTimes, minMs)
      const y = dataIndex >= 0 ? rows[idx]?.analog?.[dataIndex] : null
      if (typeof y !== 'number' || !Number.isFinite(y)) continue

      dots.push({
        name: `${phase || '相'}最小点`,
        value: [xData[idx], y]
      })
    }

    if (dots.length > 0) {
      series.push({
        name: `关键点-${channelIndex}`,
        type: 'scatter',
        xAxisIndex: i,
        yAxisIndex: i,
        symbolSize: 8,
        data: dots,
        label: {
          show: true,
          formatter: (p: any) => String(p?.name || ''),
          position: 'top'
        },
        itemStyle: { color: '#ff4d4f' }
      })
    }
  }

  rawChart.setOption(
    {
      tooltip: { trigger: 'axis', axisPointer: { type: 'cross' }, confine: true },
      legend: { show: false },
      grid: grids,
      xAxis,
      yAxis,
      dataZoom: [
        { type: 'inside', xAxisIndex: xAxis.map((_: any, i: number) => i) },
        { type: 'slider', xAxisIndex: xAxis.map((_: any, i: number) => i) }
      ],
      series
    },
    true
  )

  rawChart.resize()
  refreshMarkerSummary()
  renderMarkerGraphics()
}

/**
 * 容忍度曲线
 * 横轴：持续时间(s)
 * 纵轴：残余电压(%)
 *
 * 当前给一条前端内置的“标准容忍度折线”
 * 后续你有明确标准时，只需要替换 toleranceStandardLine 即可
 */
const toleranceStandardLine = [
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

const updateToleranceChart = () => {
  if (!toleranceChart) return

  const pointSeries = tolerancePointLegends.value.map((x: any) => {
    return {
      name: x.label,
      type: 'scatter',
      symbolSize: 7,
      data: [x.point],
      itemStyle: { color: x.color },
      label: {
        show: true,
        formatter: (p: any) => {
          const d = p?.data || {}
          return d.phase ? `${d.phase}相` : ''
        },
        position: 'top'
      }
    }
  })

  toleranceChart.setOption(
    {
      tooltip: {
        trigger: 'item',
        formatter: (p: any) => {
          const seriesName = String(p?.seriesName || '')
          const d = p?.data

          if (seriesName === '标准折点') {
            const value = (Array.isArray(p?.value) ? p.value : Array.isArray(d) ? d : []) as any[]
            const x = Number(value[0])
            const y = Number(value[1])
            return [
              `<div><b>标准折点</b></div>`,
              `<div>持续时间：${Number.isFinite(x) ? x.toFixed(3) : '-'} s</div>`,
              `<div>残余电压：${Number.isFinite(y) ? y.toFixed(3) : '-'} %</div>`
            ].join('')
          }

          const obj = (d && typeof d === 'object' && !Array.isArray(d) ? d : {}) as any
          const value = (obj?.value || []) as any[]
          const x = Number(value[0])
          const y = Number(value[1])
          return [
            `<div><b>${obj.name || '事件点'}</b></div>`,
            `<div>持续时间：${Number.isFinite(x) ? x.toFixed(3) : '-'} s</div>`,
            `<div>残余电压：${Number.isFinite(y) ? y.toFixed(3) : '-'} %</div>`,
            `<div>发生时间：${obj.occurTimeText || '-'}</div>`,
            `<div>残余电压：${formatV(obj.residualVoltage)}</div>`
          ].join('')
        }
      },
      grid: {
        left: 32,
        right: 80,
        top: 12,
        bottom: 0,
        containLabel: true
      },
      xAxis: {
        type: 'value',
        name: '持续时间 (s)',
        nameLocation: 'end',
        nameGap: 80,
        nameTextStyle: { align: 'right' },
        min: 0,
        max: 1,
        splitNumber: 5,
        axisLabel: {
          fontSize: 10,
          margin: 8,
          formatter: (v: any) => {
            const n = Number(v)
            if (!Number.isFinite(n)) return String(v)
            const s = n.toFixed(2)
            return s.replace(/\.?0+$/, '')
          }
        },
        minorSplitLine: { show: true, lineStyle: { opacity: 0.12 } },
        minorTick: { show: true, splitNumber: 4 },
        splitLine: {
          show: true,
          lineStyle: { type: 'solid', opacity: 0.35 }
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
        splitNumber: 5,
        axisLabel: {
          fontSize: 10,
          margin: 8,
          formatter: (v: any) => {
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
        {
          name: '标准容忍度折线',
          type: 'line',
          data: toleranceStandardLine,
          showSymbol: false,
          lineStyle: {
            width: 2,
            color: '#1677ff'
          },
          itemStyle: {
            color: '#1677ff'
          }
        },
        {
          name: '标准折点',
          type: 'scatter',
          data: toleranceStandardLine,
          symbolSize: 12,
          itemStyle: { color: '#1677ff', opacity: 0 },
          emphasis: {
            scale: true,
            itemStyle: { opacity: 1 }
          },
          silent: false
        },
        ...pointSeries
      ]
    },
    true
  )

  toleranceChart.resize()
}

const updateCharts = () => {
  updateRawChart()
  updateToleranceChart()
}

const selectABC = () => {
  const list = analogChannels.value || []
  const picks = ['A', 'B', 'C']
    .map((p) => list.find((x) => String(x.phase || '').toUpperCase() === p)?.channelIndex)
    .filter((x): x is number => typeof x === 'number')
  if (picks.length > 0) selectedChannels.value = picks
}

const selectFirst3 = () => {
  selectedChannels.value = (analogChannels.value || []).slice(0, 3).map((x) => x.channelIndex)
}

const reload = async () => {
  loading.value = true
  try {
    const res: any = await zwavSagService.getProcess(eventId)
    if (!res.success || !res.data) {
      ElMessage.error(res.message || '查询失败')
      return
    }

    process.value = res.data
    computedEvents.value = res.data.computedEvents || []
    markers.value = res.data.markers || []

    const refV =
      Number((res.data as any)?.event?.referenceVoltage) ||
      Number((res.data?.rmsPoints || [])?.find((x: any) => Number(x?.referenceVoltage) > 0)?.referenceVoltage) ||
      57.74

    params.referenceVoltage = refV
    params.sagThresholdPct = Number((res.data as any)?.event?.sagThresholdPct ?? 90)
    params.interruptThresholdPct = Number((res.data as any)?.event?.interruptThresholdPct ?? 10)
    params.hysteresisPct = Number((res.data as any)?.event?.hysteresisPct ?? 2)
    params.minDurationMs = Number((res.data as any)?.event?.minDurationMs ?? 10)

    analogChannels.value = (res.data.voltageChannels || []).map((c: any) => {
      return {
        channelIndex: c.channelIndex,
        channelType: 'Analog',
        channelCode: c.channelCode || '',
        channelName: c.channelName || c.channelCode || `CH${c.channelIndex}`,
        phase: c.phase || '',
        unit: c.unit || '',
        ratioA: null,
        offsetB: null,
        skew: null,
        isEnable: 1
      } as ChannelDto
    })

    if (res.data.suggestedFromSample !== undefined && res.data.suggestedFromSample !== null) {
      searchFromSample.value = Number(res.data.suggestedFromSample)
    }
    if (res.data.suggestedToSample !== undefined && res.data.suggestedToSample !== null) {
      searchToSample.value = Number(res.data.suggestedToSample)
    }

    if (selectedChannels.value.length === 0) selectABC()
    if (selectedChannels.value.length === 0) selectFirst3()

    await fetchWaveData()
  } catch (e: any) {
    ElMessage.error(e?.message ? `查询失败（${e.message}）` : '查询失败')
  } finally {
    loading.value = false
  }
}

const applyWaveSettings = async () => {
  showWaveSettingDialog.value = false
  await fetchWaveData()
}

const fetchWaveData = async () => {
  if (!analysisGuid.value) return
  if (selectedChannels.value.length === 0) {
    ElMessage.warning('请至少选择一个通道')
    return
  }

  waveLoading.value = true
  rawChart?.showLoading()
  try {
    const res: any = await zwavService.getWaveData(analysisGuid.value, {
      channels: selectedChannels.value.join(','),
      fromSample: searchFromSample.value,
      toSample: searchToSample.value,
      limit: searchLimit.value,
      downSample: searchDownSample.value
    })

    if (!res.success || !res.data) {
      ElMessage.error(res.message || '获取波形失败')
      return
    }

    lastWaveData.value = res.data
    updateCharts()
  } catch (e: any) {
    ElMessage.error(e?.message ? `获取波形失败（${e.message}）` : '获取波形失败')
  } finally {
    rawChart?.hideLoading()
    waveLoading.value = false
  }
}

const preview = async () => {
  previewing.value = true
  try {
    const res: any = await zwavSagService.previewProcess(eventId, {
      referenceVoltage: params.referenceVoltage,
      sagThresholdPct: params.sagThresholdPct,
      interruptThresholdPct: params.interruptThresholdPct,
      hysteresisPct: params.hysteresisPct,
      minDurationMs: params.minDurationMs
    })

    if (!res.success || !res.data) {
      ElMessage.error(res.message || '预览失败')
      return
    }

    if (process.value) {
      ;(process.value as any).rmsPoints = res.data.rmsPoints || []
    }

    computedEvents.value = res.data.computedEvents || []
    markers.value = res.data.markers || []

    if (res.data.suggestedFromSample !== undefined && res.data.suggestedFromSample !== null) {
      searchFromSample.value = Number(res.data.suggestedFromSample)
    }
    if (res.data.suggestedToSample !== undefined && res.data.suggestedToSample !== null) {
      searchToSample.value = Number(res.data.suggestedToSample)
    }

    updateCharts()
    ElMessage.success('预览成功')
  } catch (e: any) {
    ElMessage.error(e?.message ? `预览失败（${e.message}）` : '预览失败')
  } finally {
    previewing.value = false
  }
}

watch(selectedChannels, async () => {
  await fetchWaveData()
})

const handleResize = () => {
  windowHeight.value = window.innerHeight
  rawChart?.resize()
  toleranceChart?.resize()
  refreshMarkerSummary()
  renderMarkerGraphics()
}

onMounted(async () => {
  await nextTick()

  if (rawChartRef.value) rawChart = echarts.init(rawChartRef.value)
  if (toleranceChartRef.value) toleranceChart = echarts.init(toleranceChartRef.value)

  window.addEventListener('resize', handleResize)
  window.addEventListener('keydown', onKeyDown)
  await reload()
})

onUnmounted(() => {
  window.removeEventListener('resize', handleResize)
  window.removeEventListener('keydown', onKeyDown)
  rawChart?.dispose()
  toleranceChart?.dispose()
  rawChart = null
  toleranceChart = null
})
</script>

<style scoped>
.viewer-container {
  padding: 10px;
  background: #f5f7fa;
  min-height: 100vh;
  box-sizing: border-box;
}

.header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 12px 14px;
  background-color: #fff;
  border: 1px solid #ebeef5;
  border-radius: 8px;
  margin-bottom: 12px;
}

.title {
  display: flex;
  align-items: baseline;
  gap: 8px;
  flex-wrap: nowrap;
  min-width: 0;
}

.title h2 {
  margin: 0;
  font-size: 18px;
  white-space: nowrap;
}

.file-name {
  font-size: 14px;
  color: #303133;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  max-width: 640px;
}

.sub-title {
  font-size: 12px;
  color: #909399;
  white-space: nowrap;
}

.header-actions {
  display: flex;
  gap: 8px;
}

.content {
  display: flex;
  height: calc(100vh - 86px);
  min-height: 700px;
}

.left-panel {
  width: 220px;
  border: 1px solid #ebeef5;
  border-radius: 8px;
  background: #fff;
  overflow: hidden;
  display: flex;
  flex-direction: column;
}

.right-panel {
  flex: 1;
  margin-left: 12px;
  display: flex;
  flex-direction: column;
  gap: 12px;
  min-width: 0;
  min-height: 0;
}

.toolbar-card {
  border-radius: 8px;
}

.toolbar-row {
  display: flex;
  justify-content: flex-end;
  gap: 12px;
  align-items: center;
}

.toolbar-left {
  flex: 1;
  min-width: 0;
}

.toolbar-right {
  display: flex;
  gap: 8px;
  flex-shrink: 0;
  align-items: center;
}

.param-field {
  display: flex;
  flex-direction: column;
  gap: 4px;
  width: 100%;
}

.param-field :deep(.el-input-number) {
  width: 100%;
}

.param-desc {
  font-size: 12px;
  color: #909399;
  line-height: 1.3;
}

.charts-row {
  display: flex;
  gap: 12px;
  flex: 1;
  min-height: 0;
}

.chart-card {
  border-radius: 8px;
  min-width: 0;
}

.raw-card-wrapper {
  flex: 1;
  display: flex;
  min-width: 0;
}

.raw-card {
  flex: 1;
  display: flex;
  flex-direction: column;
}

.raw-card :deep(.el-card__header) {
  padding: 12px;
}

.raw-card :deep(.el-card__body) {
  padding: 12px;
  display: flex;
  flex-direction: column;
  flex: 1;
  min-height: 0;
}

.bottom-row {
  display: flex;
  gap: 12px;
  min-height: 0;
}

.tolerance-card {
  flex: 0 0 40%;
  max-width: 40%;
  height: 100%;
  display: flex;
  flex-direction: column;
  min-height: 0;
}

.tolerance-card :deep(.el-card__header) {
  padding: 12px;
}

.tolerance-card :deep(.el-card__body) {
  display: flex;
  flex-direction: column;
  flex: 1;
  min-height: 0;
  overflow: hidden;
}

.list-card :deep(.el-card__header) {
  padding: 12px;
}

.card-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 8px;
  font-weight: 600;
}

.card-sub {
  font-size: 12px;
  color: #909399;
  font-weight: 400;
}

.chart-scroll-container {
  flex: 1;
  overflow: auto;
  min-height: 0;
}

.raw-chart-content {
  width: 100%;
  min-height: 600px;
}

.tolerance-chart-content {
  width: 100%;
  flex: 1;
  min-height: 0;
}

.tolerance-legend {
  display: flex;
  gap: 12px;
  margin-bottom: 8px;
  flex-wrap: wrap;
  max-height: 72px;
  overflow-y: auto;
  padding-right: 4px;
}

.legend-item {
  display: inline-flex;
  align-items: center;
  font-size: 12px;
  color: #606266;
}

.legend-item::before {
  content: '';
  display: inline-block;
  margin-right: 6px;
  border-radius: 2px;
}

.legend-item.curve::before {
  width: 20px;
  height: 3px;
  background: #1677ff;
}

.legend-item.point::before {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  background: #ff4d4f;
}

.legend-item.dyn-point::before {
  display: none;
}

.legend-dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  display: inline-block;
  margin-right: 6px;
}

.tolerance-note {
  margin-top: 10px;
  font-size: 12px;
  color: #909399;
  line-height: 18px;
  flex-shrink: 0;
}

.list-card {
  flex: 1;
  min-width: 0;
  height: 100%;
  border-radius: 8px;
  display: flex;
  flex-direction: column;
}

.list-card :deep(.el-card__body) {
  display: flex;
  flex-direction: column;
  flex: 1;
  min-height: 0;
  overflow: hidden;
}

.list-card :deep(.el-table) {
  flex: 1;
  min-height: 0;
}

.list-table-wrap {
  flex: 1;
  min-height: 0;
  overflow: auto;
}

.icon-btn {
  font-size: 18px;
  color: #909399;
  padding: 0;
  height: auto;
}

.icon-btn:hover {
  color: #409eff;
}

.raw-card-wrapper.is-window-fullscreen {
  background: #fff;
  padding: 12px;
  box-sizing: border-box;
  display: flex;
  flex-direction: column;
  position: fixed;
  inset: 10px;
  z-index: 3000;
}

.raw-card-wrapper.is-window-fullscreen .raw-card {
  flex: 1;
  display: flex;
  flex-direction: column;
  min-height: 0;
}

.card-header-left {
  display: flex;
  align-items: center;
  gap: 8px;
  min-width: 0;
}

.card-header-right {
  display: flex;
  align-items: center;
  gap: 6px;
  flex-shrink: 0;
}

.left-panel :deep(.channel-sidebar) {
  height: 100%;
  display: flex;
  flex-direction: column;
  min-height: 0;
}

.left-panel :deep(.channel-section) {
  display: flex;
  flex-direction: column;
  min-height: 0;
  flex: 1;
}

.left-panel :deep(.channel-group) {
  display: flex;
  flex-direction: column;
  min-height: 0;
  flex: 1;
}

.left-panel :deep(.el-scrollbar) {
  flex: 1;
  min-height: 0;
}

.toolbar-left :deep(.el-form--inline) {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
}

.toolbar-left :deep(.el-form-item) {
  margin-bottom: 0;
}

.marker-footer {
  padding: 8px 10px;
  background-color: #fff;
  border-top: 1px solid #ebeef5;
  display: flex;
  align-items: center;
  justify-content: space-between;
  position: sticky;
  bottom: 0;
}

.marker-footer-text {
  font-size: 12px;
  color: #606266;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.raw-legend {
  padding: 8px 12px 0 12px;
}

.raw-legend-channels,
.raw-legend-meta {
  display: flex;
  flex-wrap: wrap;
  gap: 10px 14px;
  align-items: center;
}

.raw-legend-meta {
  margin-top: 6px;
}

.raw-legend-item {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  font-size: 12px;
  color: #606266;
  cursor: pointer;
  user-select: none;
}

.raw-legend-item.is-hidden {
  opacity: 0.45;
}

.raw-legend-item.is-hidden .legend-text {
  text-decoration: line-through;
}

.legend-text {
  max-width: 240px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.legend-line {
  width: 18px;
  height: 3px;
  border-radius: 2px;
}

.legend-dash {
  width: 18px;
  height: 0;
  border-bottom-width: 2px;
  border-bottom-style: dashed;
}

.dash-sag {
  border-bottom-color: #1677ff;
}

.dash-recover {
  border-bottom-color: #13c2c2;
}

.dash-interrupt {
  border-bottom-color: #fa8c16;
}

.legend-area {
  width: 18px;
  height: 10px;
  border: 1px solid rgba(255, 77, 79, 0.35);
  background: rgba(255, 77, 79, 0.12);
}

.legend-vline {
  width: 0;
  height: 12px;
  border-left: 2px solid #ff4d4f;
}

.legend-point {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  background: #ff4d4f;
}

.phase-section {
  margin-top: 16px;
}
</style>
