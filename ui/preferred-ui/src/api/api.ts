import axios, { AxiosResponse } from 'axios';
import { ElMessage } from 'element-plus';
import AutoLoginService from '@/services/autoLoginService';

// 统一的 API 基础配置
const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5000';

const api = axios.create({
  baseURL: API_BASE_URL,
  timeout: 10000,
  headers: {
    'Content-Type': 'application/json',
    // 禁用缓存
    'Cache-Control': 'no-cache, no-store, must-revalidate',
    'Pragma': 'no-cache',
    'Expires': '0'
  }
});

// 请求拦截器
api.interceptors.request.use(
  async (config) => {
    // 优先使用用户token
    let token = localStorage.getItem('token');
    
    // 如果没有用户token，尝试获取访客token
    if (!token) {
      token = await AutoLoginService.autoLogin();
    }
    
    if (token) {
      config.headers.Authorization = `Bearer ${token}`;
    }

    // 为POST和PUT请求添加时间戳和缓存控制
    if (config.method === 'post' || config.method === 'put') {
      config.params = {
        ...config.params,
        _t: Date.now()
      };
      config.headers['Cache-Control'] = 'no-cache';
      config.headers['Pragma'] = 'no-cache';
    }

    return config;
  },
  (error) => {
    return Promise.reject(error);
  }
);

// 响应拦截器
api.interceptors.response.use(
  (response: AxiosResponse) => {
    return response.data;
  },
  async (error) => {
    if (error.response) {
      const { status } = error.response;
      
      switch (status) {
        case 401:
          // 清除所有token
          localStorage.removeItem('token');
          localStorage.removeItem('username');
          AutoLoginService.clearGuestToken();
          
          // 尝试重新自动登录
          const newToken = await AutoLoginService.autoLogin();
          if (newToken) {
            // 重试原请求
            const originalRequest = error.config;
            originalRequest.headers.Authorization = `Bearer ${newToken}`;
            return api.request(originalRequest);
          } else {
            ElMessage.warning('请登录后查看完整内容');
          }
          break;
        case 404:
          ElMessage.error('请求的资源不存在');
          break;
        case 500:
          ElMessage.error('服务器内部错误');
          break;
        default:
          ElMessage.error('请求失败');
      }
    }
    return Promise.reject(error);
  }
);

export default api;
export { API_BASE_URL };