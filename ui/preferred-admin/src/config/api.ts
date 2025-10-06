// API配置文件
export const API_CONFIG = {
  // 基础URL
  BASE_URL: import.meta.env.VITE_API_BASE_URL || 'http://159.75.184.108:8080',

  // 超时时间
  TIMEOUT: Number(import.meta.env.VITE_API_TIMEOUT) || 10000,

  // API端点
  ENDPOINTS: {
    // 用户相关
    USER: {
      LIST: '/api/users',
      DETAIL: '/api/users',
      CREATE: '/api/users',
      UPDATE: '/api/users',
      DELETE: '/api/users',
      BATCH_DELETE: '/api/users/batch',
      TOGGLE_STATUS: '/api/users',
      CHANGE_PASSWORD: '/api/users/change-password'
    },

    // 认证相关
    AUTH: {
      LOGIN: '/api/auth/login',
      LOGOUT: '/api/auth/logout',
      REFRESH: '/api/auth/refresh',
      REGISTER: '/api/auth/register'
    },

    // 分类相关
    CATEGORY: {
      LIST: '/api/categories',
      CREATE: '/api/categories',
      UPDATE: '/api/categories',
      DELETE: '/api/categories'
    },

    // 标签相关
    TAG: {
      LIST: '/api/tags',
      CREATE: '/api/tags',
      UPDATE: '/api/tags',
      DELETE: '/api/tags',
      IMPORT: '/api/tags/import'
    },

    // 图片相关
    PICTURE: {
      LIST: '/api/pictures',
      UPLOAD: '/api/pictures/upload',
      DELETE: '/api/pictures'
    },

    // 文件相关
    FILE: {
      UPLOAD: '/api/files/upload',
      LIST: '/api/files',
      DELETE: '/api/files'
    },

    // 网络地址相关
    NETWORK_URL: {
      LIST: '/api/networkurls',
      CREATE: '/api/networkurls',
      UPDATE: '/api/networkurls',
      DELETE: '/api/networkurls'
    }
  }
}

// 环境信息
export const ENV_INFO = {
  isDevelopment: import.meta.env.DEV,
  isProduction: import.meta.env.PROD,
  mode: import.meta.env.MODE
}