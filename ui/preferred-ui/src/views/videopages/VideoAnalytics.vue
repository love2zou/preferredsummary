<template>
  <div class="va-container">
    <div class="list-view">
      <div class="list-header">
        <h2>视频智能分析任务</h2>
        <div class="list-actions">
          <el-input
            v-model="searchJobNo"
            placeholder="请输入任务编号查询"
            class="search-input"
            @keyup.enter="handleSearch"
            clearable
            @clear="handleSearch"
          >
            <template #append>
              <el-button :icon="Search" @click="handleSearch" />
            </template>
          </el-input>

          <el-button type="primary" @click="openCreateDialog" :icon="Plus">新建持续上传会话</el-button>
        </div>
      </div>

      <el-table
        v-loading="loadingList"
        :data="jobList"
        border
        stripe
        class="job-table"
      >
        <el-table-column prop="jobNo" label="任务编号" min-width="200">
          <template #default="{ row }">
            <span class="link-text" @click="enterWorkbench(row)">{{ row.jobNo }}</span>
          </template>
        </el-table-column>

        <el-table-column prop="status" label="状态" width="130">
          <template #default="{ row }">
            <el-tag :type="getStatusType(row.status)">{{ getJobStatusText(row) }}</el-tag>
          </template>
        </el-table-column>

        <el-table-column prop="progress" label="进度" width="170">
          <template #default="{ row }">
            <el-progress
              :percentage="row.progress ?? calcProgress(row)"
              :status="row.status === 3 ? 'exception' : undefined"
            />
          </template>
        </el-table-column>

        <el-table-column label="开始时间" width="180">
          <template #default="{ row }">
            {{ formatDateTime(row.startTime) }}
          </template>
        </el-table-column>

        <el-table-column label="文件数" width="110" align="center">
          <template #default="{ row }">
            {{ (row.videos && row.videos.length) || 0 }}
          </template>
        </el-table-column>

        <el-table-column label="操作" width="170" fixed="right">
          <template #default="{ row }">
            <el-button link type="primary" @click="enterWorkbench(row)">进入工作台</el-button>
            <el-popconfirm title="确认删除任务?" @confirm="handleDeleteJob(row.jobNo)">
              <template #reference>
                <el-button link type="danger">删除</el-button>
              </template>
            </el-popconfirm>
          </template>
        </el-table-column>
      </el-table>
    </div>

    <el-dialog
      v-model="workbenchVisible"
      title="智能分析工作台"
      fullscreen
      class="va-workbench-dialog"
      :destroy-on-close="true"
      :close-on-click-modal="false"
      @closed="onWorkbenchClosed"
    >
      <VideoAnalyticsWorkbench
        v-if="workbenchVisible && currentDetailJob"
        :job="currentDetailJob"
        :as-dialog="true"
        @back="exitWorkbench"
      />
    </el-dialog>

    <!-- 新建会话弹窗（仅创建 Job，不上传参数，不上传文件） -->
    <el-dialog
      v-model="createDialogVisible"
      title="新建持续上传会话"
      width="760px"
      top="8vh"
      :close-on-click-modal="false"
      :destroy-on-close="true"
    >
      <div class="create-tip">
        <el-alert title="说明" type="info" :closable="false" show-icon>
          <template #default>
            <div class="tip-text">
              该会话支持<strong>持续上传</strong>：你可以不断上传视频，服务端收到视频后立即入队分析；
              当你确认不再上传时，在工作台点击<strong>关闭上传</strong>即可完成收尾。
            </div>
            <div class="tip-sub">
              本页创建会话<strong>不上传算法参数</strong>，后端将使用默认参数。
              如需调整“重新分析”入队参数，可在工作台的<strong>参数设置</strong>里临时修改（不保存）。
            </div>
          </template>
        </el-alert>
      </div>

      <template #footer>
        <el-button @click="createDialogVisible = false">取消</el-button>
        <el-button type="primary" @click="createJobAndEnter" :loading="submitting">创建并进入工作台</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { videoAnalyticsService, type JobDetailDto } from '@/services/videoAnalyticsService'
import { Plus, Search } from '@element-plus/icons-vue'
import { useLocalStorage } from '@vueuse/core'
import { ElMessage } from 'element-plus'
import { onMounted, ref } from 'vue'
import VideoAnalyticsWorkbench from './VideoAnalyticsWorkbench.vue'

const loadingList = ref(false)
const searchJobNo = ref('')
const jobHistory = useLocalStorage<string[]>('video_analytics_jobs', [])
const jobList = ref<JobDetailDto[]>([])

const currentDetailJob = ref<JobDetailDto | null>(null)
const workbenchVisible = ref(false)

// 创建会话（仅创建 Job）
const createDialogVisible = ref(false)
const submitting = ref(false)

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

