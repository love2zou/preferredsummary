import request from '@/utils/request'

// 访问地址相关的接口类型定义
// 更新 NetworkUrl 接口
export interface NetworkUrl {
  id: number
  url: string
  name: string
  description?: string
  imageCode: string
  categoryCode?: string // 新增分类菜单字段
  iconUrl?: string
  isAvailable?: number
  isMark: number
  tagCodeType: string
  seqNo: number
  crtTime: string
  updTime: string
}

// 更新 NetworkUrlDto 接口
export interface NetworkUrlDto {
  name: string
  url: string
  description?: string
  imageCode: string
  categoryCode?: string // 新增分类菜单字段
  isAvailable?: number
  isMark: number
  tagCodeType: string
  seqNo: number
}

// 更新 NetworkUrlListDto 接口
export interface NetworkUrlListDto {
  id: number
  url: string
  name: string
  description?: string
  imageCode: string
  categoryCode?: string // 新增分类菜单字段
  iconUrl?: string
  isAvailable?: number
  isMark: number
  tagCodeType: string
  seqNo: number
  crtTime: string
  updTime: string
}

// 更新 NetworkUrlSearchParams 接口
export interface NetworkUrlSearchParams {
  name?: string
  categoryCode?: string // 新增分类菜单搜索字段
  tagCodeTypes?: string[]
  isMark?: number
  isAvailable?: number
}

// 更新 NetworkUrlListParams 接口
export interface NetworkUrlListParams {
  page?: number
  pageSize?: number
  name?: string
  categoryCode?: string // 新增分类菜单搜索字段
  tagCodeType?: string
  isMark?: number
  isAvailable?: number
}

export interface PagedResponse<T> {
  data: T[]
  total: number
  page: number
  pageSize: number
  totalPages: number
}

// 访问地址管理 API 接口
// 在接口定义部分添加
export interface ApiResponse<T = any> {
  success: boolean
  message: string
  data?: T
}

// 或者为简单的成功响应定义专门的类型
export interface SimpleApiResponse {
  success: boolean
  message: string
}

// 然后更新方法定义
export const networkUrlApi = {
  // 获取访问地址列表（分页）
  getNetworkUrlList(params: NetworkUrlListParams): Promise<PagedResponse<NetworkUrlListDto>> {
    return request.get('/api/networkurl/list', { params })
  },

  // 根据ID获取访问地址详情
  getNetworkUrlById(id: number): Promise<ApiResponse<NetworkUrl>> {
    return request.get(`/api/networkurl/${id}`)
  },

  // 根据标签类型获取访问地址列表
  getNetworkUrlsByTagType(tagCodeType: string): Promise<ApiResponse<NetworkUrlListDto[]>> {
    return request.get(`/api/networkurl/by-tag-type/${tagCodeType}`)
  },

  // 创建访问地址
  createNetworkUrl(data: NetworkUrlDto): Promise<SimpleApiResponse> {
    return request.post('/api/networkurl/create', data)
  },

  // 更新访问地址
  updateNetworkUrl(id: number, data: NetworkUrlDto): Promise<SimpleApiResponse> {
    return request.put(`/api/networkurl/${id}`, data)
  },

  // 删除访问地址
  deleteNetworkUrl(id: number): Promise<SimpleApiResponse> {
    return request.delete(`/api/networkurl/${id}`)
  }
}