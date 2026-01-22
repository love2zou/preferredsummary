<template>
  <div class="workbench-view" :class="{ 'as-dialog': !!props.asDialog }">
    <!-- Tabs（把“任务编号/待关闭上传/帮助提示”放到选项卡栏最右侧） -->
    <div class="tabs-wrap" v-loading="loadingDetail">
      <!-- 右侧任务信息（与 tabs 同一行，靠右） -->
      <div class="tabs-meta">
        <div class="meta-left">
          <span class="job-title">{{ currentDetailJob?.jobNo }}</span>

          <el-tag
            v-if="currentDetailJob"
            size="small"
            :type="getStatusType(currentDetailJob.status)"
            class="ml-2"
          >
            {{ getJobStatusText(currentDetailJob) }}
          </el-tag>

          <el-tag
            v-if="uploadClosed"
            size="small"
            type="info"
            class="ml-2"
            effect="plain"
          >
            已关闭上传
          </el-tag>
        </div>

        <div class="meta-right">
          <el-tooltip content="使用说明" placement="bottom" :show-after="200">
            <el-button
              class="help-btn"
              :icon="QuestionFilled"
              circle
              size="small"
              @click="helpVisible = true"
            />
          </el-tooltip>
        </div>
      </div>

      <el-tabs v-model="activeTab" class="wb-tabs" type="card">
        <!-- 1) 视频清单（第一个选项卡） -->
        <el-tab-pane name="list" label="视频清单">
          <div class="list-body">
            <!-- 左侧：表格分页 + 异常筛选 -->
            <div class="list-left">
              <div class="panel-header">
                <div class="lh-title">
                  <span>视频清单</span>
                  <span class="sub-text">（共 {{ listTotal }} 条）</span>
                </div>

                <div class="lh-controls">
                  <el-checkbox v-model="onlyAbnormal" label="仅异常" border size="small" />
                </div>
              </div>

              <div class="table-wrap">
                <el-table
                  :data="listPageVideos"
                  height="100%"
                  stripe
                  highlight-current-row
                  :row-class-name="rowClassName"
                  @row-click="onRowClick"
                >
                  <el-table-column type="index" label="#" width="55">
                    <template #default="{ $index }">
                      {{ (listPageNo - 1) * listPageSize + $index + 1 }}
                    </template>
                  </el-table-column>

                  <el-table-column prop="fileName" label="文件名" min-width="220" show-overflow-tooltip />

                  <el-table-column label="异常" width="90">
                    <template #default="{ row }">
                      <el-tag v-if="row.stats.total > 0" type="danger" size="small" effect="dark">有异常</el-tag>
                      <el-tag v-else type="success" size="small" effect="plain">正常</el-tag>
                    </template>
                  </el-table-column>

                  <el-table-column label="火/闪" width="110">
                    <template #default="{ row }">
                      <span class="mini-kpi">
                        <span class="kpi kpi-danger">火 {{ row.stats.spark }}</span>
                        <span class="kpi kpi-warning">闪 {{ row.stats.flash }}</span>
                      </span>
                    </template>
                  </el-table-column>

                  <el-table-column label="Max" width="90">
                    <template #default="{ row }">
                      <span v-if="row.stats.maxConf > 0">{{ (row.stats.maxConf * 100).toFixed(0) }}%</span>
                      <span v-else class="text-gray">-</span>
                    </template>
                  </el-table-column>

                  <el-table-column label="操作" width="110" fixed="right">
                    <template #default="{ row }">
                      <el-button size="small" type="primary" link @click.stop="openDetail(row.id)">
                        查看详情
                      </el-button>
                    </template>
                  </el-table-column>
                </el-table>
              </div>

              <!-- 清单分页：与 4/9/16 画面完全一致（同 pageNo、同 pageSize） -->
              <div class="pager">
                <div class="pager-left">
                  <span class="sub-text">每页 {{ listPageSize }} 条（随 4/9/16 画面自动变化）</span>
                </div>
                <div class="pager-right">
                  <el-button size="small" :disabled="listPageNo <= 1" @click="goPrevPage">上一页</el-button>
                  <span class="pager-mid">{{ listPageNo }} / {{ listPageCount }}</span>
                  <el-button size="small" :disabled="listPageNo >= listPageCount" @click="goNextPage">下一页</el-button>
                </div>
              </div>
            </div>

            <!-- 右侧：4/9/16 多画面（严格限制数量，翻页，不滚动显示更多） -->
            <div class="list-right">
              <div class="panel-header">
                <div class="lr-title">
                  <span>多画面预览</span>
                  <span class="sub-text">（本页 {{ listPageVideos.length }} / {{ listPageSize }}）</span>
                </div>

                <div class="lr-controls">
                  <el-segmented v-model="gridMode" :options="gridModeOptions" size="small" />
                </div>
              </div>

              <div class="grid-stage">
                <div class="video-grid" :class="gridClass">
                  <!-- 实际视频画面 -->
                  <div
                    v-for="v in listPageVideos"
                    :key="v.id"
                    class="grid-cell"
                    :class="{ active: listSelectedId === v.id }"
                    @click="selectFromGrid(v.id)"
                  >
                    <!-- 异常标识：右上角🔥；正常不显示 -->
                    <div class="grid-flame" v-if="v.stats.total > 0" title="该视频存在异常事件">🔥</div>

                    <video
                      class="grid-video"
                      :src="videoAnalyticsService.getVideoContentUrl(v.id)"
                      muted
                      preload="metadata"
                      controls
                    ></video>
                  </div>

                  <!-- 补足空格：确保 4/9/16 画面固定布局 -->
                  <div v-for="k in emptyCells" :key="'empty-' + k" class="grid-cell empty">
                    <div class="empty-tip">空</div>
                  </div>
                </div>
              </div>

              <div class="grid-hint">
                <span class="sub-text">
                  提示：右上角 🔥 表示有异常；点击任意画面可选中；分页与左侧表格同步。
                </span>
              </div>
            </div>
          </div>
        </el-tab-pane>

        <!-- 2) 视频分析详情（第二个选项卡；置信度阈值放在这里） -->
        <el-tab-pane name="detail" label="视频分析详情">
          <!-- 详情页筛选栏（包含置信度阈值） -->
          <div class="detail-filter-bar">
            <div class="df-left">
              <span class="filter-label">置信度阈值:</span>
              <el-slider
                v-model="filterConf"
                :min="0"
                :max="1"
                :step="0.05"
                style="width: 160px; margin: 0 12px"
              />

              <el-checkbox v-model="filterHasEvents" label="仅看有事件" border size="small" />

              <el-radio-group v-model="filterType" size="small" class="ml-2">
                <el-radio-button label="ALL">全部</el-radio-button>
                <el-radio-button label="Spark"><span class="text-danger">火花</span></el-radio-button>
                <el-radio-button label="Flash"><span class="text-warning">闪光</span></el-radio-button>
              </el-radio-group>
            </div>

            <div class="df-right">
              <span class="sub-text">筛选仅作用于“事件墙/详情查看”，不影响清单页的🔥标识（🔥基于是否存在事件）。</span>
            </div>
          </div>

          <div class="wb-body">
            <!-- 左栏：视频列表 -->
            <div class="wb-col-left">
              <div class="panel-header">
                <span>视频列表 ({{ filteredVideos.length }})</span>
                <div class="video-actions">
                  <el-checkbox
                    :indeterminate="selectAllIndeterminate"
                    :model-value="selectAllChecked"
                    @change="toggleSelectAll"
                  >
                    全选
                  </el-checkbox>

                  <el-popconfirm title="将清空所选视频的历史事件/截图，并重新入队分析，是否继续？" @confirm="reanalyzeSelected">
                    <template #reference>
                      <el-button
                        size="small"
                        type="danger"
                        plain
                        :disabled="selectedFileIds.length === 0 || reanalyzing || !currentDetailJob"
                        :loading="reanalyzing"
                      >
                        重新分析 ({{ selectedFileIds.length }})
                      </el-button>
                    </template>
                  </el-popconfirm>
                </div>
              </div>

              <el-scrollbar>
                <div class="video-list">
                  <div
                    v-for="vid in filteredVideos"
                    :key="vid.id"
                    class="video-card"
                    :class="{ active: currentFileId === vid.id }"
                    @click="selectVideo(vid.id)"
                  >
                    <div class="vc-row1">
                      <div class="vc-left">
                        <el-checkbox
                          :model-value="isSelected(vid.id)"
                          @change="toggleSelected(vid.id, $event)"
                          @click.stop
                        />
                        <span class="vc-name" :title="vid.fileName">{{ vid.fileName }}</span>
                      </div>

                      <div class="vc-right">
                        <el-tag v-if="vid.stats.total > 0" type="danger" size="small" effect="dark">异常</el-tag>
                        <el-tag v-else type="success" size="small" effect="plain">正常</el-tag>
                        <el-tag size="small" effect="plain" class="ml-2">{{ getVideoStatusText(vid.status) }}</el-tag>
                      </div>
                    </div>

                    <div class="vc-row2">
                      <div class="vc-badges">
                        <el-tag v-if="vid.stats.spark > 0" type="danger" size="small" effect="dark">火 {{ vid.stats.spark }}</el-tag>
                        <el-tag v-if="vid.stats.flash > 0" type="warning" size="small" effect="dark">闪 {{ vid.stats.flash }}</el-tag>
                        <span v-if="vid.stats.total === 0" class="text-gray">无异常事件</span>
                      </div>
                      <div v-if="vid.stats.maxConf > 0" class="vc-conf">
                        Max: {{ (vid.stats.maxConf * 100).toFixed(0) }}%
                      </div>
                    </div>

                    <div class="vc-row3">
                      <span class="vc-meta">时长: {{ formatDuration(vid.analysisDurationMs) }}</span>
                      <span class="vc-meta" v-if="vid.status === 1">处理中...</span>
                    </div>
                  </div>
                </div>
              </el-scrollbar>
            </div>

            <!-- 中栏：全量事件墙（按视频分组） -->
            <div class="wb-col-center">
              <div class="panel-header">
                <span>全量事件预览</span>
                <span class="sub-text">（筛选后共 {{ allFilteredEvents.length }} 个事件，{{ allEventGroups.length }} 个视频分组）</span>
              </div>

              <el-scrollbar>
                <div v-if="allEventGroups.length" class="group-wall">
                  <div v-for="g in allEventGroups" :key="g.videoId" class="group-block">
                    <div class="group-header" @click="selectVideo(g.videoId)">
                      <div class="gh-left">
                        <span class="gh-title" :title="g.fileName">{{ g.fileName }}</span>
                        <el-tag size="small" effect="plain" class="ml-2">{{ getVideoStatusText(g.status) }}</el-tag>
                        <el-tag type="danger" size="small" effect="dark" class="ml-2">异常</el-tag>
                      </div>

                      <div class="gh-right">
                        <el-tag v-if="g.spark > 0" type="danger" size="small" effect="dark">火 {{ g.spark }}</el-tag>
                        <el-tag v-if="g.flash > 0" type="warning" size="small" effect="dark">闪 {{ g.flash }}</el-tag>
                        <span class="gh-meta">事件 {{ g.total }}</span>
                        <span v-if="g.maxConf > 0" class="gh-meta strong">Max {{ (g.maxConf * 100).toFixed(0) }}%</span>
                      </div>
                    </div>

                    <div class="event-grid">
                      <div
                        v-for="evt in g.events"
                        :key="evt.id"
                        class="event-card"
                        :class="{ selected: currentFileId === g.videoId }"
                        @click="seekToEventFromWall(g.videoId, evt)"
                      >
                        <div class="ec-thumb" v-loading="snapshotLoading[evt.id]">
                          <el-image
                            v-if="snapshotCache[evt.id]"
                            :src="snapshotCache[evt.id]"
                            fit="cover"
                            class="ec-img"
                          />
                          <div v-else class="ec-placeholder">
                            <el-icon><Picture /></el-icon>
                          </div>
                          <div class="ec-time">{{ formatTime(evt.peakTimeSec) }}</div>
                        </div>

                        <div class="ec-info">
                          <div class="ec-row">
                            <el-tag size="small" :type="evt.eventType === 2 ? 'danger' : 'warning'" effect="dark">
                              {{ evt.eventType === 2 ? 'Spark' : 'Flash' }}
                            </el-tag>
                            <span class="ec-conf">{{ (Number(evt.confidence) * 100).toFixed(1) }}%</span>
                          </div>
                          <el-progress :percentage="Math.round(Number(evt.confidence) * 100)" :show-text="false" class="mt-1" />
                        </div>
                      </div>
                    </div>
                  </div>
                </div>

                <el-empty v-else description="当前筛选条件下无事件" />
              </el-scrollbar>
            </div>

            <!-- 右栏：播放器 + 右下角持续上传 -->
            <div class="wb-col-right">
              <div class="panel-header">
                <span>播放器 - {{ currentFileName }}</span>
              </div>

              <div class="player-wrapper">
                <video
                  ref="videoRef"
                  class="html-video"
                  controls
                  :src="currentVideoUrl"
                  @timeupdate="onTimeUpdate"
                ></video>

                <div class="player-controls">
                  <span class="time-display">{{ formatTime(currentTime) }}</span>
                  <div class="quick-seek">
                    <el-button size="small" @click="seekDelta(-5)">-5s</el-button>
                    <el-button size="small" @click="seekDelta(5)">+5s</el-button>
                  </div>
                </div>
              </div>

              <div class="upload-panel">
                <div class="panel-header small">
                  <span>持续上传</span>
                </div>

                <div class="upload-box">
                  <el-upload
                    drag
                    multiple
                    action="#"
                    accept="video/*"
                    :auto-upload="true"
                    :show-file-list="true"
                    :limit="1000"
                    :disabled="uploadClosed || !canUpload"
                    :http-request="doUploadRequest"
                    :before-upload="beforeUpload"
                    @exceed="onExceed"
                  >
                    <div class="el-upload__text">
                      拖拽视频到此处或点击上传<br />
                      <span class="upload-sub">单文件上传完成即入队分析（服务端不知道何时结束）</span>
                    </div>
                  </el-upload>

                  <div class="upload-actions">
                    <el-button
                      type="warning"
                      plain
                      :disabled="uploadClosed || !currentDetailJob"
                      :loading="closing"
                      @click="closeUpload"
                    >
                      关闭上传（我不再上传了）
                    </el-button>

                    <div class="upload-hint">
                      <span>已上传: {{ (currentDetailJob?.videos?.length || 0) }} 个</span>
                      <el-divider direction="vertical" />
                      <span>完成/失败: {{ doneCount }}/{{ totalCount }}</span>
                    </div>
                  </div>
                </div>
              </div>

              <div class="right-footer-hint">
                <span class="sub-text">提示：点击事件墙中的任意截图，将自动切换视频并定位到该事件前 2 秒。</span>
              </div>
            </div>
          </div>
        </el-tab-pane>
      </el-tabs>
    </div>

    <!-- 使用说明弹窗 -->
    <el-dialog v-model="helpVisible" title="使用说明" width="780px" append-to-body>
      <div class="help-content">
        <h4>1. 视频清单</h4>
        <ul>
          <li>左侧表格可按“仅异常”筛选；右侧为 4/9/16 多画面预览。</li>
          <li>右侧画面右上角 🔥 表示该视频存在异常事件。</li>
          <li>多画面严格限制数量，翻页与左侧分页同步。</li>
        </ul>

        <h4>2. 视频分析详情</h4>
        <ul>
          <li>置信度阈值等筛选器仅在该选项卡生效，用于筛选事件墙与详情查看。</li>
        </ul>
      </div>

      <template #footer>
        <el-button type="primary" @click="helpVisible = false">我知道了</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { videoAnalyticsService, type EventDto, type JobDetailDto, type JobVideoDto } from '@/services/videoAnalyticsService';
