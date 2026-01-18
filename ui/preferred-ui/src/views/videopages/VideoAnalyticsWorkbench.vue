<template>
  <div class="workbench-view" :class="{ 'as-dialog': !!props.asDialog }">
    <!-- 顶部工具栏 -->
    <div class="wb-toolbar">
      <div class="wb-nav">
        <el-button link :icon="ArrowLeft" @click="$emit('back')">{{ props.asDialog ? '关闭' : '返回列表' }}</el-button>
        <el-divider direction="vertical" />
        <span class="job-title">{{ currentDetailJob?.jobNo }}</span>
        <el-tag v-if="currentDetailJob" size="small" :type="getStatusType(currentDetailJob.status)" class="ml-2">
          {{ getJobStatusText(currentDetailJob) }}
        </el-tag>

        <el-tag v-if="uploadClosed" size="small" type="info" class="ml-2" effect="plain">
          已关闭上传
        </el-tag>
      </div>

      <div class="wb-filters">
        <span class="filter-label">置信度阈值:</span>
        <el-slider v-model="filterConf" :min="0" :max="1" :step="0.05" style="width: 120px; margin: 0 12px" />

        <el-checkbox v-model="filterHasEvents" label="仅看有事件" border size="small" />

        <el-radio-group v-model="filterType" size="small" class="ml-2">
          <el-radio-button label="ALL">全部</el-radio-button>
          <el-radio-button label="Spark"><span class="text-danger">火花</span></el-radio-button>
          <el-radio-button label="Flash"><span class="text-warning">闪光</span></el-radio-button>
        </el-radio-group>

        <el-button class="ml-4" :icon="Refresh" circle size="small" @click="refreshDetail" />
      </div>
    </div>

    <!-- 主体三栏 -->
    <div class="wb-body" v-loading="loadingDetail">
      <!-- 左栏：上传区 + 视频列表 -->
      <div class="wb-col-left">
        <div class="panel-header">
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

        <div class="panel-header" style="border-top: 1px solid #ebeef5">
          <span>视频列表 ({{ filteredVideos.length }})</span>
          <div class="video-actions">
            <el-checkbox
              :indeterminate="selectAllIndeterminate"
              :model-value="selectAllChecked"
              @change="toggleSelectAll"
            >
              全选
            </el-checkbox>
            <el-popconfirm
              title="将清空所选视频的历史事件/截图，并重新入队分析，是否继续？"
              @confirm="reanalyzeSelected"
            >
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
              <div class="vc-check" @click.stop>
                <el-checkbox
                  :model-value="isSelected(vid.id)"
                  @change="toggleSelected(vid.id, $event)"
                />
              </div>
              <div class="vc-row1">
                <span class="vc-name" :title="vid.fileName">{{ vid.fileName }}</span>
                <el-tag size="small" effect="plain">{{ getVideoStatusText(vid.status) }}</el-tag>
              </div>

              <div class="vc-row2">
                <div class="vc-badges">
                  <el-tag v-if="vid.stats.spark > 0" type="danger" size="small" effect="dark">
                    火 {{ vid.stats.spark }}
                  </el-tag>
                  <el-tag v-if="vid.stats.flash > 0" type="warning" size="small" effect="dark">
                    闪 {{ vid.stats.flash }}
                  </el-tag>
                  <span v-if="vid.stats.total === 0" class="text-gray">无异常</span>
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

      <!-- 中栏：事件预览墙 -->
      <div class="wb-col-center">
        <div class="panel-header">
          <span>事件预览 - {{ currentFileName }}</span>
          <span class="sub-text"> (共 {{ currentVideoFilteredEvents.length }} 个事件)</span>
        </div>

        <el-scrollbar>
          <div v-if="currentVideoFilteredEvents.length > 0" class="event-grid">
            <div
              v-for="evt in currentVideoFilteredEvents"
              :key="evt.id"
              class="event-card"
              @click="seekToEvent(evt)"
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
                <el-progress
                  :percentage="Math.round(Number(evt.confidence) * 100)"
                  :show-text="false"
                  class="mt-1"
                />
              </div>
            </div>
          </div>
          <el-empty v-else description="当前筛选条件下无事件" />
        </el-scrollbar>
      </div>

      <!-- 右栏：播放器与时间轴 -->
      <div class="wb-col-right">
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

        <div class="timeline-wrapper">
          <div class="panel-header small">事件时间轴</div>
          <el-scrollbar>
            <ul class="timeline-list">
              <li
                v-for="evt in currentVideoFilteredEvents"
                :key="evt.id"
                class="timeline-item"
                :class="{ active: isEventActive(evt) }"
                @click="seekToEvent(evt)"
              >
                <div class="tl-time">{{ formatTime(evt.peakTimeSec) }}</div>
                <div class="tl-type" :class="evt.eventType === 2 ? 'Spark' : 'Flash'">
                  {{ evt.eventType === 2 ? '火花' : '闪光' }}
                </div>
                <div class="tl-conf">{{ (Number(evt.confidence) * 100).toFixed(0) }}%</div>
              </li>
            </ul>
          </el-scrollbar>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { videoAnalyticsService, type EventDto, type JobDetailDto, type JobVideoDto } from '@/services/videoAnalyticsService';