const getJobStatusText = (job: any) => {
  const status = Number(job?.status)
  if (status === 1) {
    const totalVideoCount = Number(job?.totalVideoCount ?? 0)
    const vids = job?.videos || []
    if (totalVideoCount <= 0) {
      if (!vids.length) return '等待上传'
      const p = calcProgress(job)
      if (p >= 100) return '待关闭上传'
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

const formatDateTime = (dateStr?: string | null) => {
  if (!dateStr) return '-'
  const d = new Date(dateStr)
  if (isNaN(d.getTime())) return dateStr
  return d.toLocaleString('zh-CN', { hour12: false })
}

const calcProgress = (job: any) => {
  const vids = job?.videos || []
  if (!vids.length) return 0
  const done = vids.filter((v: any) => [2, 3].includes(Number(v.status))).length
  return Math.round((done / vids.length) * 100)
}

onMounted(() => {
  fetchJobList()
})

const fetchJobList = async () => {
  loadingList.value = true
  const list: JobDetailDto[] = []

  if (searchJobNo.value.trim()) {
    try {
      const res = await videoAnalyticsService.getJob(searchJobNo.value.trim())
      if (res.success) list.push(res.data)
    } catch { /* ignore */ }
  } else {
    const recent = jobHistory.value.slice(0, 10)
    const toRemove: string[] = []
    await Promise.all(recent.map(async (jn) => {
      try {
        const res = await videoAnalyticsService.getJob(jn)
        if (res.success) list.push(res.data)
      } catch (e: any) {
        if (e?.response?.status === 404) toRemove.push(jn)
      }
    }))
    if (toRemove.length) {
      const removeSet = new Set(toRemove)
      jobHistory.value = jobHistory.value.filter(x => !removeSet.has(x))
    }
  }

  jobList.value = list.sort((a: any, b: any) => {
    const ta = a.startTime ? new Date(a.startTime).getTime() : 0
    const tb = b.startTime ? new Date(b.startTime).getTime() : 0
    return tb - ta
  })

  loadingList.value = false
}

const handleSearch = () => fetchJobList()

const handleDeleteJob = async (jn: string) => {
  try {
    const res = await videoAnalyticsService.deleteJob(jn)
    if (res.success) {
      ElMessage.success('删除成功')
      jobHistory.value = jobHistory.value.filter(x => x !== jn)
      fetchJobList()
    } else {
      ElMessage.error(res.message || '删除失败')
    }
  } catch {
    ElMessage.error('删除请求失败')
  }
}

const enterWorkbench = (row: JobDetailDto) => {
  currentDetailJob.value = row
  workbenchVisible.value = true
}

const exitWorkbench = () => {
  workbenchVisible.value = false
}

const onWorkbenchClosed = () => {
  currentDetailJob.value = null
  fetchJobList()
}

const openCreateDialog = () => {
  createDialogVisible.value = true
}

const createJobAndEnter = async () => {
  submitting.value = true
  try {
    // 关键：不上传参数（传空字符串，让后端用默认 DefaultAlgoParamsJson）
    const res = await videoAnalyticsService.createJob('')
    if (!res.success) {
      ElMessage.error(res.message || '创建失败')
      return
    }

    const jobNo = res.data.jobNo
    ElMessage.success(`会话创建成功: ${jobNo}`)

    jobHistory.value = [jobNo, ...jobHistory.value.filter(x => x !== jobNo)].slice(0, 20)

    const jobRes = await videoAnalyticsService.getJob(jobNo)
    currentDetailJob.value = jobRes.success ? jobRes.data : ({ ...(res.data as any), jobNo } as any)

    createDialogVisible.value = false
    workbenchVisible.value = true
  } catch {
    ElMessage.error('创建失败')
  } finally {
    submitting.value = false
  }
}
</script>

<style scoped>
.va-container {
  height: 100vh;
  background-color: #f5f7fa;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.list-view {
  padding: 20px;
  background: #fff;
  height: 100%;
  overflow: hidden;
}

.list-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 12px;
}

.search-input {
  width: 260px;
  margin-right: 10px;
}

.job-table {
  width: 100%;
  height: calc(100vh - 108px);
}

.link-text {
  color: #409eff;
  cursor: pointer;
  font-weight: 700;
}

.create-tip {
  margin-top: 8px;
}

.tip-text {
  line-height: 1.6;
}

.tip-sub {
  margin-top: 6px;
  font-size: 12px;
  color: #909399;
  line-height: 1.6;
}

:deep(.va-workbench-dialog) {
  margin: 0;
}

:deep(.va-workbench-dialog .el-dialog__body) {
  padding: 0;
  height: calc(100vh - 54px);
  overflow: hidden;
}
</style>
