import api from '@/api'

export interface LoginResponse {
  token?: string
  user?: {
    id: number
    username: string
    email?: string
    phone?: string
    role: 'huiyuan' | 'jianlian'
    avatar?: string
    createdAt?: string
  }
}

export const authService = {
  login(payload: { username: string; password: string }): Promise<LoginResponse> {
    return api.post('/auth/login', payload)
  },
  register(payload: {
    username: string
    email: string
    password: string
    phoneNumber: string
    userTypeCode: string
    userToSystemCode: string // 新增：系统码，要求为必填
  }): Promise<any> {
    return api.post('/auth/register', payload)
  },
  logout(): Promise<any> {
    return api.post('/auth/logout')
  }
}