<template>
  <div class="user-management-container">
    <el-card>
      <template #header>
        <div class="card-header">
          <h2>用户管理</h2>
          <p>管理系统中的所有用户</p>
        </div>
      </template>
      
      <!-- 搜索和操作区域 -->
      <div class="search-section">
        <el-row :gutter="20">
          <el-col :span="5">
            <el-input
            v-model="searchForm.username"
            placeholder="搜索用户名"
            :prefix-icon="Search"
            clearable
          />
          </el-col>
          <el-col :span="5">
            <el-input
            v-model="searchForm.email"
            placeholder="搜索邮箱"
            :prefix-icon="Message"
            clearable
          />
          </el-col>
          <el-col :span="4">
            <el-select
              v-model="searchForm.isActive"
              placeholder="用户状态"
              clearable
            >
              <el-option label="激活" :value="true" />
              <el-option label="禁用" :value="false" />
            </el-select>
          </el-col>
          <el-col :span="5">
            <el-button type="primary" @click="handleSearch">
              <el-icon><Search /></el-icon>
              搜索
            </el-button>
            <el-button @click="resetSearch">
              <el-icon><Refresh /></el-icon>
              重置
            </el-button>
          </el-col>
          <el-col :span="5" style="text-align: right">
            <el-button type="success" @click="handleAdd">
              <el-icon><Plus /></el-icon>
              新增
            </el-button>
          </el-col>
        </el-row>
      </div>
      
      <!-- 表格容器 -->
      <div class="table-container">
        <!-- 用户列表表格 -->
        <el-table
          :data="userList"
          v-loading="loading"
          stripe
          style="width: 100%"
          :height="tableHeight"
        >
          <el-table-column prop="id" label="ID" width="40" />
          <el-table-column label="用户头像" width="80">
            <template #default="scope">
              <el-avatar 
                :size="40" 
                :src="scope.row.profilePictureUrl" 
                :alt="scope.row.userName"
              >
                <el-icon><User /></el-icon>
              </el-avatar>
            </template>
          </el-table-column>
          <el-table-column prop="userName" label="用户名" width="100" />
          <el-table-column prop="email" label="邮箱" min-width="120" />
          <el-table-column prop="phoneNumber" label="电话号码" width="120">
            <template #default="scope">
              {{ scope.row.phoneNumber || '-' }}
            </template>
          </el-table-column>
          <el-table-column prop="bio" label="个人简介" min-width="180">
            <template #default="scope">
              <el-tooltip 
                :content="scope.row.bio || '暂无简介'" 
                placement="top"
                :disabled="!scope.row.bio"
              >
                <span class="bio-text">{{ scope.row.bio || '-' }}</span>
              </el-tooltip>
            </template>
          </el-table-column>
          <!-- 新增：用户类型 -->
          <el-table-column prop="userTypeCode" label="用户类型" width="120">
            <template #default="scope">
              {{ getUserTypeName(scope.row.userTypeCode) || '-' }}
            </template>
          </el-table-column>
          <!-- 新增：所属系统 -->
          <el-table-column prop="userToSystemCode" label="所属系统" width="140">
            <template #default="scope">
              {{ getSystemName(scope.row.userToSystemCode) || '-' }}
            </template>
          </el-table-column>
          <!-- 修正 prop 名称为 crtTime -->
          <el-table-column prop="crtTime" label="创建时间" width="160">
            <template #default="scope">
              {{ formatDate(scope.row.crtTime) }}
            </template>
          </el-table-column>
          <el-table-column prop="isActive" label="状态" width="80">
            <template #default="scope">
              <el-tag :type="scope.row.isActive ? 'success' : 'danger'">
                {{ scope.row.isActive ? '激活' : '禁用' }}
              </el-tag>
            </template>
          </el-table-column>
          <el-table-column label="操作" width="200" fixed="right">
            <template #default="scope">
              <el-button
                type="primary"
                size="small"
                @click="handleEdit(scope.row)"
              >
                编辑
              </el-button>
              <el-button
                type="warning"
                size="small"
                @click="handleChangePassword(scope.row)"
              >
                改密
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
    
    <!-- 新增/编辑用户对话框 -->
    <el-dialog
      v-model="dialogVisible"
      :title="dialogTitle"
      width="500px"
      :before-close="handleDialogClose"
    >
      <el-form
        ref="userFormRef"
        :model="userForm"
        :rules="userRules"
        label-width="80px"
      >
        <el-form-item label="用户名" prop="username">
          <el-input
            v-model="userForm.username"
            placeholder="请输入用户名"
            :disabled="isEdit"
          />
        </el-form-item>
        
        <el-form-item label="邮箱" prop="email">
          <el-input
            v-model="userForm.email"
            placeholder="请输入邮箱"
          />
        </el-form-item>
        
        <el-form-item label="电话号码" prop="phoneNumber">
          <el-input
            v-model="userForm.phoneNumber"
            placeholder="请输入电话号码"
          />
        </el-form-item>
        
        <el-form-item label="个人简介" prop="bio">
          <el-input
            v-model="userForm.bio"
            type="textarea"
            :rows="3"
            placeholder="请输入个人简介"
          />
        </el-form-item>



        <!-- 优化：用户类型（单选下拉，来自标签管理-用户管理-用户类型） -->
        <el-form-item label="用户类型" prop="userTypeCode">
          <el-select
            v-model="userForm.userTypeCode"
            placeholder="请选择用户类型"
            filterable
            clearable
            :loading="tagLoading.userType"
            style="width: 100%"
          >
            <el-option
              v-for="tag in userTypeOptions"
              :key="tag.id"
              :label="tag.tagName"
              :value="tag.tagCode"
            />
          </el-select>
        </el-form-item>

        <!-- 优化：所属系统（单选下拉，来自标签管理-用户管理-所属系统） -->
        <el-form-item label="所属系统" prop="userToSystemCode">
          <el-select
            v-model="userForm.userToSystemCode"
            placeholder="请选择所属系统"
            filterable
            clearable
            :loading="tagLoading.system"
            style="width: 100%"
          >
            <el-option
              v-for="tag in systemOptions"
              :key="tag.id"
              :label="tag.tagName"
              :value="tag.tagCode"
            />
          </el-select>
        </el-form-item>
        
        <el-form-item label="密码" prop="password" v-if="!isEdit">
          <el-input
            v-model="userForm.password"
            type="password"
            placeholder="请输入密码"
            show-password
          />
        </el-form-item>
        
        <el-form-item label="状态" prop="isActive">
          <el-select v-model="userForm.isActive" placeholder="请选择状态">
            <el-option label="激活" :value="true" />
            <el-option label="禁用" :value="false" />
          </el-select>
        </el-form-item>
      </el-form>
      
      <template #footer>
        <span class="dialog-footer">
          <el-button @click="handleDialogClose">取消</el-button>
          <el-button type="primary" @click="handleSubmit" :loading="submitLoading">
            确定
          </el-button>
        </span>
      </template>
    </el-dialog>
    
    <!-- 修改密码对话框 -->
    <el-dialog
      v-model="passwordDialogVisible"
      title="修改密码"
      width="400px"
      :before-close="handlePasswordDialogClose"
    >
      <el-form
        ref="passwordFormRef"
        :model="passwordForm"
        :rules="passwordRules"
        label-width="80px"
      >
        <el-form-item label="用户名">
          <el-input v-model="passwordForm.username" disabled />
        </el-form-item>
        
        <el-form-item label="新密码" prop="newPassword">
          <el-input
            v-model="passwordForm.newPassword"
            type="password"
            placeholder="请输入新密码"
            show-password
          />
        </el-form-item>
        
        <el-form-item label="确认密码" prop="confirmPassword">
          <el-input
            v-model="passwordForm.confirmPassword"
            type="password"
            placeholder="请再次输入新密码"
            show-password
          />
        </el-form-item>
      </el-form>
      
      <template #footer>
        <span class="dialog-footer">
          <el-button @click="handlePasswordDialogClose">取消</el-button>
          <el-button type="primary" @click="handlePasswordSubmit" :loading="submitLoading">
            确定
          </el-button>
        </span>
      </template>
    </el-dialog>
    

  </div>
