<template>
  <div class="category-management-container">
    <el-card>
      <template #header>
        <div class="card-header">
          <h2>分类管理</h2>
          <p>管理系统中的所有分类</p>
        </div>
      </template>
      
      <!-- 搜索和操作区域 -->
      <div class="search-section">
        <el-row :gutter="20">
          <el-col :span="5">
            <el-input
              v-model="searchForm.categoryCode"
              placeholder="搜索分类代码"
              prefix-icon="Search"
              clearable
            />
          </el-col>
          <el-col :span="5">
            <el-input
              v-model="searchForm.categoryName"
              placeholder="搜索分类名称"
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
              :disabled="!selectedCategories.length"
            >
              <el-icon><Delete /></el-icon>
              批量删除 ({{ selectedCategories.length }})
            </el-button>
          </el-col>
        </el-row>
      </div>
    
      <!-- 表格容器 -->
      <div class="table-container">
        <!-- 分类列表表格 -->
        <el-table
          ref="tableRef"
          :data="categoryList"
          v-loading="loading"
          :height="tableHeight"
          stripe
          border
          style="width: 100%"
          @selection-change="handleSelectionChange"
        >
          <el-table-column type="selection" width="40" />
          <el-table-column prop="id" label="ID" width="50" />
          <el-table-column prop="categoryCode" label="分类代码" min-width="120" />
          <el-table-column prop="categoryName" label="分类名称" min-width="120" />
          <el-table-column label="分类图标" width="100">
            <template #default="scope">
              <!-- 优化图标显示逻辑 -->
              <div class="icon-display" v-if="scope.row.categoryIcon">
                <!-- Element Plus 图标 -->
                <component 
                  v-if="getIconComponent(scope.row.categoryIcon)"
                  :is="getIconComponent(scope.row.categoryIcon)"
                  style="font-size: 20px; color: #409eff;"
                />
                <!-- FontAwesome 备选图标 -->
                <i 
                  v-else
                  :class="getIconClass(scope.row.categoryIcon)"
                  style="font-size: 20px; color: #409eff;"
                ></i>
              </div>
              <span v-else class="no-icon">-</span>
            </template>
          </el-table-column>
          <el-table-column prop="description" label="描述" min-width="200" />
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

    <!-- 新增/编辑对话框 -->
    <el-dialog
      v-model="dialogVisible"
      :title="isEdit ? '编辑分类' : '新增分类'"
      width="600px"
      :close-on-click-modal="false"
    >
      <el-form
        ref="categoryFormRef"
        :model="categoryForm"
        :rules="categoryRules"
        label-width="100px"
        size="default"
      >
        <el-form-item label="分类代码" prop="categoryCode">
          <el-input
            v-model="categoryForm.categoryCode"
            placeholder="请输入分类代码"
            :disabled="isEdit"
          />
        </el-form-item>
        
        <el-form-item label="分类名称" prop="categoryName">
          <el-input
            v-model="categoryForm.categoryName"
            placeholder="请输入分类名称"
          />
        </el-form-item>
        
        <!-- 优化图标选择器 -->
        <el-form-item label="分类图标" prop="categoryIcon">
          <div class="icon-selector">
            <el-input
              v-model="categoryForm.categoryIcon"
              placeholder="请选择分类图标"
              readonly
              @click="openIconDialog"
            >
              <template #prefix>
                <div class="icon-preview" v-if="categoryForm.categoryIcon">
                  <!-- Element Plus 图标预览 -->
                  <component 
                    v-if="getIconComponent(categoryForm.categoryIcon)"
                    :is="getIconComponent(categoryForm.categoryIcon)"
                    style="width: 16px; height: 16px;"
                  />
                  <!-- FontAwesome 备选图标预览 -->
                  <i 
                    v-else
                    :class="getIconClass(categoryForm.categoryIcon)"
                    style="width: 16px;"
                  ></i>
                </div>
              </template>
              <template #suffix>
                <el-icon class="cursor-pointer">
                  <ArrowDown />
                </el-icon>
              </template>
            </el-input>
          </div>
        </el-form-item>
        
        <el-form-item label="描述" prop="description">
          <el-input
            v-model="categoryForm.description"
            type="textarea"
            :rows="3"
            placeholder="请输入描述"
            maxlength="500"
            show-word-limit
          />
        </el-form-item>
        
        <el-form-item label="排序号" prop="seqNo">
          <el-input-number
            v-model="categoryForm.seqNo"
            :min="0"
            :max="9999"
            placeholder="排序号"
            style="width: 100%"
          />
        </el-form-item>
      </el-form>
      
      <template #footer>
        <span class="dialog-footer">
          <el-button @click="dialogVisible = false">取消</el-button>
          <el-button type="primary" @click="handleSubmit" :loading="submitting">
            {{ isEdit ? '更新' : '创建' }}
          </el-button>
        </span>
      </template>
    </el-dialog>

    <!-- 优化图标选择器弹窗 -->
    <el-dialog
      v-model="iconDialogVisible"
      title="选择图标"
      width="900px"
      :close-on-click-modal="false"
      @close="handleIconDialogClose"
    >
      <div class="icon-selector-content">
        <!-- 搜索和筛选区域 -->
        <div class="icon-search-container">
          <el-row :gutter="16">
            <el-col :span="12">
              <el-input
                v-model="iconSearchKeyword"
                placeholder="搜索图标名称或关键词..."
                prefix-icon="Search"
                clearable
                @input="debouncedSearch"
              />
            </el-col>
            <el-col :span="8">
              <el-select
                v-model="selectedCategory"
                placeholder="选择分类"
                clearable
                @change="loadIconsByCategory"
                style="width: 100%"
              >
                <el-option label="全部分类" value="" />
                <el-option
                  v-for="category in iconCategories"
                  :key="category"
                  :label="category"
                  :value="category"
                />
              </el-select>
            </el-col>
            <el-col :span="4">
              <el-button @click="loadAllIcons" type="primary" plain>
                显示全部
              </el-button>
            </el-col>
          </el-row>
        </div>
        
        <!-- 图标统计信息 -->
        <div class="icon-stats">
          <el-tag type="info" size="small">
            共 {{ totalIcons }} 个图标
          </el-tag>
          <el-tag v-if="selectedCategory" type="success" size="small">
            {{ selectedCategory }} 分类
          </el-tag>
          <el-tag v-if="iconSearchKeyword" type="warning" size="small">
            搜索: {{ iconSearchKeyword }}
          </el-tag>
        </div>
        
        <!-- 优化图标网格 -->
        <div v-loading="iconsLoading" class="icon-grid">
          <div
            v-for="icon in paginatedIcons"
            :key="icon.name"
            class="icon-item"
            :class="{ 'selected': selectedIcon === icon.name }"
            @click="selectIcon(icon.name)"
            :title="`${icon.name} - ${icon.component || icon.fallbackClass}`"
          >
            <!-- Element Plus 图标显示 -->
            <div class="icon-display">
              <component 
                v-if="icon.component && getIconComponent(icon.name)"
                :is="getIconComponent(icon.name)"
                style="font-size: 24px;"
              />
              <!-- FontAwesome 备选图标 -->
              <i 
                v-else-if="icon.fallbackClass"
                :class="icon.fallbackClass"
                style="font-size: 24px;"
              ></i>
            </div>
            <span class="icon-name">{{ icon.name }}</span>
            <span class="icon-category">{{ icon.category }}</span>
            <span class="icon-type" :class="icon.component ? 'element-plus' : 'fontawesome'">
              {{ icon.component ? 'Element+' : 'FA' }}
            </span>
          </div>
        </div>
        
        <!-- 无图标提示 -->
        <div v-if="!iconsLoading && filteredIcons.length === 0" class="no-icons">
          <el-empty description="未找到匹配的图标">
            <el-button type="primary" @click="loadAllIcons">显示全部图标</el-button>
          </el-empty>
        </div>
        
        <!-- 分页 -->
        <el-pagination
          v-if="filteredIcons.length > iconPageSize"
          v-model:current-page="currentIconPage"
          :page-size="iconPageSize"
          :total="filteredIcons.length"
          layout="prev, pager, next, jumper"
          @current-change="handleIconPageChange"
          class="icon-pagination"
        />
      </div>
      
      <template #footer>
        <span class="dialog-footer">
          <el-button @click="iconDialogVisible = false">取消</el-button>
          <el-button type="primary" @click="confirmIconSelection" :disabled="!selectedIcon">
            确定
          </el-button>
        </span>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted, onUnmounted, nextTick, computed } from 'vue'
