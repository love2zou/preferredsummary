import request from '@/utils/request'

export interface PagedResponse<T> {
  data: T[]
  total: number
  page: number
  pageSize: number
  totalPages: number
}

export interface ApiResponse<T = unknown> {
  success: boolean
  message: string
  data?: T
}

export interface LookupOption {
  id: number
  label: string
  description?: string
}

export interface ReservationClub {
  id: number
  clubCode: string
  clubName: string
  city: string
  district?: string
  address?: string
  businessHours?: string
  isActive: boolean
  seqNo: number
  crtTime: string
  updTime: string
}

export interface ReservationClubEditParams {
  clubCode: string
  clubName: string
  city: string
  district?: string
  address?: string
  businessHours?: string
  isActive: boolean
  seqNo: number
}

export interface ReservationTrainer {
  id: number
  userId: number
  userName: string
  displayName: string
  clubId: number
  clubName: string
  title: string
  gender: string
  yearsOfExperience: number
  rating: number
  reviewCount: number
  servedClients: number
  satisfaction: number
  basePrice: number
  trainingArea?: string
  highlight?: string
  introduction?: string
  story?: string
  heroImageUrl?: string
  heroTone?: string
  accentTone?: string
  isRecommended: boolean
  isActive: boolean
  seqNo: number
  goals: string[]
  specialties: string[]
  badges: string[]
  certifications: string[]
  crtTime: string
  updTime: string
}

export interface ReservationTrainerEditParams {
  userId: number
  clubId: number
  displayName: string
  title: string
  gender: string
  yearsOfExperience: number
  rating: number
  reviewCount: number
  servedClients: number
  satisfaction: number
  basePrice: number
  trainingArea?: string
  highlight?: string
  introduction?: string
  story?: string
  heroImageUrl?: string
  heroTone?: string
  accentTone?: string
  isRecommended: boolean
  isActive: boolean
  seqNo: number
  goals: string[]
  specialties: string[]
  badges: string[]
  certifications: string[]
}

export interface ReservationSessionType {
  id: number
  trainerProfileId: number
  trainerName: string
  sessionCode: string
  sessionName: string
  description?: string
  durationMinutes: number
  price: number
  isActive: boolean
  seqNo: number
  crtTime: string
  updTime: string
}

export interface ReservationSessionTypeEditParams {
  trainerProfileId: number
  sessionCode: string
  sessionName: string
  description?: string
  durationMinutes: number
  price: number
  isActive: boolean
  seqNo: number
}

export interface ReservationScheduleSlot {
  id: number
  trainerProfileId: number
  trainerName: string
  clubId: number
  clubName: string
  scheduleDate: string
  startTime: string
  endTime: string
  isAvailable: boolean
  seqNo: number
  crtTime: string
  updTime: string
}

export interface ReservationScheduleSlotEditParams {
  trainerProfileId: number
  clubId: number
  scheduleDate: string
  startTime: string
  endTime: string
  isAvailable: boolean
  seqNo: number
}

export interface ReservationMemberPackage {
  id: number
  memberId: number
  memberName: string
  clubId: number
  clubName: string
  packageName: string
  membershipName?: string
  totalSessions: number
  remainingSessions: number
  effectiveDate: string
  expireDate: string
  statusCode: string
  seqNo: number
  crtTime: string
  updTime: string
}

export interface ReservationMemberPackageEditParams {
  memberId: number
  clubId: number
  packageName: string
  membershipName?: string
  totalSessions: number
  remainingSessions: number
  effectiveDate: string
  expireDate: string
  statusCode: string
  seqNo: number
}

export interface ReservationOrder {
  id: number
  reservationNo: string
  memberId: number
  memberName: string
  trainerProfileId: number
  trainerName: string
  clubId: number
  clubName: string
  sessionTypeId: number
  sessionName: string
  reservationDate: string
  startTime: string
  endTime: string
  priceAmount: number
  statusCode: string
  remark?: string
  crtTime: string
  updTime: string
}

export interface ReservationListParams {
  page?: number
  pageSize?: number
  keyword?: string
  clubId?: number
  trainerProfileId?: number
  memberId?: number
  statusCode?: string
  isActive?: boolean
  isAvailable?: boolean
  scheduleDate?: string
}