</template>

<script setup lang="ts">
import { tagApi, type Tag } from '@/api/tag'
import { userApi, type ChangePasswordParams, type UserCreateParams, type UserListParams, type User as UserType, type UserUpdateParams } from '@/api/user'
import { Message, Plus, Refresh, Search, User } from '@element-plus/icons-vue'
import { ElForm, ElMessage, ElMessageBox, type FormRules } from 'element-plus'
import { computed, nextTick, onMounted, onUnmounted, reactive, ref } from 'vue'

// 添加调试信息
console.log('UserManagement component loaded')

// 响应式数据
const loading = ref(false)
const submitLoading = ref(false)
const dialogVisible = ref(false)
const isEdit = ref(false)
const userFormRef = ref<InstanceType<typeof ElForm>>()
// 搜索表单
const searchForm = reactive({
  username: '',
  email: '',
  isActive: undefined as boolean | undefined
})

// 用户列表数据
const userList = ref<UserType[]>([])

// 分页数据
const pagination = reactive({
  currentPage: 1,
  pageSize: 10,
  total: 0
})

// 用户表单数据
const userForm = reactive({
  id: null as number | null,
  username: '',
  email: '',
  phoneNumber: '',
  bio: '',
  userTypeCode: '',        // 改为 code
  userToSystemCode: '',    // 改为 code
  profilePictureUrl: '',   // 新增：头像URL
  password: '',
  isActive: true
})

