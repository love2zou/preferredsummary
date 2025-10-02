<template>
  <div class="file-management-container">
    <el-card>
      <template #header>
        <div class="card-header">
          <h2>文件管理</h2>
          <p>管理标签导入的文件数据</p>
        </div>
      </template>
      
      <!-- 搜索和操作区域 -->
      <div class="search-section">
        <el-row :gutter="20">
          <!-- 应用类型搜索 -->
          <el-col :span="4">
            <el-select
              v-model="searchForm.appType"
              placeholder="选择应用类型"
              clearable
              style="width: 100%"
            >
              <el-option
                v-for="appType in appTypeOptions"
                :key="appType"
                :label="appType"
                :value="appType"
              />
            </el-select>
          </el-col>
          <!-- 文件名搜索 -->
          <el-col :span="5">
            <el-input
              v-model="searchForm.fileName"
              placeholder="搜索文件名"
              prefix-icon="Search"
              clearable
            />
          </el-col>
          <!-- 注释掉文件类型搜索
          <el-col :span="4">
            <el-select
              v-model="searchForm.fileType"
              placeholder="文件类型"
              clearable
              style="width: 100%"
            >
              <el-option label="Excel文件" value=".xlsx,.xls" />
              <el-option label="CSV文件" value=".csv" />
              <el-option label="其他" value="other" />
            </el-select>
          </el-col>
          -->
          <el-col :span="4">
            <el-input
              v-model="searchForm.uploadUser"
              placeholder="上传用户"
              prefix-icon="User"
              clearable
            />
          </el-col>
          <!-- 移除第二个重复的应用类型选择框 -->
          <!-- 删除第58-66行的重复选择框 -->
          <!--
          <el-col :span="4">
            <el-select
              v-model="searchForm.appType"
              placeholder="应用类型"
              clearable
            >
              <el-option label="标签管理" value="tag" />
              <el-option label="其他" value="other" />
            </el-select>
          </el-col>
          -->
          <el-col :span="4">
            <el-button type="primary" @click="handleSearch">
              <el-icon><Search /></el-icon>
              搜索
            </el-button>
            <el-button @click="handleReset">
              <el-icon><Refresh /></el-icon>
              重置
            </el-button>
          </el-col>
          <el-col :span="7" style="text-align: right">
            <el-button type="success" @click="handleUpload">
              <el-icon><Upload /></el-icon>
              上传文件
            </el-button>
            <el-button type="danger" @click="handleBatchDelete" :disabled="!selectedFiles.length">
              <el-icon><Delete /></el-icon>
              批量删除 ({{ selectedFiles.length }})
            </el-button>
            <el-button type="warning" @click="handleCleanExpired">
              <el-icon><Delete /></el-icon>
              清理过期
            </el-button>
          </el-col>
        </el-row>
      </div>
      
      <!-- 表格容器 -->
      <div class="table-container">
        <el-table
          ref="tableRef"
          :data="fileList"
          v-loading="loading"
          :height="tableHeight"
          stripe
          border
          style="width: 100%"
          @selection-change="handleSelectionChange"
        >
          <el-table-column type="selection" width="40" />
          <el-table-column prop="id" label="ID" width="60" />
          <!-- 在表格中添加应用类型列 -->
          <el-table-column prop="fileName" label="文件名" min-width="250" />
          <el-table-column prop="appType" label="应用类型" width="100" />
          <el-table-column label="文件类型" width="120">
            <template #default="scope">
              <el-tag :type="getFileTypeTagType(scope.row.fileType)">
                {{ getFileTypeLabel(scope.row.fileType) }}
              </el-tag>
            </template>
          </el-table-column>
          <el-table-column prop="fileSize" label="文件大小" width="100">
            <template #default="scope">
              {{ scope.row.fileSize || '-' }}
            </template>
          </el-table-column>
          <el-table-column prop="crtTime" label="上传时间" width="160">
            <template #default="scope">
              {{ formatDate(scope.row.crtTime || scope.row.uploadTime) }}
            </template>
          </el-table-column>
          <el-table-column prop="uploadUserName" label="上传用户" width="100">
            <template #default="scope">
               {{ scope.row.uploadUserName || '-' }}
            </template>
          </el-table-column>
          <el-table-column prop="description" label="描述" min-width="150" />
          <!-- 移除第130-137行的重复应用类型列 -->
          <!--
          <el-table-column prop="appType" label="应用类型" width="100">
            <template #default="scope">
              <el-tag :type="scope.row.appType === 'tag' ? 'success' : 'info'">
                {{ scope.row.appType === 'tag' ? '标签管理' : '其他' }}
              </el-tag>
            </template>
          </el-table-column>
          -->
          <el-table-column label="操作" width="150" fixed="right">
            <template #default="scope">
              <el-button type="primary" size="small" @click="handleDownload(scope.row)">
                下载
              </el-button>
              <el-button type="danger" size="small" @click="handleDelete(scope.row)">
                删除
              </el-button>
            </template>
          </el-table-column>
        </el-table>
        
        <!-- 分页 -->
        <el-pagination
          v-model:current-page="pagination.currentPage"
          v-model:page-size="pagination.pageSize"
          :page-sizes="[10, 20, 50, 100]"
          :total="pagination.total"
          layout="total, sizes, prev, pager, next, jumper"
          @size-change="handleSizeChange"
          @current-change="handleCurrentChange"
        />
      </div>
    </el-card>
    
    <!-- 上传文件对话框 -->
    <el-dialog
      v-model="uploadDialogVisible"
      title="上传文件"
      width="600px"
      :before-close="handleUploadDialogClose"
    >
      <el-form
        ref="uploadFormRef"
        :model="uploadForm"
        :rules="uploadRules"
        label-width="80px"
      >
        <el-form-item label="文件选择" prop="fileList">
          <el-upload
            ref="uploadRef"
            :file-list="uploadForm.fileList"
            :auto-upload="false"
            :multiple="true"
            :limit="10"
            :accept="'.xlsx,.xls,.csv,.txt'"
            :on-change="handleFileChange"
            :on-remove="handleFileRemove"
            :on-exceed="handleFileExceed"
            drag
          >
            <el-icon class="el-icon--upload"><upload-filled /></el-icon>
            <div class="el-upload__text">
              将文件拖到此处，或<em>点击上传</em>
            </div>
            <template #tip>
              <div class="el-upload__tip">
                支持 Excel(.xlsx,.xls)、CSV(.csv)、文本(.txt) 格式，最多上传10个文件，单个文件不超过50MB
              </div>
            </template>
          </el-upload>
        </el-form-item>
        
        <el-form-item label="文件描述" prop="description">
          <el-input
            v-model="uploadForm.description"
            type="textarea"
            :rows="3"
            placeholder="请输入文件描述（可选）"
            maxlength="200"
            show-word-limit
          />
        </el-form-item>
        
        <!-- 将硬编码的应用类型选项替换为动态选项 -->
        <el-form-item label="应用类型" prop="appType">
          <el-select
            v-model="uploadForm.appType"
            placeholder="请选择应用类型"
            style="width: 100%"
          >
            <el-option
              v-for="appType in appTypeOptions"
              :key="appType"
              :label="appType"
              :value="appType"
            />
          </el-select>
        </el-form-item>
      </el-form>
      
      <!-- 上传进度 -->
      <div v-if="uploadProgress.show" class="upload-progress">
        <el-progress
          :percentage="uploadProgress.percentage"
          :status="uploadProgress.status"
        >
          <template #default="{ percentage }">
            <span class="percentage-value">{{ percentage }}%</span>
            <span class="percentage-label">{{ uploadProgress.text }}</span>
          </template>
        </el-progress>
      </div>
      
      <template #footer>
        <span class="dialog-footer">
          <el-button @click="handleUploadDialogClose" :disabled="uploading">取消</el-button>
          <el-button 
            type="primary" 
            @click="handleUploadSubmit" 
            :loading="uploading"
            :disabled="uploadForm.fileList.length === 0"
          >
            {{ uploading ? '上传中...' : '开始上传' }}
          </el-button>
        </span>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted, onUnmounted, nextTick } from 'vue'
