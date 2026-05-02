import { defineStore } from 'pinia'
import { ref } from 'vue'
import {
  reservationCoachApi,
  type CoachDashboardData,
  type CoachMember,
  type CoachProfileData,
  type CoachReservationItem,
  type CoachScheduleData
} from '@/api/reservationCoach'
import { useAuthStore } from './authStore'

export const useCoachStore = defineStore('reservation-coach', () => {
  const authStore = useAuthStore()

  const dashboard = ref<CoachDashboardData | null>(null)
  const members = ref<CoachMember[]>([])
  const schedule = ref<CoachScheduleData | null>(null)
  const reservations = ref<CoachReservationItem[]>([])
  const profile = ref<CoachProfileData | null>(null)

  const ensureCoach = (): number => {
    const coachUserId = authStore.user?.userId ?? 0
    if (!coachUserId) {
      throw new Error('当前未登录教练账号')
    }
    return coachUserId
  }

  const loadDashboard = async (): Promise<void> => {
    const response = await reservationCoachApi.getDashboard(ensureCoach())
    dashboard.value = response.data
  }

  const loadMembers = async (): Promise<void> => {
    const response = await reservationCoachApi.getMembers(ensureCoach())
    members.value = response.data ?? []
  }

  const loadSchedule = async (scheduleDate?: string): Promise<void> => {
    const response = await reservationCoachApi.getSchedule(ensureCoach(), scheduleDate)
    schedule.value = response.data
  }

  const loadReservations = async (statusCode?: string): Promise<void> => {
    const response = await reservationCoachApi.getReservations(ensureCoach(), statusCode)
    reservations.value = response.data ?? []
  }

  const loadProfile = async (): Promise<void> => {
    const response = await reservationCoachApi.getProfile(ensureCoach())
    profile.value = response.data
  }

  const refresh = async (): Promise<void> => {
    await Promise.all([
      loadDashboard(),
      loadMembers(),
      loadSchedule(),
      loadReservations(),
      loadProfile()
    ])
  }

  const reset = (): void => {
    dashboard.value = null
    members.value = []
    schedule.value = null
    reservations.value = []
    profile.value = null
  }

  return {
    dashboard,
    members,
    schedule,
    reservations,
    profile,
    loadDashboard,
    loadMembers,
    loadSchedule,
    loadReservations,
    loadProfile,
    refresh,
    reset
  }
})
