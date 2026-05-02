import request from '@/utils/request'
import type { ApiResponse } from './auth'

export interface CoachReservationItem {
  id: number
  reservationNo: string
  memberId: number
  memberName: string
  memberAvatarUrl: string
  sessionName: string
  reservationDate: string
  timeRange: string
  statusCode: string
  statusLabel: string
  clubName: string
  remark: string
}

export interface CoachDashboardData {
  coachUserId: number
  trainerProfileId: number
  coachName: string
  title: string
  avatarUrl: string
  todayReservationCount: number
  upcomingReservationCount: number
  completedReservationCount: number
  boundMemberCount: number
  todayReservations: CoachReservationItem[]
}

export interface CoachMember {
  memberId: number
  memberName: string
  phoneNumber: string
  avatarUrl: string
  membershipName: string
  remainingSessions: number
  latestReservation: string
}

export interface CoachScheduleItem {
  startTime: string
  endTime: string
  isReserved: boolean
  memberName: string
  sessionName: string
}

export interface CoachScheduleData {
  scheduleDate: string
  items: CoachScheduleItem[]
}

export interface CoachProfileData {
  coachUserId: number
  trainerProfileId: number
  coachName: string
  userName: string
  email: string
  phoneNumber: string
  avatarUrl: string
  clubName: string
  title: string
  highlight: string
}

export const reservationCoachApi = {
  getDashboard(coachUserId: number): Promise<ApiResponse<CoachDashboardData>> {
    return request.get('/api/ReservationCoach/dashboard', { params: { coachUserId } })
  },
  getMembers(coachUserId: number): Promise<ApiResponse<CoachMember[]>> {
    return request.get('/api/ReservationCoach/members', { params: { coachUserId } })
  },
  getSchedule(coachUserId: number, scheduleDate?: string): Promise<ApiResponse<CoachScheduleData>> {
    return request.get('/api/ReservationCoach/schedule', { params: { coachUserId, scheduleDate } })
  },
  getReservations(coachUserId: number, statusCode?: string): Promise<ApiResponse<CoachReservationItem[]>> {
    return request.get('/api/ReservationCoach/reservations', { params: { coachUserId, statusCode } })
  },
  getProfile(coachUserId: number): Promise<ApiResponse<CoachProfileData>> {
    return request.get('/api/ReservationCoach/profile', { params: { coachUserId } })
  }
}
