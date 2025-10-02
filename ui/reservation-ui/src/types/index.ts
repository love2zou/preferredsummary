// 用户相关类型
export interface User {
  id: number
  username: string
  email: string
  phone: string
  userType: 'member' | 'trainer'
  avatar?: string
  createdAt: string
}

export interface LoginRequest {
  username: string
  password: string
  userType: 'member' | 'trainer'
}

export interface RegisterRequest {
  username: string
  email: string
  phone: string
  password: string
  confirmPassword: string
  userType: 'member' | 'trainer'
}

// 预约相关类型
export interface Booking {
  id: number
  userId: number
  trainerId: number
  trainerName: string
  date: string
  timeSlot: string
  status: 'pending' | 'confirmed' | 'cancelled' | 'completed'
  createdAt: string
}

export interface CreateBookingRequest {
  trainerId: number
  date: string
  timeSlot: string
}

// 教练相关类型
export interface Trainer {
  id: number
  name: string
  specialty: string
  experience: string
  rating: number
  avatar: string
  price: number
  description: string
}

// 时间段类型
export interface TimeSlot {
  id: string
  time: string
  available: boolean
}