import { Picture, QuestionFilled } from '@element-plus/icons-vue';
import { ElMessage } from 'element-plus';
import { computed, nextTick, onMounted, onUnmounted, ref, watch } from 'vue';

const props = defineProps<{ job: JobDetailDto; asDialog?: boolean }>()
defineEmits(['back'])

// ============ Tabs ============
const activeTab = ref<'list' | 'detail'>('list') // 默认先看“视频清单”

// ============ 类型 ============
interface VideoStats {
  spark: number
  flash: number
  total: number
  maxConf: number
  firstEventTime: number
}

interface EnrichedVideo extends JobVideoDto {
  stats: VideoStats
  events: EventDto[]
  analysisDurationMs: number
}

interface WallGroup {
  videoId: number
  fileName: string
  status: number
  spark: number
  flash: number
  total: number
  maxConf: number
  firstEventTime: number
  events: EventDto[]
}

// ============ 状态 ============
const currentDetailJob = ref<JobDetailDto | null>(null)
const loadingDetail = ref(false)
const allEvents = ref<EventDto[]>([])
const currentFileId = ref<number | null>(null)

const snapshotCache = ref<Record<number, string>>({})
const snapshotLoading = ref<Record<number, boolean>>({})

const helpVisible = ref(false)

// 上传/关闭
const uploadClosed = ref(false)
const closing = ref(false)
const canUpload = ref(true)