// 表单验证规则
const userRules: FormRules = {
  username: [
    { required: true, message: '请输入用户名', trigger: 'blur' },
    { min: 3, message: '用户名长度不能少于3位', trigger: 'blur' }
  ],
  email: [
    { required: true, message: '请输入邮箱', trigger: 'blur' },
    { type: 'email', message: '请输入正确的邮箱格式', trigger: 'blur' }
  ],
  phoneNumber: [
    { pattern: /^1[3-9]\d{9}$/, message: '请输入正确的手机号码', trigger: 'blur' }
  ],
  password: [
    { required: true, message: '请输入密码', trigger: 'blur' },
    { min: 6, message: '密码长度不能少于6位', trigger: 'blur' }
  ],
  isActive: [
    { required: true, message: '请选择状态', trigger: 'change' }
  ]
}

// 计算属性
const dialogTitle = computed(() => {
  return isEdit.value ? '编辑用户' : '新增用户'
})

// 方法
const loadUserList = async () => {
  loading.value = true
  try {
    console.log('Loading user list...', {
      page: pagination.currentPage,
      size: pagination.pageSize,
      ...searchForm
    })
    
    const params: UserListParams = {
      page: pagination.currentPage,
      size: pagination.pageSize,
      username: searchForm.username || undefined,
      email: searchForm.email || undefined,
      isActive: searchForm.isActive
    }

    const response = await userApi.getUserList(params)
    console.log('User list response:', response)
    
    // 修复：正确处理响应数据
    if (response) {
      userList.value = response.data || []
      pagination.total = response.total || 0
    } else {
      userList.value = []
      pagination.total = 0
    }
  } catch (error) {
    console.error('Load user list error:', error)
    ElMessage.error('加载用户列表失败')
    userList.value = []
    pagination.total = 0
  } finally {
    loading.value = false
  }
}

const handleSubmit = async () => {
  if (!userFormRef.value) return
  
  await userFormRef.value.validate(async (valid) => {
    if (valid) {
      submitLoading.value = true
      try {
        if (isEdit.value && userForm.id) {
          // 编辑用户
          const updateData: UserUpdateParams = {
            email: userForm.email,
            phoneNumber: userForm.phoneNumber,
            bio: userForm.bio,
            isActive: userForm.isActive,
            userTypeCode: userForm.userTypeCode || undefined,       // 改为 code
            userToSystemCode: userForm.userToSystemCode || undefined, // 改为 code
            profilePictureUrl: userForm.profilePictureUrl || undefined // 新增：头像
          }
          await userApi.updateUser(userForm.id, updateData)
          ElMessage.success('编辑成功')
        } else {
          // 新增用户
          const createData: UserCreateParams = {
            username: userForm.username,
            email: userForm.email,
            password: userForm.password,
            phoneNumber: userForm.phoneNumber || undefined,
            bio: userForm.bio || undefined,
            userTypeCode: userForm.userTypeCode || undefined,        // 改为 code
            userToSystemCode: userForm.userToSystemCode || undefined, // 改为 code
            profilePictureUrl: userForm.profilePictureUrl || undefined // 新增：头像
          }
          await userApi.createUser(createData)
          ElMessage.success('新增成功')
        }
        
        dialogVisible.value = false
        loadUserList()
      } catch (error) {
        ElMessage.error(isEdit.value ? '编辑失败' : '新增失败')
      } finally {
        submitLoading.value = false
      }
    }
  })
}