import { ArrowLeft, Picture, Refresh } from '@element-plus/icons-vue';
import { ElMessage } from 'element-plus';
import { computed, nextTick, onMounted, onUnmounted, ref, watch } from 'vue';

const props = defineProps<{ job: JobDetailDto; asDialog?: boolean }>()
defineEmits(['back'])

// ================= 类型定义 =================
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

// ================= 状态管理 =================
const currentDetailJob = ref<JobDetailDto | null>(null)
const loadingDetail = ref(false)
const allEvents = ref<EventDto[]>([])
const currentFileId = ref<number | null>(null)

const snapshotCache = ref<Record<number, string>>({}) // eventId -> snapshotUrl
const snapshotLoading = ref<Record<number, boolean>>({}) // eventId -> loading

// 上传/关闭
const uploadClosed = ref(false) // 前端态：关闭后禁用上传（后端也应拒绝）
const closing = ref(false)
const canUpload = ref(true)

// 重新分析
const selectedFileIds = ref<number[]>([])
const reanalyzing = ref(false)

// 过滤器（默认不过滤，确保持续上传能“立刻看到视频列表”）
const filterConf = ref(0)
const filterHasEvents = ref(false)
const filterType = ref<'ALL' | 'Spark' | 'Flash'>('ALL')

// 播放器
const videoRef = ref<HTMLVideoElement>()
const currentTime = ref(0)
let pollTimer: number | null = null

// ================= 工具函数 =================
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
  // 说明：你当前 DTO 没有“分析耗时”字段，这里做兜底：
  // - 若后端补了 analysisDurationMs/durationMs 等字段，会自动显示
  // - 否则用 durationSec(视频时长) 作为展示，避免一直是 '-'
  const ms =
    file?.analysisDurationMs ??
    file?.durationMs ??
    file?.costMs ??
    file?.elapsedMs

  if (ms != null) return Number(ms || 0)

  const sec = Number(file?.durationSec ?? 0)
  return sec > 0 ? sec * 1000 : 0
}

// ================= 统计信息 =================
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

// ================= 计算属性 =================
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

const currentVideoFilteredEvents = computed(() => {
  if (!currentVideo.value) return []

  let evts = currentVideo.value.events
  if (filterType.value === 'Spark') evts = evts.filter(e => e.eventType === 2)
  if (filterType.value === 'Flash') evts = evts.filter(e => e.eventType === 1)
  evts = evts.filter(e => toNum(e.confidence) >= filterConf.value)

  return evts.slice().sort((a, b) => a.peakTimeSec - b.peakTimeSec)
})

const currentVideoUrl = computed(() => {
  if (!currentFileId.value) return ''
  return videoAnalyticsService.getVideoContentUrl(currentFileId.value)
})

