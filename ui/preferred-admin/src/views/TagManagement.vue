<template>
  <div class="tag-management-container">
    <el-card>
      <template #header>
        <div class="card-header">
          <h2>标签管理</h2>
          <p>管理系统中的所有标签</p>
        </div>
      </template>
      
        <!-- 搜索和操作区域 -->
        <div class="search-section">
          <el-row :gutter="20">
            <el-col :span="5">
              <el-select
                v-model="searchForm.parName"
                placeholder="选择应用模块"
                clearable
                filterable
                style="width: 100%"
              >
                <el-option
                  v-for="module in moduleOptions"
                  :key="module"
                  :label="module"
                  :value="module"
                />
              </el-select>
            </el-col>
            <el-col :span="5">
              <el-input
                v-model="searchForm.tagName"
                placeholder="搜索标签名称"
                prefix-icon="Search"
                clearable
              />
            </el-col>
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
            <el-col :span="10" style="text-align: right">
              <el-button type="success" @click="handleAdd">
                <el-icon><Plus /></el-icon>
                新增
              </el-button>
              <el-button 
                type="danger" 
                @click="handleBatchDelete"
                :disabled="!selectedTags.length"
              >
                <el-icon><Delete /></el-icon>
                批量删除 ({{ selectedRows.length }})
              </el-button>
              <el-button type="info" @click="handleDownloadTemplate">
                <el-icon><Download /></el-icon>
                下载模板
              </el-button>
              <el-button type="warning" @click="handleImport">
                <el-icon><Upload /></el-icon>
                导入
              </el-button>
            </el-col>
          </el-row>
        </div>
      
      <!-- 表格容器 -->
      <div class="table-container">
        <!-- 标签列表表格 -->
        <el-table
          ref="tableRef"
          :data="tagList"
          v-loading="loading"
          :height="tableHeight"
          stripe
          border
          style="width: 100%"
          @selection-change="handleSelectionChange"
        >
          <el-table-column type="selection" width="40" />
          <el-table-column prop="id" label="ID" width="50" />
          <el-table-column prop="parName" label="应用模块" min-width="80" />
          <el-table-column prop="tagCode" label="标签代码" min-width="120" />
          <el-table-column prop="tagName" label="标签名称" min-width="120" />
          <el-table-column label="标签颜色" width="120">
            <template #default="scope">
              <el-tag
                :style="{ 
                  backgroundColor: scope.row.rgbColor, 
                  color: scope.row.hexColor,
                  border: `1px solid ${scope.row.hexColor}`
                }"
                effect="plain"
              >
                {{ scope.row.tagName }}
              </el-tag>
            </template>
          </el-table-column>
          <el-table-column prop="seqNo" label="排序号" width="80" />
          <el-table-column prop="crtTime" label="创建时间" width="160">
            <template #default="scope">
              {{ formatDate(scope.row.crtTime) }}
            </template>
          </el-table-column>
          <el-table-column prop="updTime" label="更新时间" width="160">
            <template #default="scope">
              {{ formatDate(scope.row.updTime) }}
            </template>
          </el-table-column>
          <el-table-column label="操作" width="150" fixed="right">
            <template #default="scope">
              <el-button
                type="primary"
                size="small"
                @click="handleEdit(scope.row)"
              >
                编辑
              </el-button>
              <el-button
                type="danger"
                size="small"
                @click="handleDelete(scope.row)"
              >
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

    <!-- 导入对话框 -->
    <el-dialog
      v-model="importDialogVisible"
      title="导入标签数据"
      width="600px"
      :close-on-click-modal="false"
    >
      <div class="import-content">
        <el-alert
          title="导入说明"
          type="info"
          :closable="false"
          style="margin-bottom: 20px"
        >
          <template #default>
            <p>1. 请先下载导入模板，按照模板格式填写数据</p>
            <p>2. 支持的文件格式：.xlsx</p>
            <p>3. 应用模块、标签代码、标签名称为必填项</p>
            <p>4. 标签代码在同一应用模块下不能重复</p>
          </template>
        </el-alert>
        
        <el-upload
          ref="uploadRef"
          class="upload-demo"
          drag
          :auto-upload="false"
          :limit="1"
          accept=".xlsx"
          :on-change="handleFileChange"
          :file-list="fileList"
        >
          <el-icon class="el-icon--upload"><upload-filled /></el-icon>
          <div class="el-upload__text">
            将Excel文件拖到此处，或<em>点击上传</em>
          </div>
          <template #tip>
            <div class="el-upload__tip">
              只能上传.xlsx文件，且不超过10MB
            </div>
          </template>
        </el-upload>
        
        <!-- 导入进度 -->
        <div v-if="importProgress.show" class="import-progress">
          <el-progress
            :percentage="importProgress.percentage"
            :status="importProgress.status"
            :stroke-width="20"
          >
            <template #default="{ percentage }">
              <span class="percentage-value">{{ percentage }}%</span>
            </template>
          </el-progress>
          <p class="progress-text">{{ importProgress.text }}</p>
        </div>
      </div>
      
      <template #footer>
        <span class="dialog-footer">
          <el-button @click="importDialogVisible = false">取消</el-button>
          <el-button 
            type="primary" 
            @click="handleConfirmImport"
            :loading="importLoading"
            :disabled="!selectedFile"
          >
            开始导入
          </el-button>
        </span>
      </template>
    </el-dialog>
    <!-- 新增/编辑对话框 -->
    <el-dialog
      v-model="dialogVisible"
      :title="isEdit ? '编辑标签' : '新增标签'"
      width="600px"
      :close-on-click-modal="false"
    >
      <el-form
        ref="tagFormRef"
        :model="tagForm"
        :rules="tagRules"
        label-width="100px"
        size="default"
      >
        <el-form-item label="应用模块" prop="parName">
          <el-select
            v-model="tagForm.parName"
            placeholder="请选择应用模块"
            allow-create
            filterable
            style="width: 100%"
            :disabled="isEdit"
          >
            <el-option
              v-for="module in moduleOptions"
              :key="module"
              :label="module"
              :value="module"
            />
          </el-select>
        </el-form-item>
        
        <el-form-item label="标签代码" prop="tagCode">
          <el-input
            v-model="tagForm.tagCode"
            placeholder="请输入标签代码"
            :disabled="isEdit"
          />
        </el-form-item>
        
        <el-form-item label="标签名称" prop="tagName">
          <el-input
            v-model="tagForm.tagName"
            placeholder="请输入标签名称"
          />
        </el-form-item>
        
        <el-form-item label="字体颜色" prop="hexColor">
          <div class="color-picker-container">
            <el-color-picker
              v-model="tagForm.hexColor"
              :predefine="predefineColors"
              @change="handleHexColorChange"
            />
            <el-input
              v-model="tagForm.hexColor"
              placeholder="#000000"
              style="margin-left: 10px; width: 120px;"
              @input="handleHexColorChange"
            />
          </div>
        </el-form-item>
        
        <el-form-item label="背景颜色" prop="rgbColor">
          <div class="color-picker-container">
            <el-color-picker
              v-model="backgroundHexColor"
              :predefine="predefineColors"
              @change="handleBackgroundColorChange"
            />
            <el-input
              v-model="backgroundHexColor"
              placeholder="#FFFFFF"
              style="margin-left: 10px; width: 120px;"
              @input="handleBackgroundColorChange"
            />
            <el-input
              v-model="tagForm.rgbColor"
              placeholder="rgba(255, 255, 255, 0.1)"
              style="margin-left: 10px; width: 200px;"
              readonly
            />
          </div>
          <div class="color-tip">
            <el-text size="small" type="info">背景颜色透明度固定为0.1</el-text>
          </div>
        </el-form-item>
        
        <el-form-item label="排序号" prop="seqNo">
          <el-input-number
            v-model="tagForm.seqNo"
            :min="0"
            :max="9999"
            placeholder="请输入排序号"
          />
        </el-form-item>
        
        <!-- 预览效果 -->
        <el-form-item label="预览效果">
          <div 
            class="tag-preview" 
            :style="{ 
              backgroundColor: tagForm.rgbColor, 
              color: tagForm.hexColor,
              border: `1px solid ${tagForm.hexColor}`
            }"
          >
            {{ tagForm.tagName || '标签预览' }}
          </div>
        </el-form-item>
      </el-form>
      
      <template #footer>
        <span class="dialog-footer">
          <el-button @click="dialogVisible = false">取消</el-button>
          <el-button 
            type="primary" 
            @click="handleSubmit"
            :loading="submitLoading"
          >
            {{ isEdit ? '更新' : '创建' }}
          </el-button>
        </span>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { tagApi, type Tag, type TagDto, type TagSearchParams } from '@/api/tag'
