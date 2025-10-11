<template>
  <div class="login-container">
    <el-card class="login-card">
      <template #header>
        <div class="card-header">
          <h2>后台管理系统</h2>
          <p>请登录您的账户</p>
        </div>
      </template>
      
      <el-form
        ref="loginFormRef"
        :model="loginForm"
        :rules="loginRules"
        label-width="0"
        size="large"
      >
        <el-form-item prop="username">
          <el-input
            v-model="loginForm.username"
            placeholder="用户名/手机号"
            prefix-icon="User"
          />
        </el-form-item>
        
        <el-form-item prop="password">
          <el-input
            v-model="loginForm.password"
            type="password"
            placeholder="密码"
            prefix-icon="Lock"
            show-password
            @keyup.enter="handleLogin"
          />
        </el-form-item>
        
        <el-form-item>
          <el-button
            type="primary"
            size="large"
            style="width: 100%"
            :loading="loading"
            @click="handleLogin"
          >
            登录
          </el-button>
        </el-form-item>
        
        <el-form-item>
          <div class="login-footer">
            <span>还没有账户？</span>
            <el-link type="primary" @click="$router.push('/register')">
              立即注册
            </el-link>
          </div>
        </el-form-item>
      </el-form>
    </el-card>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive } from 'vue'
import { ElForm, ElMessage } from 'element-plus'
import { useAuthStore } from '@/stores/auth'

const authStore = useAuthStore()
const loginFormRef = ref<InstanceType<typeof ElForm>>()
const loading = ref(false)

const loginForm = reactive({
  username: '',
  password: ''
})

const loginRules = {
  username: [
    { required: true, message: '请输入用户名或手机号', trigger: 'blur' },
    {
      validator: (_rule: any, value: string, callback: (err?: Error) => void) => {
        const v = (value || '').trim()
        const phoneRe = /^1[3-9]\d{9}$/
        // 手机号合规 或 用户名长度≥3 即通过
        if (phoneRe.test(v) || v.length >= 3) return callback()
        callback(new Error('请输入至少3位用户名或正确的手机号'))
      },
      trigger: 'blur'
    }
  ],
  password: [
    { required: true, message: '请输入密码', trigger: 'blur' },
    { min: 6, message: '密码长度不能少于6位', trigger: 'blur' }
  ]
}

// 优化后的错误处理函数
const handleLoginError = (error: any) => {
  let errorMessage = '登录失败，请稍后重试'
  
  // 处理业务逻辑错误（后端返回 200 但 success: false）
  if (error?.message) {
    errorMessage = error.message  // 直接使用后端返回的错误信息
  } else if (error?.response) {
    // 处理 HTTP 错误
    const { status, data } = error.response
    
    switch (status) {
      case 400:
        errorMessage = '请求参数错误，请检查输入信息'
        break
      case 403:
        errorMessage = '账户已被禁用，请联系管理员'
        break
      case 429:
        errorMessage = '登录尝试过于频繁，请稍后再试'
        break
      case 500:
      case 502:
      case 503:
        errorMessage = '服务器暂时无法处理请求，请稍后重试'
        break
      default:
        errorMessage = data?.message || '登录失败，请稍后重试'
    }
  } else if (error?.code === 'NETWORK_ERROR') {
    errorMessage = '网络连接失败，请检查网络设置'
  }
  
  ElMessage.error(errorMessage)
}

const handleLogin = async () => {
  if (!loginFormRef.value) return
  // 先清除可能存在的提示
  ElMessage.closeAll()
  await loginFormRef.value.validate(async (valid) => {
    if (valid) {
      loading.value = true
      try {
        const account = loginForm.username.trim()
        await authStore.login({ username: account, password: loginForm.password })
      } catch (error) {
        console.error('登录失败:', error)
        handleLoginError(error)
      } finally {
        loading.value = false
      }
    }
  })
}
</script>

<style scoped>
.login-container {
  display: flex;
  justify-content: center;
  align-items: center;
  width: 100vw;
  height: 100vh;
  margin: 0;
  padding: 0;
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  box-sizing: border-box;
}

.login-card {
  width: 400px;
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

.login-footer {
  text-align: center;
  width: 100%;
}

.login-footer span {
  color: #909399;
  font-size: 14px;
}
</style>