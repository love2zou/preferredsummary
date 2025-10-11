<template>
  <div class="access-management-container">
    <el-card>
      <template #header>
        <div class="card-header">
          <h2>访问地址管理</h2>
          <p>管理系统中的所有访问地址</p>
        </div>
      </template>
      
      <!-- 搜索区域 -->
      <div class="search-section">
        <el-row :gutter="20">
          <el-col :span="4">
            <el-input
              v-model="searchForm.name"
              placeholder="搜索名称"
              prefix-icon="Search"
              clearable
            />
          </el-col>
          <el-col :span="4">
            <el-select 
              v-model="searchForm.categoryCodes" 
              placeholder="选择分类菜单" 
              multiple
              clearable 
              style="width: 100%"
            >
              <el-option
                v-for="category in categoryList"
                :key="category.categoryCode"
                :label="category.categoryName"
                :value="category.categoryCode"
              />
            </el-select>
          </el-col>
          <el-col :span="4">
            <el-select 
              v-model="searchForm.tagCodeTypes" 
              placeholder="选择标签类型" 
              multiple 
              clearable 
              style="width: 100%"
            >
              <el-option
                v-for="tag in tagList"
                :key="tag.tagCode"
                :label="tag.tagName"
                :value="tag.tagCode"
              />
            </el-select>
          </el-col>
          <el-col :span="3">
            <el-select 
              v-model="searchForm.isMark" 
              placeholder="推荐状态" 
              clearable 
              style="width: 100%"
            >
              <el-option label="推荐" :value="1" />
              <el-option label="普通" :value="0" />
            </el-select>
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
          <el-col :span="4" style="text-align: right">
            <el-button type="success" @click="handleAdd">
              <el-icon><Plus /></el-icon>
              新增地址
            </el-button>
          </el-col>
        </el-row>
      </div>
      
      <!-- 表格容器 -->
      <div class="table-container">
        <!-- 访问地址列表表格 -->
        <el-table
          :data="networkUrlList"
          v-loading="loading"
          :height="tableHeight"
          stripe
          border
          style="width: 100%"
        >
          <el-table-column prop="id" label="ID" width="40" />
          
          <!-- 合并后的基本信息列 -->
          <el-table-column label="基本信息" min-width="300">
            <template #default="scope">
              <div class="info-container">
                <div class="icon-section">
                  <!-- 只使用关联图片，删除iconUrl逻辑 -->
                  <el-image
                    v-if="scope.row.selectedPicture"
                    :src="getServerUrl(scope.row.selectedPicture.imagePath)"
                    style="width: 40px; height: 40px" 
                    fit="cover"
                  />
                  <div v-else class="no-icon">
                    <el-icon :size="32" color="#ddd">
                      <Picture />
                    </el-icon>
                  </div>
                </div>
                <div class="text-section">
                  <!-- 第一行：可点击的名称 -->
                  <div class="name-line">
                    <el-link 
                      :href="scope.row.url" 
                      target="_blank" 
                      type="primary"
                      :underline="false"
                      class="name-link"
                    >
                      {{ scope.row.name }}
                    </el-link>
                  </div>
                  <!-- 第二、三行：描述 -->
                  <div class="description-lines">
                    {{ scope.row.description || '暂无描述' }}
                  </div>
                </div>
              </div>
            </template>
          </el-table-column>
          
          <!-- 屏蔽以下列 -->
          <!-- <el-table-column prop="url" label="访问地址" min-width="200"> -->
          <!-- <el-table-column prop="imageCode" label="图片代码" width="100" /> -->
          <!-- <el-table-column label="关联图片" width="100"> -->
          
          <!-- 优化后的标签类型列 - 使用标签管理的颜色配置 -->
          <el-table-column prop="tagCodeType" label="标签类型" width="140">
            <template #default="scope">
              <div v-if="scope.row.tagCodeType" class="tag-container">
                <span 
                  v-for="(tagCode, index) in scope.row.tagCodeType.split(',')"
                  :key="index"
                  class="custom-tag"
                  :style="{
                    color: getTagColor(tagCode.trim()),
                    backgroundColor: getTagBgColor(tagCode.trim()),
                    border: `1px solid ${getTagColor(tagCode.trim())}`,
                    padding: '2px 8px',
                    borderRadius: '4px',
                    fontSize: '12px',
                    marginRight: '4px',
                    display: 'inline-block',
                    marginBottom: '2px'
                  }"
                >
                  {{ getTagName(tagCode.trim()) }}
                </span>
              </div>
              <span v-else class="no-tag">未分类</span>
            </template>
          </el-table-column>
          
          <!-- 新增分类菜单列 -->
          <el-table-column prop="categoryCode" label="分类菜单" width="140">
            <template #default="scope">
              <div v-if="scope.row.categoryCode" class="category-container">
                <span 
                  v-for="(categoryCode, index) in scope.row.categoryCode.split(',')"
                  :key="index"
                  class="category-tag"
                >
                  {{ getCategoryName(categoryCode.trim()) }}
                </span>
              </div>
              <span v-else class="no-category">未分类</span>
            </template>
          </el-table-column>
          
          <el-table-column prop="isMark" label="是否推荐" width="100">
            <template #default="scope">
              <el-tag :type="scope.row.isMark === 1 ? 'success' : 'info'">
                {{ scope.row.isMark === 1 ? '推荐' : '普通' }}
              </el-tag>
            </template>
          </el-table-column>
          
          <el-table-column prop="isAvailable" label="是否可用" width="100">
            <template #default="scope">
              <el-tag :type="scope.row.isAvailable === 1 ? 'success' : 'danger'">
                {{ scope.row.isAvailable === 1 ? '可用' : '不可用' }}
              </el-tag>
            </template>
          </el-table-column>
          
          <el-table-column prop="seqNo" label="排序号" width="80" />
          
          <!-- 隐藏创建时间列 -->
          <!-- <el-table-column prop="crtTime" label="创建时间" width="160">
            <template #default="scope">
              {{ formatDate(scope.row.crtTime) }}
            </template>
          </el-table-column> -->
          
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
        
        <!-- 分页组件 -->
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

    <!-- 新增/编辑对话框 -->
    <el-dialog
      v-model="dialogVisible"
      :title="isEdit ? '编辑访问地址' : '新增访问地址'"
      width="700px"
      :close-on-click-modal="false"
    >
      <el-form
        ref="networkUrlFormRef"
        :model="networkUrlForm"
        :rules="networkUrlRules"
        label-width="100px"
        size="default"
      >
        <el-form-item label="名称" prop="name">
          <el-input
            v-model="networkUrlForm.name"
            placeholder="请输入名称"
          />
        </el-form-item>
        
        <el-form-item label="访问地址" prop="url">
          <el-input
            v-model="networkUrlForm.url"
            placeholder="请输入访问地址（如：https://example.com）"
          />
        </el-form-item>
        
        <el-form-item label="描述" prop="description">
          <el-input
            v-model="networkUrlForm.description"
            type="textarea"
            :rows="3"
            placeholder="请输入描述"
          />
        </el-form-item>
        
        <!-- 图片选择区域 - 优化后的紧凑布局 -->
        <el-form-item label="关联图片">
          <div class="picture-selector-compact">
            <!-- 已选择的图片预览和操作按钮在同一行 -->
            <div v-if="selectedPicture" class="selected-picture-row">
              <el-image
                :src="getServerUrl(selectedPicture.imagePath)"
                :alt="selectedPicture.imageName"
                style="width: 50px; height: 50px; border-radius: 4px; margin-right: 12px;"
                fit="cover"
              />
              <div class="picture-info-inline">
                <span class="picture-name">{{ selectedPicture.imageName }}</span>
              </div>
              <div class="picture-actions">
                <el-button 
                  type="default" 
                  size="small" 
                  @click="openPictureSelector"
                >
                  重新选择
                </el-button>
                <el-button 
                  type="danger" 
                  size="small" 
                  @click="clearPictureSelection"
                  :icon="Close"
                >
                  清除
                </el-button>
              </div>
            </div>
            
            <!-- 选择按钮 -->
            <el-button 
              v-if="!selectedPicture"
              type="primary" 
              @click="openPictureSelector"
              :icon="Picture"
            >
              选择图片
            </el-button>
          </div>
        </el-form-item>
        
        <!-- 隐藏图片代码字段 -->
        <el-form-item label="图片代码" prop="imageCode" style="display: none;">
          <el-input
            v-model="networkUrlForm.imageCode"
            placeholder="请选择图片或手动输入图片代码"
            :readonly="!!selectedPicture"
          />
        </el-form-item>
        
        <!-- 最后4个字段：一行2个布局 -->
        <el-row :gutter="20">
          <el-col :span="12">
            <el-form-item label="标签类型" prop="tagCodeTypes">
              <el-select
                v-model="networkUrlForm.tagCodeTypes"
                multiple
                filterable
                placeholder="请选择标签类型"
                style="width: 100%"
                :loading="tagLoading"
              >
                <el-option
                  v-for="tag in tagList"
                  :key="tag.id"
                  :label="tag.tagName"
                  :value="tag.tagCode"
                >
                  <span style="float: left">{{ tag.tagName }}</span>
                  <span style="float: right; color: #8492a6; font-size: 13px">{{ tag.tagCode }}</span>
                </el-option>
              </el-select>
              <div class="form-item-tip">
                可选择多个标签类型，支持搜索过滤
              </div>
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="分类菜单" prop="categoryCodes">
              <el-select
                v-model="networkUrlForm.categoryCodes"
                multiple
                filterable
                placeholder="请选择分类菜单"
                clearable
                style="width: 100%"
                :loading="categoryLoading"
              >
                <el-option
                  v-for="category in categoryList"
                  :key="category.id"
                  :label="category.categoryName"
                  :value="category.categoryCode"
                >
                  <span style="float: left">{{ category.categoryName }}</span>
                  <span style="float: right; color: #8492a6; font-size: 13px">{{ category.categoryCode }}</span>
                </el-option>
              </el-select>
              <div class="form-item-tip">
                可选择多个分类菜单，支持搜索过滤
              </div>
            </el-form-item>
          </el-col>
        </el-row>
        
        <el-row :gutter="20">
          <el-col :span="12">
            <el-form-item label="是否推荐" prop="isMark">
              <el-radio-group v-model="networkUrlForm.isMark">
                <el-radio :label="1">推荐</el-radio>
                <el-radio :label="0">普通</el-radio>
              </el-radio-group>
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="是否可用" prop="isAvailable">
              <el-radio-group v-model="networkUrlForm.isAvailable">
                <el-radio :label="1">可用</el-radio>
                <el-radio :label="0">不可用</el-radio>
              </el-radio-group>
            </el-form-item>
          </el-col>
        </el-row>
        
        <el-form-item label="排序号" prop="seqNo">
          <el-input-number
            v-model="networkUrlForm.seqNo"
            :min="0"
            :max="9999"
            placeholder="请输入排序号"
          />
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

    <!-- 图片选择对话框 -->
    <el-dialog
      v-model="pictureDialogVisible"
      title="选择图片"
      width="80%"
      :close-on-click-modal="false"
      class="picture-selector-dialog"
    >
      <!-- 搜索区域 -->
      <div class="search-section">
        <el-row :gutter="20">
          <el-col :span="8">
            <el-input
              v-model="pictureSearchForm.imageName"
              placeholder="搜索图片名称"
              prefix-icon="Search"
              clearable
            />
          </el-col>
          <el-col :span="8">
            <el-select
              v-model="pictureSearchForm.appType"
              placeholder="选择应用类型"
              clearable
              style="width: 100%"
            >
              <el-option label="访问地址-标签类型" value="访问地址-标签类型" />
            </el-select>
          </el-col>
          <el-col :span="8">
            <el-button type="primary" @click="handlePictureSearch">
              <el-icon><Search /></el-icon>
              搜索
            </el-button>
            <el-button @click="handlePictureReset">
              <el-icon><Refresh /></el-icon>
              重置
            </el-button>
          </el-col>
        </el-row>
      </div>

      <!-- 图片列表 -->
      <div class="picture-grid" v-loading="pictureLoading">
        <div 
          v-for="picture in pictureList" 
          :key="picture.id"
          class="picture-item"
          :class="{ 'selected': selectedPictureId === picture.id }"
          @click="selectPicture(picture)"
        >
          <el-image
            :src="getServerUrl(picture.imagePath)"
            :alt="picture.imageName"
            style="width: 100%; height: 100px; object-fit: cover;"
            fit="cover"
          >
            <template #error>
              <div class="image-error">
                <el-icon><Picture /></el-icon>
                <span>加载失败</span>
              </div>
            </template>
          </el-image>
          <div class="picture-item-info">
            <div class="picture-name" :title="picture.imageName">{{ picture.imageName }}</div>
          </div>
          <div class="selected-overlay" v-if="selectedPictureId === picture.id">
            <el-icon class="check-icon"><Check /></el-icon>
          </div>
        </div>
      </div>

      <!-- 分页 -->
      <el-pagination
        v-model:current-page="picturePagination.currentPage"
        v-model:page-size="picturePagination.pageSize"
        :page-sizes="[12, 24, 48]"
        :total="picturePagination.total"
        layout="total, sizes, prev, pager, next"
        @size-change="handlePictureSizeChange"
        @current-change="handlePictureCurrentChange"
        style="margin-top: 20px; justify-content: center;"
      />

      <template #footer>
        <span class="dialog-footer">
          <el-button @click="pictureDialogVisible = false">取消</el-button>
          <el-button 
            type="primary" 
            @click="confirmPictureSelection"
            :disabled="!selectedPictureId"
          >
            确定选择
          </el-button>
        </span>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { categoryApi, type Category } from '@/api/category'