import { Delete, Download, Plus, Refresh, Search, Upload, UploadFilled } from '@element-plus/icons-vue'
import { ElForm, ElMessage, ElMessageBox, ElTable, ElUpload, type FormRules, type UploadFile, type UploadFiles } from 'element-plus'
import { nextTick, onMounted, onUnmounted, reactive, ref } from 'vue'

// 响应式数据
const loading = ref(false)
const submitLoading = ref(false)
const dialogVisible = ref(false)
const isEdit = ref(false)
const tagFormRef = ref<InstanceType<typeof ElForm>>()
const tableHeight = ref(400)
const currentTagId = ref<number | null>(null)
// 默认模块选项：包含"访问地址"和"用户管理"
const DEFAULT_MODULES = ['访问地址-标签类型', '用户管理-用户类型','用户管理-所属系统',]
// 初始化包含"用户管理"
const moduleOptions = ref<string[]>([...DEFAULT_MODULES])

// 导入相关数据
const importDialogVisible = ref(false)
const importLoading = ref(false)
const uploadRef = ref<InstanceType<typeof ElUpload>>()
const fileList = ref<UploadFile[]>([])
const selectedFile = ref<File | null>(null)
const importProgress = reactive({
  show: false,
  percentage: 0,
  status: 'success' as 'success' | 'exception' | 'warning',
  text: ''
})

