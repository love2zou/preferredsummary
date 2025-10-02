import axios, { AxiosResponse } from 'axios'
import { API_CONFIG } from '@/config/api'

const api = axios.create({
  baseURL: API_CONFIG.BASE_URL,
  timeout: API_CONFIG.TIMEOUT,
  headers: {
    'Content-Type': 'application/json',
    // 禁用缓存
    'Cache-Control': 'no-cache, no-store, must-revalidate',
    'Pragma': 'no-cache',
    'Expires': '0'
  }
})

// 请求拦截器
api.interceptors.request.use(
  config => {
    const token = localStorage.getItem('token')
    if (token) {
      config.headers.Authorization = `Bearer ${token}`
    } else {
      // 如果没有token，可以尝试添加访客标识
      config.headers['X-Guest-Mode'] = 'true'
    }
    
    // 为每个请求添加时间戳，防止缓存
    config.params = {
      ...config.params,
      _t: Date.now()
    }
    
    return config
  },
  error => {
    return Promise.reject(error)
  }
)

// 响应拦截器
api.interceptors.response.use(
  (response: AxiosResponse) => {
    return response.data
  },
  error => {
    const { response } = error
    if (response) {
      console.error('API Error:', response.status, response.data)
      // 可以根据状态码进行不同处理
      if (response.status === 401) {
        // 清除token，跳转到登录页
        localStorage.removeItem('token')
        window.location.href = '/login'
      }
    } else {
      console.error('Network Error:', error.message)
    }
    return Promise.reject(error)
  }
)

export default api
export { API_CONFIG }