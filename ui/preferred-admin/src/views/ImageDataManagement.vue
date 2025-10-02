<template>
  <div class="image-data-management-container">
    <el-card>
      <template #header>
        <div class="card-header">
          <h2>图片管理</h2>
          <p>管理系统中的所有图片资源</p>
        </div>
      </template>

      <!-- 搜索和操作区域 -->
      <div class="search-section">
        <el-row :gutter="20">
          <el-col :span="5">
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
          <el-col :span="5">
            <el-input
              v-model="searchForm.imageName"
              placeholder="搜索图片名称"
              prefix-icon="Picture"
              clearable
            />
          </el-col>
          <el-col :span="5">
            <el-button type="primary" @click="handleSearch">
              <el-icon><Search /></el-icon>
              搜索
            </el-button>
            <el-button @click="handleReset">
              <el-icon><Refresh /></el-icon>
              重置
            </el-button>
          </el-col>
          <el-col :span="9" style="text-align: right">
            <el-button type="success" @click="handleAdd">
              <el-icon><Plus /></el-icon>
              新增图片
            </el-button>
          </el-col>
        </el-row>
      </div>

      <!-- 卡片容器 -->
      <div class="cards-container" v-loading="loading">
        <!-- 图片卡片网格 -->
        <div class="image-cards-grid">
          <div 
            v-for="item in imageList" 
            :key="item.id" 
            class="image-card"
          >
            <!-- 图片预览区域 -->
            <div class="image-preview">
              <el-image
                :src="getImageUrl(item.imagePath)"
                :preview-src-list="[getImageUrl(item.imagePath)]"
                fit="cover"
                class="card-image"
                :preview-teleported="true"
              >
                <template #error>
                  <div class="image-error">
                    <el-icon><PictureIcon /></el-icon>
                  </div>
                </template>
              </el-image>
              
              <!-- 简化的信息遮罩 -->
              <div class="image-overlay">
                <span class="image-size">{{ item.width }}×{{ item.height }}</span>
              </div>
            </div>
            
            <!-- 简化的卡片内容 -->
            <div class="card-content">
              <div class="card-title">
                <span class="image-name" :title="item.imageName">{{ item.imageName }}</span>
                <span class="image-id">#{{ item.id }}</span>
              </div>
              
              <!-- 将标签和按钮放在同一行 -->
              <div class="card-bottom">
                <el-tag size="small" type="info">{{ item.appType }}</el-tag>
                <div class="card-actions">
                  <el-button
                    type="primary"
                    size="small"
                    @click="handleEdit(item)"
                  >
                    编辑
                  </el-button>
                  <el-button
                    type="danger"
                    size="small"
                    @click="handleDelete(item)"
                  >
                    删除
                  </el-button>
                </div>
              </div>
            </div>
          </div>
        </div>
        
        <!-- 空状态 -->
        <div v-if="!loading && imageList.length === 0" class="empty-state">
          <el-empty description="暂无图片数据" />
        </div>
      </div>

      <!-- 分页 -->
      <div class="pagination-container">
        <el-pagination
          v-model:current-page="pagination.currentPage"
          v-model:page-size="pagination.pageSize"
          :page-sizes="[16, 32, 48, 64]"
          :total="pagination.total"
          layout="total, sizes, prev, pager, next, jumper"
          @size-change="handleSizeChange"
          @current-change="handleCurrentChange"
        />
      </div>
    </el-card>

    <!-- 新增/编辑对话框 -->
    <el-dialog
      v-model="dialogVisible"
      :title="isEdit ? '编辑图片' : '新增图片'"
      width="700px"
      :close-on-click-modal="false"
    >
      <el-form
        ref="formRef"
        :model="formData"
        :rules="formRules"
        label-width="100px"
      >
        <el-row :gutter="20">
          <el-col :span="12">
            <el-form-item label="应用类型" prop="appType">
              <el-select
                v-model="formData.appType"
                placeholder="选择应用类型"
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
          </el-col>
          <el-col :span="12">
            <el-form-item label="排序号" prop="seqNo">
              <el-input-number
                v-model="formData.seqNo"
                :min="0"
                placeholder="排序号"
                style="width: 100%"
              />
            </el-form-item>
          </el-col>
        </el-row>
        <el-form-item label="图片名称" prop="imageName">
          <el-input
            v-model="formData.imageName"
            placeholder="根据上传文件自动获取"
            style="width: 100%"
          />
        </el-form-item>
        
        <!-- 图片上传区域 -->
        <el-form-item label="图片上传" prop="imagePath">
          <div class="upload-container">
            <div v-if="!formData.imagePath && !croppedImageUrl" class="upload-area">
              <input
                ref="fileInput"
                type="file"
                accept="image/*"
                style="display: none"
                @change="handleFileSelect"
              />
              <div class="upload-placeholder" @click="selectFile">
                <el-icon class="upload-icon"><Plus /></el-icon>
                <div class="upload-text">点击选择图片</div>
                <div class="upload-tip">支持 jpg、png、gif 格式，文件大小不超过 10MB</div>
              </div>
            </div>
           
            <div v-else class="uploaded-preview">
              <!-- 优先显示裁剪后的预览图片（新增或编辑模式都适用） -->
              <img 
                v-if="croppedImageUrl" 
                :src="croppedImageUrl" 
                class="uploaded-image" 
              />
              <!-- 如果没有裁剪图片，显示服务器图片（仅编辑模式） -->
              <img 
                v-else-if="formData.imagePath" 
                :src="getImageUrl(formData.imagePath)" 
                class="uploaded-image" 
              />
              <div class="image-actions">
                <el-button size="small" @click="reSelectImage">重新选择</el-button>
              </div>
            </div>
          </div>
        </el-form-item>
        
        <el-row :gutter="20">
          <el-col :span="8">
            <el-form-item label="宽度">
              <el-input
                v-model="formData.width"
                placeholder="自动获取"
                style="width: 100%"
                readonly
              />
            </el-form-item>
          </el-col>
          <el-col :span="8">
            <el-form-item label="高度">
              <el-input
                v-model="formData.height"
                placeholder="自动获取"
                style="width: 100%"
                readonly
              />
            </el-form-item>
          </el-col>
          <el-col :span="8">
            <el-form-item label="实际宽高比">
              <el-input
                v-model="formData.aspectRatio"
                placeholder="自动计算"
                style="width: 100%"
                readonly
              />
            </el-form-item>
          </el-col>
        </el-row>

      </el-form>
      <template #footer>
        <div class="dialog-footer">
          <el-button @click="dialogVisible = false">取消</el-button>
          <el-button type="primary" @click="handleSubmit" :loading="submitLoading">确定</el-button>
        </div>
      </template>
    </el-dialog>

    <!-- 图片裁剪对话框 -->
    <el-dialog
      v-model="cropDialogVisible"
      title="图片裁剪"
      width="900px"
      :close-on-click-modal="false"
      :before-close="handleCropDialogClose"
    >
      <div class="crop-container">
        <!-- 尺寸选择 -->
        <div class="size-selector">
          <span class="selector-label">选择裁剪尺寸：</span>
          <el-radio-group v-model="selectedSizeIndex" @change="handleCorpSizeChange">
            <el-radio
              v-for="(size, index) in cropSizes"
              :key="index"
              :label="index"
              border
            >
              {{ size.label }} ({{ size.width }}:{{ size.height }})
            </el-radio>
          </el-radio-group>
        </div>

        <div class="crop-content">
          <!-- 裁剪区域 -->
          <div class="crop-area">
            <div class="crop-canvas-container">
              <canvas
                ref="cropCanvas"
                class="crop-canvas"
                @mousedown="handleMouseDown"
                @mousemove="handleMouseMove"
                @mouseup="handleMouseUp"
                @mouseleave="handleMouseUp"
                @wheel="handleWheel"
              ></canvas>
            </div>
            
            <!-- 缩放控制 -->
            <div class="zoom-controls">
              <el-button-group>
                <el-button size="small" @click="zoomOut">-</el-button>
                <el-button size="small" disabled>{{ Math.round(zoomLevel * 100) }}%</el-button>
                <el-button size="small" @click="zoomIn">+</el-button>
              </el-button-group>
              <el-button size="small" @click="resetZoom" style="margin-left: 10px">重置</el-button>
            </div>
          </div>

          <!-- 预览区域 -->
          <div class="preview-area">
            <div class="preview-title">预览效果</div>
            <div class="preview-container">
              <canvas ref="previewCanvas" class="preview-canvas"></canvas>
            </div>
            <div class="preview-info">
              <p>尺寸：{{ currentCropSize.width }} × {{ currentCropSize.height }}</p>
              <p>比例：{{ currentCropSize.width }}:{{ currentCropSize.height }}</p>
            </div>
          </div>
        </div>
      </div>
      
      <template #footer>
        <div class="dialog-footer">
          <el-button @click="handleCropDialogClose">取消</el-button>
          <el-button type="primary" @click="confirmCrop" :loading="cropLoading">确认裁剪</el-button>
        </div>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted, computed, nextTick } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { Plus, Search, Refresh, Picture as PictureIcon } from '@element-plus/icons-vue'
