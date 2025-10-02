export interface IconItem {
  name: string
  component: string  // 改为Element Plus组件名
  fallbackClass?: string  // 保留FontAwesome作为备选
  category: string
  keywords?: string[]
}

// 导入Element Plus图标组件
import {
  House, User, Setting, Search, Filter, Sort, List, Grid,
  Plus, Minus, Close, Check, Edit, Delete, Download, Upload, Share, Printer,
  ArrowUp, ArrowDown, ArrowLeft, ArrowRight, CaretTop, CaretBottom, CaretLeft, CaretRight,
  Document, Folder, FolderOpened, Picture, VideoCamera, Microphone,
  Message, Phone, ChatDotRound, Promotion,
  Briefcase, OfficeBuilding, TrendCharts, Trophy,
  ShoppingCart, CreditCard, Coin, Wallet, PriceTag, Present,
  Clock, Calendar, Timer, Location, Compass, Position,
  Bicycle, Ship, Monitor, Cpu, Lock, Unlock, Key, View, Hide, Warning, SuccessFilled, CircleClose,
  Bell, Flag, Star, Collection, Sunny, Moon, Lightning,
  Football, Basketball, MagicStick, VideoPlay, Headset,
  DocumentAdd, DocumentCopy, DocumentDelete, DocumentChecked, DocumentRemove,
  Notebook, Tickets, Memo, Files, FolderAdd, FolderDelete, FolderRemove,
  DataBoard, DataAnalysis, DataLine, Management, Reading, Checked,
  ScaleToOriginal, SetUp, FirstAidKit, Platform,
  Mute,VideoPause, Film,
} from '@element-plus/icons-vue'

