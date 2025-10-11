<template>
  <div class="notification-management-container">
    <el-card>
      <template #header>
        <div class="card-header">
          <h2>通知管理</h2>
          <p>管理系统中的所有通知</p>
        </div>
      </template>
      
      <!-- 搜索和操作区域 -->
      <div class="search-section">
        <!-- 第一行：搜索控件 + 搜索/重置按钮 -->
        <el-row :gutter="20" type="flex" align="middle">
          <el-col :span="5">
            <el-input
              v-model="searchForm.title"
              placeholder="搜索通知标题"
              prefix-icon="Search"
              clearable
            />
          </el-col>
          <el-col :span="4">
            <el-select
              v-model="searchForm.type"
              placeholder="通知类型"
              clearable
              style="width: 100%"
            >
              <el-option label="通知" value="notice" />
              <el-option label="提醒" value="remind" />
              <el-option label="告警" value="alert" />
            </el-select>
          </el-col>
          <el-col :span="4">
            <el-select
              v-model="searchForm.isRead"
              placeholder="阅读状态"
              clearable
              style="width: 100%"
            >
              <el-option label="已读" :value="1" />
              <el-option label="未读" :value="0" />
            </el-select>
          </el-col>
          <el-col :span="4">
            <el-select
              v-model="searchForm.sendStatus"
              placeholder="发送状态"
              clearable
              style="width: 100%"
            >
              <el-option label="未发送" :value="0" />
              <el-option label="已发送" :value="1" />
              <el-option label="发送失败" :value="2" />
            </el-select>
          </el-col>
          <el-col :span="7">
            <div class="search-actions">
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
        </el-row>
      
      <!-- 第二行：右侧操作按钮，防止与第一行栅格相加溢出 -->
      <el-row :gutter="20" type="flex" justify="end" class="actions-row">
        <el-col :span="24" style="text-align: left">
          <el-button type="success" @click="handleAdd">
            <el-icon><Plus /></el-icon>
            新增
          </el-button>
          <el-button 
            type="info" 
            @click="handleBatchMarkAsRead"
            :disabled="!selectedNotifications.length"
          >
            <el-icon><Check /></el-icon>
            批量已读 ({{ selectedNotifications.length }})
          </el-button>
          <el-button
            type="primary"
            @click="handleBatchSend"
            :disabled="!selectedNotifications.length"
          >
            批量发送 ({{ selectedNotifications.length }})
          </el-button>
          <el-button 
            type="danger" 
            @click="handleBatchDelete"
            :disabled="!selectedNotifications.length"
          >
            <el-icon><Delete /></el-icon>
            批量删除 ({{ selectedNotifications.length }})
          </el-button>
        </el-col>
      </el-row>
      </div>
    
      <!-- 表格容器 -->
      <div class="table-container">
        <!-- 通知列表表格 -->
        <el-table
          ref="tableRef"
          :data="notificationList"
          v-loading="loading"
          :height="tableHeight"
          stripe
          border
          style="width: 100%"
          @selection-change="handleSelectionChange"
        >
          <el-table-column type="selection" width="40" />
          <el-table-column prop="id" label="ID" width="50" />
          <el-table-column prop="isRead" label="阅读状态" width="90">
            <template #default="scope">
              <el-tag 
                :type="scope.row.isRead === 1 ? 'success' : 'warning'"
                size="small"
              >
                {{ scope.row.isRead === 1 ? '已读' : '未读' }}
              </el-tag>
            </template>
          </el-table-column>
          <el-table-column prop="name" label="通知标题" min-width="150" />
          <el-table-column prop="content" label="通知内容" min-width="300">
            <template #default="scope">
              <div class="content-cell">
                {{ scope.row.content && scope.row.content.length > 100 ? scope.row.content.substring(0, 100) + '...' : scope.row.content }}
              </div>
            </template>
          </el-table-column>
          <el-table-column prop="notifyType" label="通知类型" width="90">
            <template #default="scope">
              <el-tag :type="getTypeTagType(scope.row.notifyType)" size="small">
                {{ getTypeLabel(scope.row.notifyType) }}
              </el-tag>
            </template>
          </el-table-column>
          <!-- 新增：发送状态列 -->
          <el-table-column prop="sendStatus" label="发送状态" width="110">
            <template #default="scope">
              <el-tag :type="getSendStatusTagType(scope.row.sendStatus)" size="small">
                {{ getSendStatusLabel(scope.row.sendStatus) }}
              </el-tag>
            </template>
          </el-table-column>
          <el-table-column prop="sendUser" label="发送人" width="90" />
          <el-table-column prop="receiver" label="接收人" min-width="160" :show-overflow-tooltip="true">
            <template #default="scope">
              <span class="receiver-ellipsis">{{ formatReceiverList(scope.row.receiver) }}</span>
            </template>
          </el-table-column>
          <el-table-column prop="sendTime" label="发送时间" width="160">
            <template #default="scope">
              {{ formatDate(scope.row.sendTime) }}
            </template>
          </el-table-column>
          <el-table-column prop="remark" label="备注" width="150">
            <template #default="scope">
              <div class="content-cell">
                {{ scope.row.remark || '-' }}
              </div>
            </template>
          </el-table-column>
          <el-table-column label="操作" width="200" fixed="right">
            <template #default="scope">
              <el-button
                type="success"
                size="small"
                :disabled="scope.row.sendStatus === 1"
                @click="handleSend(scope.row)"
              >
                发送
              </el-button>
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
      :title="isEdit ? '编辑通知' : '新增通知'"
      width="600px"
      :close-on-click-modal="false"
    >
      <el-form
        ref="notificationFormRef"
        :model="notificationForm"
        :rules="notificationRules"
        label-width="100px"
        size="default"
      >
        <el-form-item label="通知标题" prop="title">
          <el-input
            v-model="notificationForm.title"
            placeholder="请输入通知标题"
            maxlength="36"
            show-word-limit
          />
        </el-form-item>
        
        <el-form-item label="通知内容" prop="content">
          <el-input
            v-model="notificationForm.content"
            type="textarea"
            :rows="4"
            placeholder="请输入通知内容"
            maxlength="400"
            show-word-limit
          />
        </el-form-item>
        
        <el-form-item label="通知类型" prop="type">
          <el-select
            v-model="notificationForm.type"
            placeholder="请选择通知类型"
            style="width: 100%"
          >
            <el-option label="通知" value="notice" />
            <el-option label="提醒" value="remind" />
            <el-option label="告警" value="alert" />
          </el-select>
        </el-form-item>
        <!-- 新增：发送状态选择（默认未发送） -->
        <el-form-item label="发送状态" prop="sendStatus">
          <el-select v-model="notificationForm.sendStatus" placeholder="请选择发送状态" style="width: 100%">
            <el-option label="未发送" :value="0" />
            <el-option label="已发送" :value="1" />
            <el-option label="发送失败" :value="2" />
          </el-select>
        </el-form-item>
        
        <!-- 模板：发送人与接收人表单项 -->
        <!-- 发送人：禁用编辑，显示当前登录用户 -->
        <el-form-item label="发送人" prop="sendUser">
          <el-input 
            v-model="notificationForm.sendUser" 
            placeholder="当前登录用户"
            :disabled="true"
          />
        </el-form-item>
        
        <!-- 接收人：改为多选下拉，来自用户管理 -->
        <el-form-item label="接收人" prop="receiver">
          <el-select
            v-model="notificationForm.receiver"
            multiple
            filterable
            placeholder="请选择接收人"
            :loading="receiverLoading"
            style="width: 100%"
          >
            <el-option
              v-for="opt in receiverOptions"
              :key="opt.value"
              :label="opt.label"
              :value="opt.value"
            />
          </el-select>
        </el-form-item>
        
        <el-form-item label="备注" prop="remark">
          <el-input
            v-model="notificationForm.remark"
            type="textarea"
            :rows="2"
            placeholder="请输入备注信息（可选）"
            maxlength="200"
            show-word-limit
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
  </div>