import { pictureApi, type Picture, type PictureDto } from '@/api/picture'
import { tagApi } from '@/api/tag'

// 添加API基础URL配置
const API_BASE_URL = 'http://localhost:5000'

// 添加图片URL处理函数
const getImageUrl = (imagePath: string) => {
  console.log('处理图片路径:', imagePath)
  if (!imagePath) return ''
  if (imagePath.startsWith('http')) {
    return imagePath // 已经是完整URL
  }
  const fullUrl = `${API_BASE_URL}${imagePath}`
  console.log('生成的完整URL:', fullUrl)
  return fullUrl // 拼接基础URL
}

// 创建表单专用接口
interface PictureFormData {
  id?: number
  appType: string
  imageCode: string
  imageName: string
  imagePath: string
  aspectRatio: number
  width: number | null
  height: number | null
  seqNo: number
  crtTime?: string
  updTime?: string
}

// 裁剪尺寸选项接口
interface CropSize {
  label: string
  width: number
  height: number
  ratio: number
}

// 响应式数据
const loading = ref(false)
const submitLoading = ref(false)
const cropLoading = ref(false)
const imageList = ref<Picture[]>([])
const dialogVisible = ref(false)
const cropDialogVisible = ref(false)
const isEdit = ref(false)
const formRef = ref()
const fileInput = ref<HTMLInputElement>()
const cropCanvas = ref<HTMLCanvasElement>()
const previewCanvas = ref<HTMLCanvasElement>()