export const ICON_LIBRARY: IconItem[] = [
  // 基础界面图标
  { name: 'home', component: 'House', fallbackClass: 'fas fa-home', category: '界面', keywords: ['首页', '主页', '房子'] },
  { name: 'user', component: 'User', fallbackClass: 'fas fa-user', category: '用户', keywords: ['用户', '人员', '个人'] },
  { name: 'users', component: 'Users', fallbackClass: 'fas fa-users', category: '用户', keywords: ['用户组', '团队', '多人'] },
  { name: 'cog', component: 'Setting', fallbackClass: 'fas fa-cog', category: '设置', keywords: ['设置', '配置', '齿轮'] },
  { name: 'search', component: 'Search', fallbackClass: 'fas fa-search', category: '界面', keywords: ['搜索', '查找', '放大镜'] },
  { name: 'filter', component: 'Filter', fallbackClass: 'fas fa-filter', category: '界面', keywords: ['过滤', '筛选', '漏斗'] },
  { name: 'sort', component: 'Sort', fallbackClass: 'fas fa-sort', category: '界面', keywords: ['排序', '分类'] },
  { name: 'list', component: 'List', fallbackClass: 'fas fa-list', category: '界面', keywords: ['列表', '清单'] },
  { name: 'grid', component: 'Grid', fallbackClass: 'fas fa-th', category: '界面', keywords: ['网格', '九宫格'] },

  // 操作图标
  { name: 'plus', component: 'Plus', fallbackClass: 'fas fa-plus', category: '操作', keywords: ['添加', '新增', '加号'] },
  { name: 'minus', component: 'Minus', fallbackClass: 'fas fa-minus', category: '操作', keywords: ['删除', '减少', '减号'] },
  { name: 'times', component: 'Close', fallbackClass: 'fas fa-times', category: '操作', keywords: ['关闭', '删除', '叉号'] },
  { name: 'check', component: 'Check', fallbackClass: 'fas fa-check', category: '操作', keywords: ['确认', '勾选', '对号'] },
  { name: 'edit', component: 'Edit', fallbackClass: 'fas fa-edit', category: '操作', keywords: ['编辑', '修改', '笔'] },
  { name: 'trash', component: 'Delete', fallbackClass: 'fas fa-trash', category: '操作', keywords: ['删除', '垃圾桶'] },
  { name: 'download', component: 'Download', fallbackClass: 'fas fa-download', category: '操作', keywords: ['下载'] },
  { name: 'upload', component: 'Upload', fallbackClass: 'fas fa-upload', category: '操作', keywords: ['上传'] },
  { name: 'share', component: 'Share', fallbackClass: 'fas fa-share', category: '操作', keywords: ['分享', '共享'] },
  { name: 'print', component: 'Printer', fallbackClass: 'fas fa-print', category: '操作', keywords: ['打印'] },

  // 导航图标
  { name: 'arrow-up', component: 'ArrowUp', fallbackClass: 'fas fa-arrow-up', category: '导航', keywords: ['向上', '箭头'] },
  { name: 'arrow-down', component: 'ArrowDown', fallbackClass: 'fas fa-arrow-down', category: '导航', keywords: ['向下', '箭头'] },
  { name: 'arrow-left', component: 'ArrowLeft', fallbackClass: 'fas fa-arrow-left', category: '导航', keywords: ['向左', '箭头'] },
  { name: 'arrow-right', component: 'ArrowRight', fallbackClass: 'fas fa-arrow-right', category: '导航', keywords: ['向右', '箭头'] },
  { name: 'chevron-up', component: 'CaretTop', fallbackClass: 'fas fa-chevron-up', category: '导航', keywords: ['向上', '折叠'] },
  { name: 'chevron-down', component: 'CaretBottom', fallbackClass: 'fas fa-chevron-down', category: '导航', keywords: ['向下', '展开'] },
  { name: 'chevron-left', component: 'CaretLeft', fallbackClass: 'fas fa-chevron-left', category: '导航', keywords: ['向左', '返回'] },
  { name: 'chevron-right', component: 'CaretRight', fallbackClass: 'fas fa-chevron-right', category: '导航', keywords: ['向右', '前进'] },

  // 文件和文档
  { name: 'file', component: 'Document', fallbackClass: 'fas fa-file', category: '文件', keywords: ['文件', '文档'] },
  { name: 'folder', component: 'Folder', fallbackClass: 'fas fa-folder', category: '文件', keywords: ['文件夹', '目录'] },
  { name: 'folder-open', component: 'FolderOpened', fallbackClass: 'fas fa-folder-open', category: '文件', keywords: ['打开文件夹'] },

  // 媒体图标
  { name: 'image', component: 'Picture', fallbackClass: 'fas fa-image', category: '媒体', keywords: ['图片', '图像', '照片'] },
  { name: 'video', component: 'VideoCamera', fallbackClass: 'fas fa-video', category: '媒体', keywords: ['视频', '录像'] },
  { name: 'microphone', component: 'Microphone', fallbackClass: 'fas fa-microphone', category: '媒体', keywords: ['麦克风', '录音'] },
  { name: 'play', component: 'VideoPlay', fallbackClass: 'fas fa-play', category: '媒体', keywords: ['播放'] },
  { name: 'headphones', component: 'Headset', fallbackClass: 'fas fa-headphones', category: '媒体', keywords: ['耳机', '音频'] },

  // 通信图标
  { name: 'envelope', component: 'Message', fallbackClass: 'fas fa-envelope', category: '通信', keywords: ['邮件', '信封'] },
  { name: 'phone', component: 'Phone', fallbackClass: 'fas fa-phone', category: '通信', keywords: ['电话'] },
  { name: 'comment', component: 'ChatDotRound', fallbackClass: 'fas fa-comment', category: '通信', keywords: ['评论', '消息'] },
  { name: 'paper-plane', component: 'Promotion', fallbackClass: 'fas fa-paper-plane', category: '通信', keywords: ['发送', '纸飞机'] },

  // 商务图标
  { name: 'briefcase', component: 'Briefcase', fallbackClass: 'fas fa-briefcase', category: '商务', keywords: ['公文包', '工作'] },
  { name: 'building', component: 'OfficeBuilding', fallbackClass: 'fas fa-building', category: '商务', keywords: ['建筑', '公司'] },
  { name: 'chart-line', component: 'TrendCharts', fallbackClass: 'fas fa-chart-line', category: '商务', keywords: ['折线图', '趋势'] },
  { name: 'calculator', component: 'Calculator', fallbackClass: 'fas fa-calculator', category: '商务', keywords: ['计算器'] },
  { name: 'trophy', component: 'Trophy', fallbackClass: 'fas fa-trophy', category: '商务', keywords: ['奖杯', '成就'] },

  // 购物和电商
  { name: 'shopping-cart', component: 'ShoppingCart', fallbackClass: 'fas fa-shopping-cart', category: '购物', keywords: ['购物车', '购买'] },
  { name: 'credit-card', component: 'CreditCard', fallbackClass: 'fas fa-credit-card', category: '购物', keywords: ['信用卡', '支付'] },
  { name: 'coins', component: 'Coin', fallbackClass: 'fas fa-coins', category: '购物', keywords: ['硬币', '金币'] },
  { name: 'wallet', component: 'Wallet', fallbackClass: 'fas fa-wallet', category: '购物', keywords: ['钱包'] },
  { name: 'tag', component: 'PriceTag', fallbackClass: 'fas fa-tag', category: '购物', keywords: ['标签', '价格'] },
  { name: 'gift', component: 'Present', fallbackClass: 'fas fa-gift', category: '购物', keywords: ['礼物', '礼品'] },

  // 时间和日期
  { name: 'clock', component: 'Clock', fallbackClass: 'fas fa-clock', category: '时间', keywords: ['时钟', '时间'] },
  { name: 'calendar', component: 'Calendar', fallbackClass: 'fas fa-calendar', category: '时间', keywords: ['日历', '日期'] },
  { name: 'stopwatch', component: 'Timer', fallbackClass: 'fas fa-stopwatch', category: '时间', keywords: ['秒表', '计时'] },

  // 位置和地图
  { name: 'map-marker-alt', component: 'Location', fallbackClass: 'fas fa-map-marker-alt', category: '位置', keywords: ['位置标记', '定位'] },
  { name: 'map', component: 'Map', fallbackClass: 'fas fa-map', category: '位置', keywords: ['地图'] },
  { name: 'compass', component: 'Compass', fallbackClass: 'fas fa-compass', category: '位置', keywords: ['指南针', '方向'] },
  { name: 'location-arrow', component: 'Position', fallbackClass: 'fas fa-location-arrow', category: '位置', keywords: ['位置箭头'] },

  // 交通工具
  { name: 'car', component: 'Car', fallbackClass: 'fas fa-car', category: '交通', keywords: ['汽车', '轿车'] },
  { name: 'bicycle', component: 'Bicycle', fallbackClass: 'fas fa-bicycle', category: '交通', keywords: ['自行车'] },
  { name: 'ship', component: 'Ship', fallbackClass: 'fas fa-ship', category: '交通', keywords: ['轮船'] },

  // 科技图标
  { name: 'desktop', component: 'Monitor', fallbackClass: 'fas fa-desktop', category: '科技', keywords: ['台式电脑', '显示器'] },
  { name: 'microchip', component: 'Cpu', fallbackClass: 'fas fa-microchip', category: '科技', keywords: ['芯片', '处理器'] },
  { name: 'hdd', component: 'HardDisk', fallbackClass: 'fas fa-hdd', category: '科技', keywords: ['硬盘'] },
  { name: 'wifi', component: 'Wifi', fallbackClass: 'fas fa-wifi', category: '科技', keywords: ['无线网络'] },
  { name: 'cloud', component: 'CloudStorage', fallbackClass: 'fas fa-cloud', category: '科技', keywords: ['云计算', '云存储'] },

  // 安全图标
  { name: 'lock', component: 'Lock', fallbackClass: 'fas fa-lock', category: '安全', keywords: ['锁定', '安全'] },
  { name: 'unlock', component: 'Unlock', fallbackClass: 'fas fa-unlock', category: '安全', keywords: ['解锁'] },
  { name: 'key', component: 'Key', fallbackClass: 'fas fa-key', category: '安全', keywords: ['钥匙', '密钥'] },
  { name: 'eye', component: 'View', fallbackClass: 'fas fa-eye', category: '安全', keywords: ['查看', '可见'] },
  { name: 'eye-slash', component: 'Hide', fallbackClass: 'fas fa-eye-slash', category: '安全', keywords: ['隐藏', '不可见'] },

  // 状态图标
  { name: 'check-circle', component: 'SuccessFilled', fallbackClass: 'fas fa-check-circle', category: '状态', keywords: ['成功', '完成', '正确'] },
  { name: 'times-circle', component: 'CircleClose', fallbackClass: 'fas fa-times-circle', category: '状态', keywords: ['错误', '失败', '取消'] },
  { name: 'exclamation-circle', component: 'Warning', fallbackClass: 'fas fa-exclamation-circle', category: '状态', keywords: ['警告', '注意'] },
  { name: 'bell', component: 'Bell', fallbackClass: 'fas fa-bell', category: '状态', keywords: ['通知', '提醒'] },
  { name: 'flag', component: 'Flag', fallbackClass: 'fas fa-flag', category: '状态', keywords: ['标记', '旗帜'] },
  { name: 'star', component: 'Star', fallbackClass: 'fas fa-star', category: '状态', keywords: ['星星', '收藏', '评分'] },
  { name: 'heart', component: 'Like', fallbackClass: 'fas fa-heart', category: '状态', keywords: ['喜欢', '爱心'] },
  { name: 'bookmark', component: 'Collection', fallbackClass: 'fas fa-bookmark', category: '状态', keywords: ['书签', '收藏'] },

  // 天气图标
  { name: 'sun', component: 'Sunny', fallbackClass: 'fas fa-sun', category: '天气', keywords: ['太阳', '晴天'] },
  { name: 'moon', component: 'Moon', fallbackClass: 'fas fa-moon', category: '天气', keywords: ['月亮', '夜晚'] },
  { name: 'bolt', component: 'Lightning', fallbackClass: 'fas fa-bolt', category: '天气', keywords: ['闪电', '雷电'] },

  // 运动和娱乐
  { name: 'football-ball', component: 'Football', fallbackClass: 'fas fa-football-ball', category: '运动', keywords: ['足球'] },
  { name: 'basketball-ball', component: 'Basketball', fallbackClass: 'fas fa-basketball-ball', category: '运动', keywords: ['篮球'] },
  { name: 'magic', component: 'MagicStick', fallbackClass: 'fas fa-magic', category: '娱乐', keywords: ['魔法', '魔术'] },

  // 保留一些常用的FontAwesome图标作为备选
  { name: 'database', component: '', fallbackClass: 'fas fa-database', category: '科技', keywords: ['数据库'] },
  { name: 'server', component: '', fallbackClass: 'fas fa-server', category: '科技', keywords: ['服务器'] },
  { name: 'code', component: '', fallbackClass: 'fas fa-code', category: '科技', keywords: ['代码', '编程'] },
  { name: 'terminal', component: '', fallbackClass: 'fas fa-terminal', category: '科技', keywords: ['终端', '命令行'] },
  { name: 'bug', component: '', fallbackClass: 'fas fa-bug', category: '科技', keywords: ['错误', '调试'] }
]

