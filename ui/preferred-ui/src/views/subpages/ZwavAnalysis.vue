<template>
  <div class="zwav-analysis-container">
    <div class="page-header">
      <h2>录波解析管理</h2>
    </div>

    <!-- 搜索栏 -->
    <div class="search-bar">
      <div class="search-left">
        <el-form :inline="true" :model="searchForm" class="search-form">
          <el-form-item label="文件名">
            <el-input v-model="searchForm.keyword" placeholder="" clearable @keyup.enter="handleSearch" />
          </el-form-item>
          <el-form-item label="状态">
            <el-select v-model="searchForm.status" placeholder="全部状态" clearable style="width: 120px">
              <el-option label="上传中" value="Uploading" />
              <el-option label="排队中" value="Queued" />
              <el-option label="解析中" value="Processing" />
              <el-option label="已完成" value="Ready" />
              <el-option label="失败" value="Failed" />
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
            <el-button type="primary" @click="handleSearch">查询</el-button>
            <el-button @click="resetSearch">重置</el-button>
          </el-form-item>
        </el-form>
      </div>
      <div class="search-right">
        <el-button type="primary" @click="openUploadDialog">
          <el-icon><Upload /></el-icon> 批量上传
        </el-button>
        <el-popconfirm title="确定要删除选中的任务和文件吗？" @confirm="handleBatchDelete" :disabled="selectedRows.length === 0">
          <template #reference>
            <el-button type="danger" :disabled="selectedRows.length === 0">
              <el-icon><Delete /></el-icon> 批量删除
            </el-button>
          </template>
        </el-popconfirm>
      </div>
    </div>

    <!-- 数据表格 -->
    <div class="data-table">
      <el-table 
        v-loading="loading" 
        :data="tableData" 
        border 
        stripe 
        style="width: 100%"
        @selection-change="handleSelectionChange"
      >
        <el-table-column type="selection" width="55" align="center" />
        <el-table-column type="index" label="序号" width="60" align="center" />
        <el-table-column prop="originalName" label="文件名" min-width="180" show-overflow-tooltip />
        <el-table-column prop="fileSize" label="大小" width="100">
          <template #default="{ row }">
            {{ formatFileSize(row.fileSize) }}
          </template>
        </el-table-column>
        <el-table-column prop="status" label="状态" width="100">
          <template #default="{ row }">
            <el-tag :type="getStatusType(row.status)">{{ getStatusText(row.status) }}</el-tag>
          </template>
        </el-table-column>
        <el-table-column prop="progress" label="进度" width="120">
          <template #default="{ row }">
            <el-progress 
              :percentage="row.progress" 
              :status="row.status === 'Failed' ? 'exception' : (row.status === 'Ready' ? 'success' : '')"
            />
          </template>
        </el-table-column>
        <el-table-column prop="crtTime" label="创建时间" width="140">
          <template #default="{ row }">
            {{ formatDateTime(row.crtTime) }}
          </template>
        </el-table-column>
        <el-table-column prop="errorMessage" label="错误信息" min-width="150" show-overflow-tooltip>
          <template #default="{ row }">
            <span class="error-text" v-if="row.errorMessage">{{ row.errorMessage }}</span>
            <span v-else>-</span>
          </template>
        </el-table-column>
        <el-table-column label="操作" width="180" fixed="right">
          <template #default="{ row }">
            <el-button 
              v-if="row.status === 'Ready'" 
              link 
              type="primary" 
              size="small"
              @click="viewOnline(row)"
            >
              在线浏览
            </el-button>
            <el-button 
              link 
              type="primary" 
              size="small"
              @click="handleDownload(row)"
            >
              下载
            </el-button>
            <el-popconfirm title="确定删除该任务和文件吗？" @confirm="handleDelete(row)">
              <template #reference>
                <el-button link type="danger" size="small">删除</el-button>
              </template>
            </el-popconfirm>
          </template>
        </el-table-column>
      </el-table>

      <div class="pagination">
        <el-pagination
          v-model:current-page="pagination.page"
          v-model:page-size="pagination.pageSize"
          :page-sizes="[10, 20, 50, 100]"
          layout="total, sizes, prev, pager, next, jumper"
          :total="pagination.total"
          @size-change="handleSizeChange"
          @current-change="handlePageChange"
        />
      </div>
    </div>

    <!-- 上传对话框 -->
    <el-dialog
      v-model="uploadDialogVisible"
      title="批量上传录波文件"
      width="600px"
      :close-on-click-modal="false"
    >
      <el-upload
        class="upload-demo"
        drag
        multiple
        action="#"
        :auto-upload="false"
        :on-change="handleFileChange"
        :on-remove="handleFileRemove"
        v-model:file-list="fileList"
        ref="uploadRef"
        accept=".zwav,.zip"
      >
        <el-icon class="el-icon--upload"><upload-filled /></el-icon>
        <div class="el-upload__text">
          拖拽文件到此处或 <em>点击上传</em>
        </div>
        <template #tip>
          <div class="el-upload__tip">
            支持 .zwav, .zip 格式，单个文件不超过 1GB
          </div>
        </template>
      </el-upload>

      <!-- 上传进度列表 -->
      <div v-if="uploadingFiles.length > 0" class="upload-progress-list">
        <h4>上传进度</h4>
        <div v-for="(item, index) in uploadingFiles" :key="index" class="upload-item">
          <span>{{ item.name }}</span>
          <div class="status">
             <el-tag size="small" :type="item.status === 'success' ? 'success' : (item.status === 'error' ? 'danger' : 'info')">
               {{ item.statusText }}
             </el-tag>
          </div>
        </div>
      </div>

      <template #footer>
        <span class="dialog-footer">
          <el-button @click="uploadDialogVisible = false">关闭</el-button>
          <el-button type="primary" @click="startBatchUpload" :loading="isUploading">
            开始上传
          </el-button>
        </span>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { zwavService, type ZwavFileAnalysis } from '@/services/zwavService'