const appTypeOptions = ref<string[]>(['访问地址']) // 应用类型选项

// 添加裁剪后的图片数据存储
const croppedImageBlob = ref<Blob | null>(null)
const croppedImageUrl = ref<string>('')

// 在响应式数据部分添加原始图片路径存储
const originalImagePath = ref<string>('')

// 裁剪相关数据
const selectedSizeIndex = ref(0) // 默认选择第一个尺寸
const originalImage = ref<HTMLImageElement | null>(null)
const selectedFile = ref<File | null>(null)
const zoomLevel = ref(1)
const imagePosition = ref({ x: 0, y: 0 })
const isDragging = ref(false)
const lastMousePos = ref({ x: 0, y: 0 })

// 裁剪尺寸选项
const cropSizes: CropSize[] = [
  { label: '标准', width: 145, height: 128, ratio: 145/128 },
  { label: '宽屏', width: 400, height: 300, ratio: 400/300 },
  { label: '超宽', width: 160, height: 90, ratio: 160/90 }
]

// 当前选择的裁剪尺寸
const currentCropSize = computed(() => cropSizes[selectedSizeIndex.value])

// 搜索表单
const searchForm = reactive({
  appType: '',
  imageName: ''
})

// 分页
const pagination = reactive({
  currentPage: 1,
  pageSize: 16,
  total: 0
})

// 表单数据
const formData = reactive<PictureFormData>({
  appType: '访问地址', // 默认选择"访问地址"
  imageCode: '',
  imageName: '',
  imagePath: '',
  aspectRatio: 0,
  width: null,
  height: null,
  seqNo: 0
})

// 表单验证规则
const formRules = {
  appType: [{ required: true, message: '请选择应用类型', trigger: 'change' }],
  imageName: [{ required: true, message: '请输入图片名称', trigger: 'blur' }],
  imagePath: [{ required: true, message: '请上传图片', trigger: 'change' }],
  seqNo: [{ required: true, message: '请输入排序号', trigger: 'blur' }]
}

// 格式化日期
const formatDate = (dateString: string) => {
  if (!dateString) return '-'
  return new Date(dateString).toLocaleString('zh-CN')
}

// 格式化宽高比显示
const formatAspectRatio = (ratio: number) => {
  if (!ratio) return '-'
  return ratio.toFixed(2)
}

// 从文件名获取图片名称（不含后缀）
const getImageNameFromFile = (fileName: string): string => {
  const lastDotIndex = fileName.lastIndexOf('.')
  return lastDotIndex > 0 ? fileName.substring(0, lastDotIndex) : fileName
}

// 生成GUID32位
const generateGUID32 = (): string => {
  const chars = '0123456789ABCDEF'
  let result = ''
  for (let i = 0; i < 32; i++) {
    result += chars[Math.floor(Math.random() * 16)]
  }
  return result
}

// 选择文件
const selectFile = () => {
  fileInput.value?.click()
}

// 文件选择处理
const handleFileSelect = (event: Event) => {
  const target = event.target as HTMLInputElement
  const file = target.files?.[0]
  
  if (!file) return
  
  // 验证文件类型
  if (!file.type.startsWith('image/')) {
    ElMessage.error('请选择图片文件！')
    return
  }
  
  // 验证文件大小（10MB）
  if (file.size > 10 * 1024 * 1024) {
    ElMessage.error('图片大小不能超过 10MB！')
    return
  }
  
  selectedFile.value = file
  // 自动设置图片名称（不含后缀）
  formData.imageName = getImageNameFromFile(file.name)
  
  // 加载图片并打开裁剪对话框
  loadImageForCrop(file)
}

// 加载图片用于裁剪
const loadImageForCrop = (file: File) => {
  const reader = new FileReader()
  reader.onload = (e) => {
    const img = new Image()
    img.onload = () => {
      originalImage.value = img
      resetCropSettings()
      cropDialogVisible.value = true
      nextTick(() => {
        initCropCanvas()
      })
    }
    img.src = e.target?.result as string
  }
  reader.readAsDataURL(file)
}

// 重置裁剪设置
const resetCropSettings = () => {
  selectedSizeIndex.value = 0
  zoomLevel.value = 1
  imagePosition.value = { x: 0, y: 0 }
  isDragging.value = false
}