// 重新分析
const selectedFileIds = ref<number[]>([])
const reanalyzing = ref(false)

// 详情页筛选器（置信度阈值在 detail）
const filterConf = ref(0)
const filterHasEvents = ref(false)
const filterType = ref<'ALL' | 'Spark' | 'Flash'>('ALL')

// 播放器
const videoRef = ref<HTMLVideoElement>()
const currentTime = ref(0)
let pollTimer: number | null = null

// 清单页：异常筛选 + 分页（pageSize = gridMode）
const onlyAbnormal = ref(false)
const listPageNo = ref(1)
const listSelectedId = ref<number | null>(null)

// 多画面：4/9/16（pageSize 绑定它）
const gridMode = ref<4 | 9 | 16>(9)
const gridModeOptions = [
  { label: '4画面', value: 4 },
  { label: '9画面', value: 9 },
  { label: '16画面', value: 16 }
]

// ============ 工具函数 ============
const toNum = (v: any) => Number(v ?? 0)

const getStatusText = (s: number) => {
  switch (s) {
    case 0: return '等待中'
    case 1: return '分析中'
    case 2: return '已完成'
    case 3: return '失败'
    case 4: return '已取消'
    default: return String(s)
  }
}

const getJobStatusText = (job: JobDetailDto | null) => {
  if (!job) return ''
  const status = Number(job.status)
  if (status === 1) {
    const totalVideoCount = Number((job as any).totalVideoCount ?? 0)
    const vids = (job as any).videos || []
    if (totalVideoCount <= 0) {
      if (!vids.length) return '等待上传'
      const done = vids.filter((v: any) => [2, 3].includes(Number(v.status))).length
      if (done >= vids.length) return '待关闭上传'
      return '分析中'
    }
  }
  return getStatusText(status)
}

