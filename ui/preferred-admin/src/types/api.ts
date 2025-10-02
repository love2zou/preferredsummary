// API 响应基础接口
export interface ApiResponse<T = any> {
  success: boolean
  message: string
  data: T
  errorCode?: string
}

// 登录响应数据类型
export interface LoginResponseData {
  token: string
  username: string
  email: string
  // 根据后端实际返回的用户信息添加其他字段
}

// 确保分页响应接口与后端一致
export interface PagedResponse<T> {
  success: boolean
  message: string
  data: {
    items: T[]           // 后端返回的数据列表
    totalCount: number   // 总记录数
    pageIndex: number    // 当前页索引
    pageSize: number     // 页大小
    totalPages: number   // 总页数
  }
}