const resetUserForm = () => {
  Object.assign(userForm, {
    id: null,
    username: '',
    email: '',
    phoneNumber: '',
    bio: '',
    userTypeCode: '',        // 改为 code
    userToSystemCode: '',    // 改为 code
    profilePictureUrl: '',   // 重置头像URL
    password: '',
    isActive: true
  })
  userFormRef.value?.clearValidate()
}

const handleDialogClose = () => {
  dialogVisible.value = false
  resetUserForm()
}

const handleSizeChange = (size: number) => {
  pagination.pageSize = size
  loadUserList()
}

const handleCurrentChange = (page: number) => {
  pagination.currentPage = page
  loadUserList()
}

const formatDate = (dateString: string) => {
  return new Date(dateString).toLocaleString('zh-CN')
}

// 添加表格高度计算
const tableHeight = ref<number | string>('auto')

// 计算表格高度
const calculateTableHeight = () => {
  nextTick(() => {
    // 获取视窗高度
    const windowHeight = window.innerHeight
    // 减去头部、搜索区域、分页等固定高度
    const fixedHeight = 300 // 根据实际情况调整
    const calculatedHeight = windowHeight - fixedHeight
    
    // 设置最小高度，确保表格有足够的显示空间
    tableHeight.value = Math.max(calculatedHeight, 400)
  })
}

// 生命周期
onMounted(async () => {
  console.log('UserManagement component mounted')
  calculateTableHeight()
  // 并行加载两个下拉选项
  await Promise.all([loadUserTypeOptions(), loadSystemOptions()])
  // 选项加载完成后再拉取列表，避免显示 "-"
  await loadUserList()
  window.addEventListener('resize', calculateTableHeight)
})

// 组件卸载时移除事件监听
onUnmounted(() => {
  window.removeEventListener('resize', calculateTableHeight)
})

// 在 loadUserList 方法后添加以下缺失的方法

const handleSearch = () => {
  console.log('Searching users with:', searchForm)
  pagination.currentPage = 1
  loadUserList()
}

const resetSearch = () => {
  console.log('Resetting search')
  searchForm.username = ''
  searchForm.email = ''
  searchForm.isActive = undefined
  pagination.currentPage = 1
  loadUserList()
}

const handleAdd = () => {
  console.log('Adding new user')
  isEdit.value = false
  resetUserForm()
  dialogVisible.value = true
  loadUserTypeOptions()
  loadSystemOptions()
}

const handleEdit = (row: UserType) => {
  console.log('Editing user:', row)
  isEdit.value = true
  Object.assign(userForm, {
    id: row.id,
    username: row.userName,
    email: row.email,
    phoneNumber: row.phoneNumber || '',
    bio: row.bio || '',
    userTypeCode: row.userTypeCode || '',
    userToSystemCode: row.userToSystemCode || '',
    profilePictureUrl: row.profilePictureUrl || '', // 优化：编辑时回填绝对URL
    password: '',
    isActive: row.isActive
  })
  dialogVisible.value = true
  loadUserTypeOptions()
  loadSystemOptions()
}