export const reservationAdminApi = {
  getClubOptions(): Promise<LookupOption[]> {
    return request.get('/api/reservationadmin/lookups/clubs')
  },
  getTrainerOptions(): Promise<LookupOption[]> {
    return request.get('/api/reservationadmin/lookups/trainers')
  },
  getUserOptions(keyword?: string): Promise<LookupOption[]> {
    return request.get('/api/reservationadmin/lookups/users', { params: { keyword } })
  },

  getClubList(params: ReservationListParams): Promise<PagedResponse<ReservationClub>> {
    return request.get('/api/reservationadmin/clubs/list', { params })
  },
  getClubDetail(id: number): Promise<ReservationClub> {
    return request.get(`/api/reservationadmin/clubs/${id}`)
  },
  createClub(data: ReservationClubEditParams): Promise<ApiResponse> {
    return request.post('/api/reservationadmin/clubs', data)
  },
  updateClub(id: number, data: ReservationClubEditParams): Promise<ApiResponse> {
    return request.put(`/api/reservationadmin/clubs/${id}`, data)
  },
  deleteClub(id: number): Promise<ApiResponse> {
    return request.delete(`/api/reservationadmin/clubs/${id}`)
  },

  getTrainerList(params: ReservationListParams): Promise<PagedResponse<ReservationTrainer>> {
    return request.get('/api/reservationadmin/trainers/list', { params })
  },
  getTrainerDetail(id: number): Promise<ReservationTrainer> {
    return request.get(`/api/reservationadmin/trainers/${id}`)
  },
  createTrainer(data: ReservationTrainerEditParams): Promise<ApiResponse> {
    return request.post('/api/reservationadmin/trainers', data)
  },
  updateTrainer(id: number, data: ReservationTrainerEditParams): Promise<ApiResponse> {
    return request.put(`/api/reservationadmin/trainers/${id}`, data)
  },
  deleteTrainer(id: number): Promise<ApiResponse> {
    return request.delete(`/api/reservationadmin/trainers/${id}`)
  },

  getSessionList(params: ReservationListParams): Promise<PagedResponse<ReservationSessionType>> {
    return request.get('/api/reservationadmin/sessions/list', { params })
  },
  getSessionDetail(id: number): Promise<ReservationSessionType> {
    return request.get(`/api/reservationadmin/sessions/${id}`)
  },
  createSession(data: ReservationSessionTypeEditParams): Promise<ApiResponse> {
    return request.post('/api/reservationadmin/sessions', data)
  },
  updateSession(id: number, data: ReservationSessionTypeEditParams): Promise<ApiResponse> {
    return request.put(`/api/reservationadmin/sessions/${id}`, data)
  },
  deleteSession(id: number): Promise<ApiResponse> {
    return request.delete(`/api/reservationadmin/sessions/${id}`)
  },

  getScheduleList(params: ReservationListParams): Promise<PagedResponse<ReservationScheduleSlot>> {
    return request.get('/api/reservationadmin/schedules/list', { params })
  },
  getScheduleDetail(id: number): Promise<ReservationScheduleSlot> {
    return request.get(`/api/reservationadmin/schedules/${id}`)
  },
  createSchedule(data: ReservationScheduleSlotEditParams): Promise<ApiResponse> {
    return request.post('/api/reservationadmin/schedules', data)
  },
  updateSchedule(id: number, data: ReservationScheduleSlotEditParams): Promise<ApiResponse> {
    return request.put(`/api/reservationadmin/schedules/${id}`, data)
  },
  deleteSchedule(id: number): Promise<ApiResponse> {
    return request.delete(`/api/reservationadmin/schedules/${id}`)
  },

  getPackageList(params: ReservationListParams): Promise<PagedResponse<ReservationMemberPackage>> {
    return request.get('/api/reservationadmin/packages/list', { params })
  },
  getPackageDetail(id: number): Promise<ReservationMemberPackage> {
    return request.get(`/api/reservationadmin/packages/${id}`)
  },
  createPackage(data: ReservationMemberPackageEditParams): Promise<ApiResponse> {
    return request.post('/api/reservationadmin/packages', data)
  },
  updatePackage(id: number, data: ReservationMemberPackageEditParams): Promise<ApiResponse> {
    return request.put(`/api/reservationadmin/packages/${id}`, data)
  },
  deletePackage(id: number): Promise<ApiResponse> {
    return request.delete(`/api/reservationadmin/packages/${id}`)
  },

  getOrderList(params: ReservationListParams): Promise<PagedResponse<ReservationOrder>> {
    return request.get('/api/reservationadmin/orders/list', { params })
  },
  updateOrderStatus(id: number, statusCode: string): Promise<ApiResponse> {
    return request.put(`/api/reservationadmin/orders/${id}/status`, { statusCode })
  }
}

export default reservationAdminApi
