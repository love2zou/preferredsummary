import api from '@/api'

export interface BoundCoach {
  coachId: number
  coachName: string
}

export interface AvailableSlot {
  slotId: number
  startTime: string // e.g. '09:00'
  endTime: string   // e.g. '09:30'
  isAvailable: boolean
}

export interface CreateBatchRequest {
  memberId: number
  coachId: number
  bookDate: string // 'YYYY-MM-DD'
  timeSlots: { startTime: string; endTime: string }[]
}

export interface BookingItem {
  id: number
  memberId: number
  coachId: number
  coachName: string
  memberName?: string // 教练视图展示会员姓名（若后端返回）
  bookDate: string // 'YYYY-MM-DD'
  startTime: string
  endTime: string
  status: number // 0: 已预约, 9: 已取消
}

export interface ApiResponse<T> {
  success: boolean
  message: string
  data: T
  errorCode?: string
}

// 新增：教练绑定的会员精简信息
export interface BoundMember {
  id: number
  userName: string
  phoneNumber?: string
}

export interface ReservedByDate {
  startTime: string
  endTime: string
  memberName: string
}

export const bookingService = {
  getBoundCoaches(memberId: number) {
    return api.get<ApiResponse<BoundCoach[]>>('/booking/bound-coaches', { params: { memberId } })
  },
  // 新增：获取教练绑定的会员列表（需要后端提供 /booking/bound-members）
  getBoundMembers(coachId: number) {
    return api.get<ApiResponse<BoundMember[]>>('/booking/bound-members', { params: { coachId } })
  },
  bindCoach(memberId: number, coachId: number) {
    return api.post<ApiResponse<any>>('/booking/bind', { memberId, coachId })
  },
  unbindCoach(memberId: number, coachId: number) {
    return api.post<ApiResponse<any>>('/booking/unbind', { memberId, coachId })
  },
  getAvailableSlots(coachId: number, bookDate: string) {
    return api.get<ApiResponse<AvailableSlot[]>>('/booking/available-slots', { params: { coachId, bookDate } })
  },
  batchCreate(payload: CreateBatchRequest) {
    return api.post<ApiResponse<any>>('/booking/batch-create', payload)
  },
  list(memberId: number) {
    return api.get<ApiResponse<BookingItem[]>>('/booking/list', { params: { memberId } })
  },
  listByCoach(coachId: number) {
    return api.get<ApiResponse<BookingItem[]>>('/booking/listbycoach', { params: { coachId } })
  },
  cancel(bookingId: number) {
    return api.post<ApiResponse<any>>('/booking/cancel', { id: bookingId })
  },
  getReservedByDate(coachId: number, bookDate: string) {
    return api.get<ApiResponse<ReservedByDate[]>>('/booking/reserved-by-date', { params: { coachId, bookDate } })
  },
}