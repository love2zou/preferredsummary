import request from '@/utils/request'

// 定时任务相关的接口类型定义
export interface ScheduledTask {
  id: number
  name: string
  code: string
  cron: string
  handler: string
  crtTime: string
  updTime: string
}

// 定时任务日志
export interface ScheduledTaskLog {
  id: number
  taskId: number
  taskCode: string
  startTime: string
  endTime?: string
  success: boolean
  crtTime: string
}

// 定时任务创建DTO
export interface ScheduledTaskCreateDto {
  name: string
  code: string
  cron: string
  handler: string
  parameters?: string | null
  enabled: boolean
  remark?: string | null
}

// 定时任务更新DTO
export interface ScheduledTaskUpdateDto {
  id: number
  name: string
  code: string
  cron: string
  handler: string
  parameters?: string | null
  enabled: boolean
  remark?: string | null
}

// 定时任务列表DTO
export interface ScheduledTaskListDto {
  id: number
  name: string
  code: string
  cron: string
  handler: string
  parameters?: string
  enabled: boolean
  remark?: string
  crtTime: string
  updTime: string
  // 扩展字段
  nextRunTime?: string
  lastRunTime?: string
  status?: string
  duration?: number
}

// 定时任务搜索参数
export interface ScheduledTaskSearchParams {
  page?: number
  pageSize?: number
  name?: string
  code?: string
  handler?: string
}

// 定时任务日志搜索参数
// 修正日志搜索参数接口
export interface ScheduledTaskLogSearchParams {
  page?: number      // 修改为 page
  size?: number      // 修改为 size
  taskId?: number
  success?: boolean
  startTime?: string
  endTime?: string
}

// 修正日志分页响应接口
export interface ScheduledTaskLogPagedResponse {
  data: ScheduledTaskLog[]  // 修改为 data
  total: number
  page: number             // 修改为 page
  pageSize: number
  totalPages: number
}

// 通用分页响应
export interface PagedResponse<T> {
  data: T[]
  total: number
  page: number
  pageSize: number
  totalPages: number
}

// 通用API响应
export interface ApiResponse<T = any> {
  success: boolean
  message: string
  data?: T
}

// 简化的API响应（不带data）
export interface SimpleApiResponse {
  success: boolean
  message: string
}

// 定时任务分页响应
export interface ScheduledTaskPagedResponse {
  data: ScheduledTaskListDto[]
  total: number
  page: number
  pageSize: number
  totalPages: number
}

// 定时任务日志分页响应
export interface ScheduledTaskLogPagedResponse {
  logs: ScheduledTaskLog[]
  total: number
  pageIndex: number
  pageSize: number
  totalPages: number
}

// 批量删除结果
export interface BatchDeleteTaskResult {
  successCount: number
  failCount: number
  failedReasons: string[]
}

// 任务启用/禁用请求
export interface TaskEnabledRequest {
  enabled: boolean
}

// 定时任务API
export const scheduledTaskApi = {
  // 获取定时任务列表
  getScheduledTaskList(params: ScheduledTaskSearchParams): Promise<ScheduledTaskPagedResponse> {
    return request.get('/api/ScheduledTask', { params })
  },

  // 根据ID获取定时任务
  getScheduledTaskById(id: number): Promise<ScheduledTaskListDto> {
    return request.get(`/api/ScheduledTask/${id}`)
  },

  // 创建定时任务
  createScheduledTask(data: ScheduledTaskCreateDto): Promise<ScheduledTaskListDto> {
    return request.post('/api/ScheduledTask', data)
  },

  // 更新定时任务
  updateScheduledTask(id: number, data: ScheduledTaskUpdateDto): Promise<ScheduledTaskListDto> {
    return request.put(`/api/ScheduledTask/${id}`, data)
  },

  // 删除定时任务
  deleteScheduledTask(id: number): Promise<SimpleApiResponse> {
    return request.delete(`/api/ScheduledTask/${id}`)
  },

  // 批量删除定时任务
  batchDeleteScheduledTasks(ids: number[]): Promise<BatchDeleteTaskResult> {
    return request.delete('/api/ScheduledTask/batch', { data: { ids } })
  },

  // 启用定时任务
  enableScheduledTask(id: number): Promise<SimpleApiResponse> {
    return request.patch(`/api/ScheduledTask/${id}/enabled`, { enabled: true })
  },

  // 禁用定时任务
  disableScheduledTask(id: number): Promise<SimpleApiResponse> {
    return request.patch(`/api/ScheduledTask/${id}/enabled`, { enabled: false })
  },

  // 手动执行定时任务
  executeScheduledTask(id: number): Promise<SimpleApiResponse> {
    return request.post(`/api/ScheduledTask/${id}/execute`)
  },

  // 获取定时任务日志列表
  getScheduledTaskLogList(params: ScheduledTaskLogSearchParams): Promise<ScheduledTaskLogPagedResponse> {
    return request.get('/api/ScheduledTask/logs', { params })
  }
}

export default scheduledTaskApi