import { Delete, Upload, UploadFilled } from '@element-plus/icons-vue'
import { ElMessage, type UploadFile, type UploadInstance, type UploadUserFile } from 'element-plus'
import { onMounted, onUnmounted, reactive, ref } from 'vue'
import { useRouter } from 'vue-router'

const router = useRouter()

// 状态
const loading = ref(false)
const tableData = ref<ZwavFileAnalysis[]>([])
const selectedRows = ref<ZwavFileAnalysis[]>([])
const dateRange = ref<string[]>([])
const uploadDialogVisible = ref(false)
const uploadRef = ref<UploadInstance>()
const fileList = ref<UploadUserFile[]>([])
const isUploading = ref(false)

// 上传状态跟踪
interface UploadingFile {
  uid: number
  name: string
  status: 'pending' | 'uploading' | 'success' | 'error'
  statusText: string
  raw: File
}
const uploadingFiles = ref<UploadingFile[]>([])

const searchForm = reactive({
  keyword: '',
  status: ''
})

const pagination = reactive({
  page: 1,
  pageSize: 20,
  total: 0
})

// 自动刷新定时器
let refreshTimer: number | null = null

// 初始化
onMounted(() => {
  fetchData()
  // 每 10 秒自动刷新一次列表状态
  refreshTimer = window.setInterval(() => {
    // 只有在没有打开上传弹窗且不在加载中时才静默刷新
    if (!uploadDialogVisible.value && !loading.value) {
      fetchData(true)
    }
  }, 10000)
})

onUnmounted(() => {
  if (refreshTimer) clearInterval(refreshTimer)
})

// 获取数据
const fetchData = async (silent = false) => {
  if (!silent) loading.value = true
  try {
    const params: any = {
      page: pagination.page,
      pageSize: pagination.pageSize,
      keyword: searchForm.keyword,
      status: searchForm.status
    }

    if (dateRange.value && dateRange.value.length === 2) {
      params.fromUtc = new Date(dateRange.value[0]).toISOString()
      // 结束日期加一天，覆盖全天
      const endDate = new Date(dateRange.value[1])
      endDate.setDate(endDate.getDate() + 1)
      params.toUtc = endDate.toISOString()
    }

    const res: any = await zwavService.getList(params)
    if (res.success) {
      tableData.value = res.data.data
      pagination.total = res.data.total
    } else {
      if (!silent) ElMessage.error(res.message || '获取列表失败')
    }
  } catch (error) {
    console.error(error)
    if (!silent) ElMessage.error('获取列表失败')
  } finally {
    if (!silent) loading.value = false
  }
}

