import request from '@/utils/request'

// 系统资源相关的接口类型定义
export interface SystemResource {
  id: number
  hostName: string
  cpuUsage: number
  memoryUsage: number
  diskName: string
  diskUsage: number
  diskTotal: number
  diskUsed: number
  diskFree: number
  crtTime: string
  updTime: string
}

export interface SystemResourceStats {
  avgCpuUsage: number
  maxCpuUsage: number
  avgMemoryUsage: number
  maxMemoryUsage: number
  avgDiskUsage: number
  maxDiskUsage: number
}

export interface ApiResponse<T> {
  success: boolean
  message: string
  data: T
}

// 系统资源管理API
export const systemResourceApi = {
  // 获取最新的系统资源信息
  getLatestSystemResource(): Promise<ApiResponse<SystemResource>> {
    return request.get('/api/systemresource/latest')
  },

  // 获取一天内的系统资源数据
  getDailySystemResourceData(): Promise<ApiResponse<SystemResource[]>> {
    return request.get('/api/systemresource/daily')
  },

  // 获取系统资源统计信息
  getSystemResourceStats(): Promise<ApiResponse<SystemResourceStats>> {
    return request.get('/api/systemresource/stats')
  }
}

export default systemResourceApi