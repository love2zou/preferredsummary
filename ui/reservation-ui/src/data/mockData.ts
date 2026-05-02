export interface TrainerSessionType {
  id: string
  label: string
  description: string
  price: number
}

export interface TrainerDateSlot {
  key: string
  label: string
  subLabel: string
  times: string[]
  moreLabel?: string
}

export interface TrainerReview {
  id: number
  author: string
  rating: number
  tag: string
  content: string
}

export interface Trainer {
  id: number
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
  gender: '男' | '女'
  goals: string[]
  specialties: string[]
  badges: string[]
  certifications: string[]
  introduction: string
  story: string
  nextSlots: string[]
  highlight: string
  heroTone: string
  accentTone: string
  sessionTypes: TrainerSessionType[]
  availableDates: TrainerDateSlot[]
  reviews: TrainerReview[]
}

export interface ReservationRecord {
  id: number
  trainerId: number
  sessionTypeId: string
  sessionLabel: string
  dateLabel: string
  calendarDate: string
  timeRange: string
  club: string
  area: string
  status: 'upcoming' | 'completed' | 'cancelled'
  tag: string
  note: string
}

export interface QuickActionItem {
  key: string
  label: string
  hint: string
  routeName: string
}

export interface ProfileMenuItem {
  key: string
  label: string
  icon: string
  routeName?: string
}

export interface UserProfile {
  name: string
  city: string
  membership: string
  homeClub: string
  remainingSessions: number
  expireAt: string
  avatarUrl: string
}

export const userProfile: UserProfile = {
  name: '张小力',
  city: '上海',
  membership: '金卡会员',
  homeClub: '世纪大道店',
  remainingSessions: 8,
  expireAt: '2025-08-20',
  avatarUrl: 'https://images.unsplash.com/photo-1566753323558-f4e0952af115?auto=format&fit=crop&w=320&q=80'
}

export const quickActions: QuickActionItem[] = [
  { key: 'reserve', label: '预约私教', hint: '立即选教练', routeName: 'trainers' },
  { key: 'courses', label: '购买课程', hint: '查看套餐权益', routeName: 'profile' },
  { key: 'my-courses', label: '我的课程', hint: '已购课明细', routeName: 'reservations' },
  { key: 'records', label: '训练记录', hint: '查看训练轨迹', routeName: 'training' }
]

export const profileMenus: ProfileMenuItem[] = [
  { key: 'reservations', label: '我的预约', icon: 'Calendar', routeName: 'reservations' },
  { key: 'courses', label: '我的课程', icon: 'Notebook' },
  { key: 'orders', label: '我的订单', icon: 'Tickets' },
  { key: 'metrics', label: '身体数据', icon: 'DataAnalysis' },
  { key: 'records', label: '训练记录', icon: 'Histogram', routeName: 'training' },
  { key: 'support', label: '联系客服', icon: 'Phone' },
  { key: 'settings', label: '设置', icon: 'Setting' }
]

