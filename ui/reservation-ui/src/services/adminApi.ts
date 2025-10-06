import axios from 'axios'
import { ElMessage } from 'element-plus'

const isDev = import.meta.env.MODE === 'development'
const apiBaseURL = isDev ? 'http://localhost:5000/api' : 'http://159.75.184.108:8080/api'

const adminApi = axios.create({
    baseURL: apiBaseURL,
    timeout: 10000
})

adminApi.interceptors.request.use(
    (config) => {
        const token = localStorage.getItem('token')
        if (token) {
            config.headers.Authorization = `Bearer ${token}`
        }
        return config
    },
    (error) => Promise.reject(error)
)

// 响应拦截器：401 静默，无弹窗提示
adminApi.interceptors.response.use(
    (response) => response.data,
    (error) => {
        if (error.response?.status === 401) {
            // 静默处理，避免页面弹出“用户未授权”提示
            return Promise.reject(error)
        }
        ElMessage.error(error.response?.data?.message || '请求失败')
        return Promise.reject(error)
    }
)

export default adminApi