import { ElMessage, ElMessageBox, ElTable, ElUpload, type FormInstance, type UploadFile, type UploadUserFile } from 'element-plus'
import { Search, Refresh, Delete, Download, User, Upload, UploadFilled } from '@element-plus/icons-vue'
import { fileApi, type FileRecord, type FileSearchParams, type FileUploadParams } from '@/api/file'
import { tagApi } from '@/api/tag' // 添加这一行来导入tagApi

// 响应式数据
const loading = ref(false)
const tableRef = ref<InstanceType<typeof ElTable>>()
const searchFormRef = ref<FormInstance>()
const tableHeight = ref(400)
const selectedFiles = ref<FileRecord[]>([])

// 添加应用类型选项
const appTypeOptions = ref<string[]>(['访问地址'])

// 搜索表单
const searchForm = reactive({
  fileName: '',
  fileType: '',
  uploadUser: '',
  appType: ''  // 添加应用类型搜索
})

// 文件列表
const fileList = ref<FileRecord[]>([])

// 分页
const pagination = reactive({
  currentPage: 1,
  pageSize: 10,
  total: 0
})

// 计算表格高度
const calculateTableHeight = () => {
  nextTick(() => {
    const windowHeight = window.innerHeight
    const headerHeight = 200
    const paginationHeight = 60
    const padding = 40
    tableHeight.value = windowHeight - headerHeight - paginationHeight - padding
  })
}

