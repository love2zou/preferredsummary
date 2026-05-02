import { defineStore } from 'pinia'
import { computed, ref } from 'vue'
import {
  reservationApi,
  type ReservationBookingPage,
  type ReservationClub,
  type ReservationCreateRequest,
  type ReservationCreateResultDto,
  type ReservationHomeData,
  type ReservationOrderItem,
  type ReservationProfileData,
  type ReservationTrainerCard,
  type ReservationTrainerDetail,
  type TrainerQuery
} from '@/api/reservation'
import { useAuthStore } from './authStore'

interface BookingPayload {
  trainerId: number
  sessionTypeId: number
  calendarDate: string
  time: string
  note: string
}

export const useReservationStore = defineStore('reservation', () => {
  const authStore = useAuthStore()

  const clubs = ref<ReservationClub[]>([])
  const home = ref<ReservationHomeData | null>(null)
  const trainers = ref<ReservationTrainerCard[]>([])
  const trainerDetails = ref<Record<number, ReservationTrainerDetail>>({})
  const bookingPages = ref<Record<number, ReservationBookingPage>>({})
  const reservationsByStatus = ref<Record<'upcoming' | 'completed' | 'cancelled', ReservationOrderItem[]>>({
    upcoming: [],
    completed: [],
    cancelled: []
  })
  const profile = ref<ReservationProfileData | null>(null)
  const loading = ref(false)
  const dataMode = ref<'api' | 'mock'>('api')

  const memberId = computed(() => authStore.user?.userId ?? 0)
  const user = computed(() => home.value?.user ?? profile.value?.user ?? null)
  const nextReservation = computed(() => home.value?.nextReservation ?? null)
  const upcomingReservations = computed(() => reservationsByStatus.value.upcoming)
  const completedReservations = computed(() => reservationsByStatus.value.completed)
  const cancelledReservations = computed(() => reservationsByStatus.value.cancelled)

  const ensureMember = (): number => {
    if (!memberId.value) {
      throw new Error('当前未登录会员账号')
    }
    return memberId.value
  }

  const getTrainerById = (trainerId: number): ReservationTrainerCard | ReservationTrainerDetail | null => {
    return (
      trainerDetails.value[trainerId] ??
      trainers.value.find((trainer) => trainer.id === trainerId) ??
      home.value?.recommendedTrainers.find((trainer) => trainer.id === trainerId) ??
      null
    )
  }

  const useApiData = (): void => {
    dataMode.value = 'api'
  }

  const loadClubs = async (): Promise<void> => {
    const response = await reservationApi.getClubs()
    clubs.value = response.data ?? []
    useApiData()
  }

  const loadHome = async (): Promise<void> => {
    const response = await reservationApi.getHome(ensureMember())
    home.value = response.data
    useApiData()
  }

  const loadTrainers = async (query: TrainerQuery = {}): Promise<void> => {
    const response = await reservationApi.getTrainers(query)
    trainers.value = response.data ?? []
    useApiData()
  }

  const loadTrainerDetail = async (trainerId: number): Promise<ReservationTrainerDetail | null> => {
    if (trainerDetails.value[trainerId]) {
      return trainerDetails.value[trainerId]
    }

    const response = await reservationApi.getTrainerDetail(trainerId)
    trainerDetails.value[trainerId] = response.data
    useApiData()
    return response.data
  }

  const loadBookingPage = async (trainerId: number): Promise<ReservationBookingPage | null> => {
    const response = await reservationApi.getBookingPage(trainerId, ensureMember())
    bookingPages.value[trainerId] = response.data
    trainerDetails.value[trainerId] = response.data.trainer
    useApiData()
    return response.data
  }

  const loadReservations = async (status: 'upcoming' | 'completed' | 'cancelled'): Promise<void> => {
    const response = await reservationApi.getReservations(ensureMember(), status)
    reservationsByStatus.value[status] = response.data ?? []
    useApiData()
  }

  const loadProfile = async (): Promise<void> => {
    const response = await reservationApi.getProfile(ensureMember())
    profile.value = response.data
    useApiData()
  }

  const refreshMemberData = async (): Promise<void> => {
    loading.value = true
    try {
      await Promise.all([
        loadHome(),
        loadProfile(),
        loadReservations('upcoming'),
        loadReservations('completed'),
        loadReservations('cancelled')
      ])
    } finally {
      loading.value = false
    }
  }

  const bookTrainer = async (payload: BookingPayload): Promise<ReservationCreateResultDto> => {
    const request: ReservationCreateRequest = {
      memberId: ensureMember(),
      trainerId: payload.trainerId,
      sessionTypeId: payload.sessionTypeId,
      reservationDate: payload.calendarDate,
      startTime: payload.time,
      remark: payload.note
    }

    const response = await reservationApi.createReservation(request)
    useApiData()

    delete bookingPages.value[payload.trainerId]
    await refreshMemberData()
    return response.data
  }

  const cancelReservation = async (reservationId: number): Promise<void> => {
    await reservationApi.cancelReservation(ensureMember(), reservationId)
    useApiData()
    await refreshMemberData()
  }

  const reset = (): void => {
    clubs.value = []
    home.value = null
    trainers.value = []
    trainerDetails.value = {}
    bookingPages.value = {}
    reservationsByStatus.value = {
      upcoming: [],
      completed: [],
      cancelled: []
    }
    profile.value = null
    dataMode.value = 'api'
  }

  return {
    clubs,
    home,
    trainers,
    trainerDetails,
    bookingPages,
    reservationsByStatus,
    profile,
    loading,
    dataMode,
    user,
    nextReservation,
    upcomingReservations,
    completedReservations,
    cancelledReservations,
    getTrainerById,
    loadClubs,
    loadHome,
    loadTrainers,
    loadTrainerDetail,
    loadBookingPage,
    loadReservations,
    loadProfile,
    refreshMemberData,
    bookTrainer,
    cancelReservation,
    reset
  }
})