// 搜索操作
const handleSearch = () => {
  pagination.page = 1
  fetchData()
}

const resetSearch = () => {
  searchForm.keyword = ''
  searchForm.status = ''
  dateRange.value = []
  handleSearch()
}

const refreshList = () => {
  fetchData()
}

const handlePageChange = (val: number) => {
  pagination.page = val
  fetchData()
}

const handleSizeChange = (val: number) => {
  pagination.pageSize = val
  pagination.page = 1
  fetchData()
}

// 批量选择
const handleSelectionChange = (val: ZwavFileAnalysis[]) => {
  selectedRows.value = val
}

// 批量删除
const handleBatchDelete = async () => {
  if (selectedRows.value.length === 0) return
  
  loading.value = true
  let successCount = 0
  let failCount = 0

  try {
    // 串行删除，避免并发问题（或者改为 Promise.all 并发删除）
    for (const row of selectedRows.value) {
      try {
        const res: any = await zwavService.deleteAnalysis(row.analysisGuid, true)
        if (res.success) {
          successCount++
        } else {
          failCount++
        }
      } catch (err) {
        failCount++
      }
    }

    if (failCount === 0) {
      ElMessage.success(`成功删除 ${successCount} 条记录`)
    } else {
      ElMessage.warning(`删除完成：成功 ${successCount} 条，失败 ${failCount} 条`)
    }
    
    // 清空选择并刷新
    selectedRows.value = []
    fetchData()
  } catch (error) {
    ElMessage.error('批量删除操作异常')
  } finally {
    loading.value = false
  }
}

// 删除
const handleDelete = async (row: ZwavFileAnalysis) => {
  try {
    const res: any = await zwavService.deleteAnalysis(row.analysisGuid, true)
    if (res.success) {
      ElMessage.success('删除成功')
      fetchData()
    } else {
      ElMessage.error(res.message || '删除失败')
    }
  } catch (error) {
    ElMessage.error('删除失败')
  }
}

// 下载
const handleDownload = async (row: ZwavFileAnalysis) => {
  try {
    const res: any = await zwavService.downloadFile(row.analysisGuid)
    // 创建 blob 链接并下载
    // 注意：api 拦截器返回的是 response.data，这里 res 应该是 Blob 对象
    const blob = new Blob([res], { type: 'application/octet-stream' })
    const url = window.URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = url
    // 使用原始文件名，如果没有则使用默认名
    link.setAttribute('download', row.originalName || 'download.zwav')
    document.body.appendChild(link)
    link.click()
    document.body.removeChild(link)
    window.URL.revokeObjectURL(url)
  } catch (error) {
    console.error(error)
    ElMessage.error('下载失败')
  }
}

// 在线浏览
const viewOnline = (row: ZwavFileAnalysis) => {
  const routeUrl = router.resolve({
    name: 'ZwavOnlineViewer',
    params: { guid: row.analysisGuid }
  })
  window.open(routeUrl.href, '_blank')
}

// 上传相关
const openUploadDialog = () => {
  uploadDialogVisible.value = true
  fileList.value = []
  uploadingFiles.value = []
}

const handleFileChange = (file: UploadFile, files: UploadFile[]) => {
  // 手动更新 fileList，确保文件列表同步
  fileList.value = files
}

const handleFileRemove = (file: UploadFile, files: UploadFile[]) => {
  fileList.value = files
}