// 格式化日期
const formatDate = (dateString: string) => {
  if (!dateString) return '-'
  const date = new Date(dateString)
  return date.toLocaleString('zh-CN', {
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit'
  })
}

// 格式化文件大小
const formatFileSize = (bytes: number) => {
  if (bytes === 0) return '0 B'
  const k = 1024
  const sizes = ['B', 'KB', 'MB', 'GB']
  const i = Math.floor(Math.log(bytes) / Math.log(k))
  return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i]
}

// 获取文件类型标签类型
const getFileTypeTagType = (fileType: string) => {
  // 移除点号并转换为小写
  const type = fileType.replace('.', '').toLowerCase()
  switch (type) {
    case 'xlsx':
    case 'xls':
      return 'success'
    case 'csv':
      return 'warning'
    case 'txt':
      return 'info'
    case 'pdf':
      return 'danger'
    case 'jpg':
    case 'jpeg':
    case 'png':
    case 'gif':
      return 'primary'
    default:
      return 'info'
  }
}

// 获取文件类型标签
const getFileTypeLabel = (fileType: string) => {
  // 移除点号并转换为小写
  const type = fileType.replace('.', '').toLowerCase()
  switch (type) {
    case 'xlsx':
      return 'Excel(xlsx)'
    case 'xls':
      return 'Excel(xls)'
    case 'csv':
      return 'CSV'
    case 'txt':
      return '文本'
    case 'pdf':
      return 'PDF'
    case 'jpg':
    case 'jpeg':
      return 'JPEG图片'
    case 'png':
      return 'PNG图片'
    case 'gif':
      return 'GIF图片'
    case 'doc':
      return 'Word文档'
    case 'docx':
      return 'Word文档'
    default:
      return type.toUpperCase()
  }
}

