import type {
  ReservationBookingPage,
  ReservationClub,
  ReservationCreateRequest,
  ReservationHomeData,
  ReservationOrderItem,
  ReservationProfileData,
  ReservationTrainerCard,
  ReservationTrainerDetail,
  TrainerQuery
} from '@/api/reservation'

type ReservationStatus = 'upcoming' | 'completed' | 'cancelled'

interface MockReservationState {
  remainingSessions: number
  reservations: ReservationOrderItem[]
}

const MOCK_STATE_KEY = 'reservation-ui-mock-state'

const mockUser = {
  memberId: 9527,
  name: '张小力',
  city: '上海',
  membership: '金卡会员',
  homeClub: '世纪大道店',
  remainingSessions: 8,
  expireAt: '2025-08-20',
  avatarUrl:
    'https://images.unsplash.com/photo-1566753323558-f4e0952af115?auto=format&fit=crop&w=320&q=80'
}

const mockClubs: ReservationClub[] = [
  {
    id: 1,
    clubCode: 'club-sh-sjdd',
    clubName: '世纪大道店',
    city: '上海',
    district: '浦东新区',
    address: '浦东新区世纪大道 1888 号',
    businessHours: '06:30 - 22:30'
  },
  {
    id: 2,
    clubCode: 'club-sh-ljz',
    clubName: '陆家嘴店',
    city: '上海',
    district: '浦东新区',
    address: '浦东新区银城中路 8 号',
    businessHours: '07:00 - 22:00'
  }
]

