import request from '@/utils/request'

// 用户相关的接口类型定义
export interface User {
  id: number
  userName: string
  email: string
  phoneNumber?: string | null
  profilePictureUrl?: string | null
  bio?: string | null
  userTypeCode?: string | null        // 改名：code
  userToSystemCode?: string | null    // 改名：code
  isActive: boolean
  isEmailVerified: boolean
  crtTime: string
  updTime?: string
  lastLoginTime?: string | null
}

export interface UserListParams {
  page?: number
  size?: number
  username?: string
  email?: string
  isActive?: boolean
}

export interface UserCreateParams {
  username: string
  email: string
  password: string
  phoneNumber?: string
  bio?: string
  userTypeCode?: string            // 改名：code
  userToSystemCode?: string        // 改名：code
  profilePictureUrl?: string       // 新增：头像URL
}

export interface UserUpdateParams {
  email?: string
  phoneNumber?: string
  bio?: string
  isActive?: boolean
  userTypeCode?: string            // 改名：code
  userToSystemCode?: string        // 改名：code
  profilePictureUrl?: string       // 新增：头像URL
}

export interface PagedResponse<T> {
  data: T[]
  total: number
  page: number
  pageSize: number
  totalPages: number
}

export interface ChangePasswordParams {
  userId: number
  newPassword: string
}

// 用户管理 API 接口
export const userApi = {
  // 获取用户列表
  getUserList(params: UserListParams): Promise<PagedResponse<User>> {
    return request.get('/api/user/list', { params })
  },

  // 获取用户详情
  getUserDetail(id: number): Promise<User> {
    return request.get(`/api/user/detail/${id}`)
  },

  // 创建用户
  createUser(data: UserCreateParams): Promise<User> {
    return request.post('/api/user/create', data)
  },

  // 更新用户
  updateUser(id: number, data: UserUpdateParams): Promise<User> {
    return request.put(`/api/user/update/${id}`, data)
  },

  // 删除用户
  deleteUser(id: number): Promise<void> {
    return request.delete(`/api/user/delete/${id}`)
  },

  // 批量删除用户
  batchDeleteUsers(ids: number[]): Promise<void> {
    return request.delete('/api/user/batch-delete', { data: { ids } })
  },

  // 切换用户状态
  toggleUserStatus(id: number): Promise<User> {
    return request.patch(`/api/user/toggle-status/${id}`)
  },

  // 修改用户密码
  changePassword(data: ChangePasswordParams): Promise<void> {
    return request.post('/api/user/change-password', data)
  }
}