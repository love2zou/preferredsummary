import api from '@/api'

export interface LoginResponse {
  token?: string
  user?: {
    id: number
    username: string
    email?: string
    phone?: string
    role: 'member' | 'trainer'
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
    phone?: string
    userType?: 'member' | 'trainer'
  }): Promise<any> {
    return api.post('/auth/register', payload)
  },
  logout(): Promise<any> {
    return api.post('/auth/logout')
  }
}