// 初始化裁剪画布
const initCropCanvas = () => {
  if (!cropCanvas.value || !originalImage.value) return
  
  const canvas = cropCanvas.value
  canvas.width = 500
  canvas.height = 400
  
  // 计算图片初始位置（居中）
  const img = originalImage.value
  const canvasAspect = canvas.width / canvas.height
  const imageAspect = img.width / img.height
  
  let displayWidth, displayHeight
  if (imageAspect > canvasAspect) {
    displayWidth = canvas.width
    displayHeight = canvas.width / imageAspect
  } else {
    displayHeight = canvas.height
    displayWidth = canvas.height * imageAspect
  }
  
  imagePosition.value = {
    x: (canvas.width - displayWidth) / 2,
    y: (canvas.height - displayHeight) / 2
  }
  
  drawCropCanvas()
  updatePreview()
}

// 绘制裁剪画布
const drawCropCanvas = () => {
  if (!cropCanvas.value || !originalImage.value) return
  
  const canvas = cropCanvas.value
  const ctx = canvas.getContext('2d')!
  const img = originalImage.value
  
  // 清除画布
  ctx.clearRect(0, 0, canvas.width, canvas.height)
  
  // 计算图片显示尺寸
  const canvasAspect = canvas.width / canvas.height
  const imageAspect = img.width / img.height
  
  let baseWidth, baseHeight
  if (imageAspect > canvasAspect) {
    baseWidth = canvas.width
    baseHeight = canvas.width / imageAspect
  } else {
    baseHeight = canvas.height
    baseWidth = canvas.height * imageAspect
  }
  
  const displayWidth = baseWidth * zoomLevel.value
  const displayHeight = baseHeight * zoomLevel.value
  
  // 绘制图片
  ctx.drawImage(
    img,
    imagePosition.value.x,
    imagePosition.value.y,
    displayWidth,
    displayHeight
  )
  
  // 绘制裁剪框
  const cropSize = currentCropSize.value
  const cropWidth = Math.min(300, canvas.width - 40)
  const cropHeight = cropWidth / cropSize.ratio
  const cropX = (canvas.width - cropWidth) / 2
  const cropY = (canvas.height - cropHeight) / 2
  
  // 绘制遮罩
  ctx.fillStyle = 'rgba(0, 0, 0, 0.5)'
  ctx.fillRect(0, 0, canvas.width, canvas.height)
  
  // 清除裁剪区域的遮罩
  ctx.globalCompositeOperation = 'destination-out'
  ctx.fillRect(cropX, cropY, cropWidth, cropHeight)
  
  // 重置合成模式
  ctx.globalCompositeOperation = 'source-over'
  
  // 绘制裁剪框边框
  ctx.strokeStyle = '#409eff'
  ctx.lineWidth = 2
  ctx.strokeRect(cropX, cropY, cropWidth, cropHeight)
  
  // 绘制角标
  const cornerSize = 10
  ctx.fillStyle = '#409eff'
  // 左上角
  ctx.fillRect(cropX - 1, cropY - 1, cornerSize, 3)
  ctx.fillRect(cropX - 1, cropY - 1, 3, cornerSize)
  // 右上角
  ctx.fillRect(cropX + cropWidth - cornerSize + 1, cropY - 1, cornerSize, 3)
  ctx.fillRect(cropX + cropWidth - 2, cropY - 1, 3, cornerSize)
  // 左下角
  ctx.fillRect(cropX - 1, cropY + cropHeight - 2, cornerSize, 3)
  ctx.fillRect(cropX - 1, cropY + cropHeight - cornerSize + 1, 3, cornerSize)
  // 右下角
  ctx.fillRect(cropX + cropWidth - cornerSize + 1, cropY + cropHeight - 2, cornerSize, 3)
  ctx.fillRect(cropX + cropWidth - 2, cropY + cropHeight - cornerSize + 1, 3, cornerSize)
}

// 更新预览
const updatePreview = () => {
  if (!previewCanvas.value || !originalImage.value || !cropCanvas.value) return
  
  const previewCtx = previewCanvas.value.getContext('2d')!
  const cropCtx = cropCanvas.value.getContext('2d')!
  const img = originalImage.value
  
  // 设置预览画布大小
  const cropSize = currentCropSize.value
  const previewWidth = 200
  const previewHeight = previewWidth / cropSize.ratio
  
  previewCanvas.value.width = previewWidth
  previewCanvas.value.height = previewHeight
  
  // 计算裁剪区域
  const cropWidth = Math.min(300, cropCanvas.value.width - 40)
  const cropHeight = cropWidth / cropSize.ratio
  const cropX = (cropCanvas.value.width - cropWidth) / 2
  const cropY = (cropCanvas.value.height - cropHeight) / 2
  
  // 计算图片显示尺寸
  const canvasAspect = cropCanvas.value.width / cropCanvas.value.height
  const imageAspect = img.width / img.height
  
  let baseWidth, baseHeight
  if (imageAspect > canvasAspect) {
    baseWidth = cropCanvas.value.width
    baseHeight = cropCanvas.value.width / imageAspect
  } else {
    baseHeight = cropCanvas.value.height
    baseWidth = cropCanvas.value.height * imageAspect
  }
  
  const displayWidth = baseWidth * zoomLevel.value
  const displayHeight = baseHeight * zoomLevel.value
  
  // 计算源图片中的裁剪区域
  const scaleX = img.width / displayWidth
  const scaleY = img.height / displayHeight
  
  const sourceX = Math.max(0, (cropX - imagePosition.value.x) * scaleX)
  const sourceY = Math.max(0, (cropY - imagePosition.value.y) * scaleY)
  const sourceWidth = Math.min(img.width - sourceX, cropWidth * scaleX)
  const sourceHeight = Math.min(img.height - sourceY, cropHeight * scaleY)
  
  // 绘制预览
  previewCtx.clearRect(0, 0, previewWidth, previewHeight)
  if (sourceWidth > 0 && sourceHeight > 0) {
    previewCtx.drawImage(
      img,
      sourceX, sourceY, sourceWidth, sourceHeight,
      0, 0, previewWidth, previewHeight
    )
  }
}