// 加载文件列表
const loadFileList = async () => {
  loading.value = true
  try {
    const response = await fileApi.getFileList({
      page: pagination.currentPage,
      size: pagination.pageSize,
      ...searchForm
    })
    
    // 添加安全检查
    if (response && response.data) {
      fileList.value = response.data.items || []
      pagination.total = response.data.total || 0
    } else {
      fileList.value = []
      pagination.total = 0
    }
  } catch (error) {
    console.error('获取文件列表失败:', error)
    ElMessage.error('获取文件列表失败')
    // 确保在错误时也有默认值
    fileList.value = []
    pagination.total = 0
  } finally {
    loading.value = false
  }
}

// 处理每页显示数量变化
const handleSizeChange = (size: number) => {
  pagination.pageSize = size
  pagination.currentPage = 1
  loadFileList()
}

// 处理当前页变化
const handleCurrentChange = (page: number) => {
  pagination.currentPage = page
  loadFileList()
}

// 搜索
const handleSearch = () => {
  pagination.currentPage = 1
  loadFileList()
}

// 添加加载应用类型选项的方法
const loadAppTypeOptions = async () => {
  try {
    const modules = await tagApi.getParNameList()
    appTypeOptions.value = modules.length > 0 ? modules : ['访问地址']
  } catch (error) {
    console.error('Load app type options error:', error)
    appTypeOptions.value = ['访问地址']
  }
}

// 更新重置搜索方法
const handleReset = () => {
  searchFormRef.value?.resetFields()
  Object.assign(searchForm, {
    fileName: '',
    fileType: '',
    uploadUser: '',
    appType: ''  // 添加应用类型重置
  })
  pagination.currentPage = 1
  loadFileList()
}

// 在组件挂载时加载应用类型选项
onMounted(() => {
  calculateTableHeight()
  loadAppTypeOptions() // 加载应用类型选项
  loadFileList()
  window.addEventListener('resize', calculateTableHeight)
})

// 处理表格选择变化
const handleSelectionChange = (selection: FileRecord[]) => {
  selectedFiles.value = selection
}

// 下载文件
const handleDownload = async (row: FileRecord) => {
  try {
    const response = await fileApi.downloadFile(row.id)
    // 如果后端返回的是ApiResponse包装的Blob
    const blob = response.data || response  // 兼容处理
    const url = window.URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = url
    link.download = row.fileName
    document.body.appendChild(link)
    link.click()
    document.body.removeChild(link)
    window.URL.revokeObjectURL(url)
    ElMessage.success('文件下载成功')
  } catch (error) {
    console.error('文件下载失败:', error)
    ElMessage.error('文件下载失败')
  }
}

// 删除文件
const handleDelete = async (row: FileRecord) => {
  try {
    await ElMessageBox.confirm(
      `确定要删除文件 "${row.fileName}" 吗？`,
      '删除确认',
      {
        confirmButtonText: '确定',
        cancelButtonText: '取消',
        type: 'warning'
      }
    )
    
    await fileApi.deleteFile(row.id)
    ElMessage.success('删除成功')
    loadFileList()
  } catch (error) {
    if (error !== 'cancel') {
      console.error('删除文件失败:', error)
      ElMessage.error('删除文件失败')
    }
  }
}

// 批量删除
const handleBatchDelete = async () => {
  if (selectedFiles.value.length === 0) {
    ElMessage.warning('请选择要删除的文件')
    return
  }

  try {
    await ElMessageBox.confirm(
      `确定要删除选中的 ${selectedFiles.value.length} 个文件吗？`,
      '批量删除确认',
      {
        confirmButtonText: '确定',
        cancelButtonText: '取消',
        type: 'warning'
      }
    )

    const ids = selectedFiles.value.map(file => file.id)
    await fileApi.batchDeleteFiles(ids)
    
    ElMessage.success('批量删除成功')
    
    // 清空选择
    tableRef.value?.clearSelection()
    selectedFiles.value = []
    
    // 重新加载列表
    loadFileList()
  } catch (error) {
    if (error !== 'cancel') {
      console.error('批量删除失败:', error)
      ElMessage.error('批量删除失败')
    }
  }
}