// 获取图标组件的辅助函数
export function getIconComponent(iconName: string) {
  const iconItem = ICON_LIBRARY.find(icon => icon.name === iconName)
  if (!iconItem) return null
  
  // 如果有Element Plus组件，返回组件
  if (iconItem.component) {
    const componentMap: Record<string, any> = {
      House, User, Setting, Search, Filter, Sort, List, Grid,
      Plus, Minus, Close, Check, Edit, Delete, Download, Upload, Share, Printer,
      ArrowUp, ArrowDown, ArrowLeft, ArrowRight, CaretTop, CaretBottom, CaretLeft, CaretRight,
      Document, Folder, FolderOpened, Picture, VideoCamera, Microphone,
      Message, Phone, ChatDotRound, Promotion,
      Briefcase, OfficeBuilding, TrendCharts, Trophy,
      ShoppingCart, CreditCard, Coin, Wallet, PriceTag, Present,
      Clock, Calendar, Timer, Location, Compass, Position,
      Bicycle, Ship, Monitor, Cpu, Lock, Unlock, Key, View, Hide, Warning, SuccessFilled, CircleClose,
      Bell, Flag, Star, Collection, Sunny, Moon, Lightning,
      Football, Basketball, MagicStick, VideoPlay, Headset,
      DocumentAdd, DocumentCopy, DocumentDelete, DocumentChecked, DocumentRemove,
      Notebook, Tickets, Memo, Files, FolderAdd, FolderDelete, FolderRemove,
      DataBoard, DataAnalysis, DataLine, Management, Reading, Checked,
      ScaleToOriginal, SetUp, FirstAidKit, Platform,
      Mute, VideoPause, Film
    }
    return componentMap[iconItem.component]
  }
  
  return null
}