export const trainers: Trainer[] = [
  {
    id: 1,
    name: '张扬',
    title: '高级私人教练',
    photoUrl: 'https://images.unsplash.com/photo-1567013127542-490d757e51fc?auto=format&fit=crop&w=640&q=80',
    rating: 4.9,
    reviewCount: 568,
    years: 6,
    servedClients: 1023,
    satisfaction: 98,
    price: 280,
    club: '世纪大道店',
    area: '私教区A',
    gender: '男',
    goals: ['减脂', '增肌'],
    specialties: ['减脂', '增肌', '体态矫正'],
    badges: ['高投入人教练', '认证教练'],
    certifications: ['ACE', 'NASM', 'CPR'],
    introduction: 'ACE 认证教练，6 年一线授课经验，擅长减脂塑形、增肌强化与体态矫正，通过更稳定的训练节奏帮助会员建立长期运动习惯。',
    story: '上课节奏循序渐进，沟通细致，适合希望从基础打稳、慢慢看见明显变化的会员。',
    nextSlots: ['今天 19:00', '明天 10:00', '周四 18:00'],
    highlight: '减脂塑形',
    heroTone: '#dff4e6',
    accentTone: '#0f8a43',
    sessionTypes: [
      { id: 'body-shape', label: '体态课', description: '体态改善与核心稳定', price: 280 },
      { id: 'regular', label: '常规私教课', description: '综合训练计划安排', price: 280 },
      { id: 'assessment', label: '体测评估', description: '训练前数据测评', price: 180 }
    ],
    availableDates: [
      { key: '2026-05-18', label: '今天', subLabel: '05-18', times: ['08:00', '09:00', '10:00', '11:00', '14:00', '15:00', '18:00', '19:00', '20:00', '21:00'] },
      { key: '2026-05-19', label: '明天', subLabel: '05-19', times: ['09:00', '10:00', '13:00', '16:00', '18:00', '20:00'] },
      { key: '2026-05-20', label: '周二', subLabel: '05-20', times: ['10:00', '11:00', '15:00', '16:00', '19:00'] },
      { key: '2026-05-21', label: '周三', subLabel: '05-21', times: ['09:00', '11:00', '17:00', '18:00', '20:00'], moreLabel: '更多' }
    ],
    reviews: [
      { id: 1, author: '林女士', rating: 5, tag: '体态改善明显', content: '两个月腰背不适缓解很多，动作纠正得很细，练完身体轻了不少。' },
      { id: 2, author: '陈先生', rating: 4.8, tag: '减脂节奏舒服', content: '不会一上来就很猛，饮食建议也很实用，训练计划能坚持下去。' }
    ]
  },
  {
    id: 2,
    name: '李想',
    title: '高级私人教练',
    photoUrl: 'https://images.unsplash.com/photo-1548690312-e3b507d8c110?auto=format&fit=crop&w=640&q=80',
    rating: 4.9,
    reviewCount: 432,
    years: 5,
    servedClients: 852,
    satisfaction: 96,
    price: 260,
    club: '世纪大道店',
    area: '私教区B',
    gender: '女',
    goals: ['增肌', '体能提升'],
    specialties: ['增肌', '体能提升'],
    badges: ['高投入人教练', '认证教练'],
    certifications: ['ACE', 'TRX', 'CPR'],
    introduction: '擅长女性力量训练与功能性提升，通过渐进式负荷安排与动作控制，帮助会员建立更强的身体表现与训练自信。',
    story: '课程风格清晰利落，节奏感强，适合希望提升力量和训练效率的人。',
    nextSlots: ['今天 20:00', '明天 11:00', '周四 19:00'],
    highlight: '体能提升',
    heroTone: '#ffe6d8',
    accentTone: '#ff7a21',
    sessionTypes: [
      { id: 'strength', label: '女性力量课', description: '力量提升与稳定控制', price: 260 },
      { id: 'regular', label: '常规私教课', description: '综合训练计划安排', price: 260 },
      { id: 'assessment', label: '功能评估', description: '动作基础评估反馈', price: 180 }
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
    name: '王浩',
    title: '私人教练',
    photoUrl: 'https://images.unsplash.com/photo-1517836357463-d25dfeac3438?auto=format&fit=crop&w=640&q=80',
    rating: 4.8,
    reviewCount: 376,
    years: 7,
    servedClients: 782,
    satisfaction: 95,
    price: 300,
    club: '世纪大道店',
    area: '体测室',
    gender: '男',
    goals: ['减脂', '运动表现提升'],
    specialties: ['减脂', '运动表现提升'],
    badges: ['高投入人教练', '认证教练'],
    certifications: ['NSCA', 'FMS', 'CPR'],
    introduction: '更偏向运动表现与综合体能提升，适合已经有训练基础，希望提升速度、耐力和爆发力的会员。',
    story: '课程强度更有挑战，擅长阶段目标拆解与进度复盘，适合想突破瓶颈的人。',
    nextSlots: ['今天 19:00', '明天 09:00', '周五 17:00'],
    highlight: '运动表现提升',
    heroTone: '#dfe7f7',
    accentTone: '#1459c2',
    sessionTypes: [
      { id: 'performance', label: '运动表现课', description: '速度 爆发 协调提升', price: 300 },
      { id: 'regular', label: '常规私教课', description: '综合训练计划安排', price: 300 },
      { id: 'assessment', label: '体测评估', description: '数据化体能测试', price: 220 }
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

export const initialReservations: ReservationRecord[] = [
  {
    id: 1001,
    trainerId: 1,
    sessionTypeId: 'regular',
    sessionLabel: '常规私教课',
    dateLabel: '今天',
    calendarDate: '2026-05-18',
    timeRange: '19:00 - 20:00',
    club: '世纪大道店',
    area: '私教区A',
    status: 'upcoming',
    tag: '待上课',
    note: ''
  },
  {
    id: 1002,
    trainerId: 2,
    sessionTypeId: 'regular',
    sessionLabel: '常规私教课',
    dateLabel: '明天',
    calendarDate: '2026-05-19',
    timeRange: '10:00 - 11:00',
    club: '世纪大道店',
    area: '私教区A',
    status: 'completed',
    tag: '已确认',
    note: ''
  },
  {
    id: 1003,
    trainerId: 3,
    sessionTypeId: 'assessment',
    sessionLabel: '体测评估',
    dateLabel: '5月20日 周三',
    calendarDate: '2026-05-20',
    timeRange: '16:00 - 17:00',
    club: '世纪大道店',
    area: '体测室',
    status: 'cancelled',
    tag: '已取消',
    note: ''
  }
]
