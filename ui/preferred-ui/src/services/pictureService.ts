import axios, { AxiosResponse } from 'axios'
import { API_CONFIG } from '@/config/api'

interface Picture {
  id: number
  code: string
  name: string
  url: string
  description?: string
  crtTime: string
  updTime: string
}

interface PictureListParams {
  page?: number
  pageSize?: number
  name?: string
  code?: string
}

interface PagedResponse<T> {
  data: T[]
  total: number
  page: number
  pageSize: number
  totalPages: number
}

const api = axios.create({
  baseURL: API_CONFIG.BASE_URL,
  timeout: API_CONFIG.TIMEOUT,
  headers: {
    'Content-Type': 'application/json'
  }
})

// 请求拦截器
api.interceptors.request.use(
  config => {
    const token = localStorage.getItem('token')
    if (token) {
      config.headers.Authorization = `Bearer ${token}`
    }
    
    // 添加时间戳防止缓存
    if (config.method === 'get') {
      config.params = {
        ...config.params,
        _t: Date.now()
      }
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
    if (error.response?.status === 401) {
      localStorage.removeItem('token')
      localStorage.removeItem('username')
      window.location.href = '/login'
    }
    return Promise.reject(error)
  }
)

export const pictureService = {
  // 根据代码获取图片
  getPictureByCode(code: string): Promise<{ data: Picture }> {
    return api.get(`/api/picture/by-code/${code}`)
  },

  // 获取图片列表
  getPictureList(params: PictureListParams = {}): Promise<PagedResponse<Picture>> {
    return api.get('/api/picture/list', { params })
  }
}

export type { Picture, PictureListParams, PagedResponse }