// 鼠标事件处理
const handleMouseDown = (event: MouseEvent) => {
  isDragging.value = true
  lastMousePos.value = { x: event.clientX, y: event.clientY }
  if (cropCanvas.value) {
    cropCanvas.value.style.cursor = 'move'
  }
}

const handleMouseMove = (event: MouseEvent) => {
  if (!isDragging.value) return
  
  const deltaX = event.clientX - lastMousePos.value.x
  const deltaY = event.clientY - lastMousePos.value.y
  
  imagePosition.value.x += deltaX
  imagePosition.value.y += deltaY
  
  lastMousePos.value = { x: event.clientX, y: event.clientY }
  
  drawCropCanvas()
  updatePreview()
}

const handleMouseUp = () => {
  isDragging.value = false
  if (cropCanvas.value) {
    cropCanvas.value.style.cursor = 'default'
  }
}

// 滚轮缩放
const handleWheel = (event: WheelEvent) => {
  event.preventDefault()
  
  const delta = event.deltaY > 0 ? -0.1 : 0.1
  const newZoom = Math.max(0.1, Math.min(3, zoomLevel.value + delta))
  
  zoomLevel.value = newZoom
  drawCropCanvas()
  updatePreview()
}

// 缩放控制
const zoomIn = () => {
  zoomLevel.value = Math.min(3, zoomLevel.value + 0.01)
  drawCropCanvas()
  updatePreview()
}

const zoomOut = () => {
  zoomLevel.value = Math.max(0.1, zoomLevel.value - 0.01)
  drawCropCanvas()
  updatePreview()
}

const resetZoom = () => {
  zoomLevel.value = 1
  if (originalImage.value && cropCanvas.value) {
    const canvas = cropCanvas.value
    const img = originalImage.value
    const canvasAspect = canvas.width / canvas.height
    const imageAspect = img.width / img.height
    
    let displayWidth, displayHeight
    if (imageAspect > canvasAspect) {
      displayWidth = canvas.width
      displayHeight = canvas.width / imageAspect
    } else {
      displayHeight = canvas.height
      displayWidth = canvas.height * imageAspect
    }
    
    imagePosition.value = {
      x: (canvas.width - displayWidth) / 2,
      y: (canvas.height - displayHeight) / 2
    }
  }
  drawCropCanvas()
  updatePreview()
}

// 尺寸变化处理
const handleCorpSizeChange = () => {
  drawCropCanvas()
  updatePreview()
}

// 确认裁剪
const confirmCrop = async () => {
  if (!cropCanvas.value || !originalImage.value || !selectedFile.value) return
  
  cropLoading.value = true
  
  try {
    const canvas = document.createElement('canvas')
    const ctx = canvas.getContext('2d')!
    const img = originalImage.value
    const cropSize = currentCropSize.value
    
    // 设置输出画布尺寸
    canvas.width = cropSize.width
    canvas.height = cropSize.height
    
    // 计算裁剪区域
    const cropWidth = Math.min(300, cropCanvas.value!.width - 40)
    const cropHeight = cropWidth / cropSize.ratio
    const cropX = (cropCanvas.value!.width - cropWidth) / 2
    const cropY = (cropCanvas.value!.height - cropHeight) / 2
    
    // 计算图片显示尺寸
    const canvasAspect = cropCanvas.value!.width / cropCanvas.value!.height
    const imageAspect = img.width / img.height
    
    let baseWidth, baseHeight
    if (imageAspect > canvasAspect) {
      baseWidth = cropCanvas.value!.width
      baseHeight = cropCanvas.value!.width / imageAspect
    } else {
      baseHeight = cropCanvas.value!.height
      baseWidth = cropCanvas.value!.height * imageAspect
    }
    
    const displayWidth = baseWidth * zoomLevel.value
    const displayHeight = baseHeight * zoomLevel.value
    
    // 计算源图片中的裁剪区域
    const scaleX = img.width / displayWidth
    const scaleY = img.height / displayHeight
    
    const sourceX = Math.max(0, (cropX - imagePosition.value.x) * scaleX)
    const sourceY = Math.max(0, (cropY - imagePosition.value.y) * scaleY)
    const sourceWidth = Math.min(img.width - sourceX, cropWidth * scaleX)
    const sourceHeight = Math.min(img.height - sourceY, cropHeight * scaleY)
    
    // 绘制裁剪后的图片
    if (sourceWidth > 0 && sourceHeight > 0) {
      ctx.drawImage(
        img,
        sourceX, sourceY, sourceWidth, sourceHeight,
        0, 0, canvas.width, canvas.height
      )
    }
    
    // 转换为Blob并暂存
    canvas.toBlob((blob) => {
      if (!blob) {
        ElMessage.error('图片裁剪失败')
        return
      }
      
      // 暂存裁剪后的图片数据
      croppedImageBlob.value = blob
      croppedImageUrl.value = URL.createObjectURL(blob)
      
      // 设置表单数据（临时显示）
      formData.imagePath = croppedImageUrl.value
      formData.width = cropSize.width
      formData.height = cropSize.height
      formData.aspectRatio = Number((cropSize.width / cropSize.height).toFixed(4)) // 保留4位小数
      
      console.log('图片裁剪完成，更新表单数据:', {
        width: formData.width,
        height: formData.height,
        aspectRatio: formData.aspectRatio,
        imagePath: formData.imagePath
      })
      
      cropDialogVisible.value = false
      ElMessage.success('图片裁剪完成，请填写信息后保存')
    }, selectedFile.value.type, 0.9)
    
  } catch (error) {
    console.error('Crop error:', error)
    ElMessage.error('裁剪失败')
  } finally {
    cropLoading.value = false
  }
}

