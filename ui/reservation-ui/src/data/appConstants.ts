export const quickActions = [
  { key: 'reserve', label: '预约私教', hint: '立即选教练', routeName: 'trainers' },
  { key: 'courses', label: '购买课程', hint: '查看课包权益', routeName: 'commerce-center' },
  { key: 'my-courses', label: '我的课程', hint: '查看已购课包', routeName: 'training' },
  { key: 'records', label: '训练记录', hint: '查看训练轨迹', routeName: 'member-center' }
]

export const profileMenus = [
  { key: 'reservations', label: '我的预约', icon: 'Calendar', routeName: 'reservations' },
  { key: 'courses', label: '我的课程', icon: 'Notebook', routeName: 'training' },
  { key: 'orders', label: '我的订单', icon: 'Tickets', routeName: 'commerce-center' },
  { key: 'metrics', label: '身体数据', icon: 'DataAnalysis', routeName: 'member-center' },
  { key: 'records', label: '训练记录', icon: 'Histogram', routeName: 'member-center' },
  { key: 'support', label: '联系教练', icon: 'Phone', routeName: 'reservation-flow' },
  { key: 'settings', label: '资料设置', icon: 'Setting', routeName: 'member-center' }
]
