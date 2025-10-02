import request from '@/utils/request'

// 分类相关的接口类型定义
export interface Category {
  id: number
  categoryCode: string
  categoryName: string
  categoryIcon: string
  description: string
  seqNo: number
  crtTime: string
  updTime: string
}

export interface CategoryDto {
  categoryCode: string
  categoryName: string
  categoryIcon: string
  description: string
  seqNo: number
}

export interface CategorySearchParams {
  categoryCode?: string
  categoryName?: string
  description?: string
}

export interface CategoryListParams {
  page?: number
  pageSize?: number
  categoryCode?: string
  categoryName?: string
  description?: string
}

export interface PagedResponse<T> {
  data: T[]
  total: number
  page: number
  pageSize: number
  totalPages: number
}

export interface ApiResponse<T> {
  success: boolean
  message: string
  data: T
}

// 分类管理API
export const categoryApi = {
  // 获取分类列表
  getCategoryList(params: CategoryListParams): Promise<PagedResponse<Category>> {
    return request.get('/api/category/list', { params })
  },

  // 根据ID获取分类
  getCategoryById(id: number): Promise<Category> {
    return request.get(`/api/category/${id}`)
  },

  // 创建分类
  createCategory(data: CategoryDto): Promise<ApiResponse<Category>> {
    return request.post('/api/category', data)
  },

  // 更新分类
  updateCategory(id: number, data: CategoryDto): Promise<ApiResponse<Category>> {
    return request.put(`/api/category/${id}`, data)
  },

  // 删除分类
  deleteCategory(id: number): Promise<ApiResponse<void>> {
    return request.delete(`/api/category/${id}`)
  },

  // 批量删除分类
  batchDeleteCategories(ids: number[]): Promise<ApiResponse<void>> {
    return request.delete('/api/category/batch', { data: { ids } })
  },

  // 检查分类代码是否存在
  checkCategoryCodeExists(categoryCode: string, excludeId?: number): Promise<boolean> {
    const params: any = { categoryCode }
    if (excludeId) {
      params.excludeId = excludeId
    }
    return request.get('/api/category/check-code', { params })
  },

  // 获取所有分类代码列表（用于下拉选择）
  getCategoryCodeList(): Promise<string[]> {
    return request.get('/api/category/codes') as Promise<string[]>
  },

  // 更新分类排序
  updateCategorySeqNo(id: number, seqNo: number): Promise<ApiResponse<void>> {
    return request.put(`/api/category/${id}/seqno`, { seqNo })
  }
}

export default categoryApi