const getStatusType = (s: number) => {
  switch (s) {
    case 2: return 'success'
    case 1: return 'primary'
    case 3: return 'danger'
    case 4: return 'info'
    default: return 'warning'
  }
}

const getVideoStatusText = (s: number) => {
  switch (s) {
    case 0: return '待处理'
    case 1: return '处理中'
    case 2: return '完成'
    case 3: return '失败'
    default: return String(s)
  }
}

const formatTime = (sec: number) => {
  const m = Math.floor(sec / 60)
  const s = Math.floor(sec % 60)
  return `${m.toString().padStart(2, '0')}:${s.toString().padStart(2, '0')}`
}

const formatDuration = (ms: number) => {
  const v = Number(ms || 0)
  if (!v) return '-'
  const totalSec = Math.max(0, Math.round(v / 1000))
  if (totalSec < 60) return `${totalSec}s`
  const mm = Math.floor(totalSec / 60)
  const ss = totalSec % 60
  return `${mm}m ${ss}s`
}

const normalizeDurationMs = (file: any) => {
  const ms = file?.analysisDurationMs ?? file?.durationMs ?? file?.costMs ?? file?.elapsedMs
  if (ms != null) return Number(ms || 0)
  const sec = Number(file?.durationSec ?? 0)
  return sec > 0 ? sec * 1000 : 0
}

// ============ 统计 ============
const totalCount = computed(() => currentDetailJob.value?.videos?.length || 0)
const doneCount = computed(() => {
  const vids = currentDetailJob.value?.videos || []
  return vids.filter((v: any) => [2, 3].includes(Number(v.status))).length
})

const selectAllChecked = computed(() => {
  const list = filteredVideos.value
  if (!list.length) return false
  return list.every(v => selectedFileIds.value.includes(v.id))
})

const selectAllIndeterminate = computed(() => {
  const list = filteredVideos.value
  if (!list.length) return false
  const hit = list.filter(v => selectedFileIds.value.includes(v.id)).length
  return hit > 0 && hit < list.length
})

// ============ enrichedVideos ============
const enrichedVideos = computed<EnrichedVideo[]>(() => {
  if (!currentDetailJob.value || !currentDetailJob.value.videos) return []

  return currentDetailJob.value.videos.map((file: any) => {
    const fileEvents = allEvents.value.filter(e => e.videoFileId === file.id)

    const stats: VideoStats = {
      spark: 0,
      flash: 0,
      total: fileEvents.length,
      maxConf: 0,
      firstEventTime: Infinity
    }

    fileEvents.forEach(e => {
      if (e.eventType === 2) stats.spark++
      if (e.eventType === 1) stats.flash++
      stats.maxConf = Math.max(stats.maxConf, toNum(e.confidence))
      stats.firstEventTime = Math.min(stats.firstEventTime, e.peakTimeSec)
    })

    if (stats.firstEventTime === Infinity) stats.firstEventTime = 0

    return {
      ...file,
      analysisDurationMs: normalizeDurationMs(file),
      stats,
      events: fileEvents
    }
  })
})

// ============ 详情页：视频列表过滤（含阈值/类型/仅看有事件） ============
const filteredVideos = computed(() => {
  let list = enrichedVideos.value

  if (filterHasEvents.value) list = list.filter(v => v.stats.total > 0)
  if (filterType.value === 'Spark') list = list.filter(v => v.stats.spark > 0)
  else if (filterType.value === 'Flash') list = list.filter(v => v.stats.flash > 0)
  if (filterConf.value > 0) list = list.filter(v => v.stats.maxConf >= filterConf.value)

  return list.slice().sort((a, b) => {
    if (b.stats.total !== a.stats.total) return b.stats.total - a.stats.total
    if (b.stats.maxConf !== a.stats.maxConf) return b.stats.maxConf - a.stats.maxConf
    if (a.stats.total > 0 && b.stats.total > 0 && a.stats.firstEventTime !== b.stats.firstEventTime)
      return a.stats.firstEventTime - b.stats.firstEventTime
    return a.id - b.id
  })
})

const currentVideo = computed(() => enrichedVideos.value.find(v => v.id === currentFileId.value))
const currentFileName = computed(() => currentVideo.value?.fileName || '-')

const currentVideoUrl = computed(() => {
  if (!currentFileId.value) return ''
  return videoAnalyticsService.getVideoContentUrl(currentFileId.value)
})

// ============ 详情页：全量事件筛选 + 分组 ============
const allFilteredEvents = computed(() => {
  let evts = allEvents.value.slice()

  if (filterType.value === 'Spark') evts = evts.filter(e => e.eventType === 2)
  if (filterType.value === 'Flash') evts = evts.filter(e => e.eventType === 1)
  evts = evts.filter(e => toNum(e.confidence) >= filterConf.value)

  return evts.sort((a, b) => a.videoFileId - b.videoFileId || a.peakTimeSec - b.peakTimeSec)
})

