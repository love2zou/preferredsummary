<template>
  <div class="page">
    <div class="login-container">
      <div class="login-content">
        <div class="login-header">
          <div class="logo brand">
            <el-icon size="64"><Trophy /></el-icon>
          </div>
          <h1 class="title">健身约课系统</h1>
          <p class="subtitle">让健身更简单</p>
        </div>

        <div class="login-form login-card">
          <el-form :model="loginForm" :rules="rules" ref="formRef">
            <el-form-item prop="username">
              <el-input
                v-model="loginForm.username"
                placeholder="请输入用户名/手机号"
                size="large"
                prefix-icon="User"
              />
            </el-form-item>
            
            <el-form-item prop="password">
              <el-input
                v-model="loginForm.password"
                type="password"
                placeholder="请输入密码"
                size="large"
                prefix-icon="Lock"
                show-password
              />
            </el-form-item>
  
            <el-form-item>
              <el-button
                type="primary"
                size="large"
                class="login-btn"
                :loading="loading"
                @click="handleLogin"
              >
                登录
              </el-button>
            </el-form-item>
          </el-form>
  
          <div class="login-footer">
            <p>还没有账号？<router-link to="/register" class="link">立即注册</router-link></p>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { authService } from '@/services/authService'
import type { User } from '@/stores/user'
import { useUserStore } from '@/stores/user'
import { Trophy } from '@element-plus/icons-vue'
import { ElMessage, type FormInstance } from 'element-plus'
import { reactive, ref } from 'vue'
import { useRouter } from 'vue-router'

console.log('Login.vue 组件开始加载')

const router = useRouter()
const userStore = useUserStore()
const formRef = ref<FormInstance>()
const loading = ref(false)

const loginForm = reactive({
  username: '',
  password: ''
})

// 新增：用户名/手机号校验（支持手机号或≥3位用户名）
const rules = {
  username: [
    { required: true, message: '请输入用户名或手机号', trigger: 'blur' },
    {
      validator: (_rule: any, value: string, callback: (err?: Error) => void) => {
        const v = (value || '').trim()
        const phoneRe = /^1[3-9]\d{9}$/
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

// handleLogin
// 在 <script setup>：handleLogin 内更新角色映射
const handleLogin = async () => {
  if (!formRef.value) return
  try {
    await formRef.value.validate()
    loading.value = true

    const raw = (loginForm.username || '').trim()
    const phoneRe = /^1[3-9]\d{9}$/
    const standardizedUsername = phoneRe.test(raw) ? raw : raw // 后端已支持手机号按 username 字段登录

    const resp = await authService.login({
      username: standardizedUsername,
      password: loginForm.password
    })

    const payload = (resp as any)?.data ?? resp
    const authToken = (payload as any)?.token ?? (payload as any)?.Token ?? ''
    // 读取后端用户类型代码并标准化为角色
    const rawType = (payload as any)?.userTypeCode
    // 持久化，供首页回退判断使用
    localStorage.setItem('userTypeCode', rawType)
  
    const userData: User = {
      id:(payload as any)?.userId ?? (payload as any)?.UserId ?? 0,
      username:
        (payload as any)?.user?.username ?? (payload as any)?.UserName ?? loginForm.username,
      email: (payload as any)?.user?.email ?? (payload as any)?.Email ?? '',
      phone: (payload as any)?.user?.phone ?? '',
      role: rawType, // 归一化后的角色，供 userStore.isTrainer 使用
      avatar: (payload as any)?.user?.avatar ?? '',
      createdAt: (payload as any)?.user?.createdAt ?? new Date().toISOString()
    }
  
      // 写入本地缓存（只使用后端真实令牌）
      localStorage.setItem('token', authToken)
      localStorage.setItem('user', JSON.stringify(userData))
      localStorage.setItem('username', userData.username)
      localStorage.setItem('tokenExpireTime', String(Date.now() + 24 * 60 * 60 * 1000))
  
      userStore.login(userData, authToken)
      ElMessage.success('登录成功')
      router.replace('/home')
    } catch (error: any) {
      console.error('登录失败:', error)
      ElMessage.error(error?.response?.data?.message || error?.message || '登录失败，请检查用户名和密码')
    } finally {
      loading.value = false
    }
}
</script>

<style scoped>
/* 视口居中：网格 + 动态视口高度支持 */
.page {
  width: 100vw;
  height: 100vh;
  display: grid;
  place-items: center;
  background: linear-gradient(180deg, #f7fff9 0%, #eaf9f0 100%);
}
/* 移动端更准确的高度单位（可用则覆盖） */
@supports (height: 100dvh) {
  .page { height: 100dvh; }
}

/* 容器只负责内边距与居中，不再用 min-height:100vh 撑满页面 */
.login-container {
  width: 100%;
  display: flex;
  flex-direction: column;
  align-items: center;   /* 横向居中 */
  justify-content: center; /* 纵向居中 */
  padding: 24px;
  gap: 16px;
}

/* 可选包裹层：存在时统一宽度约束 */
.login-content {
  width: 100%;
  max-width: 520px;
}

/* 登录卡片：在 414 宽下居中且留边，不超过 420px */
.login-form.login-card {
  width: clamp(300px, 90vw, 420px);
  margin: 0 auto;
  padding: 24px;
  border-radius: 14px;
  background: #ffffff;
  border: 1px solid #e5e7eb;
  box-shadow: 0 10px 24px rgba(22, 163, 74, 0.08);
}

/* 品牌区更紧凑，避免竖向撑高 */
.login-header {
  text-align: center;
  margin-bottom: 12px;
  color: #14532d;
}
.logo.brand :deep(svg) {
  color: #16a34a;
  filter: drop-shadow(0 4px 10px rgba(22,163,74,0.25));
}
.title { font-size: 24px; font-weight: 700; margin: 6px 0 4px; color: #14532d; }
.subtitle { font-size: 14px; color: #166534; opacity: 0.9; }

/* 表单与按钮 */
:deep(.el-form-item) { margin-bottom: 16px; }
.login-btn { width: 100%; height: 44px; font-size: 15px; font-weight: 600; }

/* 暖绿色按钮 */
:deep(.el-button--primary) {
  background-color: var(--primary-color, #16a34a);
  border-color: var(--primary-color, #16a34a);
}
:deep(.el-button--primary:hover),
:deep(.el-button--primary:focus) {
  background-color: var(--primary-color-600, #0f9f3e);
  border-color: var(--primary-color-600, #0f9f3e);
}

/* 小屏优化：更窄时仍居中且留边 */
@media (max-width: 414px) {
  .login-form.login-card {
    width: calc(100vw - 32px);
  }
}


.login-footer { text-align: center; margin-top: 16px; color: #6b7280; }
.link { color: var(--primary-color, #16a34a); }
.link:hover { text-decoration: underline; }

/* 隐藏调试信息 */
.debug-info { display: none; }


@media (max-width: 480px) {
  /* 小屏时卡片宽度自适应，仍保持居中 */
  .login-form.login-card {
    width: 100%;
    max-width: 360px;
  }
}
</style>