</template>

<script setup lang="ts">
// 统一并保留正确的类型映射（移除重复旧版，修复 L279/L285）
const normalizeType = (type: string) => {
  const t = (type || '').toLowerCase()
  if (t === 'system' || t === 'user') return 'notice'
  if (t === 'warning' || t === 'error') return 'alert'
  if (['notice', 'remind', 'alert'].includes(t)) return t
  return 'notice'
}

const getTypeTagType = (type: string) => {
  const t = normalizeType(type)
  const map: Record<string, string> = { notice: 'info', remind: 'warning', alert: 'danger' }
  return map[t] || 'info'
}

const getTypeLabel = (type: string) => {
  const t = normalizeType(type)
  const map: Record<string, string> = { notice: '通知', remind: '提醒', alert: '告警' }
  return map[t] || t
}

// 新增：发送状态映射函数（用于表格列）
const getSendStatusTagType = (status?: number) => {
  const map: Record<number, string> = { 0: 'warning', 1: 'success', 2: 'danger' }
  return map[status ?? 0] || 'info'
}

const getSendStatusLabel = (status?: number) => {
  const map: Record<number, string> = { 0: '未发送', 1: '已发送', 2: '发送失败' }
  return map[status ?? 0] || '未发送'
}
import {
  notificationApi,
  type NotificationCreateDto,
  type NotificationListDto,
  type NotificationSearchParams,
  type NotificationUpdateDto
} from '@/api/notification'
import { tagApi, type Tag } from '@/api/tag'
import { userApi, type User } from '@/api/user'
import { useAuthStore } from '@/stores/auth'
import {
  Check,
  Delete,
  Plus,
  Refresh,
  Search
} from '@element-plus/icons-vue'
import type { FormInstance } from 'element-plus'
import { ElMessage, ElMessageBox, ElTable } from 'element-plus'
import { nextTick, onMounted, onUnmounted, reactive, ref } from 'vue'

