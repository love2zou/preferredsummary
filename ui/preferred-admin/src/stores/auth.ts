import { defineStore } from 'pinia'
import { ref } from 'vue'
import request from '@/utils/request'
import { ElMessage } from 'element-plus'
import router from '@/router'
import type { ApiResponse, LoginResponseData } from '@/types/api'

export interface LoginForm {
  username: string
  password: string
}

export interface RegisterForm {
  username: string
  password: string
  email: string
  firstName?: string
  lastName?: string
}

export const useAuthStore = defineStore('auth', () => {
  const token = ref(localStorage.getItem('token') || '')
  const user = ref(JSON.parse(localStorage.getItem('user') || '{}'))
  const loginTime = ref(localStorage.getItem('loginTime') || '')
  const isLoggedIn = ref(!!token.value && !isTokenExpired())

  // 检查token是否过期（24小时）
  function isTokenExpired(): boolean {
    if (!loginTime.value) return true
    
    const loginTimestamp = parseInt(loginTime.value)
    const currentTime = Date.now()
    const expirationTime = 6 * 60 * 60 * 1000 // 24小时（毫秒）
    
    return (currentTime - loginTimestamp) > expirationTime
  }

  // 检查并处理过期登录
  function checkTokenExpiration(): boolean {
    if (token.value && isTokenExpired()) {
      ElMessage.warning('登录已过期，请重新登录')
      logout()
      return true
    }
    return false
  }

  // 登录
  const login = async (loginForm: LoginForm) => {
    try {
      const response: ApiResponse<LoginResponseData> = await request.post('/api/Auth/login', {
        username: loginForm.username,
        password: loginForm.password
      })
      
      // 检查业务逻辑是否成功
      if (response.success) {
        // 登录成功
        const currentTime = Date.now().toString()
        user.value = response.data
        isLoggedIn.value = true
        loginTime.value = currentTime
        
        localStorage.setItem('token', response.data.token)
        localStorage.setItem('user', JSON.stringify(response.data))
        localStorage.setItem('loginTime', currentTime)
        
        ElMessage.success('登录成功')
        router.push('/dashboard')
      } else {
        // 登录失败，显示具体错误信息
        throw new Error(response.message)
      }
    } catch (error: any) {
      throw error
    }
  }

  // 注册
  const register = async (registerForm: RegisterForm) => {
    try {
      const response: ApiResponse = await request.post('/api/Auth/register', registerForm)
      if (response.success) {
        // 移除这里的成功提示，让上层组件处理
        // ElMessage.success('注册成功，请登录')
        // 也不需要在这里跳转，让上层组件处理
        // router.push('/login')
        return response
      }
    } catch (error: any) {
      // 移除这里的错误提示，让上层组件处理
      // ElMessage.error(error.response?.data?.message || '注册失败')
      throw error
    }
  }

  // 登出
  const logout = () => {
    token.value = ''
    user.value = {}
    loginTime.value = ''
    isLoggedIn.value = false
    localStorage.removeItem('token')
    localStorage.removeItem('user')
    localStorage.removeItem('loginTime')
    router.push('/login')
  }

  return {
    token,
    user,
    loginTime,
    isLoggedIn,
    login,
    register,
    logout,
    checkTokenExpiration,
    isTokenExpired
  }
})