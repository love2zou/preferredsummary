import axios, { AxiosResponse } from 'axios'
import { API_CONFIG } from '@/config/api'

// 添加 TagInfo 接口定义
interface TagInfo {
  codeType: string
  name: string
  hexColor: string
  rgbColor: string
}

interface NetworkUrl {
  id: number
  url: string
  name: string
  description?: string
  imageCode: string
  categoryCode?: string
  iconUrl?: string
  isAvailable?: number
  isMark: number
  tagCodeType: string
  seqNo: number
  crtTime: string
  updTime: string
}

// 修改 NetworkUrlListDto 接口以匹配后端
interface NetworkUrlListDto {
  id: number
  url: string
  name: string
  description?: string
  imageCode: string
  categoryCode?: string
  isAvailable?: number | boolean
  isMark: number | boolean
  tagCodeType: string
  seqNo: number
  crtTime: string
  updTime: string
  // 后端新增的字段
  pictureUrl?: string
  pictureName?: string
  categoryName?: string
  categoryIcon?: string
  categoryDescription?: string
  tags?: TagInfo[]      // 多标签支持
  // 前端扩展字段
  imageError?: boolean
}

interface Picture {
  id: number
  code: string
  name: string
  url: string
  description?: string
}

interface PagedResponse<T> {
  data: T[]
  total: number
  page: number
  pageSize: number
  totalPages: number
}

interface ApiResponse<T = unknown> {
  success: boolean
  message: string
  data: T
}

interface NetworkUrlClickCountDto {
  id: number
  seqNo: number
}

interface NetworkUrlListParams {
  page?: number
  pageSize?: number
  name?: string
  categoryCode?: string
  tagCodeType?: string
  isMark?: number
  isAvailable?: number
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

export const networkUrlService = {
  // 获取访问地址列表（包含标签信息）
  getNetworkUrlList(params: NetworkUrlListParams = {}): Promise<PagedResponse<NetworkUrlListDto>> {
    return api.get('/api/networkurl/list', { params })
  },

  // 根据ID获取访问地址
  getNetworkUrlById(id: number): Promise<{ data: NetworkUrl }> {
    return api.get(`/api/networkurl/${id}`)
  },

  // 累加点击次数，后端使用seqNo字段存储
  incrementClickCount(id: number): Promise<ApiResponse<NetworkUrlClickCountDto>> {
    return api.post(`/api/networkurl/${id}/click`)
  },

  // 根据分类获取访问地址
  getNetworkUrlsByCategory(categoryCode: string): Promise<PagedResponse<NetworkUrlListDto>> {
    return api.get('/api/networkurl/list', { 
      params: { categoryCode } 
    })
  }
}

// 导出时添加 TagInfo
export type { ApiResponse, NetworkUrl, NetworkUrlClickCountDto, NetworkUrlListDto, NetworkUrlListParams, Picture, PagedResponse, TagInfo }