// 获取图标CSS类名（备选方案）
export function getIconClass(iconName: string): string {
  const iconItem = ICON_LIBRARY.find(icon => icon.name === iconName)
  return iconItem?.fallbackClass || 'fas fa-question'
}

// 判断是否为Element Plus图标
export function isElementIcon(iconName: string): boolean {
  const iconItem = ICON_LIBRARY.find(icon => icon.name === iconName)
  return !!(iconItem?.component)
}

// 按分类分组的图标
export const ICON_CATEGORIES: Record<string, IconItem[]> = {
  '界面': ICON_LIBRARY.filter(icon => icon.category === '界面'),
  '操作': ICON_LIBRARY.filter(icon => icon.category === '操作'),
  '导航': ICON_LIBRARY.filter(icon => icon.category === '导航'),
  '文件': ICON_LIBRARY.filter(icon => icon.category === '文件'),
  '媒体': ICON_LIBRARY.filter(icon => icon.category === '媒体'),
  '通信': ICON_LIBRARY.filter(icon => icon.category === '通信'),
  '商务': ICON_LIBRARY.filter(icon => icon.category === '商务'),
  '购物': ICON_LIBRARY.filter(icon => icon.category === '购物'),
  '时间': ICON_LIBRARY.filter(icon => icon.category === '时间'),
  '位置': ICON_LIBRARY.filter(icon => icon.category === '位置'),
  '交通': ICON_LIBRARY.filter(icon => icon.category === '交通'),
  '科技': ICON_LIBRARY.filter(icon => icon.category === '科技'),
  '安全': ICON_LIBRARY.filter(icon => icon.category === '安全'),
  '状态': ICON_LIBRARY.filter(icon => icon.category === '状态'),
  '天气': ICON_LIBRARY.filter(icon => icon.category === '天气'),
  '运动': ICON_LIBRARY.filter(icon => icon.category === '运动'),
  '娱乐': ICON_LIBRARY.filter(icon => icon.category === '娱乐'),
  '用户': ICON_LIBRARY.filter(icon => icon.category === '用户'),
  '设置': ICON_LIBRARY.filter(icon => icon.category === '设置')
}

// 搜索图标函数
export function searchIcons(query: string, category?: string): IconItem[] {
  if (!query.trim()) {
    return category ? ICON_CATEGORIES[category] || [] : ICON_LIBRARY
  }

  const searchTerm = query.toLowerCase()
  let iconsToSearch = category ? ICON_CATEGORIES[category] || [] : ICON_LIBRARY

  return iconsToSearch.filter(icon => {
    // 搜索名称
    if (icon.name.toLowerCase().includes(searchTerm)) return true
    
    // 搜索组件名
    if (icon.component && icon.component.toLowerCase().includes(searchTerm)) return true
    
    // 搜索CSS类名
    if (icon.fallbackClass && icon.fallbackClass.toLowerCase().includes(searchTerm)) return true
    
    // 搜索关键词
    if (icon.keywords && icon.keywords.some(keyword => 
      keyword.toLowerCase().includes(searchTerm)
    )) return true
    
    return false
  })
}

// 获取所有分类
export function getCategories(): string[] {
  return Object.keys(ICON_CATEGORIES)
}

// 根据分类获取图标
export function getIconsByCategory(category: string): IconItem[] {
  return ICON_CATEGORIES[category] || []
}