const allEventGroups = computed<WallGroup[]>(() => {
  const videos = enrichedVideos.value
  if (!videos.length) return []

  const vmap = new Map<number, EnrichedVideo>()
  videos.forEach(v => vmap.set(v.id, v))

  const gmap = new Map<number, EventDto[]>()
  allFilteredEvents.value.forEach(e => {
    const vid = Number(e.videoFileId)
    if (!gmap.has(vid)) gmap.set(vid, [])
    gmap.get(vid)!.push(e)
  })

  let groups: WallGroup[] = []
  for (const [videoId, events] of gmap.entries()) {
    const v = vmap.get(videoId)
    if (!v) continue

    const spark = events.filter(x => x.eventType === 2).length
    const flash = events.filter(x => x.eventType === 1).length
    const maxConf = events.reduce((m, x) => Math.max(m, toNum(x.confidence)), 0)
    const firstEventTime = events.reduce((m, x) => Math.min(m, x.peakTimeSec), Infinity)
    groups.push({
      videoId,
      fileName: (v as any).fileName ?? (v as any).originalName ?? String(videoId),
      status: Number((v as any).status ?? 0),
      spark,
      flash,
      total: events.length,
      maxConf,
      firstEventTime: firstEventTime === Infinity ? 0 : firstEventTime,
      events
    })
  }

  if (filterHasEvents.value) groups = groups.filter(g => g.total > 0)

  return groups.sort((a, b) => {
    if (b.total !== a.total) return b.total - a.total
    if (b.maxConf !== a.maxConf) return b.maxConf - a.maxConf
    return a.firstEventTime - b.firstEventTime
  })
})

// ============ 清单页：分页与网格（pageSize = gridMode） ============
const listPageSize = computed(() => gridMode.value)

const listAllVideos = computed(() => {
  let list = enrichedVideos.value.slice().sort((a, b) => a.id - b.id)
  if (onlyAbnormal.value) list = list.filter(v => v.stats.total > 0)
  return list
})

const listTotal = computed(() => listAllVideos.value.length)
const listPageCount = computed(() => Math.max(1, Math.ceil(listTotal.value / listPageSize.value)))

const listPageVideos = computed(() => {
  const start = (listPageNo.value - 1) * listPageSize.value
  return listAllVideos.value.slice(start, start + listPageSize.value)
})

const emptyCells = computed(() => {
  const n = listPageSize.value - listPageVideos.value.length
  return n > 0 ? Array.from({ length: n }, (_, i) => i + 1) : []
})

const gridClass = computed(() => {
  const v = gridMode.value
  if (v === 4) return 'grid-2'
  if (v === 9) return 'grid-3'
  return 'grid-4'
})

// ============ 生命周期 ============
onMounted(async () => {
  window.addEventListener('keydown', handleKeydown)
  if (props.job) {
    currentDetailJob.value = props.job
    uploadClosed.value = Number((props.job as any).totalVideoCount ?? 0) > 0
    await initWorkbench()
  }
})

onUnmounted(() => {
  stopPolling()
  window.removeEventListener('keydown', handleKeydown)
})

// 全量筛选事件变化：预热快照（详情页用）
watch(
  () => allFilteredEvents.value.map(e => e.id).join(','),
  async () => {
    await nextTick()
    preloadSnapshots(allFilteredEvents.value.slice(0, 60))
  }
)

// gridMode 改变：分页大小改变 -> 回到第一页，并保证选中项合理
watch(
  () => gridMode.value,
  () => {
    listPageNo.value = 1
    nextTick(() => {
      if (!listSelectedId.value) listSelectedId.value = listPageVideos.value[0]?.id ?? null
    })
  }
)

// 仅异常开关：数据集变化 -> 回到第一页
watch(
  () => onlyAbnormal.value,
  () => {
    listPageNo.value = 1
    nextTick(() => {
      listSelectedId.value = listPageVideos.value[0]?.id ?? null
    })
  }
)

// 数据刷新后：清单保底选中
watch(
  () => enrichedVideos.value.map(v => v.id).join(','),
  () => {
    if (!listSelectedId.value) listSelectedId.value = listPageVideos.value[0]?.id ?? null
    if (listPageNo.value > listPageCount.value) listPageNo.value = listPageCount.value
  }
)

// ============ 详情页：选中/重新分析 ============
const isSelected = (id: number) => selectedFileIds.value.includes(id)

const toggleSelected = (id: number, checked: any) => {
  const on = !!checked
  const exists = selectedFileIds.value.includes(id)
  if (on && !exists) selectedFileIds.value = [...selectedFileIds.value, id]
  if (!on && exists) selectedFileIds.value = selectedFileIds.value.filter(x => x !== id)
}

const toggleSelectAll = (checked: any) => {
  const on = !!checked
  const ids = filteredVideos.value.map(v => v.id)
  if (!ids.length) return
  if (on) {
    const set = new Set<number>(selectedFileIds.value)
    ids.forEach(id => set.add(id))
    selectedFileIds.value = Array.from(set)
  } else {
    const set = new Set<number>(ids)
    selectedFileIds.value = selectedFileIds.value.filter(id => !set.has(id))
  }
}

const reanalyzeSelected = async () => {
  if (!currentDetailJob.value?.jobNo) return
  const ids = Array.from(new Set(selectedFileIds.value)).filter(x => Number(x) > 0).map(Number)
  if (!ids.length) return

  reanalyzing.value = true
  try {
    const res = await videoAnalyticsService.reanalyze(currentDetailJob.value.jobNo, ids)
    if (res.success) {
      ElMessage.success(`已重新入队：${res.data?.requeuedCount ?? ids.length} 个视频`)
      selectedFileIds.value = []
      await refreshDetail()
      startPolling()
    } else {
      ElMessage.error(res.message || '重新分析失败')
    }
  } catch {
    ElMessage.error('重新分析失败')
  } finally {
    reanalyzing.value = false
  }
}

// ============ 初始化/轮询/刷新 ============
const initWorkbench = async () => {
  loadingDetail.value = true
  await refreshDetail()
  await nextTick()

  if (!currentFileId.value && currentDetailJob.value?.videos?.length) {
    const first = currentDetailJob.value.videos[0]
    if (first?.id) selectVideo(first.id)
  }

  if (!listSelectedId.value) listSelectedId.value = listPageVideos.value[0]?.id ?? null

  loadingDetail.value = false

  if (currentDetailJob.value && [0, 1].includes(Number(currentDetailJob.value.status))) {
    startPolling()
  }
}