import { ElMessage, ElMessageBox, ElTable } from 'element-plus'
import type { FormInstance } from 'element-plus'
import { 
  Search, Refresh, Plus, Edit, Delete, Check, Close, ArrowDown
} from '@element-plus/icons-vue'
import categoryApi, { type Category, type CategoryDto, type CategoryListParams } from '@/api/category'
import { 
  searchIcons, 
  getCategories, 
  getIconsByCategory, 
  getIconComponent,
  getIconClass,
  isElementIcon,
  type IconItem 
} from '@/data/iconLibrary'

interface CategorySearchParams {
  categoryName: string
  categoryCode: string
  page: number
  size: number
}

interface CategoryFormData {
  id?: number
  categoryCode: string
  categoryName: string
  categoryIcon: string
  description: string
  seqNo: number
}

// 响应式数据
const loading = ref(false)
const submitting = ref(false)
const tableRef = ref<InstanceType<typeof ElTable>>()
const categoryFormRef = ref<FormInstance>()
const tableHeight = ref(400)
const selectedCategories = ref<Category[]>([])

// 图标选择器相关
const iconDialogVisible = ref(false)
const iconSearchKeyword = ref('')
const selectedIcon = ref('')
const iconsLoading = ref(false)
const currentIconPage = ref(1)
const iconPageSize = 30 // 从24改为30，适应6x5的网格布局
const selectedCategory = ref('')