// 搜索表单
const searchForm = reactive<TagSearchParams>({
  parName: '',
  tagCode: '',
  tagName: ''
})

// 标签列表
const tagList = ref<Tag[]>([])

// 分页
const pagination = reactive({
  currentPage: 1,
  pageSize: 10,
  total: 0
})

// 标签表单
const tagForm = reactive<TagDto>({
  parName: '',
  tagCode: '',
  tagName: '',
  hexColor: '#000000',
  rgbColor: 'rgba(255, 255, 255, 0.1)',
  seqNo: 0
})

// RGB 颜色选择器的值
const rgbColorPicker = ref('#ffffff')
// 背景颜色的16进制值
const backgroundHexColor = ref('#ffffff')

// 预定义颜色
const predefineColors = ref([
  '#ff4500',  // 橙红色
  '#ff8c00',  // 深橙色
  '#ffd700',  // 金色
  '#90ee90',  // 浅绿色
  '#00ced1',  // 深青色
  '#1e90ff',  // 道奇蓝
  '#c71585',  // 深粉色
  '#8a2be2',  // 蓝紫色
  '#dc143c',  // 深红色
  '#32cd32',  // 酸橙绿
  '#ff69b4',  // 热粉色
  '#4169e1',  // 皇家蓝
  '#ff1493',  // 深粉色
  '#00ff7f',  // 春绿色
  '#ff6347',  // 番茄色
  '#40e0d0',  // 青绿色
  '#da70d6',  // 兰花紫
  '#98fb98',  // 苍绿色
  '#f0e68c',  // 卡其色
  '#dda0dd'   // 梅花色
])

