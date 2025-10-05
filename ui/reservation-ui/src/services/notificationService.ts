import api from '@/api'

export interface PagedResponse<T> {
  data?: T[]
  Data?: T[]
  total?: number
  Total?: number
  page?: number
  Page?: number
  pageSize?: number
  PageSize?: number
  totalPages?: number
  TotalPages?: number
}

export interface NotificationItem {
  id?: number
  name?: string
  title?: string
  content?: string
  message?: string
  notifyType?: string
  notifyStatus?: number
  isRead?: number | boolean
  sendUser?: string
  receiver?: string
  createdAt?: string
  crtTime?: string
}

export const notificationService = {
  getUnreadCount(receiver: string) {
    return api.get('/Notification/unread-count', { params: { receiver } })
  },
  list(params: { page?: number; size?: number; receiver?: string; name?: string; content?: string; notifyType?: string; notifyStatus?: number; isRead?: number; startTime?: string; endTime?: string }) {
    return api.get<PagedResponse<NotificationItem>>('/Notification/list', { params })
  },
  markRead(id: number) {
    return api.put(`/Notification/${id}/read`)
  }
}