const handleDelete = async (row: UserType) => {
  try {
    await ElMessageBox.confirm(
      `确定要删除用户 "${row.userName}" 吗？`, // 修正为 userName
      '删除确认',
      {
        confirmButtonText: '确定',
        cancelButtonText: '取消',
        type: 'warning',
      }
    )
    await userApi.deleteUser(row.id)
    ElMessage.success('删除成功')
    loadUserList()
  } catch (error) {
    if (error !== 'cancel') {
      console.error('Delete user error:', error)
      ElMessage.error('删除失败')
    }
  }
}
// 在现有响应式数据后添加
const passwordDialogVisible = ref(false)
const passwordForm = reactive({
  id: null as number | null,
  username: '',
  newPassword: '',
  confirmPassword: ''
})
const passwordFormRef = ref<InstanceType<typeof ElForm>>()

// 密码表单验证规则
const passwordRules: FormRules = {
  newPassword: [
    { required: true, message: '请输入新密码', trigger: 'blur' },
    { min: 6, message: '密码长度不能少于6位', trigger: 'blur' }
  ],
  confirmPassword: [
    { required: true, message: '请确认密码', trigger: 'blur' },
    {
      validator: (rule: any, value: string, callback: any) => {
        if (value !== passwordForm.newPassword) {
          callback(new Error('两次输入的密码不一致'))
        } else {
          callback()
        }
      },
      trigger: 'blur'
    }
  ]
}

// 处理修改密码
const handleChangePassword = (row: UserType) => {
  console.log('Changing password for user:', row)
  Object.assign(passwordForm, {
    id: row.id,
    username: row.userName, // 修正为 userName
    newPassword: '',
    confirmPassword: ''
  })
  passwordDialogVisible.value = true
}

// 重置密码表单
const resetPasswordForm = () => {
  Object.assign(passwordForm, {
    id: null,
    username: '',
    newPassword: '',
    confirmPassword: ''
  })
  passwordFormRef.value?.clearValidate()
}

// 关闭密码对话框
const handlePasswordDialogClose = () => {
  passwordDialogVisible.value = false
  resetPasswordForm()
}

// 提交密码修改
const handlePasswordSubmit = async () => {
  if (!passwordFormRef.value) return
  
  await passwordFormRef.value.validate(async (valid) => {
    if (valid && passwordForm.id) {
      submitLoading.value = true
      try {
        const changePasswordData: ChangePasswordParams = {
          userId: passwordForm.id,
          newPassword: passwordForm.newPassword
        }
        
        await userApi.changePassword(changePasswordData)
        ElMessage.success('密码修改成功')
        passwordDialogVisible.value = false
        resetPasswordForm()
      } catch (error) {
        console.error('Change password error:', error)
        ElMessage.error('密码修改失败')
      } finally {
        submitLoading.value = false
      }
    }
  })
}

// 标签下拉数据源与加载状态
const userTypeOptions = ref<Tag[]>([])
const systemOptions = ref<Tag[]>([])
const tagLoading = reactive({ userType: false, system: false })

const loadUserTypeOptions = async () => {
  tagLoading.userType = true
  try {
    const res = await tagApi.getTagsByModule('用户管理-用户类型')
    userTypeOptions.value = res?.data || []
  } catch (e) {
    console.error('加载用户类型失败:', e)
    userTypeOptions.value = []
  } finally {
    tagLoading.userType = false
  }
}

const loadSystemOptions = async () => {
  tagLoading.system = true
  try {
    const res = await tagApi.getTagsByModule('用户管理-所属系统')
    systemOptions.value = res?.data || []
  } catch (e) {
    console.error('加载所属系统失败:', e)
    systemOptions.value = []
  } finally {
    tagLoading.system = false
  }
}

const getUserTypeName = (code?: string | null) => {
  if (!code) return ''
  const found = userTypeOptions.value.find(t => t.tagCode === code)
  return found?.tagName || ''
}

const getSystemName = (code?: string | null) => {
  if (!code) return ''
  const found = systemOptions.value.find(t => t.tagCode === code)
  return found?.tagName || ''
}
onMounted(() => {
  calculateTableHeight()
  loadUserList()
  window.addEventListener('resize', calculateTableHeight)
})
</script>

<style scoped>
.user-management-container {
  padding: 20px;
  height: calc(100vh - 60px); /* 减去header高度 */
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

.dialog-footer {
  text-align: right;
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

.bio-text {
  display: inline-block;
  max-width: 120px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
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

</style>