const refreshDetail = async () => {
  if (!currentDetailJob.value) return

  try {
    const [jobRes, evtRes] = await Promise.all([
      videoAnalyticsService.getJob(currentDetailJob.value.jobNo),
      videoAnalyticsService.getJobEvents(currentDetailJob.value.jobNo)
    ])

    if (jobRes.success) {
      currentDetailJob.value = jobRes.data
      uploadClosed.value = Number((jobRes.data as any).totalVideoCount ?? 0) > 0
    }
    if (evtRes.success) allEvents.value = evtRes.data

    if (!currentFileId.value) {
      const first = currentDetailJob.value?.videos?.[0]
      if (first?.id) selectVideo(first.id)
    } else {
      const exists = currentDetailJob.value?.videos?.some(v => v.id === currentFileId.value)
      if (!exists) {
        const first = currentDetailJob.value?.videos?.[0]
        if (first?.id) selectVideo(first.id)
      }
    }

    if (listPageNo.value > listPageCount.value) listPageNo.value = listPageCount.value

    if (currentDetailJob.value && ![0, 1].includes(Number(currentDetailJob.value.status))) {
      stopPolling()
    }
  } catch (e) {
    console.error(e)
  }
}

const startPolling = () => {
  if (pollTimer) return
  pollTimer = window.setInterval(async () => {
    if (!currentDetailJob.value) {
      stopPolling()
      return
    }
    await refreshDetail()
  }, 3000)
}

const stopPolling = () => {
  if (pollTimer) {
    clearInterval(pollTimer)
    pollTimer = null
  }
}

// ============ 详情页：选视频/事件定位 ============
const selectVideo = (fileId: number) => {
  currentFileId.value = fileId
  listSelectedId.value = fileId

  const v = enrichedVideos.value.find(x => x.id === fileId)
  if (v?.events?.length) {
    const evts = v.events
      .filter(e => toNum(e.confidence) >= filterConf.value)
      .filter(e => (filterType.value === 'ALL' ? true : filterType.value === 'Spark' ? e.eventType === 2 : e.eventType === 1))
      .slice()
      .sort((a, b) => a.peakTimeSec - b.peakTimeSec)
      .slice(0, 30)

    preloadSnapshots(evts)
  }

  nextTick(() => {
    if (!v) return
    const evts = v.events
      .filter(e => toNum(e.confidence) >= filterConf.value)
      .filter(e => (filterType.value === 'ALL' ? true : filterType.value === 'Spark' ? e.eventType === 2 : e.eventType === 1))
    if (evts.length > 0) {
      const bestEvt = evts.reduce((prev, curr) => (toNum(curr.confidence) > toNum(prev.confidence) ? curr : prev), evts[0])
      seekToEvent(bestEvt, false)
    }
  })
}

const seekToEventFromWall = async (videoId: number, evt: EventDto) => {
  if (currentFileId.value !== videoId) {
    selectVideo(videoId)
    await nextTick()
  }
  seekToEvent(evt, true)
}

const preloadSnapshots = async (events: EventDto[]) => {
  const pending = events.filter(e => !snapshotCache.value[e.id] && !snapshotLoading.value[e.id])
  if (!pending.length) return

  const chunkSize = 6
  for (let i = 0; i < pending.length; i += chunkSize) {
    const chunk = pending.slice(i, i + chunkSize)
    await Promise.all(chunk.map(async (e) => {
      snapshotLoading.value[e.id] = true
      try {
        const res = await videoAnalyticsService.getEventSnapshots(e.id)
        if (res.success && res.data.length > 0) {
          const snapId = res.data[0].id
          snapshotCache.value[e.id] = videoAnalyticsService.getSnapshotUrl(snapId)
        } else {
          snapshotCache.value[e.id] = ''
        }
      } catch {
        snapshotCache.value[e.id] = ''
      } finally {
        snapshotLoading.value[e.id] = false
      }
    }))
  }
}

const seekToEvent = (evt: EventDto, autoPlay = true) => {
  if (!videoRef.value) return
  const targetTime = Math.max(0, evt.peakTimeSec - 2)
  videoRef.value.currentTime = targetTime

  if (autoPlay) {
    videoRef.value.play()
    setTimeout(() => {
      if (videoRef.value && !videoRef.value.paused) videoRef.value.pause()
    }, 4000)
  }
}

const seekDelta = (sec: number) => {
  if (videoRef.value) videoRef.value.currentTime = Math.max(0, videoRef.value.currentTime + sec)
}

const onTimeUpdate = (e: Event) => {
  const v = e.target as HTMLVideoElement
  currentTime.value = v.currentTime
}

// ============ 清单页：表格/网格联动 + 翻页 ============
const onRowClick = (row: EnrichedVideo) => {
  listSelectedId.value = row.id
}

const selectFromGrid = (id: number) => {
  listSelectedId.value = id
}

const openDetail = (id: number) => {
  activeTab.value = 'detail'
  nextTick(() => selectVideo(id))
}

const rowClassName = ({ row }: any) => {
  return listSelectedId.value === row.id ? 'is-selected-row' : ''
}

const goPrevPage = () => {
  if (listPageNo.value > 1) listPageNo.value--
  listSelectedId.value = listPageVideos.value[0]?.id ?? null
}

const goNextPage = () => {
  if (listPageNo.value < listPageCount.value) listPageNo.value++
  listSelectedId.value = listPageVideos.value[0]?.id ?? null
}

// ============ 上传相关 ============
const beforeUpload = (file: File) => {
  if (!currentDetailJob.value?.jobNo) {
    ElMessage.warning('Job 未就绪')
    return false
  }
  if (uploadClosed.value) {
    ElMessage.warning('已关闭上传，不能再上传新视频')
    return false
  }
  return true
}

const onExceed = () => ElMessage.warning('选择文件过多，请分批上传')

const doUploadRequest = async (options: any) => {
  const file: File = options.file
  if (!currentDetailJob.value?.jobNo) return

  try {
    const res = await videoAnalyticsService.uploadOne(currentDetailJob.value.jobNo, file)
    if (res.success) {
      ElMessage.success(`上传成功：${file.name}，已入队分析`)
      await refreshDetail()

      if (currentDetailJob.value?.videos?.length) {
        const last = currentDetailJob.value.videos[currentDetailJob.value.videos.length - 1]
        if (last?.id) selectVideo(last.id)
      }

      startPolling()
    } else {
      ElMessage.error(res.message || `上传失败：${file.name}`)
    }
    options.onSuccess?.(res)
  } catch (e: any) {
    console.error(e)
    ElMessage.error(`上传失败：${file.name}`)
    options.onError?.(e)
  }
}

