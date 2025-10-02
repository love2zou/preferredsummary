import request from '@/utils/request'

// 通知相关的接口类型定义
export interface Notification {
  id: number
  title: string
  content: string
  type: string
  isRead: boolean
  userId: number
  crtTime: string
  updTime: string
}

// 修正 NotificationCreateDto 接口定义
// 通知创建DTO
export interface NotificationCreateDto {
  name: string
  content: string
  notifyType: string
  notifyStatus?: number
  sendTime: string
  sendUser: string
  receiver: string
  remark?: string  // 添加备注字段
  seqNo?: number
}

// 通知更新DTO
export interface NotificationUpdateDto {
  id: number
  isRead?: number
  name?: string
  content?: string
  notifyType?: string
  notifyStatus?: number
  sendTime?: string
  sendUser?: string
  receiver?: string
  remark?: string  // 添加备注字段
  seqNo?: number
}

// 通知列表DTO
export interface NotificationListDto {
  id: number
  isRead: number
  name: string
  content: string
  notifyType: string
  notifyStatus: number
  sendTime: string
  sendUser: string
  receiver: string
  remark?: string  // 添加备注字段
  seqNo: number
}

// 通知搜索参数
export interface NotificationSearchParams {
  page?: number
  pageSize?: number
  Name?: string
  Content?: string
  NotifyType?: string
  NotifyStatus?: number
  IsRead?: number
  SendUser?: string
  Receiver?: string
  StartTime?: string
  EndTime?: string
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

// 通知管理API
export const notificationApi = {
  // 获取通知列表
  getNotificationList(params: NotificationSearchParams): Promise<PagedResponse<NotificationListDto>> {
    return request.get('/api/notification/list', { params })
  },

  // 根据ID获取通知
  getNotificationById(id: number): Promise<NotificationListDto> {
    return request.get(`/api/notification/${id}`)
  },

  // 创建通知
  createNotification(data: NotificationCreateDto): Promise<ApiResponse<NotificationListDto>> {
    return request.post('/api/notification', data)
  },

  // 更新通知
  updateNotification(id: number, data: NotificationUpdateDto): Promise<ApiResponse<NotificationListDto>> {
    return request.put(`/api/notification/${id}`, data)
  },

  // 删除通知
  deleteNotification(id: number): Promise<ApiResponse<boolean>> {
    return request.delete(`/api/notification/${id}`)
  },

  // 批量删除通知
  batchDeleteNotifications(ids: number[]): Promise<ApiResponse<boolean>> {
    return request.post('/api/notification/batch-delete', ids)
  },

  // 标记为已读
  markAsRead(id: number): Promise<ApiResponse<boolean>> {
    return request.put(`/api/notification/${id}/read`)
  },

  // 批量标记为已读
  batchMarkAsRead(ids: number[]): Promise<ApiResponse<boolean>> {
    return request.post('/api/notification/batch-read', ids)
  }
}

export default notificationApi