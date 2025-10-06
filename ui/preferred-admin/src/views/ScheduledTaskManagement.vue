<template>
  <div class="scheduled-task-management-container">
    <el-card>
      <template #header>
        <div class="card-header">
          <h2>定时任务管理</h2>
          <p>管理系统中的所有定时任务</p>
        </div>
      </template>
      
      <!-- 搜索和操作区域 -->
      <div class="search-section">
        <el-row :gutter="20" align="middle">
          <el-col :span="6">
            <el-input
              v-model="searchForm.name"
              placeholder="请输入任务名称"
              clearable
              @keyup.enter="handleSearch"
            >
              <template #prefix>
                <el-icon><Search /></el-icon>
              </template>
            </el-input>
          </el-col>
          <el-col :span="6">
            <div class="search-buttons">
              <el-button type="primary" @click="handleSearch">
                <el-icon><Search /></el-icon>
                搜索
              </el-button>
              <el-button @click="handleReset">
                <el-icon><Refresh /></el-icon>
                重置
              </el-button>
            </div>
          </el-col>
          <el-col :span="12">
            <div class="action-buttons">
              <el-button type="primary" @click="handleAdd">
                <el-icon><Plus /></el-icon>
                新增任务
              </el-button>
              <el-button 
                type="danger" 
                :disabled="selectedTasks.length === 0"
                @click="handleBatchDelete"
              >
                <el-icon><Delete /></el-icon>
                批量删除
              </el-button>
            </div>
          </el-col>
        </el-row>
      </div>

      <!-- 表格容器 -->
      <div class="table-container">
        <el-table
          ref="tableRef"
          v-loading="loading"
          :data="taskList"
          @selection-change="handleSelectionChange"
          stripe
          border
          style="width: 100%"
          :height="tableHeight"
        >
          <el-table-column type="selection" width="40" />
          <el-table-column prop="id" label="ID" width="60" />
          <el-table-column prop="name" label="任务名称" width="150" show-overflow-tooltip />
          <el-table-column prop="code" label="任务代码" width="120" show-overflow-tooltip />
          <el-table-column prop="cron" label="Cron表达式" width="120" show-overflow-tooltip />
          <el-table-column label="Cron说明" width="150" show-overflow-tooltip>
            <template #default="scope">
              {{ getCronDescription(scope.row.cron) }}
            </template>
          </el-table-column>
          <el-table-column prop="enabled" label="状态" width="100">
            <template #default="scope">
              <el-tag :type="scope.row.enabled ? 'success' : 'danger'">
                {{ scope.row.enabled ? '启用' : '禁用' }}
              </el-tag>
            </template>
          </el-table-column>
          <el-table-column prop="nextRuntime" label="下次执行时间" width="160">
            <template #default="scope">
              {{ scope.row.nextRuntime ? formatDate(scope.row.nextRuntime) : '-' }}
            </template>
          </el-table-column>
          <el-table-column prop="lastRunTime" label="上次执行时间" width="160">
            <template #default="scope">
              {{ scope.row.lastRunTime ? formatDate(scope.row.lastRunTime) : '-' }}
            </template>
          </el-table-column>
          <el-table-column prop="duration" label="执行耗时(ms)" width="120">
            <template #default="scope">
              {{ scope.row.duration || '-' }}
            </template>
          </el-table-column>
         <el-table-column prop="handler" label="处理器" width="150" show-overflow-tooltip />
          <el-table-column prop="crtTime" label="创建时间" width="160">
            <template #default="scope">
              {{ formatDate(scope.row.crtTime) }}
            </template>
          </el-table-column>
          <el-table-column label="操作" width="350" fixed="right">
            <template #default="scope">
              <el-button
                type="primary"
                size="small"
                @click="handleEdit(scope.row)"
              >
                编辑
              </el-button>
              <el-button
                type="success"
                size="small"
                @click="handleExecute(scope.row)"
              >
                执行
              </el-button>
              <el-button
                :type="scope.row.enabled ? 'warning' : 'info'"
                size="small"
                @click="handleToggleStatus(scope.row)"
              >
                {{ scope.row.enabled ? '禁用' : '启用' }}
              </el-button>
              <el-button
                type="info"
                size="small"
                @click="handleViewLogs(scope.row)"
              >
                日志
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
      :title="isEdit ? '编辑定时任务' : '新增定时任务'"
      width="600px"
      :close-on-click-modal="false"
    >
      <el-form
        ref="taskFormRef"
        :model="taskForm"
        :rules="taskRules"
        label-width="120px"
        size="default"
      >
        <el-form-item label="任务名称" prop="name">
          <el-input
            v-model="taskForm.name"
            placeholder="请输入任务名称"
            maxlength="50"
            show-word-limit
          />
        </el-form-item>
        
        <el-form-item label="任务代码" prop="code">
          <el-input
            v-model="taskForm.code"
            placeholder="请输入任务代码"
            :disabled="isEdit"
            maxlength="50"
            show-word-limit
          />
        </el-form-item>
        
        <el-form-item label="Cron表达式" prop="cron">
          <el-select
            v-model="taskForm.cron"
            placeholder="请选择Cron表达式"
            filterable
            allow-create
            style="width: 100%"
            @change="handleCronChange"
          >
            <el-option
              v-for="item in cronOptions"
              :key="item.value"
              :label="`${item.label} (${item.value})`"
              :value="item.value"
            />
          </el-select>
          <div class="form-tip">
            可选择常用表达式或手动输入自定义表达式
          </div>
        </el-form-item>
        
        <el-form-item label="Cron表达式说明" prop="cronDescription">
          <el-input
            v-model="taskForm.cronDescription"
            placeholder="Cron表达式说明将自动生成"
            readonly
          />
        </el-form-item>
        
        <el-form-item label="处理器" prop="handler">
          <el-input
            v-model="taskForm.handler"
            placeholder="请输入处理器类名"
            maxlength="100"
            show-word-limit
          />
        </el-form-item>
        
        <el-form-item label="执行参数" prop="parameters">
          <el-input
            v-model="taskForm.parameters"
            type="textarea"
            :rows="3"
            placeholder="请输入执行参数（JSON格式），可选"
            maxlength="1000"
            show-word-limit
          />
          <div class="form-tip">
            示例：{"timeout": 30, "retryCount": 3}
          </div>
        </el-form-item>
        
        <el-form-item label="是否启用">
          <el-switch
            v-model="taskForm.enabled"
            active-text="启用"
            inactive-text="禁用"
          />
        </el-form-item>
        
        <el-form-item label="备注">
          <el-input
            v-model="taskForm.remark"
            type="textarea"
            :rows="2"
            placeholder="请输入备注信息，可选"
            maxlength="255"
            show-word-limit
          />
        </el-form-item>
      </el-form>
      
      <template #footer>
        <span class="dialog-footer">
          <el-button @click="dialogVisible = false">取消</el-button>
          <el-button type="primary" @click="handleSubmit" :loading="submitting">
            确定
          </el-button>
        </span>
      </template>
    </el-dialog>

    <!-- 日志查看对话框 -->
    <el-dialog
      v-model="logDialogVisible"
      title="任务执行日志"
      width="80%"
      :close-on-click-modal="false"
      class="log-dialog"
    >
      <div class="log-dialog-content">
        <div class="log-search-section">
        <el-row :gutter="20">
          <el-col :span="6">
            <el-select v-model="logSearchForm.success" placeholder="执行结果" clearable>
              <el-option label="成功" :value="true" />
              <el-option label="失败" :value="false" />
            </el-select>
          </el-col>
          <el-col :span="12">
            <el-date-picker
              v-model="logDateRange"
              type="datetimerange"
              range-separator="至"
              start-placeholder="开始时间"
              end-placeholder="结束时间"
              format="YYYY-MM-DD HH:mm:ss"
              value-format="YYYY-MM-DD HH:mm:ss"
              @change="handleLogDateChange"
            />
          </el-col>
          <el-col :span="6">
            <el-button type="primary" @click="handleLogSearch">
              <el-icon><Search /></el-icon>
              搜索
            </el-button>
          </el-col>
        </el-row>
      </div>

        <div class="log-table-container">
          <el-table
            v-loading="logLoading"
            :data="logList"
            stripe
            border
            style="width: 100%"
            :height="logTableHeight"
          >
        <el-table-column prop="id" label="ID" width="80" />
        <el-table-column prop="taskCode" label="任务代码" width="150" show-overflow-tooltip />
        <el-table-column prop="startTime" label="开始时间" width="180">
          <template #default="scope">
            {{ formatDate(scope.row.startTime) }}
          </template>
        </el-table-column>
        <el-table-column prop="endTime" label="结束时间" width="180">
          <template #default="scope">
            {{ scope.row.endTime ? formatDate(scope.row.endTime) : '-' }}
          </template>
        </el-table-column>
        <el-table-column prop="success" label="执行结果" width="120">
          <template #default="scope">
            <el-tag :type="scope.row.success ? 'success' : 'danger'">
              {{ scope.row.success ? '成功' : '失败' }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column prop="duration" label="执行耗时" width="120">
          <template #default="scope">
            {{ scope.row.duration ? scope.row.duration + 'ms' : '-' }}
          </template>
        </el-table-column>
        <el-table-column prop="errorMessage" label="错误信息" min-width="200" show-overflow-tooltip>
          <template #default="scope">
            {{ scope.row.errorMessage || '-' }}
          </template>
        </el-table-column>
        <el-table-column prop="crtTime" label="记录时间" width="180">
          <template #default="scope">
            {{ formatDate(scope.row.crtTime) }}
          </template>
        </el-table-column>
          </el-table>
        </div>
        
        <!-- 日志分页 -->
        <el-pagination
          v-model:current-page="logPagination.currentPage"
          v-model:page-size="logPagination.pageSize"
          :page-sizes="[10, 20, 50, 100]"
          :total="logPagination.total"
          layout="total, sizes, prev, pager, next, jumper"
          @size-change="handleLogSizeChange"
          @current-change="handleLogCurrentChange"
        />
      </div>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import scheduledTaskApi, {
  type ScheduledTaskCreateDto,
  type ScheduledTaskListDto,
  type ScheduledTaskSearchParams,
  type ScheduledTaskUpdateDto
} from '@/api/scheduledTask'
import {
  Delete,
  Plus,
  Refresh,
  Search
} from '@element-plus/icons-vue'
import type { FormInstance } from 'element-plus'
import { ElMessage, ElMessageBox, ElTable } from 'element-plus'
import { onMounted, onUnmounted, reactive, ref } from 'vue'

interface TaskFormData {
  id?: number
  name: string
  code: string
  cron: string
  cronDescription: string
  handler: string
  parameters?: string
  enabled: boolean
  remark?: string
}

// 任务表单
// 在 taskForm 定义之后添加
const taskForm = reactive<TaskFormData>({
  name: '',
  code: '',
  cron: '',
  cronDescription: '',
  handler: '',
  parameters: '',
  enabled: true,
  remark: ''
})

// 更全面的Cron表达式选项
const cronOptions = [
  // 秒级
  { label: '每秒执行', value: '* * * * * ?' },
  { label: '每5秒执行', value: '*/5 * * * * ?' },
  { label: '每10秒执行', value: '*/10 * * * * ?' },
  { label: '每30秒执行', value: '*/30 * * * * ?' },
  
  // 分钟级
  { label: '每分钟执行', value: '0 * * * * ?' },
  { label: '每2分钟执行', value: '0 */2 * * * ?' },
  { label: '每5分钟执行', value: '0 */5 * * * ?' },
  { label: '每10分钟执行', value: '0 */10 * * * ?' },
  { label: '每15分钟执行', value: '0 */15 * * * ?' },
  { label: '每20分钟执行', value: '0 */20 * * * ?' },
  { label: '每30分钟执行', value: '0 */30 * * * ?' },
  
  // 小时级
  { label: '每小时执行', value: '0 0 * * * ?' },
  { label: '每2小时执行', value: '0 0 */2 * * ?' },
  { label: '每3小时执行', value: '0 0 */3 * * ?' },
  { label: '每4小时执行', value: '0 0 */4 * * ?' },
  { label: '每6小时执行', value: '0 0 */6 * * ?' },
  { label: '每8小时执行', value: '0 0 */8 * * ?' },
  { label: '每12小时执行', value: '0 0 */12 * * ?' },
  
  // 每天特定时间
  { label: '每天0点执行', value: '0 0 0 * * ?' },
  { label: '每天1点执行', value: '0 0 1 * * ?' },
  { label: '每天6点执行', value: '0 0 6 * * ?' },
  { label: '每天8点执行', value: '0 0 8 * * ?' },
  { label: '每天9点执行', value: '0 0 9 * * ?' },
  { label: '每天12点执行', value: '0 0 12 * * ?' },
  { label: '每天18点执行', value: '0 0 18 * * ?' },
  { label: '每天22点执行', value: '0 0 22 * * ?' },
  
  // 工作日
  { label: '工作日9点执行', value: '0 0 9 ? * MON-FRI' },
  { label: '工作日18点执行', value: '0 0 18 ? * MON-FRI' },
  { label: '工作日每小时执行', value: '0 0 * ? * MON-FRI' },
  { label: '工作日每30分钟执行', value: '0 */30 * ? * MON-FRI' },
  
  // 周末
  { label: '周末9点执行', value: '0 0 9 ? * SAT,SUN' },
  { label: '周末18点执行', value: '0 0 18 ? * SAT,SUN' },
  
  // 每周特定日期
  { label: '每周一0点执行', value: '0 0 0 ? * MON' },
  { label: '每周二0点执行', value: '0 0 0 ? * TUE' },
  { label: '每周三0点执行', value: '0 0 0 ? * WED' },
  { label: '每周四0点执行', value: '0 0 0 ? * THU' },
  { label: '每周五0点执行', value: '0 0 0 ? * FRI' },
  { label: '每周六0点执行', value: '0 0 0 ? * SAT' },
  { label: '每周日0点执行', value: '0 0 0 ? * SUN' },
  
  // 每月
  { label: '每月1号0点执行', value: '0 0 0 1 * ?' },
  { label: '每月15号0点执行', value: '0 0 0 15 * ?' },
  { label: '每月最后一天0点执行', value: '0 0 0 L * ?' },
  
  // 每年
  { label: '每年1月1号0点执行', value: '0 0 0 1 1 ?' },
  { label: '每年12月31号0点执行', value: '0 0 0 31 12 ?' }
]

// 表单验证规则
const taskRules = {
  name: [
    { required: true, message: '请输入任务名称', trigger: 'blur' },
    { min: 1, max: 50, message: '长度在 1 到 50 个字符', trigger: 'blur' }
  ],
  code: [
    { required: true, message: '请输入任务代码', trigger: 'blur' },
    { min: 1, max: 50, message: '长度在 1 到 50 个字符', trigger: 'blur' },
    { pattern: /^[a-zA-Z0-9_]+$/, message: '只能包含字母、数字和下划线', trigger: 'blur' }
  ],
  cron: [
    { required: true, message: '请选择或输入Cron表达式', trigger: 'blur' },
    { min: 1, max: 50, message: '长度在 1 到 50 个字符', trigger: 'blur' }
  ],
  handler: [
    { required: true, message: '请输入处理器', trigger: 'blur' },
    { min: 1, max: 100, message: '长度在 1 到 100 个字符', trigger: 'blur' }
  ],
  parameters: [
    { 
      validator: (rule: any, value: string, callback: any) => {
        if (value && value.trim()) {
          try {
            JSON.parse(value)
            callback()
          } catch (error) {
            callback(new Error('参数必须是有效的JSON格式'))
          }
        } else {
          callback()
        }
      }, 
      trigger: 'blur' 
    }
  ]
}

// 响应式数据
// 响应式数据
const loading = ref(false)
const submitting = ref(false)
const logLoading = ref(false)
const tableRef = ref<InstanceType<typeof ElTable>>()
const taskFormRef = ref<FormInstance>()
const selectedTasks = ref<ScheduledTaskListDto[]>([])

// 日志相关变量
const logDialogVisible = ref(false)
const logList = ref<any[]>([])
const logDateRange = ref<[string, string] | null>(null)

// 日志搜索表单
const logSearchForm = reactive({
  taskId: 0,
  success: undefined as boolean | undefined,
  startTime: '',
  endTime: ''
})

// 日志分页
const logPagination = reactive({
  currentPage: 1,
  pageSize: 10,
  total: 0
})

// 添加表格高度计算
const tableHeight = ref(400)
const logTableHeight = ref(300)

// 日期格式化函数
const formatDate = (date: string | Date) => {
  if (!date) return '-'
  const d = new Date(date)
  return d.toLocaleString('zh-CN', {
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit'
  })
}

// 搜索表单（简化）
const searchForm = reactive({
  name: ''
})

// 任务列表
const taskList = ref<ScheduledTaskListDto[]>([])

// 分页
const pagination = reactive({
  currentPage: 1,
  pageSize: 10,
  total: 0
})

// 对话框相关
const dialogVisible = ref(false)
const isEdit = ref(false)

// 获取任务列表（简化搜索参数）
const getTaskList = async () => {
  try {
    loading.value = true
    const params: ScheduledTaskSearchParams = {
      page: pagination.currentPage,
      pageSize: pagination.pageSize,
      name: searchForm.name || undefined
    }
    
    const response = await scheduledTaskApi.getScheduledTaskList(params)
    taskList.value = response.data
    pagination.total = response.total
  } catch (error) {
    console.error('获取任务列表失败:', error)
    ElMessage.error('获取任务列表失败')
  } finally {
    loading.value = false
  }
}

// 搜索表单（简化）
const handleSearch = () => {
  pagination.currentPage = 1
  getTaskList()
}

// 重置搜索（简化）
const handleReset = () => {
  Object.assign(searchForm, {
    name: ''
  })
  handleSearch()
}

// 新增（添加cronDescription字段）
const handleAdd = () => {
  isEdit.value = false
  Object.assign(taskForm, {
    name: '',
    code: '',
    cron: '',
    cronDescription: '',
    handler: '',
    parameters: '',
    enabled: true,
    remark: ''
  })
  dialogVisible.value = true
}

// 编辑（添加cronDescription字段）
const handleEdit = (row: ScheduledTaskListDto) => {
  isEdit.value = true
  Object.assign(taskForm, {
    id: row.id,
    name: row.name,
    code: row.code,
    cron: row.cron,
    cronDescription: getCronDescription(row.cron),
    handler: row.handler,
    parameters: row.parameters || '',
    enabled: row.enabled,
    remark: row.remark || ''
  })
  dialogVisible.value = true
}

// 切换任务状态（修复状态判断）
const handleToggleStatus = async (row: ScheduledTaskListDto) => {
  try {
    const isEnabled = row.enabled
    const action = isEnabled ? '禁用' : '启用'
    
    await ElMessageBox.confirm(
      `确定要${action}任务 "${row.name}" 吗？`,
      `${action}确认`,
      {
        confirmButtonText: '确定',
        cancelButtonText: '取消',
        type: 'warning'
      }
    )
    
    if (isEnabled) {
      await scheduledTaskApi.disableScheduledTask(row.id)
    } else {
      await scheduledTaskApi.enableScheduledTask(row.id)
    }
    
    ElMessage.success(`${action}成功`)
    getTaskList()
  } catch (error: any) {
    if (error !== 'cancel') {
      console.error('状态切换失败:', error)
      ElMessage.error('状态切换失败')
    }
  }
}

// 提交表单
const handleSubmit = async () => {
  if (!taskFormRef.value) return
  
  try {
    await taskFormRef.value.validate()
    submitting.value = true
    
    if (isEdit.value) {
      const updateData: ScheduledTaskUpdateDto = {
        id: taskForm.id!,
        name: taskForm.name,
        code: taskForm.code,  // 添加这行
        cron: taskForm.cron,
        handler: taskForm.handler,
        parameters: taskForm.parameters || null,
        enabled: taskForm.enabled,
        remark: taskForm.remark || null
      }
      await scheduledTaskApi.updateScheduledTask(taskForm.id!, updateData)
      ElMessage.success('更新成功')
    } else {
      const createData: ScheduledTaskCreateDto = {
        name: taskForm.name,
        code: taskForm.code,
        cron: taskForm.cron,
        handler: taskForm.handler,
        parameters: taskForm.parameters || null,
        enabled: taskForm.enabled,
        remark: taskForm.remark || null
      }
      await scheduledTaskApi.createScheduledTask(createData)
      ElMessage.success('创建成功')
    }
    
    dialogVisible.value = false
    getTaskList()
  } catch (error) {
    console.error('提交失败:', error)
    ElMessage.error('提交失败')
  } finally {
    submitting.value = false
  }
}

// 删除
const handleDelete = async (row: ScheduledTaskListDto) => {
  try {
    await ElMessageBox.confirm(
      `确定要删除任务 "${row.name}" 吗？`,
      '删除确认',
      {
        confirmButtonText: '确定',
        cancelButtonText: '取消',
        type: 'warning'
      }
    )
    
    await scheduledTaskApi.deleteScheduledTask(row.id)
    ElMessage.success('删除成功')
    getTaskList()
  } catch (error: any) {
    if (error !== 'cancel') {
      console.error('删除失败:', error)
      ElMessage.error('删除失败')
    }
  }
}

// 批量删除
const handleBatchDelete = async () => {
  if (selectedTasks.value.length === 0) {
    ElMessage.warning('请选择要删除的任务')
    return
  }
  
  try {
    await ElMessageBox.confirm(
      `确定要删除选中的 ${selectedTasks.value.length} 个任务吗？`,
      '批量删除确认',
      {
        confirmButtonText: '确定',
        cancelButtonText: '取消',
        type: 'warning'
      }
    )
    
    const ids = selectedTasks.value.map(task => task.id)
    const result = await scheduledTaskApi.batchDeleteScheduledTasks(ids)
    
    if (result.failCount > 0) {
      ElMessage.warning(`批量删除完成，成功：${result.successCount}，失败：${result.failCount}`)
      console.warn('删除失败的原因：', result.failedReasons)
    } else {
      ElMessage.success(`批量删除成功，共删除 ${result.successCount} 个任务`)
    }
    
    getTaskList()
    selectedTasks.value = []
  } catch (error: any) {
    if (error !== 'cancel') {
      console.error('批量删除失败:', error)
      ElMessage.error('批量删除失败')
    }
  }
}

// 执行任务
const handleExecute = async (row: ScheduledTaskListDto) => {
  try {
    await ElMessageBox.confirm(
      `确定要立即执行任务 "${row.name}" 吗？`,
      '执行确认',
      {
        confirmButtonText: '确定',
        cancelButtonText: '取消',
        type: 'info'
      }
    )
    
    await scheduledTaskApi.executeScheduledTask(row.id)
    ElMessage.success('任务执行成功')
    getTaskList()
  } catch (error: any) {
    if (error !== 'cancel') {
      console.error('任务执行失败:', error)
      ElMessage.error('任务执行失败')
    }
  }
}

// Cron表达式变化处理
const handleCronChange = (value: string) => {
  taskForm.cronDescription = getCronDescription(value)
}

// 日志搜索
const handleLogSearch = () => {
  logPagination.currentPage = 1
  getLogList()
}

// 日志日期范围变化
const handleLogDateChange = (dates: [string, string] | null) => {
  if (dates) {
    logSearchForm.startTime = dates[0]
    logSearchForm.endTime = dates[1]
  } else {
    logSearchForm.startTime = ''
    logSearchForm.endTime = ''
  }
}

// 显示Cron帮助
const showCronHelper = () => {
  ElMessageBox.alert(
    `Cron表达式格式：秒 分 时 日 月 周\n\n常用示例：\n• 0 0 12 * * ? - 每天12点执行\n• 0 */5 * * * ? - 每5分钟执行\n• 0 0 0 * * ? - 每天0点执行\n• 0 0 9-17 * * MON-FRI - 工作日9-17点每小时执行\n• 0 0 0 1 * ? - 每月1号0点执行`,
    'Cron表达式帮助',
    {
      confirmButtonText: '确定'
    }
  )
}

// 选择变化
const handleSelectionChange = (selection: ScheduledTaskListDto[]) => {
  selectedTasks.value = selection
}

// 分页变化
const handleSizeChange = (size: number) => {
  pagination.pageSize = size
  pagination.currentPage = 1
  getTaskList()
}

const handleCurrentChange = (page: number) => {
  pagination.currentPage = page
  getTaskList()
}

// 日志分页变化
const handleLogSizeChange = (size: number) => {
  logPagination.pageSize = size
  logPagination.currentPage = 1
  getLogList()
}

const handleLogCurrentChange = (page: number) => {
  logPagination.currentPage = page
  getLogList()
}

// 日志相关方法
// 查看日志
const handleViewLogs = (row: ScheduledTaskListDto) => {
  logSearchForm.taskId = row.id
  logSearchForm.success = undefined
  logSearchForm.startTime = ''
  logSearchForm.endTime = ''
  logDateRange.value = null
  logPagination.currentPage = 1
  logPagination.pageSize = 10
  logPagination.total = 0
  logList.value = []
  logDialogVisible.value = true
  getLogList()
}

// 获取日志列表
const getLogList = async () => {
  try {
    logLoading.value = true
    const params = {
      taskId: logSearchForm.taskId,
      page: logPagination.currentPage,
      size: logPagination.pageSize,
      success: logSearchForm.success,
      startTime: logSearchForm.startTime || undefined,
      endTime: logSearchForm.endTime || undefined
    }
    
    const response = await scheduledTaskApi.getScheduledTaskLogList(params)
    
    // 处理响应数据结构
    if (response && response.data) {
      logList.value = response.data || []
      logPagination.total = response.total || 0
    } else {
      logList.value = []
      logPagination.total = 0
    }
  } catch (error) {
    console.error('获取日志列表失败:', error)
    ElMessage.error('获取日志列表失败')
    logList.value = []
    logPagination.total = 0
  } finally {
    logLoading.value = false
  }
}

// 更新Cron表达式说明生成函数
const getCronDescription = (cron: string) => {
  if (!cron) return '-'
  
  const cronMap: Record<string, string> = {
    // 秒级
    '* * * * * ?': '每秒执行',
    '*/5 * * * * ?': '每5秒执行',
    '*/10 * * * * ?': '每10秒执行',
    '*/30 * * * * ?': '每30秒执行',
    
    // 分钟级
    '0 * * * * ?': '每分钟执行',
    '0 */2 * * * ?': '每2分钟执行',
    '0 */5 * * * ?': '每5分钟执行',
    '0 */10 * * * ?': '每10分钟执行',
    '0 */15 * * * ?': '每15分钟执行',
    '0 */20 * * * ?': '每20分钟执行',
    '0 */30 * * * ?': '每30分钟执行',
    
    // 小时级
    '0 0 * * * ?': '每小时执行',
    '0 0 */2 * * ?': '每2小时执行',
    '0 0 */3 * * ?': '每3小时执行',
    '0 0 */4 * * ?': '每4小时执行',
    '0 0 */6 * * ?': '每6小时执行',
    '0 0 */8 * * ?': '每8小时执行',
    '0 0 */12 * * ?': '每12小时执行',
    
    // 每天特定时间
    '0 0 0 * * ?': '每天0点执行',
    '0 0 1 * * ?': '每天1点执行',
    '0 0 6 * * ?': '每天6点执行',
    '0 0 8 * * ?': '每天8点执行',
    '0 0 9 * * ?': '每天9点执行',
    '0 0 12 * * ?': '每天12点执行',
    '0 0 18 * * ?': '每天18点执行',
    '0 0 22 * * ?': '每天22点执行',
    
    // 工作日
    '0 0 9 ? * MON-FRI': '工作日9点执行',
    '0 0 18 ? * MON-FRI': '工作日18点执行',
    '0 0 * ? * MON-FRI': '工作日每小时执行',
    '0 */30 * ? * MON-FRI': '工作日每30分钟执行',
    
    // 周末
    '0 0 9 ? * SAT,SUN': '周末9点执行',
    '0 0 18 ? * SAT,SUN': '周末18点执行',
    
    // 每周特定日期
    '0 0 0 ? * MON': '每周一0点执行',
    '0 0 0 ? * TUE': '每周二0点执行',
    '0 0 0 ? * WED': '每周三0点执行',
    '0 0 0 ? * THU': '每周四0点执行',
    '0 0 0 ? * FRI': '每周五0点执行',
    '0 0 0 ? * SAT': '每周六0点执行',
    '0 0 0 ? * SUN': '每周日0点执行',
    
    // 每月
    '0 0 0 1 * ?': '每月1号0点执行',
    '0 0 0 15 * ?': '每月15号0点执行',
    '0 0 0 L * ?': '每月最后一天0点执行',
    
    // 每年
    '0 0 0 1 1 ?': '每年1月1号0点执行',
    '0 0 0 31 12 ?': '每年12月31号0点执行'
  }
  
  return cronMap[cron] || '自定义表达式'
}

// 提升作用域：让 onMounted 与 onUnmounted 使用相同函数引用
const calculateTableHeight = () => {
  const windowHeight = window.innerHeight
  const headerHeight = 60 // 顶部导航高度
  const cardHeaderHeight = 80 // 卡片头部高度
  const searchSectionHeight = 100 // 搜索区域高度
  const paginationHeight = 60 // 分页高度
  const padding = 80 // 其他间距
  
  tableHeight.value = windowHeight - headerHeight - cardHeaderHeight - searchSectionHeight - paginationHeight - padding
  logTableHeight.value = 300 // 日志表格固定高度
}

// 组件挂载时获取数据
onMounted(() => {
  getTaskList()
  // 计算表格高度并注册监听
  calculateTableHeight()
  window.addEventListener('resize', calculateTableHeight)
})

// 清理事件监听
onUnmounted(() => {
  // 移除监听使用同一个函数引用
  window.removeEventListener('resize', calculateTableHeight)
})
</script>

<style scoped>
.scheduled-task-management-container {
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
  padding: 16px 20px;
  background-color: #f8f9fa;
  border-radius: 6px;
  border: 1px solid #e4e7ed;
}

.search-buttons {
  display: flex;
  gap: 10px;
  align-items: center;
}

.action-buttons {
  display: flex;
  gap: 10px;
  justify-content: flex-end;
  margin-bottom: 16px;
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

/* 表格容器样式 */
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

/* 日志弹窗样式优化 */
.log-dialog :deep(.el-dialog) {
  margin-top: 5vh !important;
}

.log-dialog :deep(.el-dialog__body) {
  padding: 10px 20px;
  height: 75vh;
  overflow: hidden;
}

.log-dialog-content {
  height: 100%;
  display: flex;
  flex-direction: column;
  gap: 15px;
}

.log-search-section {
  padding: 15px;
  background-color: #f8f9fa;
  border-radius: 4px;
  flex-shrink: 0;
}

.log-table-container {
  flex: 1;
  overflow: hidden;
  border: 1px solid #ebeef5;
  border-radius: 4px;
}

.log-table-container .el-table {
  height: 100%;
}

.log-dialog .el-pagination {
  justify-content: center;
  margin-top: 0;
  flex-shrink: 0;
  padding: 10px 0;
}

.form-tip {
  font-size: 12px;
  color: #909399;
  margin-top: 4px;
}

/* 响应式设计 */
@media (max-width: 1200px) {
  .scheduled-task-management-container {
    padding: 16px;
  }
  
  .search-section {
    padding: 16px;
  }
  
  .log-dialog :deep(.el-dialog) {
    width: 90% !important;
    margin-top: 3vh !important;
  }
  
  .log-dialog :deep(.el-dialog__body) {
    height: 80vh;
  }
}

@media (max-width: 768px) {
  .scheduled-task-management-container {
    padding: 12px;
  }
  
  .search-section {
    padding: 12px;
  }
  
  .el-col {
    margin-bottom: 12px;
  }
  
  .action-buttons {
    justify-content: flex-start;
  }
  
  .log-dialog :deep(.el-dialog) {
    width: 95% !important;
    margin-top: 2vh !important;
  }
  
  .log-search-section .el-row {
    flex-direction: column;
  }
  
  .log-search-section .el-col {
    width: 100% !important;
    margin-bottom: 10px;
  }
}
</style>