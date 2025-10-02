import axios, { AxiosResponse } from 'axios'
import { API_CONFIG } from '@/config/api'

interface Tag {
  id: number
  name: string
  codeType: string
  description?: string
  seqNo: number
  crtTime: string
  updTime: string
  // 添加颜色字段
  hexColor: string
  rgbColor: string
}

interface TagListParams {
  page?: number
  pageSize?: number
  name?: string
  codeType?: string
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

export const tagService = {
  // 获取标签列表
  getTagList(params: TagListParams = {}): Promise<PagedResponse<Tag>> {
    return api.get('/api/tag/list', { params })
  },

  // 根据ID获取标签
  getTagById(id: number): Promise<{ data: Tag }> {
    return api.get(`/api/tag/${id}`)
  }
}

export type { Tag, TagListParams, PagedResponse }