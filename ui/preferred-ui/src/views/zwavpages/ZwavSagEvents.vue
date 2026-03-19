<template>
  <div class="zwav-sag-container">
    <div class="page-header">
      <h2>录波暂降分析</h2>
    </div>

    <div class="search-bar">
      <div class="search-row">
        <el-form :inline="true" :model="searchForm" class="search-form">
          <el-form-item label="关键词">
            <el-input
              v-model="searchForm.keyword"
              placeholder="录波文件名"
              clearable
              @keyup.enter="handleSearch"
            />
          </el-form-item>

          <el-form-item label="事件类型">
            <el-select v-model="searchForm.eventType" placeholder="全部" clearable style="width: 140px">
              <el-option label="正常" value="Normal" />
              <el-option label="暂降" value="Sag" />
              <el-option label="中断" value="Interruption" />
            </el-select>
          </el-form-item>

          <el-form-item label="相别">
            <el-select v-model="searchForm.phase" placeholder="全部" clearable style="width: 140px">
              <el-option label="A" value="A" />
              <el-option label="B" value="B" />
              <el-option label="C" value="C" />
              <el-option label="AB" value="AB" />
              <el-option label="BC" value="BC" />
              <el-option label="CA" value="CA" />
              <el-option label="ABC" value="ABC" />
            </el-select>
          </el-form-item>

          <el-form-item label="时间范围">
            <el-date-picker
              v-model="dateRange"
              type="daterange"
              range-separator="至"
              start-placeholder="开始日期"
              end-placeholder="结束日期"
              value-format="YYYY-MM-DD"
            />
          </el-form-item>

          <el-form-item>
            <el-button type="primary" :loading="loading" @click="handleSearch">查询</el-button>
            <el-button @click="resetSearch">重置</el-button>
          </el-form-item>
        </el-form>
      </div>
    </div>

    <div class="page-actions">
      <div class="page-actions-left">
        <el-popconfirm title="确定要批量删除选中的事件吗？" @confirm="handleBatchDelete">
          <template #reference>
            <el-button type="danger" :disabled="selectedRows.length === 0">批量删除</el-button>
          </template>
        </el-popconfirm>
      </div>
      <div class="page-actions-right">
        <el-button type="primary" @click="openCreateDialog">
          <el-icon><Plus /></el-icon>
          新增暂降分析
        </el-button>
        <el-button @click="openChannelRuleDialog">电压暂降通道识别词库</el-button>
      </div>
    </div>

    <div class="data-table">
      <el-table
        :data="rows"
        v-loading="loading"
        border
        stripe
        style="width: 100%"
        @selection-change="handleSelectionChange"
      >
        <el-table-column type="selection" width="50" align="center" />
        <el-table-column type="index" label="序号" width="60" align="center" />
        <el-table-column prop="originalName" label="录波文件名" min-width="240" show-overflow-tooltip />

        <el-table-column prop="status" label="状态" width="80">
          <template #default="{ row }">
            <el-tag :type="getAnalyzeStatusType(row.status)">
              {{ getAnalyzeStatusText(row.status) }}
            </el-tag>
          </template>
        </el-table-column>

        <el-table-column prop="hasSag" label="是否暂降" width="100" align="center">
          <template #default="{ row }">
            <el-tag :type="row.hasSag ? 'danger' : 'success'">
              {{ row.hasSag ? '有' : '无' }}
            </el-tag>
          </template>
        </el-table-column>

        <el-table-column prop="eventType" label="事件类型" width="100">
          <template #default="{ row }">{{ formatEventType(row.eventType) }}</template>
        </el-table-column>

        <el-table-column prop="worstPhase" label="最严重相" width="100" />
        <el-table-column prop="occurTimeUtc" label="发生时间" width="200">
          <template #default="{ row }">{{ formatDateTimeMs(row.occurTimeUtc) }}</template>
        </el-table-column>
        <el-table-column prop="durationMs" label="持续时间(ms)" width="100" align="left">
          <template #default="{ row }">{{ formatMs(row.durationMs) }}</template>
        </el-table-column>
        <el-table-column prop="sagPercent" label="暂降幅值(%)" width="100" align="left">
          <template #default="{ row }">{{ formatPercent(row.sagPercent) }}</template>
        </el-table-column>
        <el-table-column prop="residualVoltage" label="残余电压(V)" width="100" align="left">
          <template #default="{ row }">{{ formatNumber(row.residualVoltage) }}</template>
        </el-table-column>

        <el-table-column label="操作" width="240" fixed="right" align="center">
          <template #default="{ row }">
            <el-button link type="primary" @click="openSagWave(row)">暂降波形</el-button>
            <el-button link type="primary" @click="viewOnline(row)">录波浏览</el-button>

            <el-popconfirm title="确定要删除该事件吗？" @confirm="handleDelete(row)">
              <template #reference>
                <el-button link type="danger">删除</el-button>
              </template>
            </el-popconfirm>
          </template>
        </el-table-column>
      </el-table>

      <div class="pagination">
        <el-pagination
          v-model:current-page="page"
          v-model:page-size="pageSize"
          :page-sizes="[10, 20, 50, 100]"
          layout="total, sizes, prev, pager, next, jumper"
          :total="total"
          @size-change="onSizeChange"
          @current-change="onPageChange"
        />
      </div>
    </div>

    <!-- 新增暂降分析 -->
    <el-dialog v-model="createDialogVisible" title="新增暂降分析" width="960px" :close-on-click-modal="false">
      <div class="dialog-body">
        <div class="analysis-params">
          <div class="fixed-algorithm-tip">
            <el-alert
              title="分析算法已固定：1周波 RMS 窗口 + 半周波更新"
              type="info"
              :closable="false"
              show-icon
            />
          </div>

          <el-form :inline="true" size="small">
            <el-form-item label="参考电压(V)">
              <el-input-number v-model="analysisParams.referenceVoltage" :min="0" :step="0.01" :precision="2" />
            </el-form-item>

            <el-form-item label="暂降阈值(%)">
              <el-input-number v-model="analysisParams.sagThresholdPct" :min="0" :max="100" :step="1" />
            </el-form-item>

            <el-form-item label="最小持续(ms)">
              <el-input-number v-model="analysisParams.minDurationMs" :min="0" :step="1" />
            </el-form-item>

            <el-form-item label="中断阈值(%)">
              <el-input-number v-model="analysisParams.interruptThresholdPct" :min="0" :max="100" :step="1" />
            </el-form-item>

            <el-form-item label="迟滞(%)">
              <el-input-number v-model="analysisParams.hysteresisPct" :min="0" :max="20" :step="0.5" />
            </el-form-item>

            <el-form-item>
              <el-checkbox v-model="analysisParams.forceRebuild">强制重新分析</el-checkbox>
            </el-form-item>
          </el-form>
        </div>

        <el-table
          v-loading="analysisLoading"
          :data="analysisRows"
          border
          stripe
          style="width: 100%"
          height="420"
          @selection-change="handleAnalysisSelectionChange"
        >
          <el-table-column type="selection" width="55" align="center" />
          <el-table-column type="index" label="序号" width="60" align="center" />
          <el-table-column prop="originalName" label="录波文件名" min-width="240" show-overflow-tooltip />
          <el-table-column prop="status" label="状态" width="120">
            <template #default="{ row }">
              <el-tag :type="getStatusType(row.status)">{{ getStatusText(row.status) }}</el-tag>
            </template>
          </el-table-column>
          <el-table-column prop="fileSize" label="大小" width="120">
            <template #default="{ row }">{{ formatFileSize(row.fileSize) }}</template>
          </el-table-column>
          <el-table-column prop="crtTime" label="创建时间" width="180">
            <template #default="{ row }">{{ formatDateTime(row.crtTime) }}</template>
          </el-table-column>
        </el-table>

        <div class="dialog-pagination">
          <el-pagination
            v-model:current-page="analysisPagination.page"
            v-model:page-size="analysisPagination.pageSize"
            :page-sizes="[10, 20, 50, 100]"
            layout="total, sizes, prev, pager, next, jumper"
            :total="analysisPagination.total"
            @size-change="handleAnalysisSizeChange"
            @current-change="handleAnalysisPageChange"
          />
        </div>
      </div>

      <template #footer>
        <span class="dialog-footer">
          <el-button @click="createDialogVisible = false">关闭</el-button>
          <el-button
            type="primary"
            :loading="analyzing"
            :disabled="selectedAnalyses.length === 0"
            @click="startSagAnalysis"
          >
            开始分析
          </el-button>
        </span>
      </template>
    </el-dialog>

    <!-- 通道识别词库弹窗 -->
    <el-dialog
      v-model="channelRuleDialogVisible"
      title="电压暂降通道识别词库"
      width="800px"
      :close-on-click-modal="false"
    >
      <div class="dialog-body">
        <div class="search-bar inner-search-bar">
          <el-input
            v-model="channelRuleKeyword"
            placeholder="请输入规则名称"
            style="width: 220px"
            clearable
            @keyup.enter="handleChannelRuleSearch"
          />
          <el-button type="primary" @click="handleChannelRuleSearch">查询</el-button>
          <el-button type="success" @click="openChannelRuleEditDialog()">新增规则</el-button>
        </div>

        <el-table :data="channelRuleRows" v-loading="channelRuleLoading" border stripe style="width: 100%">
          <el-table-column type="index" label="序号" width="60" align="center" />
          <el-table-column prop="ruleName" label="规则名称 (包含文本)" min-width="200" />
          <el-table-column prop="seqNo" label="排序号" width="100" align="center" />
          <el-table-column prop="crtTime" label="创建时间" width="180">
            <template #default="{ row }">{{ formatDateTime(row.crtTime) }}</template>
          </el-table-column>
          <el-table-column label="操作" width="150" align="center">
            <template #default="{ row }">
              <el-button link type="primary" @click="openChannelRuleEditDialog(row)">编辑</el-button>
              <el-popconfirm title="确定要删除该规则吗？" @confirm="handleChannelRuleDelete(row)">
                <template #reference>
                  <el-button link type="danger">删除</el-button>
                </template>
              </el-popconfirm>
            </template>
          </el-table-column>
        </el-table>

        <div class="dialog-pagination">
          <el-pagination
            v-model:current-page="channelRulePagination.page"
            v-model:page-size="channelRulePagination.pageSize"
            :page-sizes="[10, 20, 50]"
            layout="total, sizes, prev, pager, next"
            :total="channelRulePagination.total"
            @size-change="handleChannelRuleSizeChange"
            @current-change="handleChannelRulePageChange"
          />
        </div>
      </div>

      <template #footer>
        <span class="dialog-footer">
          <el-button @click="channelRuleDialogVisible = false">关闭</el-button>
        </span>
      </template>
    </el-dialog>

    <!-- 通道规则编辑弹窗 -->
    <el-dialog
      v-model="channelRuleEditDialogVisible"
      :title="isEditRule ? '编辑规则' : '新增规则'"
      width="500px"
      :close-on-click-modal="false"
    >
      <el-form :model="channelRuleForm" label-width="100px">
        <el-form-item label="规则名称" required>
          <el-input v-model="channelRuleForm.ruleName" placeholder="例如：Ua, Ub, 10kV" />
        </el-form-item>
        <el-form-item label="排序号">
          <el-input-number v-model="channelRuleForm.seqNo" :min="0" :step="1" />
        </el-form-item>
      </el-form>

      <template #footer>
        <span class="dialog-footer">
          <el-button @click="channelRuleEditDialogVisible = false">取消</el-button>
          <el-button type="primary" :loading="channelRuleSaving" @click="saveChannelRule">保存</el-button>
        </span>
      </template>
    </el-dialog>

  </div>