// ================= 生命周期与方法 =================
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

// 当“当前视频事件列表”变化时（轮询刷新后常见），自动加载快照，让事件墙立刻更新
watch(
  () => currentVideoFilteredEvents.value.map(e => e.id).join(','),
  async () => {
    if (!currentFileId.value) return
    await nextTick()
    loadSnapshotsForCurrentVideo()
  }
)

watch(
  () => currentDetailJob.value?.videos?.map(v => v.id).join(',') || '',
  () => {
    const ids = new Set((currentDetailJob.value?.videos || []).map(v => v.id))
    selectedFileIds.value = selectedFileIds.value.filter(id => ids.has(id))
  }
)

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

const initWorkbench = async () => {
  loadingDetail.value = true
  await refreshDetail()
  await nextTick()

  // 初次进入：如果有视频但未选中，默认选第一个（持续上传场景更符合直觉）
  if (!currentFileId.value && currentDetailJob.value?.videos?.length) {
    const first = currentDetailJob.value.videos[0]
    if (first?.id) selectVideo(first.id)
  }

  loadingDetail.value = false

  // 轮询：只要 job 还在等待/分析，就持续刷新
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
      // 可选：如果后端提供了 uploadClosed 字段，可同步
      // uploadClosed.value = !!(jobRes.data as any).uploadClosed
    }
    if (evtRes.success) allEvents.value = evtRes.data

    // 确保有选中视频（持续上传时，刷新可能先于 selectVideo）
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

    // 分析结束则停
    if (currentDetailJob.value && ![0, 1].includes(Number(currentDetailJob.value.status))) {
      stopPolling()
    }
  } catch (e) {
    console.error(e)
  }
}

const selectVideo = (fileId: number) => {
  currentFileId.value = fileId
  loadSnapshotsForCurrentVideo()

  nextTick(() => {
    const evts = currentVideoFilteredEvents.value
    if (evts.length > 0) {
      const bestEvt = evts.reduce((prev, curr) => (toNum(curr.confidence) > toNum(prev.confidence) ? curr : prev), evts[0])
      seekToEvent(bestEvt, false)
    }
  })
}

