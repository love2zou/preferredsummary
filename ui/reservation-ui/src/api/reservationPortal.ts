import request from '@/utils/request'
import type { ApiResponse } from './auth'
import type { ReservationOrderItem } from './reservation'
import type {
  CoachDashboardData,
  CoachMember,
  CoachReservationItem,
  CoachScheduleData
} from './reservationCoach'

export interface ReservationReviewMetric {
  label: string
  score: number
  description: string
}

export interface ReservationConversationMessage {
  senderRole: string
  senderName: string
  content: string
  sentTime: string
}

export interface ReservationCheckInData {
  reservationOrderId: number
  coachName: string
  checkInMethod: string
  checkInTime: string
  clubName: string
  areaName: string
}

export interface ReservationFlowData {
  upcomingReservation?: ReservationOrderItem | null
  completedReservation?: ReservationOrderItem | null
  checkIn?: ReservationCheckInData | null
  messages: ReservationConversationMessage[]
  cancelReasons: string[]
  reviewMetrics: ReservationReviewMetric[]
  reviewTags: string[]
}

export interface ReservationCoursePackage {
  id: number
  packageCode: string
  packageName: string
  categoryName: string
  summary: string
  coverImageUrl: string
  originalPrice: number
  salePrice: number
  totalSessions: number
  validDays: number
  coachLevel: string
  clubScope: string
  isRecommended: boolean
  statusCode: string
}

export interface ReservationCourseOrder {
  id: number
  orderNo: string
  packageName: string
  coverImageUrl: string
  originAmount: number
  discountAmount: number
  pointDiscountAmount: number
  payAmount: number
  paymentMethod: string
  statusCode: string
  orderTime: string
}

export interface ReservationMemberCoupon {
  couponId: number
  title: string
  couponType: string
  discountValue: number
  minAmount: number
  ruleText: string
  startDate: string
  endDate: string
  statusCode: string
}

export interface ReservationCommerceData {
  packages: ReservationCoursePackage[]
  featuredPackage?: ReservationCoursePackage | null
  orders: ReservationCourseOrder[]
  coupons: ReservationMemberCoupon[]
}

export interface ReservationMemberProfile {
  memberId: number
  name: string
  phoneNumber: string
  avatarUrl: string
  gender: string
  age: number
  heightCm: number
  weightKg: number
  birthday: string
  city: string
  membershipName: string
  healthRemark: string
  primaryGoal: string
  secondaryGoals: string[]
}

export interface ReservationBodyMetric {
  measureTime: string
  weightKg: number
  bodyFatRate: number
  bmi: number
  muscleKg: number
}

export interface ReservationTrainingRecord {
  recordTime: string
  title: string
  coachName: string
  durationMinutes: number
  calories: number
  locationName: string
  statusCode: string
  summary: string
}

export interface ReservationTrainingPlanItem {
  dayLabel: string
  title: string
  durationMinutes: number
  caloriesTarget: number
  isCompleted: boolean
}

export interface ReservationTrainingPlan {
  planName: string
  goal: string
  startDate: string
  endDate: string
  progressText: string
  items: ReservationTrainingPlanItem[]
}

export interface ReservationMemberNotification {
  title: string
  content: string
  notifyType: string
  sendTime: string
}

export interface ReservationMemberCenterData {
  profile: ReservationMemberProfile
  bodyMetrics: ReservationBodyMetric[]
  trainingRecords: ReservationTrainingRecord[]
  trainingPlan?: ReservationTrainingPlan | null
  notifications: ReservationMemberNotification[]
}

export interface ReservationCoachWorkbenchData {
  dashboard: CoachDashboardData
  schedule?: CoachScheduleData | null
  pendingAudits: CoachReservationItem[]
  members: CoachMember[]
  records: ReservationTrainingRecord[]
  trainingPlan?: ReservationTrainingPlan | null
  messages: ReservationConversationMessage[]
}

export const reservationPortalApi = {
  getMemberFlow(memberId: number): Promise<ApiResponse<ReservationFlowData>> {
    return request.get('/api/ReservationPortal/member/flow', { params: { memberId } })
  },
  getCommerce(memberId: number): Promise<ApiResponse<ReservationCommerceData>> {
    return request.get('/api/ReservationPortal/member/commerce', { params: { memberId } })
  },
  getMemberCenter(memberId: number): Promise<ApiResponse<ReservationMemberCenterData>> {
    return request.get('/api/ReservationPortal/member/center', { params: { memberId } })
  },
  getCoachWorkbench(coachUserId: number): Promise<ApiResponse<ReservationCoachWorkbenchData>> {
    return request.get('/api/ReservationPortal/coach/workbench', { params: { coachUserId } })
  }
}