// 关闭裁剪对话框
const handleCropDialogClose = () => {
  cropDialogVisible.value = false
  selectedFile.value = null
  originalImage.value = null
  // 重置文件输入
  if (fileInput.value) {
    fileInput.value.value = ''
  }
}

// 清理暂存的图片数据
const cleanupTempImage = () => {
  if (croppedImageUrl.value) {
    URL.revokeObjectURL(croppedImageUrl.value)
  }
  croppedImageBlob.value = null
  croppedImageUrl.value = ''
}

// 重新选择图片
const reSelectImage = () => {
  cleanupTempImage()
  formData.imagePath = ''
  formData.width = null
  formData.height = null
  formData.aspectRatio = 0
  selectFile()
}

// 加载应用类型选项
const loadAppTypeOptions = async () => {
  try {
    const modules = await tagApi.getParNameList()
    appTypeOptions.value = modules.length > 0 ? modules : ['访问地址']
  } catch (error) {
    console.error('Load app type options error:', error)
    appTypeOptions.value = ['访问地址']
  }
}

// 新增图片
const handleAdd = () => {
  isEdit.value = false
  cleanupTempImage() // 清理之前的暂存数据
  
  // 重置表单数据
  Object.assign(formData, {
    appType: '访问地址', // 默认选择"访问地址"
    imageCode: generateGUID32(), // 预生成GUID32位
    imageName: '',
    imagePath: '',
    aspectRatio: 0,
    width: null,
    height: null,
    seqNo: 0
  })
  
  console.log('新增图片，初始化表单数据:', { ...formData })
  dialogVisible.value = true
}

// 编辑图片
const handleEdit = (row: Picture) => {
  isEdit.value = true
  originalImagePath.value = row.imagePath // 保存原始图片路径
  Object.assign(formData, { ...row })
  dialogVisible.value = true
}

// 搜索
const handleSearch = () => {
  pagination.currentPage = 1
  loadImageList()
}

// 重置搜索
const handleReset = () => {
  searchForm.appType = ''
  searchForm.imageName = ''
  pagination.currentPage = 1
  loadImageList()
}

// 加载图片列表
const loadImageList = async () => {
  try {
    loading.value = true
    const params = {
      page: pagination.currentPage,
      pageSize: pagination.pageSize,
      appType: searchForm.appType || undefined,
      imageName: searchForm.imageName || undefined
    }
    
    const response = await pictureApi.getPictureList(params)
    imageList.value = response.data || []
    pagination.total = response.total || 0
    
    // 添加调试信息
    console.log('图片列表数据:', imageList.value)
    if (imageList.value.length > 0) {
      console.log('第一张图片原始路径:', imageList.value[0].imagePath)
      console.log('第一张图片完整URL:', getImageUrl(imageList.value[0].imagePath))
    }
  } catch (error) {
    console.error('加载图片列表失败:', error)
    ElMessage.error('加载图片列表失败')
  } finally {
    loading.value = false
  }
}

// 删除图片
const handleDelete = async (row: Picture) => {
  try {
    await ElMessageBox.confirm(
      `确定要删除图片 "${row.imageName}" 吗？`,
      '确认删除',
      {
        confirmButtonText: '确定',
        cancelButtonText: '取消',
        type: 'warning'
      }
    )
    
    await pictureApi.deletePicture(row.id!)
    ElMessage.success('删除成功')
    loadImageList()
  } catch (error) {
    if (error !== 'cancel') {
      ElMessage.error('删除失败')
    }
  }
}

