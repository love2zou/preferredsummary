import { defineStore } from 'pinia'
import { ref } from 'vue'

export interface Booking {
  id: number
  memberId: number
  trainerId: number
  trainerName: string
  date: string
  timeSlot: string
  status: 'pending' | 'confirmed' | 'cancelled' | 'completed'
  createdAt: string
}

export interface TimeSlot {
  id: string
  time: string
  available: boolean
  trainerId?: number
  trainerName?: string
}

export const useBookingStore = defineStore('booking', () => {
  const bookings = ref<Booking[]>([])
  const availableSlots = ref<TimeSlot[]>([])

  // 获取用户的预约记录
  const fetchUserBookings = async (userId: number) => {
    // 模拟API调用
    const mockBookings: Booking[] = [
      {
        id: 1,
        memberId: userId,
        trainerId: 1,
        trainerName: '张教练',
        date: '2024-01-20',
        timeSlot: '09:00-10:00',
        status: 'confirmed',
        createdAt: '2024-01-15T10:00:00Z'
      },
      {
        id: 2,
        memberId: userId,
        trainerId: 2,
        trainerName: '李教练',
        date: '2024-01-22',
        timeSlot: '14:00-15:00',
        status: 'pending',
        createdAt: '2024-01-16T15:30:00Z'
      }
    ]
    bookings.value = mockBookings
  }

  // 获取可用时间段
  const fetchAvailableSlots = async (date: string) => {
    // 模拟API调用
    const mockSlots: TimeSlot[] = [
      { id: '1', time: '09:00-10:00', available: true, trainerId: 1, trainerName: '张教练' },
      { id: '2', time: '10:00-11:00', available: false },
      { id: '3', time: '11:00-12:00', available: true, trainerId: 2, trainerName: '李教练' },
      { id: '4', time: '14:00-15:00', available: true, trainerId: 1, trainerName: '张教练' },
      { id: '5', time: '15:00-16:00', available: true, trainerId: 3, trainerName: '王教练' },
      { id: '6', time: '16:00-17:00', available: false },
      { id: '7', time: '17:00-18:00', available: true, trainerId: 2, trainerName: '李教练' },
      { id: '8', time: '18:00-19:00', available: true, trainerId: 3, trainerName: '王教练' },
      { id: '9', time: '19:00-20:00', available: true, trainerId: 1, trainerName: '张教练' }
    ]
    availableSlots.value = mockSlots
  }

  // 创建预约
  const createBooking = async (bookingData: {
    trainerId: number
    trainerName: string
    date: string
    timeSlot: string
  }) => {
    // 模拟API调用
    const newBooking: Booking = {
      id: Date.now(),
      memberId: 1, // 当前用户ID
      ...bookingData,
      status: 'pending',
      createdAt: new Date().toISOString()
    }
    bookings.value.push(newBooking)
    return newBooking
  }

  // 取消预约
  const cancelBooking = async (bookingId: number) => {
    const booking = bookings.value.find(b => b.id === bookingId)
    if (booking) {
      booking.status = 'cancelled'
    }
  }

  return {
    bookings,
    availableSlots,
    fetchUserBookings,
    fetchAvailableSlots,
    createBooking,
    cancelBooking
  }
})