const mockTrainerDetails: ReservationTrainerDetail[] = [
  {
    id: 1,
    userId: 201,
    name: '张扬',
    title: '高级私人教练',
    photoUrl:
      'https://images.unsplash.com/photo-1567013127542-490d757e51fc?auto=format&fit=crop&w=640&q=80',
    rating: 4.9,
    reviewCount: 568,
    years: 6,
    servedClients: 1023,
    satisfaction: 98,
    price: 280,
    club: '世纪大道店',
    area: '私教区A',
    gender: '男',
    highlight: '减脂塑形',
    heroTone: '#dff4e6',
    accentTone: '#0f8a43',
    goals: ['减脂', '增肌'],
    specialties: ['减脂', '增肌', '体态矫正'],
    badges: ['高投入人教练', '认证教练'],
    nextSlots: ['今天 19:00', '明天 10:00', '周四 18:00'],
    introduction:
      'ACE 认证教练，6 年一线授课经验，擅长减脂塑形、增肌强化与体态矫正，通过稳定的节奏帮助会员建立长期训练习惯。',
    story:
      '课程节奏循序渐进，动作纠正细致，适合希望先打稳基础、逐步看见身体变化的会员。',
    certifications: ['ACE', 'NASM', 'CPR'],
    sessionTypes: [
      { id: 101, code: 'posture', label: '体态课', description: '体态改善与核心稳定', price: 280, durationMinutes: 60 },
      { id: 102, code: 'regular', label: '常规私教课', description: '综合训练计划安排', price: 280, durationMinutes: 60 },
      { id: 103, code: 'assessment', label: '体测评估', description: '训练前数据测评', price: 180, durationMinutes: 60 }
    ],
    availableDates: [
      { key: '2026-05-18', label: '今天', subLabel: '05-18', times: ['08:00', '09:00', '10:00', '11:00', '14:00', '15:00', '18:00', '19:00'] },
      { key: '2026-05-19', label: '明天', subLabel: '05-19', times: ['09:00', '10:00', '13:00', '16:00', '18:00', '20:00'] },
      { key: '2026-05-20', label: '周二', subLabel: '05-20', times: ['10:00', '11:00', '15:00', '16:00', '19:00'] },
      { key: '2026-05-21', label: '周三', subLabel: '05-21', times: ['09:00', '11:00', '17:00', '18:00', '20:00'], moreLabel: '晚间' }
    ],
    reviews: [
      { id: 1, author: '林女士', rating: 5, tag: '体态改善明显', content: '两个月腰背不适缓解很多，动作纠正很细，练完身体轻了不少。' },
      { id: 2, author: '陈先生', rating: 4.8, tag: '减脂节奏舒服', content: '不会一上来就很猛，饮食建议也很实用，训练计划能坚持下去。' }
    ]
  },
  {
    id: 2,
    userId: 202,
    name: '李想',
    title: '高级私人教练',
    photoUrl:
      'https://images.unsplash.com/photo-1548690312-e3b507d8c110?auto=format&fit=crop&w=640&q=80',
    rating: 4.9,
    reviewCount: 432,
    years: 5,
    servedClients: 852,
    satisfaction: 96,
    price: 260,
    club: '世纪大道店',
    area: '私教区B',
    gender: '女',
    highlight: '体能提升',
    heroTone: '#ffe6d8',
    accentTone: '#ff7a21',
    goals: ['增肌', '体能提升'],
    specialties: ['增肌', '体能提升'],
    badges: ['高投入人教练', '认证教练'],
    nextSlots: ['今天 20:00', '明天 11:00', '周四 19:00'],
    introduction:
      '擅长女性力量训练与功能性提升，通过渐进式负荷安排与动作控制，帮助会员建立更强的身体表现与训练自信。',
    story:
      '课程风格清晰利落，节奏感强，适合希望提升力量和训练效率的人。',
    certifications: ['ACE', 'TRX', 'CPR'],
    sessionTypes: [
      { id: 201, code: 'strength', label: '女性力量课', description: '力量提升与稳定控制', price: 260, durationMinutes: 60 },
      { id: 202, code: 'regular', label: '常规私教课', description: '综合训练计划安排', price: 260, durationMinutes: 60 },
      { id: 203, code: 'assessment', label: '功能评估', description: '动作基础评估反馈', price: 180, durationMinutes: 60 }
    ],
    availableDates: [
      { key: '2026-05-18', label: '今天', subLabel: '05-18', times: ['10:00', '11:00', '16:00', '18:00', '20:00'] },
      { key: '2026-05-19', label: '明天', subLabel: '05-19', times: ['08:00', '11:00', '14:00', '17:00', '19:00'] },
      { key: '2026-05-20', label: '周二', subLabel: '05-20', times: ['09:00', '12:00', '15:00', '18:00'] },
      { key: '2026-05-21', label: '周三', subLabel: '05-21', times: ['10:00', '13:00', '17:00', '20:00'], moreLabel: '更多' }
    ],
    reviews: [
      { id: 3, author: '周小姐', rating: 5, tag: '动作反馈专业', content: '每次都会根据当天状态调整强度，练完很扎实但不会过度疲劳。' },
      { id: 4, author: '韩女士', rating: 4.9, tag: '上课氛围轻松', content: '沟通很舒服，动作逻辑讲得很清楚，力量课也没有想象中可怕。' }
    ]
  },
  {
    id: 3,
    userId: 203,
    name: '王浩',
    title: '私人教练',
    photoUrl:
      'https://images.unsplash.com/photo-1517836357463-d25dfeac3438?auto=format&fit=crop&w=640&q=80',
    rating: 4.8,
    reviewCount: 376,
    years: 7,
    servedClients: 782,
    satisfaction: 95,
    price: 300,
    club: '世纪大道店',
    area: '体测室',
    gender: '男',
    highlight: '运动表现提升',
    heroTone: '#dfe7f7',
    accentTone: '#1459c2',
    goals: ['减脂', '运动表现提升'],
    specialties: ['减脂', '运动表现提升'],
    badges: ['高投入人教练', '认证教练'],
    nextSlots: ['今天 19:00', '明天 09:00', '周五 17:00'],
    introduction:
      '更偏向运动表现与综合体能提升，适合已经有训练基础，希望提升速度、耐力和爆发力的会员。',
    story:
      '课程强度更有挑战，擅长阶段目标拆解与进度复盘，适合想突破瓶颈的人。',
    certifications: ['NSCA', 'FMS', 'CPR'],
    sessionTypes: [
      { id: 301, code: 'performance', label: '运动表现课', description: '速度、爆发、协调提升', price: 300, durationMinutes: 60 },
      { id: 302, code: 'regular', label: '常规私教课', description: '综合训练计划安排', price: 300, durationMinutes: 60 },
      { id: 303, code: 'assessment', label: '体测评估', description: '数据化体能测试', price: 220, durationMinutes: 60 }
    ],
    availableDates: [
      { key: '2026-05-18', label: '今天', subLabel: '05-18', times: ['08:00', '10:00', '13:00', '16:00', '19:00'] },
      { key: '2026-05-19', label: '明天', subLabel: '05-19', times: ['09:00', '11:00', '15:00', '18:00'] },
      { key: '2026-05-20', label: '周二', subLabel: '05-20', times: ['10:00', '14:00', '16:00', '20:00'] },
      { key: '2026-05-21', label: '周三', subLabel: '05-21', times: ['09:00', '12:00', '17:00', '21:00'], moreLabel: '更多' }
    ],
    reviews: [
      { id: 5, author: '杜先生', rating: 4.9, tag: '提升很明显', content: '跑步和深蹲动作都有明显进步，训练计划安排得很系统。' },
      { id: 6, author: '宋同学', rating: 4.8, tag: '目标拆解清楚', content: '每周都有清晰目标，知道自己为什么练，也知道进度到了哪一步。' }
    ]
  }
]

