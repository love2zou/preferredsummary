<template>
  <div class="page">
    <div class="navbar">
      <span class="navbar-back" @click="$router.go(-1)">
        <el-icon><ArrowLeft /></el-icon> 返回
      </span>
      <span class="navbar-title">注册账号</span>
      <span></span>
    </div>

    <div class="register-container">
      <div class="register-form">
        <el-form :model="registerForm" :rules="rules" ref="formRef" label-position="left" label-width="96px">
          <el-form-item label="用户类型" prop="userTypeCode">
            <el-radio-group v-model="registerForm.userTypeCode" size="large">
              <el-radio value="huiyuan">会员</el-radio>
              <el-radio value="jiaolian">教练</el-radio>
            </el-radio-group>
          </el-form-item>

          <el-form-item label="用户名" prop="username">
            <el-input
              v-model="registerForm.username"
              placeholder="请输入用户名"
              size="large"
            />
          </el-form-item>

          <el-form-item label="手机号" prop="phoneNumber">
            <el-input
              v-model="registerForm.phoneNumber"
              placeholder="请输入手机号"
              size="large"
            />
          </el-form-item>

          <el-form-item label="邮箱" prop="email">
            <el-input
              v-model="registerForm.email"
              placeholder="请输入邮箱地址"
              size="large"
            />
          </el-form-item>

          <el-form-item label="密码" prop="password">
            <el-input
              v-model="registerForm.password"
              type="password"
              placeholder="请输入密码"
              size="large"
              show-password
            />
          </el-form-item>

          <el-form-item label="确认密码" prop="confirmPassword">
            <el-input
              v-model="registerForm.confirmPassword"
              type="password"
              placeholder="请再次输入密码"
              size="large"
              show-password
            />
          </el-form-item>

          <el-form-item>
            <el-button
              type="primary"
              size="large"
              class="register-btn"
              :loading="loading"
              @click="handleRegister"
            >
              注册
            </el-button>
          </el-form-item>
        </el-form>
      <!-- register-footer 已删除 -->
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { authService } from '@/services/authService'
import { ArrowLeft } from '@element-plus/icons-vue'
import { ElMessage, type FormInstance } from 'element-plus'
import { reactive, ref } from 'vue'
import { useRouter } from 'vue-router'

const router = useRouter()
const formRef = ref<FormInstance>()
const loading = ref(false)

const registerForm = reactive({
  userTypeCode: 'huiyuan',
  username: '',
  phoneNumber: '',
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

const rules = {
  userTypeCode: [
    { required: true, message: '请选择用户类型', trigger: 'change' }
  ],
  username: [
    { required: true, message: '请输入用户名', trigger: 'blur' },
    { min: 2, max: 20, message: '用户名长度在2-20个字符', trigger: 'blur' }
  ],
  phoneNumber: [
    { required: true, message: '请输入手机号', trigger: 'blur' },
    { pattern: /^1[3-9]\d{9}$/, message: '请输入正确的手机号', trigger: 'blur' }
  ],
  email: [
    { required: true, message: '请输入邮箱地址', trigger: 'blur' },
    { type: 'email', message: '请输入正确的邮箱地址', trigger: 'blur' }
  ],
  password: [
    { required: true, message: '请输入密码', trigger: 'blur' },
    { min: 6, max: 20, message: '密码长度在6-20个字符', trigger: 'blur' }
  ],
  confirmPassword: [
    { required: true, message: '请确认密码', trigger: 'blur' },
    { validator: validateConfirmPassword, trigger: 'blur' }
  ]
}

// script setup: handleRegister
const handleRegister = async () => {
  if (!formRef.value) return
  
  try {
    await formRef.value.validate()
    loading.value = true

    const payload = {
      username: registerForm.username.trim(),
      email: registerForm.email.trim(),
      password: registerForm.password,
      phoneNumber: registerForm.phoneNumber.trim(),
      userTypeCode: registerForm.userTypeCode,
      userToSystemCode: 'lvsejianshenxitong' // 默认系统码，避免保存失败
    }

    await authService.register(payload)
    
    ElMessage.success('注册成功，请登录')
    router.push('/login')
    
  } catch (error: any) {
    console.error('Register failed:', error)
    const msg = error?.response?.data?.message || error?.message || '注册失败，请重试'
    ElMessage.error(msg)
  } finally {
    loading.value = false
  }
}
</script>

<style scoped>
.register-container {
  padding: 16px;
}

.register-form {
  background: white;
  padding: 24px;
  border-radius: 12px;
  box-shadow: var(--shadow);
  max-width: 560px; /* 表单居中且更易阅读 */
  margin: 0 auto;
}

:deep(.el-form-item) {
  align-items: center; /* 标签与输入居中对齐 */
}

:deep(.el-form-item__label) {
  font-weight: 500;
  color: var(--text-color);
}

:deep(.el-input),
:deep(.el-radio-group) {
  width: 100%;
}

:deep(.el-radio) {
  margin-right: 24px;
}
</style>