import api from './api'

interface Category {
  id: number
  categoryCode: string
  categoryName: string
  categoryIcon: string
  description: string
  seqNo: number
  crtTime: string
  updTime: string
}

interface PagedResponse<T> {
  data: T[]
  total: number
  page: number
  pageSize: number
  totalPages: number
}

interface CategoryListParams {
  page?: number
  pageSize?: number
  categoryName?: string
  categoryCode?: string
}

export const categoryService = {
  // 获取分类列表
  getCategoryList(params: CategoryListParams = {}): Promise<PagedResponse<Category>> {
    return api.get('/api/category/list', { params })
  },

  // 根据ID获取分类
  getCategoryById(id: number): Promise<{ data: Category }> {
    return api.get(`/api/category/${id}`)
  }
}

export type { Category, CategoryListParams, PagedResponse }