const initialReservations: ReservationOrderItem[] = [
  {
    id: 1001,
    reservationNo: 'RS20260518001',
    trainerId: 1,
    trainerName: '张扬',
    trainerPhotoUrl: mockTrainerDetails[0].photoUrl,
    sessionTypeId: 102,
    sessionLabel: '常规私教课',
    dateLabel: '今天',
    calendarDate: '2026-05-18',
    timeRange: '19:00 - 20:00',
    club: '世纪大道店',
    area: '私教区A',
    status: 'upcoming',
    tag: '待上课',
    note: '',
    price: 280
  },
  {
    id: 1002,
    reservationNo: 'RS20260519002',
    trainerId: 2,
    trainerName: '李想',
    trainerPhotoUrl: mockTrainerDetails[1].photoUrl,
    sessionTypeId: 202,
    sessionLabel: '常规私教课',
    dateLabel: '明天',
    calendarDate: '2026-05-19',
    timeRange: '10:00 - 11:00',
    club: '世纪大道店',
    area: '私教区B',
    status: 'completed',
    tag: '已确认',
    note: '',
    price: 260
  },
  {
    id: 1003,
    reservationNo: 'RS20260520003',
    trainerId: 3,
    trainerName: '王浩',
    trainerPhotoUrl: mockTrainerDetails[2].photoUrl,
    sessionTypeId: 303,
    sessionLabel: '体测评估',
    dateLabel: '5月20日 周二',
    calendarDate: '2026-05-20',
    timeRange: '16:00 - 17:00',
    club: '世纪大道店',
    area: '体测室',
    status: 'cancelled',
    tag: '已取消',
    note: '',
    price: 220
  }
]

const clone = <T>(value: T): T => JSON.parse(JSON.stringify(value)) as T

const toTrainerCard = (trainer: ReservationTrainerDetail): ReservationTrainerCard => ({
  id: trainer.id,
  userId: trainer.userId,
  name: trainer.name,
  title: trainer.title,
  photoUrl: trainer.photoUrl,
  rating: trainer.rating,
  reviewCount: trainer.reviewCount,
  years: trainer.years,
  servedClients: trainer.servedClients,
  satisfaction: trainer.satisfaction,
  price: trainer.price,
  club: trainer.club,
  area: trainer.area,
  gender: trainer.gender,
  highlight: trainer.highlight,
  heroTone: trainer.heroTone,
  accentTone: trainer.accentTone,
  goals: trainer.goals,
  specialties: trainer.specialties,
  badges: trainer.badges,
  nextSlots: trainer.nextSlots
})

const readState = (): MockReservationState => {
  const raw = localStorage.getItem(MOCK_STATE_KEY)
  if (!raw) {
    return {
      remainingSessions: mockUser.remainingSessions,
      reservations: clone(initialReservations)
    }
  }

  try {
    return JSON.parse(raw) as MockReservationState
  } catch {
    return {
      remainingSessions: mockUser.remainingSessions,
      reservations: clone(initialReservations)
    }
  }
}

const writeState = (state: MockReservationState): void => {
  localStorage.setItem(MOCK_STATE_KEY, JSON.stringify(state))
}

const formatDateLabel = (dateKey: string, fallbackLabel?: string): string => {
  if (fallbackLabel) return fallbackLabel
  const date = new Date(dateKey)
  if (Number.isNaN(date.getTime())) return dateKey
  const month = date.getMonth() + 1
  const day = date.getDate()
  const week = ['日', '一', '二', '三', '四', '五', '六'][date.getDay()]
  return `${month}月${day}日 周${week}`
}