</template>

<script setup lang="ts">
import {
  zwavSagService,
  type ZwavSagChannelRuleDto,
  type ZwavSagListItemDto,
} from '@/services/zwavSagService'
import { zwavService, type ZwavFileAnalysis } from '@/services/zwavService'
import { Plus } from '@element-plus/icons-vue'
import { ElMessage } from 'element-plus'
import { onMounted, reactive, ref } from 'vue'
import { useRouter } from 'vue-router'

const router = useRouter()

const DEFAULT_ANALYSIS_PARAMS = {
  referenceVoltage: 57.74,
  sagThresholdPct: 90,
  interruptThresholdPct: 10,
  hysteresisPct: 2,
  forceRebuild: false,
  minDurationMs: 10
}

const loading = ref(false)
const dateRange = ref<string[]>([])

const searchForm = reactive({
  keyword: '',
  eventType: '',
  phase: ''
})

const rows = ref<ZwavSagListItemDto[]>([])
const selectedRows = ref<ZwavSagListItemDto[]>([])
const total = ref(0)
const page = ref(1)
const pageSize = ref(20)

const formatPercent = (v?: number | null) =>
  v !== undefined && v !== null && Number.isFinite(Number(v)) ? Number(v).toFixed(2) : '-'

const formatMs = (v?: number | null) =>
  v !== undefined && v !== null && Number.isFinite(Number(v)) ? Number(v).toFixed(3) : '-'