const startBatchUpload = async () => {
  // 优先使用 uploadRef.value.uploadFiles，这是组件内部维护的文件列表
  // 如果拿不到，则回退到 fileList.value
  // 注意：uploadRef.value.uploadFiles 可能包含已上传的文件，需要过滤出 raw 存在且 status 为 ready 的文件（或者全部重新上传，视需求而定）
  // 这里简化为取 raw
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const uploadFiles = (uploadRef.value ? (uploadRef.value as any).uploadFiles : []) as UploadUserFile[]
  const filesToUpload = uploadFiles.length > 0 ? uploadFiles : fileList.value || []
  
  if (filesToUpload.length === 0) {
    ElMessage.warning('请先选择文件')
    return
  }

  isUploading.value = true
  
  // 初始化上传列表状态
  uploadingFiles.value = filesToUpload.map((f: any) => ({
    uid: f.uid,
    name: f.name,
    status: 'pending',
    statusText: '等待上传',
    raw: f.raw as unknown as File
  }))

  // 串行或并行上传，这里采用串行以避免并发问题
  for (const item of uploadingFiles.value) {
    item.status = 'uploading'
    item.statusText = '上传中...'
    
    try {
      // 1. 上传文件
      const uploadRes: any = await zwavService.uploadFile(item.raw)
      if (uploadRes.success) {
        item.statusText = '创建任务中...'
        const fileId = uploadRes.data.fileId
        
        // 2. 创建解析任务
        const createRes: any = await zwavService.createAnalysis(fileId)
        if (createRes.success) {
          item.status = 'success'
          item.statusText = '已完成'
        } else {
          item.status = 'error'
          item.statusText = createRes.message || '创建任务失败'
        }
      } else {
        item.status = 'error'
        item.statusText = uploadRes.message || '上传失败'
      }
    } catch (err) {
      item.status = 'error'
      item.statusText = '网络错误'
    }
  }

  isUploading.value = false
  ElMessage.success('批量处理完成')
  
  // 刷新列表
  fetchData()
  
  // 清空选择的文件（可选）
  // uploadRef.value?.clearFiles()
}

// 辅助函数
const formatFileSize = (mb: number) => {
  if (mb === undefined || mb === null) return '-'
  mb = mb / 1024;
  return mb.toFixed(2) + ' kb'
}

const formatDateTime = (str: string) => {
  if (!str) return '-'
  return new Date(str).toLocaleString()
}

const getStatusType = (status: string) => {
  const map: Record<string, string> = {
    'Uploading': 'info',
    'Queued': 'warning',
    'Processing': 'primary',
    'Ready': 'success',
    'Failed': 'danger',
    'Canceled': 'info'
  }
  return map[status] || ''
}

const getStatusText = (status: string) => {
  const map: Record<string, string> = {
    'Uploading': '上传中',
    'Queued': '排队中',
    'Processing': '解析中',
    'Ready': '已完成',
    'Failed': '失败',
    'Canceled': '已取消'
  }
  return map[status] || status
}
</script>

<style scoped>
.zwav-analysis-container {
  padding: 40px; /* 增加内边距 */
  background-color: #fff;
  min-height: 100vh;
  max-width: 1600px; /* 限制最大宽度 */
  margin: 0 auto; /* 居中 */
}

.page-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 20px;
  border-bottom: 1px solid #eee;
  padding-bottom: 15px;
}

.page-header h2 {
  margin: 0;
  color: #303133;
}

.search-bar {
  background-color: #f5f7fa;
  padding: 15px;
  border-radius: 4px;
  margin-bottom: 20px;
  display: flex;
  justify-content: space-between;
  align-items: center; /* 改为垂直居中 */
}

.search-left {
  flex: 1;
}

.search-right {
  margin-left: 20px;
}

.search-form {
  margin-bottom: 0;
}

.search-form .el-form-item {
  margin-bottom: 0;
}

.pagination {
  margin-top: 20px;
  display: flex;
  justify-content: flex-end;
}

.error-text {
  color: #f56c6c;
  font-size: 12px;
}

.upload-progress-list {
  margin-top: 20px;
  border-top: 1px solid #eee;
  padding-top: 10px;
  max-height: 200px;
  overflow-y: auto;
}

.upload-item {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 5px 0;
  border-bottom: 1px dashed #eee;
}

/* 优化进度条样式，去除默认的最小宽度 */
.data-table :deep(.el-progress__text) {
  min-width: auto;
}
</style>