// 清理过期文件
const handleCleanExpired = async () => {
  try {
    await ElMessageBox.confirm(
      '确定要清理过期文件吗？此操作将删除超过30天的文件。',
      '清理确认',
      {
        confirmButtonText: '确定',
        cancelButtonText: '取消',
        type: 'warning'
      }
    )
    
    const response = await fileApi.cleanExpiredFiles()
    // 修改为访问response.data中的数据
    ElMessage.success(`清理完成，共删除 ${response.data.deletedCount} 个过期文件`)
    loadFileList()
  } catch (error) {
    if (error !== 'cancel') {
      console.error('清理过期文件失败:', error)
      ElMessage.error('清理过期文件失败')
    }
  }
}

// 组件挂载时
onMounted(() => {
  loadFileList()
  calculateTableHeight()
  
  // 监听窗口大小变化
  window.addEventListener('resize', calculateTableHeight)
})

// 组件卸载时移除事件监听
onUnmounted(() => {
  window.removeEventListener('resize', calculateTableHeight)
})

// 上传相关数据
const uploadDialogVisible = ref(false)
const uploading = ref(false)
const uploadRef = ref<InstanceType<typeof ElUpload>>()
const uploadFormRef = ref<FormInstance>()

// 上传表单
// 修改上传表单的默认值
const uploadForm = reactive({
  fileList: [] as UploadUserFile[],
  description: '',
  appType: '访问地址'  // 改为默认值'访问地址'
})

// 上传进度
const uploadProgress = reactive({
  show: false,
  percentage: 0,
  status: 'success' as 'success' | 'exception' | 'warning',
  text: ''
})

// 上传表单验证规则
const uploadRules = {
  fileList: [
    { 
      required: true, 
      validator: (rule: any, value: any, callback: any) => {
        if (!value || value.length === 0) {
          callback(new Error('请选择要上传的文件'))
        } else {
          callback()
        }
      }, 
      trigger: 'change' 
    }
  ],
  appType: [
    { required: true, message: '请选择应用类型', trigger: 'change' }
  ]
}

// 处理上传按钮点击
const handleUpload = () => {
  uploadDialogVisible.value = true
}

// 处理文件选择变化
const handleFileChange = (file: UploadFile, fileList: UploadUserFile[]) => {
  // 验证文件大小
  if (file.raw && file.raw.size > 50 * 1024 * 1024) {
    ElMessage.error('文件大小不能超过50MB')
    return false
  }
  
  // 验证文件类型
  const allowedTypes = ['.xlsx', '.xls', '.csv', '.txt']
  const fileName = file.name.toLowerCase()
  const isValidType = allowedTypes.some(type => fileName.endsWith(type))
  
  if (!isValidType) {
    ElMessage.error('只支持 Excel、CSV、文本格式的文件')
    return false
  }
  
  uploadForm.fileList = fileList
  // 手动触发表单验证
  uploadFormRef.value?.validateField('fileList')
}

// 处理文件移除
const handleFileRemove = (file: UploadFile, fileList: UploadUserFile[]) => {
  uploadForm.fileList = fileList
  // 手动触发表单验证
  uploadFormRef.value?.validateField('fileList')
}

// 处理文件数量超限
const handleFileExceed = (files: File[], fileList: UploadUserFile[]) => {
  ElMessage.warning('最多只能上传10个文件')
}

// 重置上传表单
const resetUploadForm = () => {
  uploadForm.fileList = []
  uploadForm.description = ''
  uploadForm.appType = '访问地址'  // 改为默认值'访问地址'
  uploadProgress.show = false
  uploadProgress.percentage = 0
  uploadProgress.status = 'success'
  uploadProgress.text = ''
  uploadFormRef.value?.clearValidate()
}