// 提交表单
const handleSubmit = async () => {
  try {
    await formRef.value.validate()
    submitLoading.value = true
    
    // 检查是否有新的裁剪图片需要上传（新增或编辑时重新选择图片）
    if (croppedImageBlob.value) {
      // 确保 imageCode 存在
      if (!formData.imageCode) {
        formData.imageCode = generateGUID32()
      }
      
      try {
        // 如果是编辑模式且有新图片，先删除旧图片
        if (isEdit.value && croppedImageBlob.value && originalImagePath.value) {
          try {
            console.log('编辑模式：准备删除旧图片:', originalImagePath.value)
            // 使用原始图片路径删除旧图片
            await pictureApi.deleteImageFile(originalImagePath.value)
            console.log('旧图片删除成功')
          } catch (deleteError) {
            console.warn('删除旧图片失败，但继续上传新图片:', deleteError)
            // 删除失败不阻止后续操作，只记录警告
          }
        }
        
        // 1. 上传新图片到后台固定文件夹
        const aspectRatioString = `${formData.width}:${formData.height}`
        const file = new File([croppedImageBlob.value], selectedFile.value!.name, { 
          type: selectedFile.value!.type 
        })
        
        console.log('开始上传图片:', {
          name: file.name,
          size: file.size,
          type: file.type,
          aspectRatioString,
          isEdit: isEdit.value
        })
        
        // 上传图片（后端会自动存储到固定文件夹）
        const uploadResult = await pictureApi.uploadImage(file, aspectRatioString)
        console.log('图片上传成功:', uploadResult)
        
        // 更新表单数据中的图片路径
        formData.imagePath = uploadResult.data.url
        
        // 2. 根据是否编辑模式选择创建或更新
        if (isEdit.value) {
          // 编辑模式：更新记录
          const updateData = {
            ...formData,
            aspectRatio: formData.aspectRatio || 0,
            aspectRatioString: `${formData.width}:${formData.height}`,
            fileExtension: selectedFile.value ? selectedFile.value.name.split('.').pop() : 'jpg'
          }
          
          console.log('准备更新图片记录:', updateData)
          await pictureApi.updatePicture(formData.id!, updateData as PictureDto)
          ElMessage.success('更新成功')
        } else {
          // 新增模式：创建记录
          const createData = {
            appType: formData.appType,
            imageCode: formData.imageCode,
            imageName: formData.imageName,
            imagePath: uploadResult.data.url,
            width: formData.width,
            height: formData.height,
            aspectRatio: formData.aspectRatio,
            aspectRatioString: `${formData.width}:${formData.height}`,
            seqNo: formData.seqNo || 0,
            fileExtension: selectedFile.value ? selectedFile.value.name.split('.').pop() : 'jpg'
          }
          
          console.log('准备创建图片记录:', createData)
          await pictureApi.createPicture(createData as PictureDto)
          ElMessage.success('创建成功')
        }
        
        // 成功后关闭对话框并刷新列表
        dialogVisible.value = false
        loadImageList()
        cleanupTempImage()
        
      } catch (operationError: any) {
        console.error('操作失败:', operationError)
        
        let errorMessage = '保存失败'
        if (operationError.response) {
          const { status, data } = operationError.response
          if (status === 400) {
            if (data.errors) {
              const errorMessages = Object.values(data.errors).flat()
              errorMessage = `验证失败: ${errorMessages.join(', ')}`
            } else if (data.message || data.Message) {
              errorMessage = data.message || data.Message
            }
          }
        }
        
        ElMessage.error(errorMessage)
        throw operationError
      }
      
    } else if (isEdit.value) {
      // 编辑模式但没有新图片：只更新其他信息
      const updateData = {
        ...formData,
        aspectRatio: formData.aspectRatio || 0,
        aspectRatioString: formData.aspectRatio ? `${Math.round(formData.aspectRatio * 1000)}:1000` : '0:0'
      }
      
      await pictureApi.updatePicture(formData.id!, updateData as PictureDto)
      ElMessage.success('更新成功')
      
      dialogVisible.value = false
      loadImageList()
      
    } else {
      ElMessage.error('请先选择并裁剪图片')
      return
    }
    
  } catch (error: any) {
    console.error('提交失败:', error)
    
    if (!error.response || !error.response.data) {
      ElMessage.error(error.message || '操作失败')
    }
  } finally {
    submitLoading.value = false
  }
}



// 分页处理
const handleSizeChange = (size: number) => {
  pagination.pageSize = size
  pagination.currentPage = 1
  loadImageList()
}

const handleCurrentChange = (page: number) => {
  pagination.currentPage = page
  loadImageList()
}

// 初始化
onMounted(() => {
  loadImageList()
  loadAppTypeOptions() // 加载应用类型选项
})
</script>

<style scoped>
.image-data-management-container {
  height: 100vh;
  display: flex;
  flex-direction: column;
  padding: 20px;
  box-sizing: border-box;
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
  border-radius: 8px;
}

/* 修复卡片容器样式 */
.cards-container {
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.image-cards-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(200px, 1fr));
  grid-auto-rows: max-content;
  gap: 16px;
  padding: 16px 0;
  overflow-y: auto;
}

/* 简化的图片卡片样式 */
.image-card {
  background: white;
  border-radius: 8px;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.1);
  overflow: hidden;
  transition: all 0.3s ease;
  border: 1px solid #f0f0f0;
  height: fit-content;
  max-height: 220px;
}