// 图标数据
const allIcons = ref<IconItem[]>([])
const filteredIcons = ref<IconItem[]>([])
const iconCategories = ref<string[]>([])

// 搜索表单
const searchForm = reactive({
  categoryName: '',
  categoryCode: ''
})

// 分类列表
const categoryList = ref<Category[]>([])

// 分页
const pagination = reactive({
  currentPage: 1,
  pageSize: 10,
  total: 0
})

// 对话框相关
const dialogVisible = ref(false)
const isEdit = ref(false)

// 分类表单
const categoryForm = reactive<CategoryFormData>({
  categoryCode: '',
  categoryName: '',
  categoryIcon: '',
  description: '',
  seqNo: 0
})

// 计算属性
const totalIcons = computed(() => allIcons.value.length)

const paginatedIcons = computed(() => {
  const start = (currentIconPage.value - 1) * iconPageSize
  return filteredIcons.value.slice(start, start + iconPageSize)
})

// 表单验证规则
const categoryRules = {
  categoryCode: [
    { required: true, message: '请输入分类代码', trigger: 'blur' },
    { min: 1, max: 20, message: '分类代码长度在 1 到 20 个字符', trigger: 'blur' }
  ],
  categoryName: [
    { required: true, message: '请输入分类名称', trigger: 'blur' },
    { min: 1, max: 50, message: '分类名称长度在 1 到 50 个字符', trigger: 'blur' }
  ],
  categoryIcon: [
    { required: true, message: '请选择分类图标', trigger: 'change' }
  ],
  description: [
    { max: 500, message: '描述不能超过 500 个字符', trigger: 'blur' }
  ],
  seqNo: [
    { required: true, message: '请输入排序号', trigger: 'blur' },
    { type: 'number', min: 0, max: 9999, message: '排序号必须在 0 到 9999 之间', trigger: 'blur' }
  ]
}

// 图标相关函数
const loadIconCategories = () => {
  iconCategories.value = getCategories()
}

const searchLocalIcons = (query: string) => {
  try {
    const results = searchIcons(query)
    filteredIcons.value = results
    currentIconPage.value = 1
  } catch (error) {
    console.error('搜索图标失败:', error)
    ElMessage.error('搜索图标失败')
  }
}