interface NotificationFormData {
  id?: number
  title: string
  content: string
  type: string
  sendUser: string
  receiver: string[]
  remark: string
  // 新增：发送状态
  sendStatus: number
}

// 响应式数据
const loading = ref(false)
const submitting = ref(false)
const tableRef = ref<InstanceType<typeof ElTable>>()
const notificationFormRef = ref<FormInstance>()
const tableHeight = ref(400)
const selectedNotifications = ref<NotificationListDto[]>([])

// 搜索表单
const searchForm = reactive({
  title: '',
  type: '',
  isRead: undefined as number | undefined,
  // 新增：发送状态搜索
  sendStatus: undefined as number | undefined
})

// 通知列表
const notificationList = ref<NotificationListDto[]>([])

// 分页
const pagination = reactive({
  currentPage: 1,
  pageSize: 10,
  total: 0
})

// 对话框相关
const dialogVisible = ref(false)
const isEdit = ref(false)
// 声明 authStore
const authStore = useAuthStore()

// 新增：标签名称映射与用户详情映射
const userTypeNameMap = ref<Record<string, string>>({})
const systemNameMap = ref<Record<string, string>>({})
const userDetailMap = ref<Record<string, { userTypeCode?: string | null; userToSystemCode?: string | null }>>({})

const getUserTypeName = (code?: string | null) => {
  if (!code) return '-'
  return userTypeNameMap.value[code] || code
}

const getSystemName = (code?: string | null) => {
  if (!code) return '-'
  return systemNameMap.value[code] || code
}

const buildReceiverLabel = (userName: string) => {
  const detail = userDetailMap.value[userName]
  const typeName = getUserTypeName(detail?.userTypeCode)
  const sysName = getSystemName(detail?.userToSystemCode)
  return `${userName} [${typeName} - ${sysName}]`
}

// 接收人下拉数据源与加载状态
const receiverOptions = ref<{ label: string; value: string }[]>([])
const receiverLoading = ref(false)