.image-card:hover {
  transform: translateY(-2px);
  box-shadow: 0 4px 16px rgba(0, 0, 0, 0.15);
}

/* 简化的图片预览区域 */
.image-preview {
  position: relative;
  height: 128px;
  overflow: hidden;
}

.card-image {
  width: 100%;
  height: 100%;
  object-fit: cover;
}

.image-overlay {
  position: absolute;
  top: 8px;
  right: 8px;
  opacity: 0;
  transition: opacity 0.3s ease;
}

.image-card:hover .image-overlay {
  opacity: 1;
}

.image-size {
  background: rgba(0, 0, 0, 0.7);
  color: white;
  padding: 2px 6px;
  border-radius: 4px;
  font-size: 11px;
  font-weight: 500;
}

/* 简化的卡片内容 */
.card-content {
  padding: 8px;
}

.card-title {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 8px;
}

.image-name {
  font-size: 14px;
  font-weight: 600;
  color: #303133;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  flex: 1;
  margin-right: 8px;
}

.image-id {
  color: #909399;
  font-size: 11px;
  background: #f5f7fa;
  padding: 1px 4px;
  border-radius: 3px;
}

/* 新增底部布局样式 */
.card-bottom {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-top: 8px;
}

.seq-no {
  color: #606266;
  font-size: 11px;
}

/* 调整操作按钮样式 */
.card-actions {
  display: flex;
  gap: 6px;
}

.card-actions .el-button {
  padding: 4px 8px;
  font-size: 12px;
}

/* 空状态 */
.empty-state {
  display: flex;
  justify-content: center;
  align-items: center;
  height: 300px;
}

/* 分页容器 */
.pagination-container {
  display: flex;
  justify-content: flex-end;
  padding: 16px 0;
  border-top: 1px solid #f0f0f0;
  margin-top: 16px;
}

.dialog-footer {
  text-align: right;
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

/* 上传容器样式 */
.upload-container {
  border: 1px solid #dcdfe6;
  border-radius: 6px;
  padding: 20px;
  background-color: #fafafa;
}

.upload-area {
  width: 100%;
}

.upload-placeholder {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  height: 200px;
  border: 2px dashed #d9d9d9;
  border-radius: 6px;
  cursor: pointer;
  transition: border-color 0.3s;
}

.upload-placeholder:hover {
  border-color: #409eff;
}

.upload-icon {
  font-size: 48px;
  color: #c0c4cc;
  margin-bottom: 16px;
}

.upload-text {
  color: #606266;
  font-size: 16px;
  margin-bottom: 8px;
}

.upload-tip {
  color: #909399;
  font-size: 12px;
}

.uploaded-preview {
  display: flex;
  flex-direction: column;
  align-items: center;
}

.uploaded-image {
  max-width: 200px;
  max-height: 150px;
  border-radius: 6px;
  margin-bottom: 10px;
}

.image-actions {
  display: flex;
  gap: 10px;
}

.image-error {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 100%;
  height: 100%;
  background-color: #f5f7fa;
  color: #909399;
}

/* 裁剪对话框样式 */
.crop-container {
  padding: 20px;
}

.size-selector {
  margin-bottom: 20px;
  padding: 15px;
  background-color: #f8f9fa;
  border-radius: 6px;
}

.selector-label {
  display: inline-block;
  margin-right: 15px;
  font-weight: 500;
  color: #303133;
}

.crop-content {
  display: flex;
  gap: 30px;
}

.crop-area {
  flex: 1;
}

.crop-canvas-container {
  border: 1px solid #dcdfe6;
  border-radius: 6px;
  overflow: hidden;
  margin-bottom: 15px;
}

.crop-canvas {
  display: block;
  cursor: default;
}

.zoom-controls {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 10px;
}

.preview-area {
  width: 250px;
  flex-shrink: 0;
}

.preview-title {
  font-size: 16px;
  font-weight: 500;
  color: #303133;
  margin-bottom: 15px;
  text-align: center;
}

.preview-container {
  display: flex;
  justify-content: center;
  margin-bottom: 15px;
  padding: 20px;
  background-color: #f8f9fa;
  border-radius: 6px;
}

.preview-canvas {
  border: 1px solid #dcdfe6;
  border-radius: 4px;
}

.preview-info {
  text-align: center;
  color: #606266;
  font-size: 14px;
}

.preview-info p {
  margin: 5px 0;
}

/* 响应式设计 */
@media (max-width: 768px) {
  .image-cards-grid {
    grid-template-columns: repeat(auto-fill, minmax(160px, 1fr));
    gap: 12px;
  }
  
  .search-section .el-row {
    flex-direction: column;
  }
  
  .search-section .el-col {
    margin-bottom: 10px;
  }
}

@media (max-width: 480px) {
  .image-cards-grid {
    grid-template-columns: repeat(auto-fill, minmax(140px, 1fr));
    gap: 10px;
  }
  
  .image-preview {
    height: 128px;
  }
  
  .card-content {
    padding: 8px;
  }
}
</style>