import {
  networkUrlApi,
  type NetworkUrlDto,
  type NetworkUrlListDto
} from '@/api/networkUrl'
import { pictureApi, type PictureSearchParams, type Picture as PictureType } from '@/api/picture'
import { tagApi, type Tag } from '@/api/tag'
import { getServerUrl } from '@/utils/url'
import { Check, Close, Picture, Plus, Refresh, Search } from '@element-plus/icons-vue'
import { ElForm, ElMessage, ElMessageBox, type FormRules } from 'element-plus'
import { nextTick, onMounted, onUnmounted, reactive, ref } from 'vue'

// 响应式数据
const loading = ref(false)
const submitLoading = ref(false)
const dialogVisible = ref(false)
const isEdit = ref(false)
const networkUrlFormRef = ref<InstanceType<typeof ElForm>>()
const searchFormRef = ref<InstanceType<typeof ElForm>>()
const tableHeight = ref(400)
const currentNetworkUrlId = ref<number | null>(null)

// 图片选择相关
const pictureDialogVisible = ref(false)
const pictureLoading = ref(false)
const selectedPicture = ref<PictureType | null>(null)
const selectedPictureId = ref<number | null>(null)
const pictureList = ref<PictureType[]>([])

// 标签相关数据
const tagList = ref<Tag[]>([])
const tagLoading = ref(false)

