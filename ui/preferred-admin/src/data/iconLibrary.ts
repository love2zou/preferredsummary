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
  Mute, VideoPause, Film, Paperclip, Stamp, PieChart,
  Refresh, ZoomIn, ZoomOut, FullScreen, Back,
  ChatLineRound, ChatSquare, Connection, Link, UserFilled,
  Tools, Magnet, Coffee, IceCream, Apple, Orange,
  Cloudy, Drizzling, WindPower, Loading, More, MoreFilled, QuestionFilled, InfoFilled
} from '@element-plus/icons-vue'

export const ICON_LIBRARY: IconItem[] = [
  // 基础界面图标
  { name: 'home', component: 'House', fallbackClass: 'fas fa-home', category: '界面', keywords: ['首页', '主页', '房子'] },
  { name: 'user', component: 'User', fallbackClass: 'fas fa-user', category: '用户', keywords: ['用户', '人员', '个人'] },
  { name: 'users', component: 'UserFilled', fallbackClass: 'fas fa-users', category: '用户', keywords: ['用户组', '团队', '多人'] },
  { name: 'cog', component: 'Setting', fallbackClass: 'fas fa-cog', category: '设置', keywords: ['设置', '配置', '齿轮'] },
  { name: 'search', component: 'Search', fallbackClass: 'fas fa-search', category: '界面', keywords: ['搜索', '查找', '放大镜'] },
  { name: 'filter', component: 'Filter', fallbackClass: 'fas fa-filter', category: '界面', keywords: ['过滤', '筛选', '漏斗'] },
  { name: 'sort', component: 'Sort', fallbackClass: 'fas fa-sort', category: '界面', keywords: ['排序', '分类'] },
  { name: 'list', component: 'List', fallbackClass: 'fas fa-list', category: '界面', keywords: ['列表', '清单'] },
  { name: 'grid', component: 'Grid', fallbackClass: 'fas fa-th', category: '界面', keywords: ['网格', '九宫格'] },
  { name: 'refresh', component: 'Refresh', fallbackClass: 'fas fa-sync', category: '界面', keywords: ['刷新', '重新加载'] },
  { name: 'zoom', component: 'Zoom', fallbackClass: 'fas fa-search-plus', category: '界面', keywords: ['缩放', '放大'] },
  { name: 'zoom-in', component: 'ZoomIn', fallbackClass: 'fas fa-search-plus', category: '界面', keywords: ['放大', '缩放'] },
  { name: 'zoom-out', component: 'ZoomOut', fallbackClass: 'fas fa-search-minus', category: '界面', keywords: ['缩小', '缩放'] },
  { name: 'fullscreen', component: 'FullScreen', fallbackClass: 'fas fa-expand', category: '界面', keywords: ['全屏', '展开'] },
  { name: 'back', component: 'Back', fallbackClass: 'fas fa-arrow-left', category: '界面', keywords: ['返回', '后退'] },
  { name: 'forward', component: 'Forward', fallbackClass: 'fas fa-arrow-right', category: '界面', keywords: ['前进', '向前'] },

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
  { name: 'copy', component: 'DocumentCopy', fallbackClass: 'fas fa-copy', category: '操作', keywords: ['复制'] },
  { name: 'more', component: 'More', fallbackClass: 'fas fa-ellipsis-h', category: '操作', keywords: ['更多', '菜单'] },

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
  { name: 'file-add', component: 'DocumentAdd', fallbackClass: 'fas fa-file-plus', category: '文件', keywords: ['新建文件'] },
  { name: 'file-delete', component: 'DocumentDelete', fallbackClass: 'fas fa-file-minus', category: '文件', keywords: ['删除文件'] },
  { name: 'files', component: 'Files', fallbackClass: 'fas fa-copy', category: '文件', keywords: ['多个文件'] },
  { name: 'notebook', component: 'Notebook', fallbackClass: 'fas fa-book', category: '文件', keywords: ['笔记本', '记事本'] },
  { name: 'memo', component: 'Memo', fallbackClass: 'fas fa-sticky-note', category: '文件', keywords: ['备忘录', '便签'] },

  // 办公用品图标
  { name: 'paperclip', component: 'Paperclip', fallbackClass: 'fas fa-paperclip', category: '办公', keywords: ['回形针', '附件'] },
  { name: 'stamp', component: 'Stamp', fallbackClass: 'fas fa-stamp', category: '办公', keywords: ['印章', '邮票'] },
  { name: 'scissors', component: 'Scissors', fallbackClass: 'fas fa-cut', category: '办公', keywords: ['剪刀', '裁剪'] },
  { name: 'ruler', component: 'Ruler', fallbackClass: 'fas fa-ruler', category: '办公', keywords: ['尺子', '测量'] },
  { name: 'calculator', component: 'Calculator', fallbackClass: 'fas fa-calculator', category: '办公', keywords: ['计算器'] },
  { name: 'pie-chart', component: 'PieChart', fallbackClass: 'fas fa-chart-pie', category: '办公', keywords: ['饼图', '图表'] },
  { name: 'bar-chart', component: 'BarChart', fallbackClass: 'fas fa-chart-bar', category: '办公', keywords: ['柱状图', '图表'] },
  { name: 'tickets', component: 'Tickets', fallbackClass: 'fas fa-ticket-alt', category: '办公', keywords: ['票据', '工单'] },

  // 音乐和音频图标
  { name: 'headphones', component: 'Headphones', fallbackClass: 'fas fa-headphones', category: '音乐', keywords: ['耳机', '音频'] },
  { name: 'microphone', component: 'Microphone', fallbackClass: 'fas fa-microphone', category: '音乐', keywords: ['麦克风', '录音'] },
  { name: 'microphone-one', component: 'MicrophoneOne', fallbackClass: 'fas fa-microphone-alt', category: '音乐', keywords: ['话筒', '录音'] },
  { name: 'sound-wave', component: 'SoundWave', fallbackClass: 'fas fa-volume-up', category: '音乐', keywords: ['音波', '声音'] },
  { name: 'radio', component: 'Radio', fallbackClass: 'fas fa-broadcast-tower', category: '音乐', keywords: ['收音机', '广播'] },
  { name: 'disc', component: 'Disc', fallbackClass: 'fas fa-compact-disc', category: '音乐', keywords: ['光盘', 'CD'] },
  { name: 'music-note', component: 'MusicNote', fallbackClass: 'fas fa-music', category: '音乐', keywords: ['音符', '音乐'] },
  { name: 'mute', component: 'Mute', fallbackClass: 'fas fa-volume-mute', category: '音乐', keywords: ['静音', '禁音'] },

  // 媒体图标
  { name: 'image', component: 'Picture', fallbackClass: 'fas fa-image', category: '媒体', keywords: ['图片', '图像', '照片'] },
  { name: 'video', component: 'VideoCamera', fallbackClass: 'fas fa-video', category: '媒体', keywords: ['视频', '录像'] },
  { name: 'play', component: 'VideoPlay', fallbackClass: 'fas fa-play', category: '媒体', keywords: ['播放'] },
  { name: 'pause', component: 'VideoPause', fallbackClass: 'fas fa-pause', category: '媒体', keywords: ['暂停'] },
  { name: 'film', component: 'Film', fallbackClass: 'fas fa-film', category: '媒体', keywords: ['电影', '胶片'] },
  { name: 'headset', component: 'Headset', fallbackClass: 'fas fa-headset', category: '媒体', keywords: ['耳机', '客服'] },

  // 通信图标
  { name: 'envelope', component: 'Message', fallbackClass: 'fas fa-envelope', category: '通信', keywords: ['邮件', '信封'] },
  { name: 'phone', component: 'Phone', fallbackClass: 'fas fa-phone', category: '通信', keywords: ['电话'] },
  { name: 'comment', component: 'ChatDotRound', fallbackClass: 'fas fa-comment', category: '通信', keywords: ['评论', '消息'] },
  { name: 'chat-line', component: 'ChatLineRound', fallbackClass: 'fas fa-comment-alt', category: '通信', keywords: ['聊天', '对话'] },
  { name: 'chat-square', component: 'ChatSquare', fallbackClass: 'fas fa-comments', category: '通信', keywords: ['聊天框', '对话'] },
  { name: 'paper-plane', component: 'Promotion', fallbackClass: 'fas fa-paper-plane', category: '通信', keywords: ['发送', '纸飞机'] },
  { name: 'connection', component: 'Connection', fallbackClass: 'fas fa-link', category: '通信', keywords: ['连接', '网络'] },
  { name: 'link', component: 'Link', fallbackClass: 'fas fa-external-link-alt', category: '通信', keywords: ['链接', '外链'] },

  // 商务图标
  { name: 'briefcase', component: 'Briefcase', fallbackClass: 'fas fa-briefcase', category: '商务', keywords: ['公文包', '工作'] },
  { name: 'building', component: 'OfficeBuilding', fallbackClass: 'fas fa-building', category: '商务', keywords: ['建筑', '公司'] },
  { name: 'chart-line', component: 'TrendCharts', fallbackClass: 'fas fa-chart-line', category: '商务', keywords: ['折线图', '趋势'] },
  { name: 'trophy', component: 'Trophy', fallbackClass: 'fas fa-trophy', category: '商务', keywords: ['奖杯', '成就'] },
  { name: 'data-board', component: 'DataBoard', fallbackClass: 'fas fa-tachometer-alt', category: '商务', keywords: ['仪表板', '数据'] },
  { name: 'data-analysis', component: 'DataAnalysis', fallbackClass: 'fas fa-chart-area', category: '商务', keywords: ['数据分析'] },
  { name: 'management', component: 'Management', fallbackClass: 'fas fa-tasks', category: '商务', keywords: ['管理', '任务'] },

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
  { name: 'compass', component: 'Compass', fallbackClass: 'fas fa-compass', category: '位置', keywords: ['指南针', '方向'] },
  { name: 'location-arrow', component: 'Position', fallbackClass: 'fas fa-location-arrow', category: '位置', keywords: ['位置箭头'] },

  // 交通工具
  { name: 'bicycle', component: 'Bicycle', fallbackClass: 'fas fa-bicycle', category: '交通', keywords: ['自行车'] },
  { name: 'ship', component: 'Ship', fallbackClass: 'fas fa-ship', category: '交通', keywords: ['轮船'] },

  // 科技图标
  { name: 'desktop', component: 'Monitor', fallbackClass: 'fas fa-desktop', category: '科技', keywords: ['台式电脑', '显示器'] },
  { name: 'microchip', component: 'Cpu', fallbackClass: 'fas fa-microchip', category: '科技', keywords: ['芯片', '处理器'] },

  // 工具图标
  { name: 'tools', component: 'Tools', fallbackClass: 'fas fa-tools', category: '工具', keywords: ['工具', '维修'] },
  { name: 'wrench', component: 'Wrench', fallbackClass: 'fas fa-wrench', category: '工具', keywords: ['扳手', '修理'] },
  { name: 'hammer', component: 'Hammer', fallbackClass: 'fas fa-hammer', category: '工具', keywords: ['锤子', '建设'] },
  { name: 'screwdriver', component: 'Screwdriver', fallbackClass: 'fas fa-screwdriver', category: '工具', keywords: ['螺丝刀'] },
  { name: 'magnet', component: 'Magnet', fallbackClass: 'fas fa-magnet', category: '工具', keywords: ['磁铁', '吸引'] },

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
  { name: 'question-circle', component: 'QuestionFilled', fallbackClass: 'fas fa-question-circle', category: '状态', keywords: ['问题', '帮助'] },
  { name: 'info-circle', component: 'InfoFilled', fallbackClass: 'fas fa-info-circle', category: '状态', keywords: ['信息', '提示'] },
  { name: 'loading', component: 'Loading', fallbackClass: 'fas fa-spinner', category: '状态', keywords: ['加载', '等待'] },
  { name: 'bell', component: 'Bell', fallbackClass: 'fas fa-bell', category: '状态', keywords: ['通知', '提醒'] },
  { name: 'flag', component: 'Flag', fallbackClass: 'fas fa-flag', category: '状态', keywords: ['标记', '旗帜'] },
  { name: 'star', component: 'Star', fallbackClass: 'fas fa-star', category: '状态', keywords: ['星星', '收藏', '评分'] },
  { name: 'bookmark', component: 'Collection', fallbackClass: 'fas fa-bookmark', category: '状态', keywords: ['书签', '收藏'] },
  { name: 'checked', component: 'Checked', fallbackClass: 'fas fa-check-square', category: '状态', keywords: ['已选中', '勾选'] },

  // 天气图标
  { name: 'sun', component: 'Sunny', fallbackClass: 'fas fa-sun', category: '天气', keywords: ['太阳', '晴天'] },
  { name: 'moon', component: 'Moon', fallbackClass: 'fas fa-moon', category: '天气', keywords: ['月亮', '夜晚'] },
  { name: 'bolt', component: 'Lightning', fallbackClass: 'fas fa-bolt', category: '天气', keywords: ['闪电', '雷电'] },
  { name: 'cloud', component: 'Cloudy', fallbackClass: 'fas fa-cloud', category: '天气', keywords: ['云', '多云'] },
  { name: 'drizzle', component: 'Drizzling', fallbackClass: 'fas fa-cloud-drizzle', category: '天气', keywords: ['毛毛雨', '小雨'] },
  { name: 'rain', component: 'Raining', fallbackClass: 'fas fa-cloud-rain', category: '天气', keywords: ['下雨', '雨天'] },
  { name: 'snow', component: 'Snowing', fallbackClass: 'fas fa-snowflake', category: '天气', keywords: ['下雪', '雪花'] },
  { name: 'wind', component: 'WindPower', fallbackClass: 'fas fa-wind', category: '天气', keywords: ['风', '大风'] },

  // 运动和娱乐
  { name: 'football-ball', component: 'Football', fallbackClass: 'fas fa-football-ball', category: '运动', keywords: ['足球'] },
  { name: 'basketball-ball', component: 'Basketball', fallbackClass: 'fas fa-basketball-ball', category: '运动', keywords: ['篮球'] },
  { name: 'magic', component: 'MagicStick', fallbackClass: 'fas fa-magic', category: '娱乐', keywords: ['魔法', '魔术'] },

  // 食物图标
  { name: 'coffee', component: 'Coffee', fallbackClass: 'fas fa-coffee', category: '食物', keywords: ['咖啡', '饮品'] },
  { name: 'ice-cream', component: 'IceCream', fallbackClass: 'fas fa-ice-cream', category: '食物', keywords: ['冰淇淋', '甜品'] },
  { name: 'apple', component: 'Apple', fallbackClass: 'fas fa-apple-alt', category: '食物', keywords: ['苹果', '水果'] },
  { name: 'orange', component: 'Orange', fallbackClass: 'fas fa-lemon', category: '食物', keywords: ['橙子', '水果'] },

  // 保留一些常用的FontAwesome图标作为备选
  { name: 'database', component: '', fallbackClass: 'fas fa-database', category: '科技', keywords: ['数据库'] },
  { name: 'server', component: '', fallbackClass: 'fas fa-server', category: '科技', keywords: ['服务器'] },
  { name: 'code', component: '', fallbackClass: 'fas fa-code', category: '科技', keywords: ['代码', '编程'] },
  { name: 'terminal', component: '', fallbackClass: 'fas fa-terminal', category: '科技', keywords: ['终端', '命令行'] },
  { name: 'bug', component: '', fallbackClass: 'fas fa-bug', category: '科技', keywords: ['错误', '调试'] },
  { name: 'wifi', component: '', fallbackClass: 'fas fa-wifi', category: '科技', keywords: ['无线网络'] },
  { name: 'bluetooth', component: '', fallbackClass: 'fab fa-bluetooth', category: '科技', keywords: ['蓝牙'] },
  { name: 'usb', component: '', fallbackClass: 'fab fa-usb', category: '科技', keywords: ['USB接口'] }
]