const closeUpload = async () => {
  if (!currentDetailJob.value?.jobNo) return
  closing.value = true
  try {
    const res = await videoAnalyticsService.closeJob(currentDetailJob.value.jobNo)
    if (res.success) {
      uploadClosed.value = true
      ElMessage.success('已关闭上传：后续不再接收新视频。等待队列处理完成后任务将自动完成。')
      startPolling()
      await refreshDetail()
    } else {
      ElMessage.error(res.message || '关闭失败')
    }
  } catch (e) {
    ElMessage.error('关闭失败')
  } finally {
    closing.value = false
  }
}

// ============ 快捷键 ============
const handleKeydown = (e: KeyboardEvent) => {
  if (!videoRef.value) return
  if (['INPUT', 'TEXTAREA'].includes((e.target as HTMLElement).tagName)) return

  switch (e.code) {
    case 'Space':
      e.preventDefault()
      videoRef.value.paused ? videoRef.value.play() : videoRef.value.pause()
      break
    case 'ArrowLeft':
      e.preventDefault()
      seekDelta(e.shiftKey ? -5 : -1)
      break
    case 'ArrowRight':
      e.preventDefault()
      seekDelta(e.shiftKey ? 5 : 1)
      break
  }
}
</script>

<style scoped>
.workbench-view {
  display: flex;
  flex-direction: column;
  height: 100%;
  background-color: #f5f7fa;
}

/* Tabs 容器 */
.tabs-wrap {
  flex: 1;
  overflow: hidden;
  padding: 10px;
  position: relative; /* 让 tabs-meta 绝对定位到 tab header 右侧 */
}

/* 任务信息放到 tab header 同一行右侧 */
.tabs-meta {
  position: absolute;
  top: 14px;            /* 与 el-tabs header 垂直对齐（card tabs 一般较高） */
  right: 16px;
  z-index: 6;
  display: flex;
  align-items: center;
  gap: 10px;
  max-width: 45%;
}

.meta-left {
  display: inline-flex;
  align-items: center;
  min-width: 0;
  gap: 6px;
}

.meta-right {
  display: inline-flex;
  align-items: center;
  flex-shrink: 0;
}

/* 给 tabs header 右侧预留空间，避免与 meta 覆盖 */
.wb-tabs :deep(.el-tabs__header) {
  margin: 0 0 10px 0;
}
.wb-tabs :deep(.el-tabs__nav-wrap) {
  padding-right: 420px; /* 关键：给右侧“任务编号/状态/帮助”腾位置 */
}

/* tabs 内容高度 */
.wb-tabs { height: 100%; }
:deep(.el-tabs__content) { height: calc(100% - 42px); overflow: hidden; }
:deep(.el-tab-pane) { height: 100%; }

.panel-header {
  padding: 10px 15px;
  border-bottom: 1px solid #ebeef5;
  font-weight: 700;
  font-size: 14px;
  background: #fafafa;
  display: flex;
  justify-content: space-between;
  align-items: center;
}
.panel-header.small { font-size: 12px; padding: 8px 10px; }