// 分类菜单相关数据
const categoryList = ref<Category[]>([])
const categoryLoading = ref(false)

// 搜索表单
const searchForm = ref({
  name: '',
  tagCodeTypes: [] as string[], // 多选标签类型
  categoryCodes: [] as string[], // 改为多选分类菜单
  isMark: undefined as number | undefined
})

// 图片搜索表单
const pictureSearchForm = reactive<PictureSearchParams>({
  imageName: '',
  appType: '访问地址-标签类型'
})

// 访问地址列表（扩展类型以包含图片信息）
interface ExtendedNetworkUrlListDto {
  id: number
  url: string
  name: string
  description?: string
  imageCode: string
  categoryCode?: string // 新增分类菜单字段
  isAvailable?: number
  isMark: number
  tagCodeType: string
  seqNo: number
  crtTime: string
  updTime: string
  selectedPicture?: PictureType | null
}
const networkUrlList = ref<ExtendedNetworkUrlListDto[]>([])

// 分页
const pagination = reactive({
  currentPage: 1,
  pageSize: 10,
  total: 0
})

// 图片分页
const picturePagination = reactive({
  currentPage: 1,
  pageSize: 12,
  total: 0
})

// 访问地址表单
const networkUrlForm = reactive<NetworkUrlDto & { tagCodeTypes: string[]; categoryCodes: string[] }>({
  name: '',
  url: '',
  description: '',
  imageCode: '',
  isAvailable: 1,
  isMark: 0,
  tagCodeType: '', // 保留原字段用于后端兼容
  tagCodeTypes: [], // 新增多选字段
  categoryCode: '', // 保留原字段用于后端兼容
  categoryCodes: [], // 新增多选分类菜单字段
  seqNo: 0
})