// 加载接收人选项：先拉取标签名称映射，再拉取用户列表
const loadReceiverOptions = async () => {
  receiverLoading.value = true
  try {
    const [userTypeRes, systemRes] = await Promise.all([
      tagApi.getTagsByModule('用户管理-用户类型'),
      tagApi.getTagsByModule('用户管理-所属系统')
    ])
    const userTypeTags = Array.isArray(userTypeRes?.data) ? userTypeRes.data : []
    const systemTags = Array.isArray(systemRes?.data) ? systemRes.data : []

    userTypeNameMap.value = userTypeTags.reduce((acc: Record<string, string>, t: Tag) => {
      acc[t.tagCode] = t.tagName
      return acc
    }, {})
    systemNameMap.value = systemTags.reduce((acc: Record<string, string>, t: Tag) => {
      acc[t.tagCode] = t.tagName
      return acc
    }, {})

    const res = await userApi.getUserList({ page: 1, size: 1000 })
    const list = Array.isArray(res?.data) ? res.data : []

    // 建立用户名到详情的映射，供列表显示与下拉标签使用
    userDetailMap.value = {}
    list.forEach((u: User) => {
      userDetailMap.value[u.userName] = {
        userTypeCode: u.userTypeCode || undefined,
        userToSystemCode: u.userToSystemCode || undefined
      }
    })

    receiverOptions.value = list.map((u: User) => ({
      value: u.userName,
      label: buildReceiverLabel(u.userName)
    }))
  } catch (e) {
    receiverOptions.value = []
  } finally {
    receiverLoading.value = false
  }
}

// 列表中显示接收人（兼容历史数据）
const formatReceiverList = (receiver: string | string[]) => {
  const names = Array.isArray(receiver)
    ? receiver
    : (receiver || '').split(',').filter(Boolean)
  if (!names.length) return '-'
  return names.map((n) => buildReceiverLabel(n)).join(', ')
}

// 通知表单
const notificationForm = reactive<NotificationFormData>({
  title: '',
  content: '',
  type: 'notice',
  sendUser: '',
  receiver: [] as string[],
  remark: '',
  sendStatus: 0
})

