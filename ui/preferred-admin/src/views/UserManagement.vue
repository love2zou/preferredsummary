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
        <el-row :gutter="16">
          <el-col :span="4">
            <el-input
              v-model="searchForm.username"
              placeholder="搜索用户名"
              :prefix-icon="Search"
              clearable
            />
          </el-col>
          <!-- 新增：按 FullName 搜索 -->
          <el-col :span="4">
            <el-input
              v-model="searchForm.fullName"
              placeholder="搜索姓名"
              :prefix-icon="User"
              clearable
            />
          </el-col>
          <el-col :span="4">
            <el-input
              v-model="searchForm.email"
              placeholder="搜索邮箱"
              :prefix-icon="Message"
              clearable
            />
          </el-col>
          <el-col :span="3">
            <el-select
              v-model="searchForm.isActive"
              placeholder="用户状态"
              clearable
              style="width: 100%"
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
          <el-col :span="4" style="text-align: right">
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
          <!-- 新增：FullName 列，显示在用户名后 -->
          <el-table-column prop="fullName" label="姓名" width="140">
            <template #default="scope">
              {{ scope.row.fullName || '-' }}
            </template>
          </el-table-column>
          <el-table-column prop="email" label="邮箱" min-width="120" />
          <el-table-column prop="phoneNumber" label="电话号码" width="120">
            <template #default="scope">
              {{ scope.row.phoneNumber || '-' }}
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
          <div class="username-input-group">
            <el-input
              v-model="userForm.username"
              placeholder="请输入用户名"
              :disabled="isEdit || generatingNames"
            />
            <el-button 
              type="primary" 
              :icon="Refresh" 
              @click="generateUserNames"
              :loading="generatingNames"
              size="small"
              v-if="!isEdit"
            >
              {{ generatingNames ? '生成中...' : '自动生成' }}
            </el-button>
          </div>
        </el-form-item>
        
        <el-form-item label="姓名" prop="fullName">
          <el-input
            v-model="userForm.fullName"
            placeholder="请输入姓名"
            maxlength="100"
            :disabled="generatingNames"
          />
        </el-form-item>

        <el-form-item label="邮箱" prop="email">
          <el-input
            v-model="userForm.email"
            placeholder="请输入邮箱"
          />
        </el-form-item>

        <!-- 头像上传 -->
        <el-form-item label="头像上传" prop="profilePictureUrl">
          <div class="avatar-upload-container">
            <div v-if="!userForm.profilePictureUrl && !uploadedAvatarUrl" class="avatar-upload-area">
              <input
                ref="avatarFileInput"
                type="file"
                accept="image/*"
                style="display: none"
                @change="handleAvatarFileSelect"
              />
              <div class="avatar-upload-placeholder" @click="selectAvatarFile">
                <el-icon class="avatar-upload-icon"><Plus /></el-icon>
                <div class="avatar-upload-text">点击上传头像</div>
                <div class="avatar-upload-tip">支持 jpg、png、gif 格式，文件大小不超过 5MB</div>
              </div>
            </div>
           
            <div v-else class="avatar-uploaded-preview">
              <!-- 优先显示新上传的头像 -->
              <img 
                v-if="uploadedAvatarUrl" 
                :src="uploadedAvatarUrl" 
                class="avatar-uploaded-image" 
              />
              <!-- 如果没有新上传的头像，显示服务器头像（仅编辑模式） -->
              <img 
                v-else-if="userForm.profilePictureUrl" 
                :src="userForm.profilePictureUrl" 
                class="avatar-uploaded-image" 
              />
              <div class="avatar-image-actions">
                <el-button size="small" @click="reSelectAvatarImage">重新选择</el-button>
              </div>
            </div>
          </div>
        </el-form-item>

        <el-form-item label="电话号码" prop="phoneNumber">
          <el-input
            v-model="userForm.phoneNumber"
            placeholder="请输入电话号码"
            type="tel"
            maxlength="11"
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
import { userApi, type ChangePasswordParams, type PagedResponse, type UserCreateParams, type UserListParams, type User as UserType, type UserUpdateParams, type UserNamePair } from '@/api/user'
import { pictureApi } from '@/api/picture'
import { Message, Plus, Refresh, Search, User, InfoFilled } from '@element-plus/icons-vue'
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
const generatingNames = ref(false) // 生成用户名状态
const avatarFileInput = ref<HTMLInputElement>()
const uploadedAvatarUrl = ref<string>('')
const uploadedAvatarBlob = ref<Blob | null>(null)
  // 新增：对话框标题（编辑/新增自动切换）