// 获取图标组件的辅助函数
export function getIconComponent(iconName: string) {
  const iconItem = ICON_LIBRARY.find(icon => icon.name === iconName)
  if (!iconItem) return null
  
  // 如果有Element Plus组件，返回组件
  if (iconItem.component) {
    const componentMap: Record<string, any> = {
      House, User, UserFilled, Setting, Search, Filter, Sort, List, Grid,
      Plus, Minus, Close, Check, Edit, Delete, Download, Upload, Share, Printer,
      ArrowUp, ArrowDown, ArrowLeft, ArrowRight, CaretTop, CaretBottom, CaretLeft, CaretRight,
      Document, Folder, FolderOpened, Picture, VideoCamera, Microphone,
      Message, Phone, ChatDotRound, ChatLineRound, ChatSquare, Promotion,
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
      Paperclip, Stamp, PieChart, Refresh,ZoomIn, ZoomOut, FullScreen, Back,
      Connection, Link, Tools, Magnet, Coffee, IceCream, Apple, Orange,
      Cloudy, Drizzling, WindPower, Loading, More, MoreFilled, QuestionFilled, InfoFilled
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
  '办公': ICON_LIBRARY.filter(icon => icon.category === '办公'),
  '音乐': ICON_LIBRARY.filter(icon => icon.category === '音乐'),
  '媒体': ICON_LIBRARY.filter(icon => icon.category === '媒体'),
  '通信': ICON_LIBRARY.filter(icon => icon.category === '通信'),
  '商务': ICON_LIBRARY.filter(icon => icon.category === '商务'),
  '购物': ICON_LIBRARY.filter(icon => icon.category === '购物'),
  '时间': ICON_LIBRARY.filter(icon => icon.category === '时间'),
  '位置': ICON_LIBRARY.filter(icon => icon.category === '位置'),
  '交通': ICON_LIBRARY.filter(icon => icon.category === '交通'),
  '科技': ICON_LIBRARY.filter(icon => icon.category === '科技'),
  '工具': ICON_LIBRARY.filter(icon => icon.category === '工具'),
  '安全': ICON_LIBRARY.filter(icon => icon.category === '安全'),
  '状态': ICON_LIBRARY.filter(icon => icon.category === '状态'),
  '天气': ICON_LIBRARY.filter(icon => icon.category === '天气'),
  '运动': ICON_LIBRARY.filter(icon => icon.category === '运动'),
  '娱乐': ICON_LIBRARY.filter(icon => icon.category === '娱乐'),
  '食物': ICON_LIBRARY.filter(icon => icon.category === '食物'),
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