// API配置文件
export const API_CONFIG = {
  // 基础URL
  BASE_URL: import.meta.env.VITE_API_BASE_URL || 'http://159.75.184.108:8080',

  // 超时时间
  TIMEOUT: Number(import.meta.env.VITE_API_TIMEOUT) || 300000,

  // API端点
  ENDPOINTS: {
    // 认证相关
    AUTH: {
      LOGIN: '/api/auth/login',
      LOGOUT: '/api/auth/logout',
      REGISTER: '/api/auth/register',
      REFRESH: '/api/auth/refresh'
    },

    // 分类相关
    CATEGORY: {
      LIST: '/api/categories'
    },

    // 标签相关
    TAG: {
      LIST: '/api/tags'
    },

    // 图片相关
    PICTURE: {
      LIST: '/api/pictures',
      DETAIL: '/api/pictures'
    },

    // 网络地址相关
    NETWORK_URL: {
      LIST: '/api/networkurls'
    }
  }
}

// 环境信息
export const ENV_INFO = {
  isDevelopment: import.meta.env.DEV,
  isProduction: import.meta.env.PROD,
  mode: import.meta.env.MODE
}