// 表单验证规则
const tagRules: FormRules = {
  parName: [
    { required: true, message: '请输入应用模块', trigger: 'blur' },
    { max: 50, message: '应用模块长度不能超过50个字符', trigger: 'blur' }
  ],
  tagCode: [
    { required: true, message: '请输入标签代码', trigger: 'blur' },
    { max: 20, message: '标签代码长度不能超过20个字符', trigger: 'blur' },
    { 
      validator: (rule, value, callback) => {
        if (value && tagForm.parName) {
          tagApi.checkTagCodeExists(
            tagForm.parName, 
            value, 
            isEdit.value && currentTagId.value ? currentTagId.value : undefined
          ).then(exists => {
            if (exists) {
              callback(new Error('该应用模块下标签代码已存在'))
            } else {
              callback()
            }
          }).catch(() => {
            callback()
          })
        } else {
          callback()
        }
      }, 
      trigger: 'blur' 
    }
  ],
  tagName: [
    { required: true, message: '请输入标签名称', trigger: 'blur' },
    { max: 50, message: '标签名称长度不能超过50个字符', trigger: 'blur' }
  ],
  hexColor: [
    { required: true, message: '请选择字体颜色', trigger: 'blur' },
    { pattern: /^#[0-9A-Fa-f]{6}$/, message: '字体颜色格式不正确，应为#RRGGBB格式', trigger: 'blur' }
  ],
  rgbColor: [
    { required: true, message: '请选择背景颜色', trigger: 'blur' },
    { max: 50, message: '背景颜色长度不能超过50个字符', trigger: 'blur' }
  ]
}

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

// 处理字体颜色变化
const handleHexColorChange = (color: string) => {
  if (color && color.startsWith('#') && color.length === 7) {
    tagForm.hexColor = color
  }
}

// 处理背景颜色变化
const handleBackgroundColorChange = (color: string) => {
  if (color && color.startsWith('#') && color.length === 7) {
    backgroundHexColor.value = color
    // 将hex颜色转换为rgba格式，透明度固定为0.1
    const r = parseInt(color.slice(1, 3), 16)
    const g = parseInt(color.slice(3, 5), 16)
    const b = parseInt(color.slice(5, 7), 16)
    tagForm.rgbColor = `rgba(${r}, ${g}, ${b}, 0.1)`
  }
}

// 加载标签列表
const loadTagList = async () => {
  loading.value = true
  try {
    const response = await tagApi.getTagList({
      page: pagination.currentPage,
      pageSize: pagination.pageSize,
      ...searchForm
    })
    
    tagList.value = response.data
    pagination.total = response.total
  } catch (error) {
    console.error('加载标签列表失败:', error)
    ElMessage.error('加载标签列表失败')
  } finally {
    loading.value = false
  }
}

// 处理每页显示数量变化
const handleSizeChange = (size: number) => {
  pagination.pageSize = size
  pagination.currentPage = 1
  loadTagList()
}

// 处理当前页变化
const handleCurrentChange = (page: number) => {
  pagination.currentPage = page
  loadTagList()
}

// 加载应用模块选项
const loadModuleOptions = async () => {
  try {
    const modules = await tagApi.getParNameList()
    const merged = Array.from(new Set([...(modules || []), ...DEFAULT_MODULES]))
    moduleOptions.value = merged
  } catch (error) {
    console.error('Load module options error:', error)
    // 加载失败使用默认值
    moduleOptions.value = [...DEFAULT_MODULES]
  }
}

// 搜索
const handleSearch = () => {
  pagination.currentPage = 1
  loadTagList()
}

// 重置搜索
const handleReset = () => {
  Object.assign(searchForm, {
    parName: '',
    tagCode: '',
    tagName: ''
  })
  pagination.currentPage = 1
  loadTagList()
}

// 新增标签
const handleAdd = () => {
  isEdit.value = false
  currentTagId.value = null
  resetTagForm()
  dialogVisible.value = true
}

// 编辑标签
// 编辑标签
const handleEdit = (row: Tag) => {
  isEdit.value = true
  currentTagId.value = row.id
  Object.assign(tagForm, {
    parName: row.parName,
    tagCode: row.tagCode,
    tagName: row.tagName,
    hexColor: row.hexColor,
    rgbColor: row.rgbColor,
    seqNo: row.seqNo
  })
  
  // 从rgba值中提取hex颜色
  if (row.rgbColor && row.rgbColor.startsWith('rgba(')) {
    const rgbaMatch = row.rgbColor.match(/rgba\((\d+),\s*(\d+),\s*(\d+),\s*[\d.]+\)/)
    if (rgbaMatch) {
      const r = parseInt(rgbaMatch[1])
      const g = parseInt(rgbaMatch[2])
      const b = parseInt(rgbaMatch[3])
      const hex = '#' + [r, g, b].map(x => {
        const hex = x.toString(16)
        return hex.length === 1 ? '0' + hex : hex
      }).join('')
      backgroundHexColor.value = hex
    }
  }
  
  rgbColorPicker.value = row.hexColor || '#000000'
  dialogVisible.value = true
}

// 删除标签
const handleDelete = async (row: Tag) => {
  try {
    await ElMessageBox.confirm(
      `确定要删除标签 "${row.tagName}" 吗？`,
      '删除确认',
      {
        confirmButtonText: '确定',
        cancelButtonText: '取消',
        type: 'warning'
      }
    )
    
    await tagApi.deleteTag(row.id)
    ElMessage.success('删除成功')
    loadTagList()
  } catch (error) {
    if (error !== 'cancel') {
      console.error('Delete tag error:', error)
      ElMessage.error('删除失败')
    }
  }
}

// 提交表单
const handleSubmit = async () => {
  if (!tagFormRef.value) return
  
  await tagFormRef.value.validate(async (valid) => {
    if (!valid) return
    
    submitLoading.value = true
    try {
      if (isEdit.value && currentTagId.value) {
        await tagApi.updateTag(currentTagId.value, tagForm)
        ElMessage.success('更新成功')
      } else {
        await tagApi.createTag(tagForm)
        ElMessage.success('创建成功')
      }
      
      dialogVisible.value = false
      loadTagList()
    } catch (error) {
      console.error('Submit error:', error)
      ElMessage.error(isEdit.value ? '更新失败' : '创建失败')
    } finally {
      submitLoading.value = false
    }
  })
}

// 重置表单
const resetTagForm = () => {
  Object.assign(tagForm, {
    parName: '',
    tagCode: '',
    tagName: '',
    hexColor: '#000000',
    rgbColor: 'rgba(255, 255, 255, 0.1)',
    seqNo: 0
  })
  
  // 重置背景颜色选择器
  rgbColorPicker.value = '#ffffff'
  backgroundHexColor.value = '#ffffff'
  
  // 清除表单验证
  nextTick(() => {
    tagFormRef.value?.clearValidate()
  })
}

// 组件挂载时
onMounted(() => {
  loadTagList()
  loadModuleOptions() // 加载应用模块选项
  calculateTableHeight()
  
  // 监听窗口大小变化
  window.addEventListener('resize', calculateTableHeight)
})

// 组件卸载时移除事件监听
onUnmounted(() => {
  window.removeEventListener('resize', calculateTableHeight)
})


// 下载导入模板
const handleDownloadTemplate = async () => {
  try {
    const response = await tagApi.downloadTemplate()
    const blob = new Blob([response], {
      type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet'
    })
    const url = window.URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = url
    link.download = '标签导入模板.xlsx'
    document.body.appendChild(link)
    link.click()
    document.body.removeChild(link)
    window.URL.revokeObjectURL(url)
    ElMessage.success('模板下载成功')
  } catch (error) {
    console.error('下载模板失败:', error)
    ElMessage.error('下载模板失败')
  }
}

// 打开导入对话框
const handleImport = () => {
  resetImportData()
  importDialogVisible.value = true
}

// 文件选择变化
const handleFileChange = (file: UploadFile, files: UploadFiles) => {
  if (file.raw) {
    selectedFile.value = file.raw
    fileList.value = [file]
  }
}

// 确认导入
const handleConfirmImport = async () => {
  if (!selectedFile.value) {
    ElMessage.warning('请选择要导入的文件')
    return
  }

  importLoading.value = true
  importProgress.show = true
  importProgress.percentage = 0
  importProgress.status = 'success'
  importProgress.text = '正在上传文件...'

  try {
    const formData = new FormData()
    formData.append('file', selectedFile.value)

    // 模拟进度更新
    const progressInterval = setInterval(() => {
      if (importProgress.percentage < 90) {
        importProgress.percentage += 10
        if (importProgress.percentage <= 30) {
          importProgress.text = '正在解析Excel文件...'
        } else if (importProgress.percentage <= 60) {
          importProgress.text = '正在验证数据...'
        } else {
          importProgress.text = '正在保存数据...'
        }
      }
    }, 200)

    const response = await tagApi.importTags(formData)
    
    clearInterval(progressInterval)
    importProgress.percentage = 100
    importProgress.text = '导入完成'

    // 下载结果文件
    if (response.data && response.data.resultFile) {
      try {
        // 确保Base64字符串格式正确
        const base64Data = response.data.resultFile.replace(/\s/g, '')
        
        // 解码Base64数据
        const binaryString = atob(base64Data)
        const bytes = new Uint8Array(binaryString.length)
        for (let i = 0; i < binaryString.length; i++) {
          bytes[i] = binaryString.charCodeAt(i)
        }
        
        const blob = new Blob([bytes], {
          type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet'
        })
        
        const url = window.URL.createObjectURL(blob)
        const link = document.createElement('a')
        link.href = url
        link.download = response.data.resultFileName || '导入结果.xlsx'
        document.body.appendChild(link)
        link.click()
        document.body.removeChild(link)
        window.URL.revokeObjectURL(url)
      } catch (error) {
        console.error('文件下载失败:', error)
        ElMessage.error('结果文件下载失败，请联系管理员')
      }
    }

    ElMessage.success(`导入完成！成功：${response.data.successCount}，失败：${response.data.failCount}`)
    
    // 刷新列表
    loadTagList()
    
    // 关闭对话框
    setTimeout(() => {
      importDialogVisible.value = false
      resetImportData()
    }, 1000)
    
  } catch (error) {
    console.error('导入失败:', error)
    importProgress.status = 'exception'
    importProgress.text = '导入失败'
    ElMessage.error('导入失败，请检查文件格式和数据')
  } finally {
    importLoading.value = false
  }
}

// 重置导入数据
const resetImportData = () => {
  selectedFile.value = null
  fileList.value = []
  importProgress.show = false
  importProgress.percentage = 0
  importProgress.status = 'success'
  importProgress.text = ''
  uploadRef.value?.clearFiles()
}

/* 移除重复的样式定义 */
// 在响应式数据部分添加（大约第340行附近）：
// 批量删除相关数据
const tableRef = ref<InstanceType<typeof ElTable>>()
const selectedTags = ref<Tag[]>([])
const selectedRows = ref<Tag[]>([])

// 在方法部分添加（大约第600行附近）：
// 处理表格选择变化
const handleSelectionChange = (selection: Tag[]) => {
  selectedTags.value = selection
  selectedRows.value = selection
}

// 批量删除处理
// 批量删除处理
const handleBatchDelete = async () => {
  if (selectedTags.value.length === 0) {
    ElMessage.warning('请选择要删除的标签')
    return
  }

  try {
    await ElMessageBox.confirm(
      `确定要删除选中的 ${selectedTags.value.length} 个标签吗？`,
      '批量删除确认',
      {
        confirmButtonText: '确定',
        cancelButtonText: '取消',
        type: 'warning'
      }
    )

    const ids = selectedTags.value.map(row => row.id)
    // 调用批量删除API
    await tagApi.batchDeleteTags(ids)
    
    ElMessage.success('批量删除成功')
    
    // 清空选择
    tableRef.value?.clearSelection()
    selectedTags.value = []
    selectedRows.value = []
    
    // 重新加载列表
    loadTagList()
  } catch (error) {
    if (error !== 'cancel') {
      console.error('Batch delete error:', error)
      ElMessage.error('批量删除失败')
    }
  }
}

</script>

<style scoped>
.tag-management-container {
  padding: 20px;
  height: calc(100vh - 60px); /* 减去header高度 */
  overflow-y: auto;
  box-sizing: border-box;
  display: flex;
  flex-direction: column;
}

/* 确保卡片内容充满容器 */
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

/* 表格容器样式 - 关键优化 */
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
  flex-shrink: 0; /* 防止分页组件被压缩 */
}

.color-display {
  display: flex;
  align-items: center;
  justify-content: center;
}

.color-box {
  padding: 4px 8px;
  border-radius: 4px;
  font-size: 12px;
  font-weight: 500;
  text-align: center;
  min-width: 60px;
}

.color-picker-container {
  display: flex;
  align-items: center;
}

.tag-preview {
  padding: 6px 12px;
  border-radius: 4px;
  font-size: 14px;
  font-weight: 500;
  text-align: center;
  min-width: 80px;
  display: inline-block;
}

.dialog-footer {
  text-align: right;
}

.search-section {
  margin-bottom: 20px;
  padding: 20px;
  background-color: #f8f9fa;
  border-radius: 4px;
}

.color-tip {
  margin-top: 5px;
}

.color-picker-container {
  display: flex;
  align-items: center;
}
</style>