const loadSnapshotsForCurrentVideo = async () => {
  const evts = currentVideoFilteredEvents.value
  const pending = evts.filter(e => !snapshotCache.value[e.id] && !snapshotLoading.value[e.id])

  const chunkSize = 5
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

const isEventActive = (evt: EventDto) => Math.abs(currentTime.value - evt.peakTimeSec) < 1

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

// ================= 上传相关 =================
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

      // 上传后立刻刷新，让“视频列表”立刻出现
      await refreshDetail()

      // 每次上传成功都选中新视频（体验最佳）
      if (currentDetailJob.value?.videos?.length) {
        const last = currentDetailJob.value.videos[currentDetailJob.value.videos.length - 1]
        if (last?.id) selectVideo(last.id)
      }

      // 确保轮询开启（事件异步产生）
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

// ================= 快捷键 =================
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

.workbench-view.as-dialog {
  height: 100%;
  background-color: transparent;
}

.workbench-view.as-dialog .wb-body {
  padding: 0;
}

.workbench-view.as-dialog .wb-toolbar {
  border-bottom: 0;
  padding: 6px 6px 10px;
  background: transparent;
}

.workbench-view.as-dialog .wb-col-left,
.workbench-view.as-dialog .wb-col-center,
.workbench-view.as-dialog .wb-col-right {
  border-radius: 8px;
}

.wb-toolbar {
  height: 50px;
  background: #fff;
  border-bottom: 1px solid #dcdfe6;
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 0 20px;
  flex-shrink: 0;
}

.wb-nav {
  display: flex;
  align-items: center;
}

.job-title {
  font-weight: 700;
  font-size: 16px;
  margin-left: 10px;
}

.wb-filters {
  display: flex;
  align-items: center;
}

.filter-label {
  font-size: 12px;
  color: #606266;
}

.wb-body {
  flex: 1;
  display: flex;
  overflow: hidden;
  padding: 10px;
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
.wb-col-center { flex: 1; min-width: 420px; }
.wb-col-right { width: 360px; flex-shrink: 0; }

.panel-header {
  padding: 10px 15px;
  border-bottom: 1px solid #ebeef5;
  font-weight: 700;
  font-size: 14px;
  background: #fafafa;
  display: flex;
  justify-content: space-between;
}

.video-actions {
  display: flex;
  align-items: center;
  gap: 10px;
}

.panel-header.small {
  font-size: 12px;
  padding: 8px 10px;
}

.sub-text {
  font-weight: 400;
  color: #909399;
  font-size: 12px;
}

.upload-box {
  padding: 10px;
}

.upload-sub {
  font-size: 12px;
  color: #909399;
}

.upload-actions {
  margin-top: 10px;
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.upload-hint {
  font-size: 12px;
  color: #606266;
}

.video-list { padding: 10px; }

.video-card {
  padding: 10px;
  border: 1px solid #ebeef5;
  border-radius: 6px;
  margin-bottom: 8px;
  cursor: pointer;
  transition: all 0.2s;
  position: relative;
}

.vc-check {
  position: absolute;
  top: 6px;
  left: 6px;
  z-index: 2;
}

.video-card:hover {
  border-color: #c0c4cc;
  background-color: #fdfdfd;
}

.video-card.active {
  border-color: #409eff;
  background-color: #ecf5ff;
}

.vc-row1 {
  display: flex;
  justify-content: space-between;
  margin-bottom: 6px;
}

.vc-name {
  font-size: 13px;
  font-weight: 600;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  max-width: 190px;
}

.vc-row2 {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.vc-row3 {
  margin-top: 6px;
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.vc-meta {
  font-size: 12px;
  color: #909399;
}

.vc-conf {
  font-size: 12px;
  color: #67c23a;
  font-weight: 700;
}

.text-gray { color: #909399; font-size: 12px; }

.event-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(160px, 1fr));
  gap: 15px;
  padding: 15px;
}

.event-card {
  border: 1px solid #ebeef5;
  border-radius: 6px;
  overflow: hidden;
  cursor: pointer;
  transition: transform 0.2s;
}

.event-card:hover {
  transform: translateY(-2px);
  box-shadow: 0 4px 12px rgba(0,0,0,0.10);
}

.ec-thumb {
  height: 100px;
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

.ec-row {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 4px;
}

.ec-conf {
  font-size: 12px;
  font-weight: 700;
  color: #606266;
}

.player-wrapper {
  background: #000;
  height: 240px;
  display: flex;
  flex-direction: column;
}

.html-video {
  width: 100%;
  height: 100%;
  object-fit: contain;
}

.player-controls {
  height: 40px;
  background: #333;
  display: flex;
  align-items: center;
  padding: 0 10px;
  justify-content: space-between;
}

.time-display { color: #fff; font-size: 12px; }

.timeline-wrapper {
  flex: 1;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  border-top: 1px solid #dcdfe6;
}

.timeline-list {
  padding: 0;
  margin: 0;
  list-style: none;
}

.timeline-item {
  display: flex;
  padding: 10px 15px;
  border-bottom: 1px solid #f0f0f0;
  cursor: pointer;
  font-size: 13px;
  align-items: center;
}

.timeline-item:hover { background-color: #f5f7fa; }
.timeline-item.active { background-color: #ecf5ff; border-left: 3px solid #409eff; }

.tl-time { width: 60px; font-family: monospace; color: #606266; }
.tl-type { flex: 1; font-weight: 700; }
.tl-type.Spark { color: #f56c6c; }
.tl-type.Flash { color: #e6a23c; }
.tl-conf { color: #909399; font-size: 12px; }

.ml-2 { margin-left: 8px; }
.ml-4 { margin-left: 16px; }
.mt-1 { margin-top: 4px; }
.text-danger { color: #f56c6c; }
.text-warning { color: #e6a23c; }
</style>
