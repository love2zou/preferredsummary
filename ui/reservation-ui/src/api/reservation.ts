import request from '@/utils/request'
import type { ApiResponse } from './auth'

export interface ReservationMemberSummary {
  memberId: number
  name: string
  city: string
  membership: string
  homeClub: string
  remainingSessions: number
  expireAt: string
  avatarUrl?: string
}

export interface ReservationSessionType {
  id: number
  code: string
  label: string
  description: string
  price: number
  durationMinutes: number
}

export interface ReservationReview {
  id: number
  author: string
  rating: number
  tag: string
  content: string
}

export interface ReservationDateSlot {
  key: string
  label: string
  subLabel: string
  times: string[]
  moreLabel?: string
}

export interface ReservationTrainerCard {
  id: number
  userId: number
  name: string
  title: string
  photoUrl: string
  rating: number
  reviewCount: number
  years: number
  servedClients: number
  satisfaction: number
  price: number
  club: string
  area: string
  gender: string
  highlight: string
  heroTone: string
  accentTone: string
  goals: string[]
  specialties: string[]
  badges: string[]
  nextSlots: string[]
}

export interface ReservationTrainerDetail extends ReservationTrainerCard {
  introduction: string
  story: string
  certifications: string[]
  sessionTypes: ReservationSessionType[]
  availableDates: ReservationDateSlot[]
  reviews: ReservationReview[]
}

export interface ReservationOrderItem {
  id: number
  reservationNo: string
  trainerId: number
  trainerName: string
  trainerPhotoUrl: string
  sessionTypeId: number
  sessionLabel: string
  dateLabel: string
  calendarDate: string
  timeRange: string
  club: string
  area: string
  status: 'upcoming' | 'completed' | 'cancelled'
  tag: string
  note: string
  price: number
}

export interface ReservationHomeData {
  user: ReservationMemberSummary
  nextReservation?: ReservationOrderItem
  recommendedTrainers: ReservationTrainerCard[]
}

export interface ReservationBookingPage {
  trainer: ReservationTrainerDetail
  remainingSessions: number
}

export interface ReservationProfileData {
  user: ReservationMemberSummary
  upcomingCount: number
  completedCount: number
  cancelledCount: number
}

export interface TrainerQuery {
  clubId?: number
  goal?: string
  gender?: string
  keyword?: string
  sortBy?: string
}

export interface ReservationClub {
  id: number
  clubCode: string
  clubName: string
  city: string
  district?: string
  address?: string
  businessHours?: string
}

export interface ReservationCreateRequest {
  memberId: number
  trainerId: number
  sessionTypeId: number
  reservationDate: string
  startTime: string
  remark?: string
}

export interface ReservationCreateResultDto {
  reservationId: number
  reservationNo: string
  remainingSessions: number
}

export const reservationApi = {
  getHome(memberId: number): Promise<ApiResponse<ReservationHomeData>> {
    return request.get('/api/ReservationApp/home', { params: { memberId } })
  },
  getClubs(): Promise<ApiResponse<ReservationClub[]>> {
    return request.get('/api/ReservationApp/clubs')
  },
  getTrainers(params: TrainerQuery): Promise<ApiResponse<ReservationTrainerCard[]>> {
    return request.get('/api/ReservationApp/trainers', { params })
  },
  getTrainerDetail(trainerId: number): Promise<ApiResponse<ReservationTrainerDetail>> {
    return request.get(`/api/ReservationApp/trainers/${trainerId}`)
  },
  getBookingPage(trainerId: number, memberId: number): Promise<ApiResponse<ReservationBookingPage>> {
    return request.get(`/api/ReservationApp/trainers/${trainerId}/booking`, { params: { memberId } })
  },
  createReservation(data: ReservationCreateRequest): Promise<ApiResponse<ReservationCreateResultDto>> {
    return request.post('/api/ReservationApp/reservations', data)
  },
  getReservations(memberId: number, status?: string): Promise<ApiResponse<ReservationOrderItem[]>> {
    return request.get('/api/ReservationApp/reservations', { params: { memberId, status } })
  },
  cancelReservation(memberId: number, reservationId: number): Promise<ApiResponse<unknown>> {
    return request.post('/api/ReservationApp/reservations/cancel', { memberId, reservationId })
  },
  getProfile(memberId: number): Promise<ApiResponse<ReservationProfileData>> {
    return request.get('/api/ReservationApp/profile', { params: { memberId } })
  }
}