// 表单验证规则
const notificationRules = {
  title: [
    { required: true, message: '请输入通知标题', trigger: 'blur' },
    { min: 1, max: 36, message: '通知标题长度在 1 到 36 个字符', trigger: 'blur' }
  ],
  content: [
    { required: true, message: '请输入通知内容', trigger: 'blur' },
    { min: 1, max: 400, message: '通知内容长度在 1 到 400 个字符', trigger: 'blur' }
  ],
  type: [
    { required: true, message: '请选择通知类型', trigger: 'change' }
  ],
  sendUser: [
    { required: true, message: '请输入发送人', trigger: 'blur' }
  ],
  receiver: [
    { validator: (_: any, value: string[], cb: Function) => {
        if (!value || value.length === 0) cb(new Error('请至少选择一个接收人'))
        else cb()
      }, trigger: 'change' }
  ],
  remark: [
    { max: 200, message: '备注长度不能超过200个字符', trigger: 'blur' }
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

// API调用方法
const loadNotificationList = async () => {
  try {
    loading.value = true
    const params: NotificationSearchParams = {
      page: pagination.currentPage,
      pageSize: pagination.pageSize,
      Name: searchForm.title,
      NotifyType: searchForm.type,
      IsRead: searchForm.isRead,
      SendStatus: searchForm.sendStatus
    }
    const response = await notificationApi.getNotificationList(params)
    notificationList.value = response.data
    pagination.total = response.total
  } catch (error) {
    ElMessage.error('加载通知列表失败')
  } finally {
    loading.value = false
  }
}

// 提交表单
const handleSubmit = async () => {
  if (!notificationFormRef.value) return
  try {
    await notificationFormRef.value.validate()
    submitting.value = true
    const submitData: NotificationCreateDto = {
      name: notificationForm.title,
      content: notificationForm.content,
      notifyType: notificationForm.type,
      sendTime: new Date().toISOString(),
      sendUser: notificationForm.sendUser,
      receiver: notificationForm.receiver.join(','),
      remark: notificationForm.remark,
      notifyStatus: 0,
      seqNo: 0,
      // 新增：发送状态
      sendStatus: notificationForm.sendStatus
    }
    if (isEdit.value) {
      const updateData: NotificationUpdateDto = {
      id: notificationForm.id!,
      name: notificationForm.title,
      content: notificationForm.content,
      notifyType: notificationForm.type,
      sendUser: notificationForm.sendUser,
      receiver: notificationForm.receiver.join(','),
      remark: notificationForm.remark,
      sendStatus: notificationForm.sendStatus
    }
    await notificationApi.updateNotification(updateData)
    ElMessage.success('更新成功')
    } else {
      await notificationApi.createNotification(submitData)
      ElMessage.success('创建成功')
    }
    dialogVisible.value = false
    await loadNotificationList()
  } catch (error) {
    ElMessage.error('操作失败')
  } finally {
    submitting.value = false
  }
}

// 删除通知
const handleDelete = async (row: NotificationListDto) => {
  try {
    await ElMessageBox.confirm(
      `确定要删除通知 "${row.name}" 吗？`,
      '确认删除',
      {
        confirmButtonText: '确定',
        cancelButtonText: '取消',
        type: 'warning'
      }
    )
    
    await notificationApi.deleteNotification(row.id)
    ElMessage.success('删除成功')
    await loadNotificationList()
  } catch (error) {
    if (error !== 'cancel') {
      console.error('删除通知失败:', error)
      ElMessage.error('删除通知失败')
    }
  }
}

// 批量删除
const handleBatchDelete = async () => {
  if (selectedNotifications.value.length === 0) {
    ElMessage.warning('请选择要删除的通知')
    return
  }

  try {
    await ElMessageBox.confirm(
      `确定要删除选中的 ${selectedNotifications.value.length} 个通知吗？`,
      '确认批量删除',
      {
        confirmButtonText: '确定',
        cancelButtonText: '取消',
        type: 'warning'
      }
    )
    
    const ids = selectedNotifications.value.map(notification => notification.id)
    await notificationApi.batchDeleteNotifications(ids)
    ElMessage.success('批量删除成功')
    selectedNotifications.value = []
    await loadNotificationList()
  } catch (error) {
    if (error !== 'cancel') {
      console.error('批量删除失败:', error)
      ElMessage.error('批量删除失败')
    }
  }
}
// 单条发送
const handleSend = async (row: NotificationListDto) => {
  try {
    if (row.sendStatus === 1) {
      ElMessage.info('该通知已发送')
      return
    }
    await ElMessageBox.confirm(
      `确定发送通知 "${row.name}" 吗？`,
      '确认发送',
      {
        confirmButtonText: '确定',
        cancelButtonText: '取消',
        type: 'warning'
      }
    )
    await notificationApi.sendNotification(row.id)
    ElMessage.success('发送成功')
    await loadNotificationList()
  } catch (error) {
    if (error !== 'cancel') {
      console.error('发送通知失败:', error)
      ElMessage.error('发送通知失败')
    }
  }
}

// 批量发送
const handleBatchSend = async () => {
  if (!selectedNotifications.value.length) {
    ElMessage.warning('请选择要发送的通知')
    return
  }
  try {
    await ElMessageBox.confirm(
      `确定要发送选中的 ${selectedNotifications.value.length} 个通知吗？`,
      '确认批量发送',
      {
        confirmButtonText: '确定',
        cancelButtonText: '取消',
        type: 'warning'
      }
    )
    const ids = selectedNotifications.value.map(n => n.id)
    await notificationApi.batchSendNotifications(ids)
    ElMessage.success('批量发送成功')
    selectedNotifications.value = []
    tableRef.value?.clearSelection()
    await loadNotificationList()
  } catch (error) {
    if (error !== 'cancel') {
      console.error('批量发送失败:', error)
      ElMessage.error('批量发送失败')
    }
  }
}
// 标记为已读
const handleMarkAsRead = async (row: NotificationListDto) => {
  try {
    await notificationApi.markAsRead(row.id)
    ElMessage.success('标记已读成功')
    await loadNotificationList()
  } catch (error) {
    console.error('标记已读失败:', error)
    ElMessage.error('标记已读失败')
  }
}

// 批量标记为已读
const handleBatchMarkAsRead = async () => {
  if (selectedNotifications.value.length === 0) {
    ElMessage.warning('请选择要标记为已读的通知')
    return
  }

  try {
    const ids = selectedNotifications.value.map(notification => notification.id)
    await notificationApi.batchMarkAsRead(ids)
    ElMessage.success('批量标记已读成功')
    selectedNotifications.value = []
    await loadNotificationList()
  } catch (error) {
    console.error('批量标记已读失败:', error)
    ElMessage.error('批量标记已读失败')
  }
}

// 处理每页显示数量变化
const handleSizeChange = async (val: number) => {
  pagination.pageSize = val
  pagination.currentPage = 1
  await loadNotificationList()
}

// 处理当前页变化
const handleCurrentChange = async (val: number) => {
  pagination.currentPage = val
  await loadNotificationList()
}

// 搜索
const handleSearch = async () => {
  pagination.currentPage = 1
  await loadNotificationList()
}

// 重置搜索
const handleReset = async () => {
  searchForm.title = ''
  searchForm.type = ''
  searchForm.isRead = undefined
  searchForm.sendStatus = undefined
  pagination.currentPage = 1
  await loadNotificationList()
}

// 处理表格选择变化
const handleSelectionChange = (val: NotificationListDto[]) => {
  selectedNotifications.value = val
}

// 新增通知
const handleAdd = () => {
  isEdit.value = false
  Object.assign(notificationForm, {
    title: '',
    content: '',
    type: 'notice',
    sendUser: authStore.user?.userName || '',
    receiver: [] as string[],
    remark: '',
    // 新增：默认未发送
    sendStatus: 0
  })
  dialogVisible.value = true
}
// 编辑通知时确保选项已加载
const handleEdit = async (row: NotificationListDto) => {
  isEdit.value = true
  if (!receiverOptions.value.length) {
    await loadReceiverOptions()
  }
  Object.assign(notificationForm, {
    id: row.id,
    title: row.name,
    content: row.content,
    type: normalizeType(row.notifyType),
    sendUser: row.sendUser,
    receiver: (row.receiver || '').split(',').filter(Boolean),
    remark: row.remark || '',
    // 新增：回填发送状态（兼容后端返回）
    sendStatus: typeof row.sendStatus === 'number' ? row.sendStatus : 0
  })
  dialogVisible.value = true
}

// 组件生命周期
onMounted(() => {
  loadNotificationList()
  calculateTableHeight()
  loadReceiverOptions()
  notificationForm.sendUser = authStore.user?.userName || ''
  window.addEventListener('resize', calculateTableHeight)
})

onUnmounted(() => {
  window.removeEventListener('resize', calculateTableHeight)
})
</script>

<style scoped>
.notification-management-container {
  padding: 20px;
  height: calc(100vh - 60px);
  overflow-y: auto;
}

.card-header {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.card-header h2 {
  margin: 0;
  font-size: 20px;
  font-weight: 600;
  color: #303133;
}

.card-header p {
  margin: 0;
  font-size: 14px;
  color: #909399;
}

.search-section {
  margin-bottom: 20px;
  padding: 20px;
  background-color: #f8f9fa;
  border-radius: 8px;
}
.search-actions {
  display: flex;
  align-items: center;
  gap: 10px;
}
.actions-row {
  margin-top: 8px;
}
.table-container {
  background-color: #fff;
  border-radius: 8px;
  overflow: hidden;
}

.content-cell {
  word-break: break-word;
  line-height: 1.4;
}

.el-pagination {
  margin-top: 20px;
  justify-content: center;
}

.dialog-footer {
  display: flex;
  justify-content: flex-end;
  gap: 12px;
}

/* 响应式设计 */
@media (max-width: 1200px) {
  .notification-management-container {
    padding: 16px;
  }
  
  .search-section {
    padding: 16px;
  }
}

@media (max-width: 768px) {
  .notification-management-container {
    padding: 12px;
  }
  
  .search-section {
    padding: 12px;
  }
  
  .el-col {
    margin-bottom: 12px;
  }
}

.receiver-ellipsis { 
  display: block; 
  overflow: hidden; 
  text-overflow: ellipsis; 
  white-space: nowrap; 
}
</style>