.sub-text { font-weight: 400; color: #909399; font-size: 12px; }
.text-gray { color: #909399; }

.ml-2 { margin-left: 8px; }
.mt-1 { margin-top: 4px; }
.text-danger { color: #f56c6c; }
.text-warning { color: #e6a23c; }

.job-title {
  font-weight: 700;
  font-size: 14px;
  max-width: 220px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.help-btn { border-color: #dcdfe6; }

/* ===== 清单页 ===== */
.list-body {
  height: 100%;
  display: flex;
  gap: 8px;            /* 缩小间距 */
  overflow: hidden;
}

.list-left, .list-right {
  background: #fff;
  border-radius: 6px;
  box-shadow: 0 1px 4px rgba(0,0,0,0.08);
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.list-left { width: 560px; flex-shrink: 0; }
.list-right { flex: 1; min-width: 640px; }

.lh-title { display: flex; align-items: baseline; gap: 8px; }
.lh-controls { display: flex; align-items: center; gap: 8px; }

.table-wrap { flex: 1; overflow: hidden; padding: 10px; }

.pager {
  padding: 10px;
  border-top: 1px solid #ebeef5;
  background: #fafafa;
  display: flex;
  justify-content: space-between;
  align-items: center;
}
.pager-right { display: flex; align-items: center; gap: 10px; }
.pager-mid { font-size: 12px; color: #606266; }

:deep(.is-selected-row td) { background-color: #ecf5ff !important; }

.mini-kpi { display: inline-flex; gap: 6px; }
.kpi { font-size: 12px; font-weight: 700; }
.kpi-danger { color: #f56c6c; }
.kpi-warning { color: #e6a23c; }

.lr-title { display: flex; align-items: baseline; gap: 8px; }
.lr-controls { display: flex; align-items: center; gap: 8px; }

.grid-stage {
  flex: 1;
  padding: 6px;         /* 缩小边距 */
  overflow: hidden;     /* 不允许滚动出现更多画面 */
}

/* 多画面：间距更小 */
.video-grid {
  height: 100%;
  display: grid;
  gap: 6px;             /* 画面之间间距更小 */
}

.video-grid.grid-2 { grid-template-columns: repeat(2, 1fr); grid-auto-rows: 1fr; }
.video-grid.grid-3 { grid-template-columns: repeat(3, 1fr); grid-auto-rows: 1fr; }
.video-grid.grid-4 { grid-template-columns: repeat(4, 1fr); grid-auto-rows: 1fr; }

.grid-cell {
  border: 1px solid #ebeef5;
  border-radius: 8px;
  overflow: hidden;
  background: #000;
  cursor: pointer;
  transition: all 0.2s;
  position: relative;
  min-height: 140px;
}

.grid-cell:hover { box-shadow: 0 4px 12px rgba(0,0,0,0.10); transform: translateY(-1px); }
.grid-cell.active { border-color: #409eff; box-shadow: 0 0 0 2px rgba(64,158,255,0.15) inset; }

/* 视频铺满：object-fit 改为 cover */
.grid-video {
  width: 100%;
  height: 100%;
  object-fit: cover;    /* 关键：铺满画面 */
  background: #000;
}

.grid-flame {
  position: absolute;
  top: 6px;
  right: 8px;
  z-index: 2;
  font-size: 18px;
  line-height: 1;
  text-shadow: 0 2px 6px rgba(0,0,0,0.5);
  pointer-events: none;
}

.grid-cell.empty {
  background: #f5f7fa;
  border-style: dashed;
  cursor: default;
}
.empty-tip {
  height: 100%;
  display: flex;
  align-items: center;
  justify-content: center;
  color: #c0c4cc;
  font-size: 12px;
}

.grid-hint { padding: 8px 10px; border-top: 1px solid #ebeef5; background: #fafafa; }

/* ===== 详情页筛选栏（置信度阈值在这里） ===== */
.detail-filter-bar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  background: #fff;
  border: 1px solid #ebeef5;
  border-radius: 6px;
  padding: 10px 12px;
  margin-bottom: 10px;
}
.df-left { display: flex; align-items: center; }
.filter-label { font-size: 12px; color: #606266; }

/* ===== 详情页布局 ===== */
.wb-body {
  height: calc(100% - 58px);
  display: flex;
  overflow: hidden;
  gap: 10px;
}

.wb-col-left, .wb-col-center, .wb-col-right {
  background: #fff;
  border-radius: 6px;
  box-shadow: 0 1px 4px rgba(0,0,0,0.08);
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.wb-col-left { width: 320px; flex-shrink: 0; }
.wb-col-center { flex: 1; min-width: 520px; }
.wb-col-right { width: 380px; flex-shrink: 0; }

.video-actions { display: flex; align-items: center; gap: 10px; }
.video-list { padding: 10px; }

.video-card {
  padding: 10px;
  border: 1px solid #ebeef5;
  border-radius: 6px;
  margin-bottom: 8px;
  cursor: pointer;
  transition: all 0.2s;
}
.video-card:hover { border-color: #c0c4cc; background-color: #fdfdfd; }
.video-card.active { border-color: #409eff; background-color: #ecf5ff; }

.vc-row1 { display: flex; justify-content: space-between; margin-bottom: 6px; gap: 8px; }
.vc-left { display: flex; align-items: center; gap: 8px; min-width: 0; flex: 1; }
.vc-right { display: flex; align-items: center; flex-shrink: 0; }

.vc-name {
  font-size: 13px;
  font-weight: 600;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  flex: 1;
}

.vc-row2 { display: flex; justify-content: space-between; align-items: center; }
.vc-row3 { margin-top: 6px; display: flex; justify-content: space-between; align-items: center; }

.vc-meta { font-size: 12px; color: #909399; }
.vc-conf { font-size: 12px; color: #67c23a; font-weight: 700; }

/* 事件墙 */
.group-wall { padding: 12px; }
.group-block { border: 1px solid #ebeef5; border-radius: 8px; margin-bottom: 12px; overflow: hidden; }

.group-header {
  position: sticky;
  top: 0;
  z-index: 2;
  background: #fafafa;
  border-bottom: 1px solid #ebeef5;
  padding: 10px 12px;
  display: flex;
  justify-content: space-between;
  align-items: center;
  cursor: pointer;
}

.gh-left { display: flex; align-items: center; min-width: 0; flex: 1; }
.gh-title { font-weight: 700; font-size: 13px; color: #303133; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.gh-right { display: flex; align-items: center; gap: 8px; flex-shrink: 0; }

.gh-meta { font-size: 12px; color: #606266; }
.gh-meta.strong { font-weight: 700; color: #67c23a; }

.event-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(160px, 1fr));
  gap: 12px;
  padding: 12px;
}

.event-card {
  border: 1px solid #ebeef5;
  border-radius: 6px;
  overflow: hidden;
  cursor: pointer;
  transition: transform 0.2s;
  background: #fff;
}

.event-card:hover { transform: translateY(-2px); box-shadow: 0 4px 12px rgba(0,0,0,0.10); }
.event-card.selected { border-color: #409eff; }

.ec-thumb {
  height: 104px;
  background: #f5f7fa;
  position: relative;
  display: flex;
  align-items: center;
  justify-content: center;
}

.ec-img { width: 100%; height: 100%; }
.ec-placeholder { font-size: 24px; color: #dcdfe6; }

.ec-time {
  position: absolute;
  bottom: 0;
  right: 0;
  background: rgba(0,0,0,0.6);
  color: #fff;
  font-size: 12px;
  padding: 2px 6px;
}

.ec-info { padding: 8px; }
.ec-row { display: flex; justify-content: space-between; align-items: center; margin-bottom: 4px; }
.ec-conf { font-size: 12px; font-weight: 700; color: #606266; }

/* 播放器 + 上传 */
.player-wrapper { background: #000; height: 260px; display: flex; flex-direction: column; }
.html-video { width: 100%; height: 100%; object-fit: contain; }

.player-controls {
  height: 40px;
  background: #333;
  display: flex;
  align-items: center;
  padding: 0 10px;
  justify-content: space-between;
}

.time-display { color: #fff; font-size: 12px; }

.upload-panel {
  flex: 1;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  border-top: 1px solid #dcdfe6;
}

.upload-box { padding: 10px; overflow: auto; }
.upload-sub { font-size: 12px; color: #909399; }

.upload-actions { margin-top: 10px; display: flex; flex-direction: column; gap: 8px; }
.upload-hint { font-size: 12px; color: #606266; }

.right-footer-hint { padding: 8px 10px; border-top: 1px solid #ebeef5; background: #fafafa; }

/* help */
.help-content { line-height: 1.7; color: #303133; }
.help-content h4 { margin: 10px 0 6px; font-size: 14px; }
.help-content ul { margin: 0 0 10px 18px; padding: 0; }
.help-content li { margin: 4px 0; }
</style>
