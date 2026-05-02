import axios from 'axios'
import { ElMessage } from 'element-plus'
import { API_CONFIG } from '@/config/api'

const request = axios.create({
  baseURL: API_CONFIG.BASE_URL,
  timeout: API_CONFIG.TIMEOUT,
  headers: {
    'Content-Type': 'application/json'
  }
})

request.interceptors.request.use((config) => {
  const token = localStorage.getItem('reservation-ui-token')
  if (token) {
    config.headers.Authorization = `Bearer ${token}`
  }
  return config
})

request.interceptors.response.use(
  (response) => response.data,
  (error) => {
    const status = error.response?.status
    if (status === 401) {
      localStorage.removeItem('reservation-ui-token')
      localStorage.removeItem('reservation-ui-user')
      ElMessage.error('登录已失效，请重新登录')
    } else if (status === 404) {
      ElMessage.error(error.response?.data?.message || '请求资源不存在')
    } else {
      ElMessage.error(error.response?.data?.message || '请求失败，请稍后重试')
    }
    return Promise.reject(error)
  }
)

export default request
