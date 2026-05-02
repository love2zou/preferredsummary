import request from '@/utils/request'

export interface ApiResponse<T> {
  success: boolean
  message: string
  data: T
  errorCode?: string
}

export interface LoginResponseData {
  token: string
  userId: number
  userName: string
  userTypeCode: string
  email: string
}

export const authApi = {
  login(userName: string, password: string): Promise<ApiResponse<LoginResponseData>> {
    return request.post('/api/Auth/login', {
      username: userName,
      password
    })
  }
}