const dialogTitle = computed(() => (isEdit.value ? '编辑用户' : '新增用户'))
// 搜索表单
const searchForm = reactive({
  username: '',
  // 新增：FullName 搜索字段
  fullName: '',
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

// 用户表单数据：新增 fullName
const userForm = reactive({
  id: null as number | null,
  username: '',
  // 新增：姓名/昵称
  fullName: '',
  email: '',
  phoneNumber: '',
  bio: '',
  userTypeCode: '',        // 改为 code
  userToSystemCode: '',    // 改为 code
  profilePictureUrl: '',   // 新增：头像URL
  password: '',
  isActive: true
})

// 表单验证规则（邮箱、电话非必填；新增 fullName 校验）
const userRules: FormRules = {
  username: [
    { required: true, message: '请输入用户名', trigger: 'blur' },
    { min: 3, message: '用户名长度不能少于3位', trigger: 'blur' }
  ],
  // 去掉邮箱必填，仅格式校验
  email: [
    { type: 'email', message: '请输入正确的邮箱格式', trigger: 'blur' }
  ],
  // FullName 非必填，长度限制
  fullName: [
    { max: 100, message: '长度不能超过100个字符', trigger: 'blur' }
  ],
  // 去掉电话号码必填，仅格式校验
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


const loadUserList = async () => {
  loading.value = true
  try {
    const params: UserListParams = {
      page: pagination.currentPage,
      size: pagination.pageSize,
      username: searchForm.username || undefined,
      fullName: searchForm.fullName || undefined,
      email: searchForm.email || undefined,
      isActive: searchForm.isActive
    }

    const response: PagedResponse<UserType> = await userApi.getUserList(params)
    userList.value = Array.isArray(response.data) ? response.data : []
    pagination.total = typeof response.total === 'number' ? response.total : 0
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
        // 如果有新上传的头像，先上传到服务器
        let avatarUrl = userForm.profilePictureUrl
        if (uploadedAvatarBlob.value) {
          try {
            const file = new File([uploadedAvatarBlob.value], 'avatar.jpg', { type: uploadedAvatarBlob.value.type })
            const uploadResponse = await pictureApi.uploadImage(file, '1:1')
            avatarUrl = uploadResponse.data.url
          } catch (uploadError) {
            console.error('头像上传失败:', uploadError)
            ElMessage.error('头像上传失败，请重试')
            return
          }
        }

        if (isEdit.value && userForm.id) {
          // 编辑用户
          const updateData: (UserUpdateParams & { fullName?: string }) = {
            email: userForm.email || undefined,
            // 新增：传递 FullName
            fullName: userForm.fullName || undefined,
            phoneNumber: userForm.phoneNumber || undefined,
            bio: userForm.bio || undefined,
            isActive: userForm.isActive,
            userTypeCode: userForm.userTypeCode || undefined,       // 改为 code
            userToSystemCode: userForm.userToSystemCode || undefined, // 改为 code
            profilePictureUrl: avatarUrl || undefined // 使用上传后的URL
          }
          await userApi.updateUser(userForm.id, updateData)
          ElMessage.success('编辑成功')
        } else {
          // 新增用户
          const createData: (UserCreateParams & { fullName?: string }) = {
            username: userForm.username,
            email: userForm.email || undefined,
            password: userForm.password,
            // 新增：传递 FullName（接口已为可选）
            fullName: userForm.fullName || undefined,
            phoneNumber: userForm.phoneNumber || undefined,
            bio: userForm.bio || undefined,
            userTypeCode: userForm.userTypeCode || undefined,        // 改为 code
            userToSystemCode: userForm.userToSystemCode || undefined, // 改为 code
            profilePictureUrl: avatarUrl || undefined // 使用上传后的URL
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

// 生成用户名和姓名
const generateUserNames = async () => {
  try {
    generatingNames.value = true
    const response = await userApi.generateUserNamePair()
    
    if (response.success && response.data) {
      userForm.username = response.data.userName
      userForm.fullName = response.data.fullName
      ElMessage.success('用户名和姓名生成成功！')
    } else {
      ElMessage.error(response.message || '生成失败')
    }
  } catch (error) {
    console.error('生成用户名失败:', error)
    ElMessage.error('生成用户名失败，请重试')
  } finally {
    generatingNames.value = false
  }
}

const resetUserForm = () => {
  Object.assign(userForm, {
    id: null,
    username: '',
    fullName: '',
    email: '',
    phoneNumber: '',
    bio: '',
    userTypeCode: '',        // 改为 code
    userToSystemCode: '',    // 改为 code
    profilePictureUrl: '',   // 重置头像URL
    password: '',
    isActive: true
  })
  // 清理头像上传相关数据
  cleanupAvatarTempData()
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

// 头像上传相关方法
// 选择头像文件
const selectAvatarFile = () => {
  avatarFileInput.value?.click()
}

// 头像文件选择处理
const handleAvatarFileSelect = (event: Event) => {
  const target = event.target as HTMLInputElement
  const file = target.files?.[0]
  
  if (!file) return
  
  // 验证文件类型
  if (!file.type.startsWith('image/')) {
    ElMessage.error('请选择图片文件！')
    return
  }
  
  // 验证文件大小（5MB）
  if (file.size > 5 * 1024 * 1024) {
    ElMessage.error('头像文件大小不能超过 5MB！')
    return
  }
  
  // 读取文件并创建预览
  const reader = new FileReader()
  reader.onload = (e) => {
    const result = e.target?.result as string
    uploadedAvatarUrl.value = result
    
    // 将文件转换为Blob存储
    file.arrayBuffer().then(buffer => {
      uploadedAvatarBlob.value = new Blob([buffer], { type: file.type })
    })
  }
  reader.readAsDataURL(file)
}

// 重新选择头像
const reSelectAvatarImage = () => {
  cleanupAvatarTempData()
  userForm.profilePictureUrl = ''
  selectAvatarFile()
}

// 清理头像临时数据
const cleanupAvatarTempData = () => {
  if (uploadedAvatarUrl.value && uploadedAvatarUrl.value.startsWith('blob:')) {
    URL.revokeObjectURL(uploadedAvatarUrl.value)
  }
  uploadedAvatarBlob.value = null
  uploadedAvatarUrl.value = ''
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
  // 清理头像临时数据
  cleanupAvatarTempData()
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
  // 新增：清空 FullName 搜索字段
  searchForm.fullName = ''
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

  // 自动生成用户名和姓名
  generateUserNames()

  loadUserTypeOptions()
  loadSystemOptions()
}

const handleEdit = (row: UserType) => {
  console.log('Editing user:', row)
  isEdit.value = true
  Object.assign(userForm, {
    id: row.id,
    username: row.userName,
    // 新增：从列表回填 FullName
    fullName: (row as any).fullName || '',
    email: row.email,
    phoneNumber: row.phoneNumber || '',
    bio: row.bio || '',
    userTypeCode: row.userTypeCode || '',
    userToSystemCode: row.userToSystemCode || '',
    profilePictureUrl: row.profilePictureUrl || '',
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

.username-input-group {
  display: flex;
  align-items: center;
  gap: 8px;
}

.username-input-group .el-input {
  flex: 1;
}

.username-input-group .el-button {
  flex-shrink: 0;
}

/* 头像上传样式 */
.avatar-upload-container {
  border: 1px solid #dcdfe6;
  border-radius: 6px;
  padding: 15px;
  background-color: #fafafa;
}

.avatar-upload-area {
  width: 100%;
}

.avatar-upload-placeholder {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  height: 120px;
  border: 2px dashed #d9d9d9;
  border-radius: 6px;
  cursor: pointer;
  transition: border-color 0.3s;
}

.avatar-upload-placeholder:hover {
  border-color: #409eff;
}

.avatar-upload-icon {
  font-size: 32px;
  color: #c0c4cc;
  margin-bottom: 8px;
}

.avatar-upload-text {
  color: #606266;
  font-size: 14px;
  margin-bottom: 4px;
}

.avatar-upload-tip {
  color: #909399;
  font-size: 12px;
}

.avatar-uploaded-preview {
  display: flex;
  flex-direction: column;
  align-items: center;
}

.avatar-uploaded-image {
  width: 80px;
  height: 80px;
  border-radius: 50%;
  object-fit: cover;
  margin-bottom: 10px;
  border: 2px solid #e4e7ed;
}

.avatar-image-actions {
  display: flex;
  gap: 10px;
}

</style>