const loadIconsByCategory = () => {
  try {
    if (selectedCategory.value) {
      filteredIcons.value = getIconsByCategory(selectedCategory.value)
    } else {
      filteredIcons.value = allIcons.value
    }
    currentIconPage.value = 1
  } catch (error) {
    console.error('加载分类图标失败:', error)
    ElMessage.error('加载分类图标失败')
  }
}

const loadAllIcons = () => {
  filteredIcons.value = allIcons.value
  selectedCategory.value = ''
  iconSearchKeyword.value = ''
  currentIconPage.value = 1
}

// 防抖搜索
let searchTimeout: ReturnType<typeof setTimeout>;
const debouncedSearch = () => {
  if (searchTimeout) {
    clearTimeout(searchTimeout)
  }
  searchTimeout = setTimeout(() => {
    if (iconSearchKeyword.value.trim()) {
      searchLocalIcons(iconSearchKeyword.value)
    } else {
      loadIconsByCategory()
    }
  }, 300)
}

// 分页相关
const handleIconPageChange = (page: number) => {
  currentIconPage.value = page
}

// 图标选择
const selectIcon = (iconClass: string) => {
  selectedIcon.value = iconClass
}

const confirmIconSelection = () => {
  if (selectedIcon.value) {
    categoryForm.categoryIcon = selectedIcon.value
    iconDialogVisible.value = false
    ElMessage.success('图标选择成功')
  } else {
    ElMessage.warning('请选择一个图标')
  }
}

const openIconDialog = () => {
  iconDialogVisible.value = true
  selectedIcon.value = categoryForm.categoryIcon || ''
  iconSearchKeyword.value = ''
  selectedCategory.value = ''
  currentIconPage.value = 1
  loadAllIcons()
}