// 表单验证规则
const networkUrlRules: FormRules = {
  name: [
    { required: true, message: '请输入名称', trigger: 'blur' },
    { min: 1, max: 100, message: '名称长度在 1 到 100 个字符', trigger: 'blur' }
  ],
  url: [
    { required: true, message: '请输入访问地址', trigger: 'blur' },
    { type: 'url', message: '请输入正确的URL格式', trigger: 'blur' }
  ],
  tagCodeTypes: [
    { required: true, message: '请选择标签类型', trigger: 'change' }
  ],
  categoryCodes: [
    { required: true, message: '请选择分类菜单', trigger: 'change' }
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

// 根据imageCode获取图片信息
const getPictureByCode = async (imageCode: string): Promise<PictureType | null> => {
  if (!imageCode) return null
  
  try {
    const response = await pictureApi.getPictureList({
      page: 1,
      pageSize: 1,
      imageName: '', // 使用imageName而不是imageCode
      appType: '访问地址-标签类型'
    })
    
    // 在结果中查找匹配的imageCode
    const picture = response.data.find(p => p.imageCode === imageCode)
    return picture || null
  } catch (error) {
    console.error('获取图片信息失败:', error)
    return null
  }
}

// 根据标签代码获取标签颜色
const getTagColor = (tagCode: string) => {
  if (!tagCode || !tagList.value.length) return '#409eff'
  
  const tag = tagList.value.find(t => t.tagCode === tagCode)
  return tag?.hexColor || '#409eff'
}

// 根据标签代码获取标签背景色
const getTagBgColor = (tagCode: string) => {
  if (!tagCode || !tagList.value.length) return 'rgba(64, 158, 255, 0.1)'
  
  const tag = tagList.value.find(t => t.tagCode === tagCode)
  return tag?.rgbColor || 'rgba(64, 158, 255, 0.1)'
}

// 根据标签代码获取标签名称
const getTagName = (tagCode: string) => {
  if (!tagCode || !tagList.value.length) return tagCode
  
  const tag = tagList.value.find(t => t.tagCode === tagCode)
  return tag?.tagName || tagCode // 如果找不到对应的标签名称，则显示标签代码
}

// 根据分类代码获取分类名称
const getCategoryName = (categoryCode: string) => {
  console.log('查找分类代码:', categoryCode)
  console.log('当前分类列表:', categoryList.value)
  
  if (!categoryCode || !categoryList.value.length) {
    console.log('分类代码为空或分类列表为空')
    return categoryCode
  }
  
  const category = categoryList.value.find(c => c.categoryCode === categoryCode)
  console.log('找到的分类:', category)
  
  return category?.categoryName || categoryCode // 如果找不到对应的分类名称，则显示分类代码
}

// 获取分类列表
const getCategoryList = async () => {
  try {
    categoryLoading.value = true
    console.log('开始获取分类列表')
    
    const response = await categoryApi.getCategoryList({
      page: 1,
      pageSize: 1000 // 获取所有分类
    })
    
    categoryList.value = response.data
    console.log('分类列表获取成功:', categoryList.value)
  } catch (error) {
    console.error('获取分类列表失败:', error)
    ElMessage.error('获取分类列表失败')
    categoryList.value = []
  } finally {
    categoryLoading.value = false
  }
}

// 获取标签列表
const getTagList = async () => {
  try {
    tagLoading.value = true
    console.log('开始获取标签列表')
    
    // 获取访问地址相关的标签
    const response = await tagApi.getTagsByModule('访问地址-标签类型')
    console.log('原始标签响应:', response)
    
    // 从ApiResponse中提取实际的数据数组
    const tags = response.data // 直接使用response.data，因为类型已经正确定义
    console.log('提取的标签数据:', tags)
    
    // 确保tags是数组，并过滤掉无效值
    if (Array.isArray(tags)) {
      tagList.value = tags.filter(tag => 
        tag !== null && 
        tag !== undefined && 
        typeof tag === 'object' && 
        tag.id !== null && 
        tag.id !== undefined
      )
    } else {
      console.warn('标签数据不是数组格式:', tags)
      tagList.value = []
    }
    
    console.log('过滤后的标签列表:', tagList.value)
    console.log('标签列表长度:', tagList.value.length)
  } catch (error) {
    console.error('获取标签列表失败:', error)
    ElMessage.error('获取标签列表失败')
    tagList.value = [] // 确保设置为空数组
  } finally {
    tagLoading.value = false
  }
}

// 获取访问地址列表
const getNetworkUrlList = async () => {
  try {
    loading.value = true
    const params = {
      page: pagination.currentPage,
      pageSize: pagination.pageSize,
      name: searchForm.value.name,
      categoryCode: searchForm.value.categoryCodes.join(','), // 改为多选分类搜索参数
      tagCodeType: searchForm.value.tagCodeTypes.join(','),
      isMark: searchForm.value.isMark
    }
    
    const response = await networkUrlApi.getNetworkUrlList(params)
    
    // 添加调试信息
    console.log('访问地址列表响应:', response)
    console.log('第一条数据的categoryCode:', response.data[0]?.categoryCode)
    
    // 为每个网络地址获取关联的图片信息
    const extendedList: ExtendedNetworkUrlListDto[] = await Promise.all(
      response.data.map(async (item) => {
        const picture = await getPictureByCode(item.imageCode)
        console.log(`ID ${item.id} 的categoryCode:`, item.categoryCode) // 添加调试
        return {
          ...item,
          selectedPicture: picture
        }
      })
    )
    
    networkUrlList.value = extendedList
    pagination.total = response.total
  } catch (error) {
    console.error('获取访问地址列表失败:', error)
    ElMessage.error('获取访问地址列表失败')
  } finally {
    loading.value = false
  }
}

// 获取图片列表
const getPictureList = async () => {
  try {
    pictureLoading.value = true
    const params = {
      page: picturePagination.currentPage,
      pageSize: picturePagination.pageSize,
      ...pictureSearchForm
    }
    
    const response = await pictureApi.getPictureList(params)
    pictureList.value = response.data
    picturePagination.total = response.total
  } catch (error) {
    console.error('获取图片列表失败:', error)
    ElMessage.error('获取图片列表失败')
  } finally {
    pictureLoading.value = false
  }
}

// 打开图片选择器
const openPictureSelector = () => {
  pictureDialogVisible.value = true
  selectedPictureId.value = null
  getPictureList()
}

// 选择图片
const selectPicture = (picture: PictureType) => {
  selectedPictureId.value = picture.id
}

// 确认图片选择
const confirmPictureSelection = () => {
  const selected = pictureList.value.find(p => p.id === selectedPictureId.value)
  if (selected) {
    selectedPicture.value = selected
    networkUrlForm.imageCode = selected.imageCode
    pictureDialogVisible.value = false
  }
}

// 清除图片选择
const clearPictureSelection = () => {
  selectedPicture.value = null
  selectedPictureId.value = null
  networkUrlForm.imageCode = ''
}

// 图片搜索
const handlePictureSearch = () => {
  picturePagination.currentPage = 1
  getPictureList()
}

// 重置图片搜索
const handlePictureReset = () => {
  Object.assign(pictureSearchForm, {
    imageName: '',
    appType: '访问地址-标签类型'
  })
  picturePagination.currentPage = 1
  getPictureList()
}

// 图片分页大小改变
const handlePictureSizeChange = (size: number) => {
  picturePagination.pageSize = size
  picturePagination.currentPage = 1
  getPictureList()
}

// 图片当前页改变
const handlePictureCurrentChange = (page: number) => {
  picturePagination.currentPage = page
  getPictureList()
}

// 搜索处理
const handleSearch = () => {
  pagination.currentPage = 1
  getNetworkUrlList()
}

// 重置搜索表单
const handleReset = () => {
  Object.assign(searchForm.value, {
    name: '',
    tagCodeTypes: [],
    categoryCodes: [], // 重置多选分类菜单
    isMark: undefined
  })
  getNetworkUrlList()
}

// 新增
const handleAdd = () => {
  isEdit.value = false
  currentNetworkUrlId.value = null
  selectedPicture.value = null
  selectedPictureId.value = null
  resetForm()
  getTagList()
  getCategoryList()
  dialogVisible.value = true
}

// 编辑
const handleEdit = async (row: ExtendedNetworkUrlListDto) => {
  isEdit.value = true
  currentNetworkUrlId.value = row.id
  
  // 处理标签类型多选
  const tagCodes = row.tagCodeType ? row.tagCodeType.split(',').map(code => code.trim()) : []
  
  // 处理分类菜单多选（假设后端返回逗号分隔的字符串）
  const categoryCodes = row.categoryCode ? row.categoryCode.split(',').map(code => code.trim()) : []
  
  Object.assign(networkUrlForm, {
    name: row.name,
    url: row.url,
    description: row.description || '',
    imageCode: row.imageCode,
    isAvailable: row.isAvailable ?? 1,
    isMark: row.isMark,
    tagCodeType: row.tagCodeType,
    tagCodeTypes: tagCodes,
    categoryCode: row.categoryCode || '',
    categoryCodes: categoryCodes, // 填充多选分类菜单
    seqNo: row.seqNo
  })
  
  // 设置关联图片
  if (row.selectedPicture) {
    selectedPicture.value = row.selectedPicture
    selectedPictureId.value = row.selectedPicture.id
  } else {
    selectedPicture.value = null
    selectedPictureId.value = null
  }
  
  // 获取关联图片信息
  if (row.imageCode) {
    await getPictureByCode(row.imageCode)
  }
  
  getCategoryList() // 加载分类列表
  getTagList()
  dialogVisible.value = true
}

// 删除
const handleDelete = async (row: NetworkUrlListDto) => {
  try {
    await ElMessageBox.confirm(
      `确定要删除访问地址 "${row.name}" 吗？`,
      '确认删除',
      {
        confirmButtonText: '确定',
        cancelButtonText: '取消',
        type: 'warning'
      }
    )
    
    await networkUrlApi.deleteNetworkUrl(row.id)
    ElMessage.success('删除成功')
    getNetworkUrlList()
  } catch (error: any) {
    if (error !== 'cancel') {
      console.error('删除访问地址失败:', error)
      ElMessage.error('删除访问地址失败')
    }
  }
}

// 提交表单
const handleSubmit = async () => {
  if (!networkUrlFormRef.value) return
  
  await networkUrlFormRef.value.validate(async (valid) => {
    if (valid) {
      submitLoading.value = true
      try {
        // 将多选数组转换为逗号分隔的字符串
        const submitData = {
          ...networkUrlForm,
          tagCodeType: networkUrlForm.tagCodeTypes.join(','),
          categoryCode: networkUrlForm.categoryCodes.join(',') // 处理多选分类菜单
        }
        
        if (isEdit.value && currentNetworkUrlId.value) {
          await networkUrlApi.updateNetworkUrl(currentNetworkUrlId.value, submitData)
          ElMessage.success('更新成功')
        } else {
          await networkUrlApi.createNetworkUrl(submitData)
          ElMessage.success('创建成功')
        }
        
        dialogVisible.value = false
        resetForm()
        getNetworkUrlList()
      } catch (error) {
        console.error('Submit error:', error)
        ElMessage.error(isEdit.value ? '更新失败' : '创建失败')
      } finally {
        submitLoading.value = false
      }
    }
  })
}

// 重置表单
const resetForm = () => {
  Object.assign(networkUrlForm, {
    name: '',
    url: '',
    description: '',
    imageCode: '',
    isAvailable: 1,
    isMark: 0,
    tagCodeType: '',
    tagCodeTypes: [],
    categoryCode: '',
    categoryCodes: [], // 重置多选分类菜单
    seqNo: 0
  })
  selectedPicture.value = null
  selectedPictureId.value = null
  networkUrlFormRef.value?.clearValidate()
}

// 分页大小改变
const handleSizeChange = (size: number) => {
  pagination.pageSize = size
  pagination.currentPage = 1
  getNetworkUrlList()
}

// 当前页改变
const handleCurrentChange = (page: number) => {
  pagination.currentPage = page
  getNetworkUrlList()
}

// 窗口大小改变事件
const handleResize = () => {
  calculateTableHeight()
}

// 组件挂载
onMounted(() => {
  calculateTableHeight()
  getNetworkUrlList()
  getTagList() // 获取标签列表
  getCategoryList() // 确保获取分类列表
  
  window.addEventListener('resize', calculateTableHeight)
})

// 组件卸载
onUnmounted(() => {
  window.removeEventListener('resize', handleResize)
})
</script>

<style scoped>
.access-management-container {
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

.card-header {
  margin-bottom: 0;
}

.card-header h2 {
  margin: 0 0 8px 0;
  font-size: 20px;
  font-weight: 600;
  color: #303133;
}

.card-header p {
  margin: 0;
  font-size: 14px;
  color: #909399;
}



/* 表格容器样式 - 关键修复 */
.table-container {
  flex: 1;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  margin-top: 20px;
}

/* 表格样式 */
.el-table {
  border-radius: 4px;
  overflow: hidden;
  flex: 1;
  width: 100%;
}

/* 分页组件样式 - 关键修复 */
.el-pagination {
  justify-content: flex-end;
  margin-top: 20px;
  flex-shrink: 0; /* 防止分页组件被压缩 */
  padding: 10px 0;
}

.dialog-footer {
  display: flex;
  justify-content: flex-end;
  gap: 10px;
}

.form-item-tip {
  font-size: 12px;
  color: #909399;
  margin-top: 4px;
  line-height: 1.4;
}

/* 图片选择器样式 */
.picture-selector {
  width: 100%;
}

.picture-selector-compact {
  width: 100%;
}

.selected-picture-row {
  display: flex;
  align-items: center;
  padding: 8px;
  border: 1px solid #e4e7ed;
  border-radius: 4px;
  background-color: #f9f9f9;
  margin-bottom: 10px;
}

.picture-info-inline {
  flex: 1;
  margin-right: 12px;
}

.picture-info-inline .picture-name {
  font-size: 14px;
  color: #303133;
  font-weight: 500;
}

.picture-actions {
  display: flex;
  gap: 8px;
}

.picture-actions .el-button {
  margin-left: 0;
}

.selected-picture {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 8px;
  border: 1px solid #e4e7ed;
  border-radius: 4px;
  background-color: #f8f9fa;
  margin-bottom: 10px;
}

.picture-info {
  flex: 1;
}

.picture-name {
  font-size: 14px;
  font-weight: 500;
  color: #303133;
}

.picture-code {
  font-size: 12px;
  color: #909399;
}

.picture-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(160px, 1fr));
  gap: 16px;
  min-height: 300px;
  max-height: 500px;
  overflow-y: auto;
}

.picture-item {
  position: relative;
  border: 2px solid #e4e7ed;
  border-radius: 8px;
  overflow: hidden;
  cursor: pointer;
  transition: all 0.3s ease;
  height: fit-content;
  max-height: 220px;
}

.picture-item:hover {
  border-color: #409eff;
  box-shadow: 0 2px 8px rgba(64, 158, 255, 0.2);
}

.picture-item.selected {
  border-color: #409eff;
  box-shadow: 0 2px 8px rgba(64, 158, 255, 0.3);
}

.picture-item-info {
  padding: 8px;
  background-color: #fff;
}

.picture-item-info .picture-name {
  font-size: 12px;
  font-weight: 500;
  color: #303133;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.picture-item-info .picture-code {
  font-size: 11px;
  color: #909399;
  margin-top: 2px;
}

.selected-overlay {
  position: absolute;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background-color: rgba(64, 158, 255, 0.1);
  display: flex;
  align-items: center;
  justify-content: center;
}

.check-icon {
  font-size: 24px;
  color: #409eff;
  background-color: #fff;
  border-radius: 50%;
  padding: 4px;
}

.image-error {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  height: 100%;
  color: #c0c4cc;
  font-size: 12px;
}

:deep(.el-table) {
  font-size: 14px;
}

:deep(.el-table th) {
  background-color: #fafafa;
  font-weight: 600;
}

:deep(.el-button + .el-button) {
  margin-left: 8px;
}

:deep(.picture-selector-dialog .el-dialog__body) {
  padding: 20px;
}

/* 合并信息列样式 - 优化对齐 */
.info-container {
  display: flex;
  align-items: center; /* 改为center实现垂直居中对齐 */
  gap: 8px;
  padding: 4px 0;
}

.icon-section {
  flex-shrink: 0;
  width: 40px;
  height: 40px;
  display: flex; /* 添加flex布局 */
  align-items: center; /* 图片容器内部也居中 */
  justify-content: center; /* 图片容器内部也居中 */
}

.no-icon {
  width: 40px;
  height: 40px;
  display: flex;
  align-items: center;
  justify-content: center;
  background-color: #f5f7fa;
  border-radius: 4px;
}

.text-section {
  flex: 1;
  min-width: 0; /* 允许文本收缩 */
  display: flex; /* 添加flex布局 */
  flex-direction: column; /* 垂直排列名称和描述 */
  justify-content: center; /* 文字部分垂直居中 */
}

.name-line {
  margin-bottom: 2px; /* 从4px减少到2px */
}

.name-link {
  font-weight: 600;
  font-size: 14px; /* 保持14px */
  line-height: 20px; /* 相应调整行高 */
  cursor: pointer;
  transition: color 0.3s;
}

.name-link:hover {
  color: #409eff;
  text-decoration: underline;
}

.description-lines {
  font-size: 14px;
  color: #606266;
  line-height: 20px;
  /* 限制显示2行描述 */
  display: -webkit-box;
  /* stylelint-disable-next-line property-no-vendor-prefix */
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
  word-break: break-word;
}

/* 标签样式优化 */
.tag-style {
  border-radius: 12px;
  padding: 4px 8px;
  font-weight: 500;
  font-size: 12px;
  border: 1px solid transparent;
}

.tag-style.el-tag--primary {
  background-color: #ecf5ff;
  border-color: #b3d8ff;
  color: #409eff;
}

.tag-style.el-tag--success {
  background-color: #f0f9ff;
  border-color: #95de64;
  color: #52c41a;
}

.tag-style.el-tag--warning {
  background-color: #fdf6ec;
  border-color: #f7ba2a;
  color: #e6a23c;
}

.tag-style.el-tag--danger {
  background-color: #fef0f0;
  border-color: #fbc4c4;
  color: #f56c6c;
}

.tag-style.el-tag--info {
  background-color: #f4f4f5;
  border-color: #d3d4d6;
  color: #909399;
}

.tag-style:hover {
  transform: translateY(-1px);
  box-shadow: 0 2px 4px rgba(0, 0, 0, 0.1);
}

/* 标签容器样式 */
.tag-container {
  display: flex;
  flex-wrap: wrap;
  gap: 4px;
}

.custom-tag {
  white-space: nowrap;
  font-weight: 500;
  transition: all 0.2s;
}

.custom-tag:hover {
  opacity: 0.8;
  transform: scale(1.05);
}

.no-tag {
  color: #999;
  font-style: italic;
}

/* 分类标签样式 */
.category-container {
  display: flex;
  flex-wrap: wrap;
  gap: 4px;
}

.category-tag {
  background-color: #f0f9ff;
  color: #0369a1;
  border: 1px solid #0ea5e9;
  padding: 2px 8px;
  border-radius: 4px;
  font-size: 12px;
  display: inline-block;
  margin-bottom: 2px;
}

.no-category {
  color: #999;
  font-style: italic;
}

/* 搜索区域样式 - 参考用户管理 */
.search-section {
  padding: 20px;
  background-color: #f8f9fa;
  border-radius: 4px;
}

/* 移除原有的search-form相关样式，替换为以下样式 */
.search-section .el-input,
.search-section .el-select {
  width: 100%;
}

.search-section .el-button {
  margin-right: 8px;
}

.search-section .el-button:last-child {
  margin-right: 0;
}

/* 响应式优化 */
@media (max-width: 1200px) {
  .search-section .el-row {
    flex-wrap: wrap;
  }
  
  .search-section .el-col {
    margin-bottom: 10px;
  }
}

@media (max-width: 768px) {
  .search-section .el-col {
    flex: 0 0 100%;
    max-width: 100%;
  }
  
  .search-section .el-col[style*="text-align: right"] {
    text-align: left !important;
  }
}

/* 表单项提示样式 */
.form-item-tip {
  font-size: 12px;
  color: #909399;
  margin-top: 4px;
  line-height: 1.4;
}
</style>