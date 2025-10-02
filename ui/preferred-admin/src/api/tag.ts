import request from '@/utils/request'

// 标签相关的接口类型定义
export interface Tag {
  id: number
  parName: string
  tagCode: string
  tagName: string
  hexColor: string
  rgbColor: string
  seqNo: number
  crtTime: string
  updTime: string
}

export interface TagDto {
  parName: string
  tagCode: string
  tagName: string
  hexColor: string
  rgbColor: string
  seqNo: number
}

export interface TagSearchParams {
  parName?: string
  tagCode?: string
  tagName?: string
}

export interface TagListParams {
  page?: number
  pageSize?: number
  parName?: string
  tagCode?: string
  tagName?: string
}

export interface PagedResponse<T> {
  data: T[]
  total: number
  page: number
  pageSize: number
  totalPages: number
}


// 添加ApiResponse接口定义
export interface ApiResponse<T> {
  success: boolean
  message: string
  data: T
}

// 导入相关接口
export interface ImportResult {
  totalCount: number
  successCount: number
  failCount: number
  resultFile: string
  resultFileName:string
}

// 标签管理 API 接口
// 在 tagApi 对象中添加批量删除方法（大约第100行附近）
export const tagApi = {
  // 获取标签列表（分页）
  getTagList(params: TagListParams): Promise<PagedResponse<Tag>> {
    return request.get('/api/tag/list', { params })
  },

  // 获取标签总数
  getTagCount(params?: TagSearchParams): Promise<number> {
    return request.get('/api/tag/count', { params })
  },

  // 根据ID获取标签详情
  getTagById(id: number): Promise<Tag> {
    return request.get(`/api/tag/${id}`)
  },

  // 根据应用模块获取标签列表
  getTagsByModule(parName: string): Promise<ApiResponse<Tag[]>> {
    return request.get(`/api/tag/by-module/${parName}`)
  },

  // 创建标签
  createTag(data: TagDto): Promise<Tag> {
    return request.post('/api/tag', data)
  },

  // 更新标签
  updateTag(id: number, data: TagDto): Promise<Tag> {
    return request.put(`/api/tag/${id}`, data)
  },

  // 删除标签
  deleteTag(id: number): Promise<void> {
    return request.delete(`/api/tag/${id}`)
  },

  // 检查标签代码是否存在
  checkTagCodeExists(parName: string, tagCode: string, excludeId?: number): Promise<boolean> {
    const params: any = { parName, tagCode }
    if (excludeId) {
      params.excludeId = excludeId
    }
    return request.get('/api/tag/check-code', { params })
  },

  // 获取应用模块列表
  getParNameList(): Promise<string[]> {
    return request.get('/api/tag/modules') as Promise<string[]>
  },


  // 下载导入模板
  downloadTemplate(): Promise<ArrayBuffer> {
    return request({
      url: '/api/Tag/download-template',
      method: 'get',
      responseType: 'arraybuffer'
    })
  },

  // 导入标签
  importTags(formData: FormData): Promise<ApiResponse<ImportResult>> {
    return request({
      url: '/api/Tag/import',
      method: 'post',
      data: formData,
      headers: {
        'Content-Type': 'multipart/form-data'
      }
    })
  },

  // 批量删除标签
  batchDeleteTags(ids: number[]): Promise<void> {
    return request({
      url: '/api/Tag/batch',
      method: 'delete',
      data: ids
    })
  }
}