const getTrainerDetailInternal = (trainerId: number): ReservationTrainerDetail => {
  const trainer = mockTrainerDetails.find((item) => item.id === trainerId)
  if (!trainer) {
    throw new Error('教练不存在')
  }
  return clone(trainer)
}

export const reservationMockService = {
  getClubs(): ReservationClub[] {
    return clone(mockClubs)
  },

  getHome(): ReservationHomeData {
    const state = readState()
    const nextReservation = state.reservations.find((item) => item.status === 'upcoming')
    return {
      user: {
        ...mockUser,
        remainingSessions: state.remainingSessions
      },
      nextReservation: nextReservation ? clone(nextReservation) : undefined,
      recommendedTrainers: mockTrainerDetails.map(toTrainerCard).slice(0, 3)
    }
  },

  getTrainers(query: TrainerQuery = {}): ReservationTrainerCard[] {
    let list = mockTrainerDetails.map(toTrainerCard)

    if (query.keyword) {
      const keyword = query.keyword.trim().toLowerCase()
      list = list.filter((item) =>
        [item.name, item.title, item.club, item.area, ...item.goals, ...item.specialties]
          .join(' ')
          .toLowerCase()
          .includes(keyword)
      )
    }

    if (query.clubId) {
      const clubName = mockClubs.find((club) => club.id === query.clubId)?.clubName
      list = list.filter((item) => item.club === clubName)
    }

    if (query.goal) {
      list = list.filter((item) => item.goals.includes(query.goal as string))
    }

    if (query.gender) {
      list = list.filter((item) => item.gender === query.gender)
    }

    return clone(list)
  },

  getTrainerDetail(trainerId: number): ReservationTrainerDetail {
    return getTrainerDetailInternal(trainerId)
  },

  getBookingPage(trainerId: number): ReservationBookingPage {
    const state = readState()
    return {
      trainer: getTrainerDetailInternal(trainerId),
      remainingSessions: state.remainingSessions
    }
  },

  getReservations(status?: ReservationStatus): ReservationOrderItem[] {
    const state = readState()
    const reservations = status
      ? state.reservations.filter((item) => item.status === status)
      : state.reservations
    return clone(reservations)
  },

  getProfile(): ReservationProfileData {
    const state = readState()
    return {
      user: {
        ...mockUser,
        remainingSessions: state.remainingSessions
      },
      upcomingCount: state.reservations.filter((item) => item.status === 'upcoming').length,
      completedCount: state.reservations.filter((item) => item.status === 'completed').length,
      cancelledCount: state.reservations.filter((item) => item.status === 'cancelled').length
    }
  },

  createReservation(data: ReservationCreateRequest): { reservationId: number; reservationNo: string; remainingSessions: number } {
    const state = readState()
    const trainer = getTrainerDetailInternal(data.trainerId)
    const session = trainer.sessionTypes.find((item) => item.id === data.sessionTypeId)
    const date = trainer.availableDates.find((item) => item.key === data.reservationDate)

    if (!session || !date) {
      throw new Error('预约信息不完整')
    }

    const reservationId = Date.now()
    const startTime = data.startTime
    const [hour, minute] = startTime.split(':').map(Number)
    const endHour = String((hour + 1) % 24).padStart(2, '0')
    const endMinute = String(minute).padStart(2, '0')

    state.reservations.unshift({
      id: reservationId,
      reservationNo: `RS${reservationId}`,
      trainerId: trainer.id,
      trainerName: trainer.name,
      trainerPhotoUrl: trainer.photoUrl,
      sessionTypeId: session.id,
      sessionLabel: session.label,
      dateLabel: formatDateLabel(data.reservationDate, date.label),
      calendarDate: data.reservationDate,
      timeRange: `${startTime} - ${endHour}:${endMinute}`,
      club: trainer.club,
      area: trainer.area,
      status: 'upcoming',
      tag: '待上课',
      note: data.remark || '',
      price: session.price
    })

    state.remainingSessions = Math.max(0, state.remainingSessions - 1)
    writeState(state)

    return {
      reservationId,
      reservationNo: `RS${reservationId}`,
      remainingSessions: state.remainingSessions
    }
  },

  cancelReservation(reservationId: number): void {
    const state = readState()
    const target = state.reservations.find((item) => item.id === reservationId)
    if (!target || target.status !== 'upcoming') {
      return
    }

    target.status = 'cancelled'
    target.tag = '已取消'
    state.remainingSessions += 1
    writeState(state)
  }
}