const handleIconDialogClose = () => {
  selectedIcon.value = ''
  iconSearchKeyword.value = ''
  selectedCategory.value = ''
  currentIconPage.value = 1
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

// API调用方法
const loadCategoryList = async () => {
  try {
    loading.value = true
    const params: CategoryListParams = {
      page: pagination.currentPage,
      pageSize: pagination.pageSize,
      ...searchForm
    }
    const response = await categoryApi.getCategoryList(params)
    categoryList.value = response.data
    pagination.total = response.total
  } catch (error) {
    console.error('加载分类列表失败:', error)
    ElMessage.error('加载分类列表失败')
  } finally {
    loading.value = false
  }
}

// 提交表单
const handleSubmit = async () => {
  if (!categoryFormRef.value) return
  
  try {
    await categoryFormRef.value.validate()
    
    submitting.value = true
    
    // 检查分类代码是否重复
    const codeExists = await categoryApi.checkCategoryCodeExists(
      categoryForm.categoryCode,
      isEdit.value ? categoryForm.id : undefined
    )
    
    if (codeExists) {
      ElMessage.error('分类代码已存在，请使用其他代码')
      return
    }

    const categoryData: CategoryDto = {
      categoryCode: categoryForm.categoryCode,
      categoryName: categoryForm.categoryName,
      categoryIcon: categoryForm.categoryIcon,
      description: categoryForm.description,
      seqNo: categoryForm.seqNo
    }

    if (isEdit.value) {
      await categoryApi.updateCategory(categoryForm.id!, categoryData)
      ElMessage.success('分类更新成功')
    } else {
      await categoryApi.createCategory(categoryData)
      ElMessage.success('分类创建成功')
    }

    dialogVisible.value = false
    await loadCategoryList()
  } catch (error) {
    console.error('保存分类失败:', error)
    ElMessage.error('保存分类失败')
  } finally {
    submitting.value = false
  }
}

// 删除分类
const handleDelete = async (row: Category) => {
  try {
    await ElMessageBox.confirm(
      `确定要删除分类 "${row.categoryName}" 吗？`,
      '确认删除',
      {
        confirmButtonText: '确定',
        cancelButtonText: '取消',
        type: 'warning'
      }
    )
    
    await categoryApi.deleteCategory(row.id)
    ElMessage.success('删除成功')
    await loadCategoryList()
  } catch (error) {
    if (error !== 'cancel') {
      console.error('删除分类失败:', error)
      ElMessage.error('删除分类失败')
    }
  }
}

// 批量删除
const handleBatchDelete = async () => {
  if (selectedCategories.value.length === 0) {
    ElMessage.warning('请选择要删除的分类')
    return
  }

  try {
    await ElMessageBox.confirm(
      `确定要删除选中的 ${selectedCategories.value.length} 个分类吗？`,
      '确认批量删除',
      {
        confirmButtonText: '确定',
        cancelButtonText: '取消',
        type: 'warning'
      }
    )
    
    const ids = selectedCategories.value.map(category => category.id)
    await categoryApi.batchDeleteCategories(ids)
    ElMessage.success('批量删除成功')
    selectedCategories.value = []
    await loadCategoryList()
  } catch (error) {
    if (error !== 'cancel') {
      console.error('批量删除失败:', error)
      ElMessage.error('批量删除失败')
    }
  }
}

// 处理每页显示数量变化
const handleSizeChange = async (val: number) => {
  pagination.pageSize = val
  pagination.currentPage = 1
  await loadCategoryList()
}

// 处理当前页变化
const handleCurrentChange = async (val: number) => {
  pagination.currentPage = val
  await loadCategoryList()
}

// 搜索
const handleSearch = async () => {
  pagination.currentPage = 1
  await loadCategoryList()
}

// 重置搜索
const handleReset = async () => {
  searchForm.categoryName = ''
  searchForm.categoryCode = ''
  pagination.currentPage = 1
  await loadCategoryList()
}

// 处理表格选择变化
const handleSelectionChange = (val: Category[]) => {
  selectedCategories.value = val
}

// 新增分类
const handleAdd = () => {
  isEdit.value = false
  Object.assign(categoryForm, {
    categoryCode: '',
    categoryName: '',
    categoryIcon: '',
    description: '',
    seqNo: categoryList.value.length + 1
  })
  dialogVisible.value = true
}

// 编辑分类
const handleEdit = (row: Category) => {
  isEdit.value = true
  Object.assign(categoryForm, {
    id: row.id,
    categoryCode: row.categoryCode,
    categoryName: row.categoryName,
    categoryIcon: row.categoryIcon,
    description: row.description,
    seqNo: row.seqNo
  })
  dialogVisible.value = true
}

// 初始化图标库
const initializeIconLibrary = () => {
  try {
    iconsLoading.value = true
    allIcons.value = searchIcons('')
    filteredIcons.value = allIcons.value
    loadIconCategories()
    ElMessage.success(`图标库初始化完成，共加载 ${allIcons.value.length} 个图标`)
  } catch (error) {
    console.error('图标库初始化失败:', error)
    ElMessage.error('图标库初始化失败')
  } finally {
    iconsLoading.value = false
  }
}

// 组件生命周期
onMounted(() => {
  loadCategoryList()
  calculateTableHeight()
  initializeIconLibrary()
  
  window.addEventListener('resize', calculateTableHeight)
})

onUnmounted(() => {
  window.removeEventListener('resize', calculateTableHeight)
  if (searchTimeout) {
    clearTimeout(searchTimeout)
  }
})
</script>

<style scoped>
.category-management-container {
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

.dialog-footer {
  text-align: right;
}

/* 图标选择器样式 - 优化版本 */
.icon-selector {
  width: 100%;
}

.icon-selector .el-input {
  cursor: pointer;
}

.icon-selector-content {
  height: 500px;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.icon-search-container {
  margin-bottom: 16px;
  padding-bottom: 16px;
  border-bottom: 1px solid #ebeef5;
  flex-shrink: 0;
}

.icon-stats {
  display: flex;
  gap: 8px;
  margin-bottom: 16px;
  padding: 8px 0;
  flex-shrink: 0;
}

.icon-grid {
  display: grid;
  grid-template-columns: repeat(6, 1fr);
  gap: 12px;
  flex: 1;
  overflow-y: auto;
  padding: 12px;
  align-content: start;
  max-height: 350px;
}

.icon-item {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 12px 8px;
  border: 1px solid #e4e7ed;
  border-radius: 8px;
  cursor: pointer;
  transition: all 0.3s ease;
  background: #fff;
  min-height: 80px;
  box-sizing: border-box;
  position: relative;
}

.icon-item:hover {
  border-color: #409eff;
  background: #f0f9ff;
  transform: translateY(-2px);
  box-shadow: 0 4px 12px rgba(64, 158, 255, 0.15);
}

.icon-item.selected {
  border-color: #409eff;
  background: #e6f7ff;
  box-shadow: 0 0 0 2px rgba(64, 158, 255, 0.2);
}

/* 图标显示容器样式 */
.icon-display {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 32px;
  height: 32px;
  margin-bottom: 8px;
  border-radius: 4px;
  background: rgba(64, 158, 255, 0.05);
  transition: all 0.3s ease;
}

.icon-item:hover .icon-display {
  background: rgba(64, 158, 255, 0.1);
}

.icon-item.selected .icon-display {
  background: rgba(64, 158, 255, 0.15);
}

/* Element Plus 图标样式 */
.icon-display svg {
  width: 24px;
  height: 24px;
  color: #606266;
  transition: color 0.3s ease;
}

.icon-item:hover .icon-display svg {
  color: #409eff;
}

.icon-item.selected .icon-display svg {
  color: #409eff;
}

/* FontAwesome 图标样式 */
.icon-display i {
  font-size: 24px;
  color: #606266;
  transition: color 0.3s ease;
  line-height: 1;
}

.icon-item:hover .icon-display i {
  color: #409eff;
}

.icon-item.selected .icon-display i {
  color: #409eff;
}

.icon-name {
  font-size: 11px;
  color: #606266;
  text-align: center;
  word-break: break-all;
  line-height: 1.2;
  font-weight: 500;
  max-height: 26px;
  overflow: hidden;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  margin-bottom: 2px;
}

.icon-category {
  display: none; /* 隐藏分类标签以节省空间 */
}

/* 图标类型标签 */
.icon-type {
  position: absolute;
  top: 2px;
  right: 2px;
  font-size: 8px;
  padding: 1px 3px;
  border-radius: 2px;
  font-weight: bold;
  line-height: 1;
}

.icon-type.element-plus {
  background: #67c23a;
  color: white;
}

.icon-type.fontawesome {
  background: #e6a23c;
  color: white;
}

.icon-pagination {
  display: flex;
  justify-content: center;
  margin-top: 16px;
  padding-top: 16px;
  border-top: 1px solid #ebeef5;
  flex-shrink: 0;
}

.no-icons {
  text-align: center;
  padding: 40px;
  flex: 1;
  display: flex;
  flex-direction: column;
  justify-content: center;
  align-items: center;
}

.cursor-pointer {
  cursor: pointer;
}

/* 响应式设计 */
@media (max-width: 1200px) {
  .icon-grid {
    grid-template-columns: repeat(5, 1fr);
  }
}

@media (max-width: 992px) {
  .icon-grid {
    grid-template-columns: repeat(4, 1fr);
    gap: 10px;
  }
  
  .icon-item {
    min-height: 70px;
    padding: 10px 6px;
  }
  
  .icon-display {
    width: 28px;
    height: 28px;
  }
  
  .icon-display svg,
  .icon-display i {
    font-size: 20px;
    width: 20px;
    height: 20px;
  }
}

@media (max-width: 768px) {
  .icon-grid {
    grid-template-columns: repeat(3, 1fr);
    gap: 8px;
  }
  
  .icon-item {
    padding: 8px 4px;
    min-height: 60px;
  }
  
  .icon-display {
    width: 24px;
    height: 24px;
  }
  
  .icon-display svg,
  .icon-display i {
    font-size: 18px;
    width: 18px;
    height: 18px;
  }
  
  .icon-name {
    font-size: 10px;
  }
}

/* 加载状态优化 */
.icon-grid[v-loading] {
  min-height: 200px;
}

/* 滚动条样式优化 */
.icon-grid::-webkit-scrollbar {
  width: 6px;
}

.icon-grid::-webkit-scrollbar-track {
  background: #f1f1f1;
  border-radius: 3px;
}

.icon-grid::-webkit-scrollbar-thumb {
  background: #c1c1c1;
  border-radius: 3px;
}

.icon-grid::-webkit-scrollbar-thumb:hover {
  background: #a8a8a8;
}
</style>