const formatNumber = (v?: number | null) =>
  v !== undefined && v !== null && Number.isFinite(Number(v)) ? Number(v).toFixed(2) : '-'

const formatDateTime = (str?: string | null) => {
  if (!str) return '-'
  const d = new Date(str)
  return isNaN(d.getTime()) ? str : d.toLocaleString()
}

const formatDateTimeMs = (str?: string | null) => {
  if (!str) return '-'
  const d = new Date(str)
  if (isNaN(d.getTime())) return str
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

const formatFileSize = (bytes?: number | null) => {
  if (bytes === undefined || bytes === null) return '-'
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(2)} KB`
  if (bytes < 1024 * 1024 * 1024) return `${(bytes / 1024 / 1024).toFixed(2)} MB`
  return `${(bytes / 1024 / 1024 / 1024).toFixed(2)} GB`
}

const formatEventType = (type?: string | null) => {
  if (!type) return '-'
  if (type === 'Normal') return '正常'
  if (type === 'Sag') return '暂降'
  if (type === 'Interruption') return '中断'
  return type
}

const getAnalyzeStatusType = (status?: number) => {
  if (status === 0) return 'warning'
  if (status === 1) return 'primary'
  if (status === 2) return 'success'
  if (status === 3) return 'danger'
  return 'info'
}

const getAnalyzeStatusText = (status?: number) => {
  if (status === 0) return '待处理'
  if (status === 1) return '处理中'
  if (status === 2) return '成功'
  if (status === 3) return '失败'
  return '-'
}

const getStatusType = (status?: string) => {
  const map: Record<string, string> = {
    Queued: 'warning',
    Canceled: 'info',
    Completed: 'success',
    Failed: 'danger',
    ParsingRead: 'primary',
    ParsingExtract: 'primary',
    ParsingCfg: 'primary',
    ParsingHdr: 'primary',
    ParsingChannel: 'primary',
    ParsingDat: 'primary'
  }
  return status ? map[status] || 'info' : 'info'
}

const getStatusText = (status?: string) => {
  const map: Record<string, string> = {
    Queued: '排队中',
    Canceled: '已取消',
    Completed: '已完成',
    Failed: '失败',
    ParsingRead: '读取录波',
    ParsingExtract: '解压中',
    ParsingCfg: '解析CFG',
    ParsingHdr: '解析HDR',
    ParsingChannel: '解析通道',
    ParsingDat: '解析DAT数据'
  }
  return status ? map[status] || status : '-'
}

const reload = async () => {
  loading.value = true
  try {
    const params: Record<string, any> = {
      keyword: searchForm.keyword || undefined,
      eventType: searchForm.eventType || undefined,
      phase: searchForm.phase || undefined,
      page: page.value,
      pageSize: pageSize.value
    }

    if (dateRange.value && dateRange.value.length === 2) {
      params.fromUtc = new Date(dateRange.value[0]).toISOString()

      const endDate = new Date(dateRange.value[1])
      endDate.setDate(endDate.getDate() + 1)
      params.toUtc = endDate.toISOString()
    }

    const res = await zwavSagService.list(params)

    if (!res.success) {
      ElMessage.error(res.message || '查询失败')
      return
    }

    rows.value = res.data?.data || []
    total.value = res.data?.total || 0
  } catch (e: any) {
    ElMessage.error(e?.message ? `查询失败（${e.message}）` : '查询失败')
  } finally {
    loading.value = false
  }
}

const openSagWave = (row: ZwavSagListItemDto) => {
  const href = router.resolve({ name: 'ZwavSagProcess', params: { id: row.id } }).href
  window.open(href, '_blank')
}

const handleSelectionChange = (val: ZwavSagListItemDto[]) => {
  selectedRows.value = val || []
}

const handleSearch = () => {
  page.value = 1
  reload()
}

const resetSearch = () => {
  searchForm.keyword = ''
  searchForm.eventType = ''
  searchForm.phase = ''
  dateRange.value = []
  handleSearch()
}

const onPageChange = (p: number) => {
  page.value = p
  reload()
}

const onSizeChange = (s: number) => {
  pageSize.value = s
  page.value = 1
  reload()
}

const createDialogVisible = ref(false)
const analysisLoading = ref(false)
const analyzing = ref(false)
const analysisRows = ref<ZwavFileAnalysis[]>([])
const selectedAnalyses = ref<ZwavFileAnalysis[]>([])

const analysisPagination = reactive({
  page: 1,
  pageSize: 10,
  total: 0
})

const analysisParams = reactive({
  ...DEFAULT_ANALYSIS_PARAMS
})

const resetAnalysisParams = () => {
  analysisParams.referenceVoltage = DEFAULT_ANALYSIS_PARAMS.referenceVoltage
  analysisParams.sagThresholdPct = DEFAULT_ANALYSIS_PARAMS.sagThresholdPct
  analysisParams.interruptThresholdPct = DEFAULT_ANALYSIS_PARAMS.interruptThresholdPct
  analysisParams.hysteresisPct = DEFAULT_ANALYSIS_PARAMS.hysteresisPct
  analysisParams.forceRebuild = DEFAULT_ANALYSIS_PARAMS.forceRebuild
  analysisParams.minDurationMs = DEFAULT_ANALYSIS_PARAMS.minDurationMs
}

const fetchAnalysisList = async () => {
  analysisLoading.value = true
  try {
    const params: Record<string, any> = {
      page: analysisPagination.page,
      pageSize: analysisPagination.pageSize,
      status: 'Completed'
    }

    const res: any = await zwavService.getList(params)

    if (!res.success) {
      ElMessage.error(res.message || '获取录波解析任务列表失败')
      return
    }

    analysisRows.value = res.data?.items || res.data?.data || []
    analysisPagination.total = res.data?.total || 0
  } catch (e: any) {
    ElMessage.error(e?.message ? `获取录波解析任务列表失败（${e.message}）` : '获取录波解析任务列表失败')
  } finally {
    analysisLoading.value = false
  }
}

const openCreateDialog = async () => {
  createDialogVisible.value = true
  selectedAnalyses.value = []
  analysisPagination.page = 1
  resetAnalysisParams()
  await fetchAnalysisList()
}

const handleAnalysisSelectionChange = (val: ZwavFileAnalysis[]) => {
  selectedAnalyses.value = val
}

const handleAnalysisPageChange = (p: number) => {
  analysisPagination.page = p
  fetchAnalysisList()
}

const handleAnalysisSizeChange = (s: number) => {
  analysisPagination.pageSize = s
  analysisPagination.page = 1
  fetchAnalysisList()
}

const buildAnalyzePayload = (options?: { fileIds?: number[]; analysisGuids?: string[]; forceRebuild?: boolean }) => {
  return {
    fileIds: options?.fileIds || [],
    analysisGuids: options?.analysisGuids || [],
    forceRebuild: options?.forceRebuild ?? analysisParams.forceRebuild,
    referenceVoltage: analysisParams.referenceVoltage,
    sagThresholdPct: analysisParams.sagThresholdPct,
    interruptThresholdPct: analysisParams.interruptThresholdPct,
    hysteresisPct: analysisParams.hysteresisPct,
    minDurationMs: analysisParams.minDurationMs
  }
}

const startSagAnalysis = async () => {
  const analysisGuids = selectedAnalyses.value.map(x => x.analysisGuid).filter(Boolean)

  if (analysisGuids.length === 0) {
    ElMessage.warning('请至少选择一个录波文件')
    return
  }

  analyzing.value = true
  try {
    const res = await zwavSagService.analyze(
      buildAnalyzePayload({
        analysisGuids,
        forceRebuild: analysisParams.forceRebuild
      })
    )

    if (res.success) {
      ElMessage.success(`已生成结果记录：${res.data?.createdEventCount || 0} 条`)
      createDialogVisible.value = false
      page.value = 1
      reload()
    } else {
      ElMessage.error(res.message || '暂降分析失败')
    }
  } catch (e: any) {
    ElMessage.error(e?.message ? `暂降分析失败（${e.message}）` : '暂降分析失败')
  } finally {
    analyzing.value = false
  }
}

const viewOnline = async (row: ZwavSagListItemDto) => {
  try {
    const r = await zwavService.createAnalysis(row.fileId, false)
    const guid = r.data?.analysisGuid
    if (!r.success || !guid) {
      ElMessage.error(r.message || '获取解析任务失败')
      return
    }

    const routeUrl = router.resolve({
      name: 'ZwavOnlineViewer',
      params: { guid }
    })
    window.open(routeUrl.href, '_blank')
  } catch (e: any) {
    ElMessage.error(e?.message ? `打开在线浏览失败（${e.message}）` : '打开在线浏览失败')
  }
}

const handleDelete = async (row: ZwavSagListItemDto) => {
  try {
    const res = await zwavSagService.delete(row.id)
    if (res.success) {
      ElMessage.success('删除成功')
      reload()
    } else {
      ElMessage.error(res.message || '删除失败')
    }
  } catch (e: any) {
    ElMessage.error(e?.message ? `删除失败（${e.message}）` : '删除失败')
  }
}

const handleBatchDelete = async () => {
  const items = selectedRows.value || []
  if (items.length === 0) return

  let ok = 0
  let fail = 0
  for (let i = 0; i < items.length; i++) {
    try {
      const res = await zwavSagService.delete(items[i].id)
      if (res.success) ok++
      else fail++
    } catch {
      fail++
    }
  }

  ElMessage.success(`批量删除完成：成功 ${ok} 条，失败 ${fail} 条`)
  selectedRows.value = []
  reload()
}

onMounted(() => {
  reload()
})

// 通道规则管理
const channelRuleDialogVisible = ref(false)
const channelRuleKeyword = ref('')
const channelRuleLoading = ref(false)
const channelRuleRows = ref<ZwavSagChannelRuleDto[]>([])
const channelRulePagination = reactive({
  page: 1,
  pageSize: 10,
  total: 0
})

const openChannelRuleDialog = () => {
  channelRuleDialogVisible.value = true
  channelRuleKeyword.value = ''
  channelRulePagination.page = 1
  fetchChannelRules()
}

const fetchChannelRules = async () => {
  channelRuleLoading.value = true
  try {
    const res = await zwavSagService.getChannelRuleList({
      keyword: channelRuleKeyword.value || undefined,
      page: channelRulePagination.page,
      pageSize: channelRulePagination.pageSize
    })
    if (res.success) {
      channelRuleRows.value = res.data?.data || []
      channelRulePagination.total = res.data?.total || 0
    } else {
      ElMessage.error(res.message || '获取规则列表失败')
    }
  } catch (e: any) {
    ElMessage.error(e?.message || '获取规则列表失败')
  } finally {
    channelRuleLoading.value = false
  }
}

const handleChannelRuleSearch = () => {
  channelRulePagination.page = 1
  fetchChannelRules()
}

const handleChannelRulePageChange = (p: number) => {
  channelRulePagination.page = p
  fetchChannelRules()
}

const handleChannelRuleSizeChange = (s: number) => {
  channelRulePagination.pageSize = s
  channelRulePagination.page = 1
  fetchChannelRules()
}

const handleChannelRuleDelete = async (row: ZwavSagChannelRuleDto) => {
  try {
    const res = await zwavSagService.deleteChannelRule(row.id)
    if (res.success) {
      ElMessage.success('删除成功')
      fetchChannelRules()
    } else {
      ElMessage.error(res.message || '删除失败')
    }
  } catch (e: any) {
    ElMessage.error(e?.message || '删除失败')
  }
}

// 规则编辑
const channelRuleEditDialogVisible = ref(false)
const isEditRule = ref(false)
const channelRuleSaving = ref(false)
const currentRuleId = ref(0)
const channelRuleForm = reactive({
  ruleName: '',
  seqNo: 0
})

const computeLocalNextChannelRuleSeqNo = () => {
  const items = channelRuleRows.value || []
  let max = 0
  for (let i = 0; i < items.length; i++) {
    const v = Number((items[i] as any).seqNo)
    if (Number.isFinite(v) && v > max) max = v
  }
  return max + 1
}

const getNextChannelRuleSeqNo = async () => {
  const fallback = computeLocalNextChannelRuleSeqNo()
  try {
    const first = await zwavSagService.getChannelRuleList({ page: 1, pageSize: 200 })
    if (!first.success) return fallback

    const totalPages = first.data?.totalPages || 1
    if (totalPages <= 1) {
      const items = first.data?.data || []
      let max = 0
      for (let i = 0; i < items.length; i++) {
        const v = Number((items[i] as any).seqNo)
        if (Number.isFinite(v) && v > max) max = v
      }
      return max + 1
    }

    const last = await zwavSagService.getChannelRuleList({ page: totalPages, pageSize: 200 })
    if (!last.success) return fallback

    const items = last.data?.data || []
    let max = 0
    for (let i = 0; i < items.length; i++) {
      const v = Number((items[i] as any).seqNo)
      if (Number.isFinite(v) && v > max) max = v
    }
    return max + 1
  } catch {
    return fallback
  }
}

const openChannelRuleEditDialog = async (row?: ZwavSagChannelRuleDto) => {
  channelRuleEditDialogVisible.value = true
  if (row) {
    isEditRule.value = true
    currentRuleId.value = row.id
    channelRuleForm.ruleName = row.ruleName
    channelRuleForm.seqNo = row.seqNo
  } else {
    isEditRule.value = false
    currentRuleId.value = 0
    channelRuleForm.ruleName = ''
    const localNext = computeLocalNextChannelRuleSeqNo()
    channelRuleForm.seqNo = localNext
    const serverNext = await getNextChannelRuleSeqNo()
    if (channelRuleEditDialogVisible.value && !isEditRule.value && channelRuleForm.seqNo === localNext) {
      channelRuleForm.seqNo = serverNext
    }
  }
}

const saveChannelRule = async () => {
  if (!channelRuleForm.ruleName?.trim()) {
    ElMessage.warning('规则名称不能为空')
    return
  }

  channelRuleSaving.value = true
  try {
    let res: any
    if (isEditRule.value) {
      res = await zwavSagService.updateChannelRule(currentRuleId.value, {
        ruleName: channelRuleForm.ruleName.trim(),
        seqNo: channelRuleForm.seqNo
      })
    } else {
      res = await zwavSagService.createChannelRule({
        ruleName: channelRuleForm.ruleName.trim(),
        seqNo: channelRuleForm.seqNo
      })
    }

    if (res.success) {
      ElMessage.success(isEditRule.value ? '更新成功' : '新增成功')
      channelRuleEditDialogVisible.value = false
      fetchChannelRules()
    } else {
      ElMessage.error(res.message || '保存失败')
    }
  } catch (e: any) {
    ElMessage.error(e?.message || '保存失败')
  } finally {
    channelRuleSaving.value = false
  }
}
</script>

<style scoped>
.zwav-sag-container {
  padding: 24px;
  background-color: #fff;
  min-height: 100vh;
  max-width: 1600px;
  margin: 0 auto;
  box-sizing: border-box;
}

.page-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 18px;
  border-bottom: 1px solid #eee;
  padding-bottom: 14px;
}

.page-header h2 {
  margin: 0;
  color: #303133;
  font-size: 22px;
  font-weight: 600;
}

.search-bar {
  background-color: #f5f7fa;
  padding: 16px;
  border-radius: 6px;
  margin-bottom: 20px;
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.inner-search-bar {
  margin-bottom: 15px;
  padding: 10px;
}

.search-row {
  width: 100%;
}

.page-actions {
  margin: -10px 0 20px;
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 10px;
}

.page-actions-left {
  display: flex;
  align-items: center;
  gap: 10px;
}

.page-actions-right {
  display: flex;
  justify-content: flex-end;
  align-items: center;
  gap: 10px;
}

.search-form {
  margin-bottom: 0;
}

.search-form .el-form-item {
  margin-bottom: 8px;
}

.data-table {
  background: #fff;
}

.pagination {
  margin-top: 20px;
  display: flex;
  justify-content: flex-end;
}

.analysis-params {
  margin-bottom: 14px;
  padding: 12px 12px 4px;
  background: #f8fafc;
  border: 1px solid #ebeef5;
  border-radius: 6px;
}

.fixed-algorithm-tip {
  margin-bottom: 12px;
}

.dialog-pagination {
  margin-top: 12px;
  display: flex;
  justify-content: flex-end;
}

.phase-section {
  margin-top: 20px;
}

.dialog-footer {
  display: inline-flex;
  gap: 10px;
}
</style>
