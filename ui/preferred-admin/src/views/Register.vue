<template>
  <div class="register-container">
    <el-card class="register-card">
      <template #header>
        <div class="card-header">
          <h2>用户注册</h2>
          <p>创建您的账户</p>
        </div>
      </template>
      
      <el-form
        ref="registerFormRef"
        :model="registerForm"
        :rules="registerRules"
        label-width="80px"
        size="large"
      >
        <el-form-item label="用户名" prop="username" required>
          <el-input
            v-model="registerForm.username"
            placeholder="请输入用户名（至少3位）"
            prefix-icon="User"
          />
        </el-form-item>
        
        <el-form-item label="邮箱" prop="email" required>
          <el-input
            v-model="registerForm.email"
            placeholder="请输入邮箱地址"
            prefix-icon="Message"
          />
        </el-form-item>
        
        <el-form-item label="密码" prop="password" required>
          <el-input
            v-model="registerForm.password"
            type="password"
            placeholder="请输入密码（至少6位）"
            prefix-icon="Lock"
            show-password
          />
        </el-form-item>
        
        <el-form-item label="确认密码" prop="confirmPassword" required>
          <el-input
            v-model="registerForm.confirmPassword"
            type="password"
            placeholder="请再次输入密码"
            prefix-icon="Lock"
            show-password
            @keyup.enter="handleRegister"
          />
        </el-form-item>
        
        <el-form-item>
          <el-button
            type="primary"
            size="large"
            style="width: 100%"
            :loading="loading"
            @click="handleRegister"
          >
            注册
          </el-button>
        </el-form-item>
        
        <el-form-item>
          <div class="register-footer">
            <span>已有账户？</span>
            <el-link type="primary" @click="$router.push('/login')">
              立即登录
            </el-link>
          </div>
        </el-form-item>
      </el-form>
    </el-card>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive } from 'vue'
import { ElForm, ElMessage, ElNotification, type FormRules } from 'element-plus'
import { InfoFilled } from '@element-plus/icons-vue'
import { useAuthStore } from '@/stores/auth'
import { useRouter } from 'vue-router'

const authStore = useAuthStore()
const router = useRouter()
const registerFormRef = ref<InstanceType<typeof ElForm>>()
const loading = ref(false)

const registerForm = reactive({
  username: '',
  email: '',
  password: '',
  confirmPassword: ''
})

const validateConfirmPassword = (rule: any, value: string, callback: Function) => {
  if (value !== registerForm.password) {
    callback(new Error('两次输入的密码不一致'))
  } else {
    callback()
  }
}

const registerRules: FormRules = {
  username: [
    { required: true, message: '请输入用户名', trigger: 'blur' },
    { min: 3, message: '用户名长度不能少于3位', trigger: 'blur' }
  ],
  email: [
    { required: true, message: '请输入邮箱', trigger: 'blur' },
    { type: 'email', message: '请输入正确的邮箱格式', trigger: 'blur' }
  ],
  password: [
    { required: true, message: '请输入密码', trigger: 'blur' },
    { min: 6, message: '密码长度不能少于6位', trigger: 'blur' }
  ],
  confirmPassword: [
    { required: true, message: '请再次输入密码', trigger: 'blur' },
    { validator: validateConfirmPassword, trigger: 'blur' }
  ]
}

// 成功提示和自动跳转
const showSuccessAndRedirect = () => {
  // 显示成功通知
  ElNotification({
    title: '注册成功',
    message: '恭喜您，账户创建成功！3秒后将自动跳转到登录页面',
    type: 'success',
    duration: 3000,
    position: 'top-right'
  })
  
  // 3秒后自动跳转到登录页面
  setTimeout(() => {
    router.push('/login')
  }, 3000)
}

// 处理错误信息
const handleRegisterError = (error: any) => {
  let errorMessage = '注册失败，请稍后重试'
  
  // 根据错误类型显示友好的错误信息
  if (error?.response?.status === 400) {
    const errorData = error.response.data

    // 检查是否是用户名或邮箱已存在的错误
    if (errorData?.message?.includes('用户名') || errorData?.message?.includes('username')) {
      errorMessage = '该用户名已被注册，请选择其他用户名'
    } else if (errorData?.message?.includes('邮箱') || errorData?.message?.includes('email')) {
      errorMessage = '该邮箱已被注册，请使用其他邮箱或直接登录'
    } else if (errorData?.errors) {
      // 处理验证错误
      const firstError = Object.values(errorData.errors)[0]
      if (Array.isArray(firstError) && firstError.length > 0) {
        errorMessage = firstError[0] as string
      }
    }
  } else if (error?.response?.status === 409) {
    errorMessage = '用户信息冲突，该用户名或邮箱已存在'
  } else if (error?.response?.status >= 500) {
    errorMessage = '服务器暂时无法处理请求，请稍后重试'
  }
  
  ElMessage.error(errorMessage)
}

const handleRegister = async () => {
  if (!registerFormRef.value) return
  
  await registerFormRef.value.validate(async (valid) => {
    if (valid) {
      loading.value = true
      try {
        const { confirmPassword, ...registerData } = registerForm
        await authStore.register(registerData)
        
        // 注册成功，显示成功提示并自动跳转
        showSuccessAndRedirect()
        
      } catch (error) {
        console.error('注册失败:', error)
        handleRegisterError(error)
      } finally {
        loading.value = false
      }
    }
  })
}
</script>

<style scoped>
.register-container {
  display: flex;
  justify-content: center;
  align-items: center;
  min-height: 100vh;
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
}

.register-card {
  width: 450px;
  box-shadow: 0 8px 32px rgba(0, 0, 0, 0.1);
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

.register-footer {
  text-align: center;
  width: 100%;
}

.register-footer span {
  color: #909399;
  font-size: 14px;
}

.form-tips {
  text-align: center;
  width: 100%;
  margin: 10px 0;
}

.required-mark {
  color: #f56c6c;
  font-weight: bold;
}

/* 自定义必填项星号样式 */
:deep(.el-form-item.is-required .el-form-item__label::before) {
  content: '*';
  color: #f56c6c;
  margin-right: 4px;
}
</style>