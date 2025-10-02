import api from './api'

interface LoginRequest {
  username: string
  password: string
}

interface RegisterRequest {
  username: string
  email: string
  password: string
}

// API 响应基础接口
export interface ApiResponse<T = any> {
  success: boolean
  message: string
  data: T
  errorCode?: string
}

export const authService = {
  // 用户登录
  login(data: LoginRequest): Promise<ApiResponse> {
    return api.post('/api/auth/login', data)
  },

  // 用户注册
  register(data: RegisterRequest): Promise<{ message: string }> {
    return api.post('/api/auth/register', data)
  },

  // 退出登录
  logout() {
    localStorage.removeItem('token')
    // 移除重定向，只清除token
    console.log('用户已退出登录')
  }
}

export type { LoginRequest, RegisterRequest }