// 关闭上传对话框
const handleUploadDialogClose = () => {
  if (uploading.value) {
    ElMessage.warning('文件正在上传中，请稍候...')
    return
  }
  uploadDialogVisible.value = false
  resetUploadForm()
}

// 提交上传
const handleUploadSubmit = async () => {
  if (!uploadFormRef.value) return
  
  // 先检查文件列表
  if (uploadForm.fileList.length === 0) {
    ElMessage.warning('请选择要上传的文件')
    return
  }
  
  try {
    const valid = await uploadFormRef.value.validate()
    if (valid) {
      uploading.value = true
      uploadProgress.show = true
      uploadProgress.percentage = 0
      uploadProgress.status = 'success'
      uploadProgress.text = '准备上传...'
      
      try {
        const files = uploadForm.fileList
          .map(file => file.raw!)
          .filter(Boolean)
        
        if (files.length === 0) {
          throw new Error('没有有效的文件可以上传')
        }
        
        if (files.length === 1) {
          // 单文件上传
          uploadProgress.text = '正在上传文件...'
          const uploadParams: FileUploadParams = {
            file: files[0],
            description: uploadForm.description,
            appType: uploadForm.appType
          }
          
          const response = await fileApi.uploadFile(uploadParams)
          uploadProgress.percentage = 100
          uploadProgress.text = '上传完成'
          ElMessage.success('文件上传成功')
        } else {
          // 批量上传
          uploadProgress.text = `正在上传 ${files.length} 个文件...`
          const response = await fileApi.batchUploadFiles(files, uploadForm.description, uploadForm.appType)
          uploadProgress.percentage = 100
          uploadProgress.text = '批量上传完成'
          ElMessage.success(`成功上传 ${files.length} 个文件`)
        }
        
        // 关闭对话框并刷新列表
        setTimeout(() => {
          uploadDialogVisible.value = false
          resetUploadForm()
          loadFileList()
        }, 1500)
        
      } catch (error: any) {
        console.error('文件上传失败:', error)
        uploadProgress.status = 'exception'
        uploadProgress.text = '上传失败'
        
        // 根据错误类型显示不同的错误信息
        if (error.response?.status === 405) {
          ElMessage.error('服务器不支持文件上传功能，请联系管理员')
        } else if (error.response?.status === 413) {
          ElMessage.error('文件太大，请选择较小的文件')
        } else {
          ElMessage.error(error.response?.data?.message || '文件上传失败')
        }
      } finally {
        uploading.value = false
      }
    }
  } catch (error) {
    console.error('表单验证失败:', error)
    ElMessage.warning('请检查表单填写是否完整')
  }
}
</script>

<style scoped>
.file-management-container {
  padding: 20px;
  height: calc(100vh - 60px);
  overflow-y: auto;
  box-sizing: border-box;
  display: flex;
  flex-direction: column;
}

.card-header {
  text-align: center;
}

.card-header h2 {
  margin: 0 0 8px 0;
  color: #303133;
}

.card-header p {
  margin: 0;
  color: #909399;
  font-size: 14px;
}

.search-section {
  margin-bottom: 20px;
  padding: 20px;
  background-color: #f8f9fa;
  border-radius: 4px;
}

.el-card {
  flex: 1;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.el-card :deep(.el-card__body) {
  flex: 1;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.table-container {
  flex: 1;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.el-table {
  border-radius: 4px;
  overflow: hidden;
  flex: 1;
  width: 100%;
}

.el-pagination {
  justify-content: flex-end;
  margin-top: 20px;
  flex-shrink: 0;
}

.upload-progress {
  margin: 20px 0;
}

.percentage-value {
  margin-right: 8px;
  font-weight: bold;
}

.percentage-label {
  color: #909399;
  font-size: 12px;
}

.dialog-footer {
  text-align: right;
}

.el-upload {
  width: 100%;
}

.el-upload__tip {
  color: #909399;
  font-size: 12px;
  line-height: 1.4;
}
</style>