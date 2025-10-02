import request from '@/utils/request'

// 文件记录接口
export interface FileRecord {
  id: number
  fileName: string
  fileType: string
  filePath: string
  fileSize: string  // 改为字符串类型
  description: string  // 改为必需字段
  uploadUserId: number
  uploadUserName: string
  appType: string
  seqNo: number
  crtTime: string
  updTime: string
}

// 文件搜索参数
export interface FileSearchParams {
  fileName?: string
  fileType?: string
  uploadUser?: string
  appType?: string
  startTime?: string
  endTime?: string
}

// 文件列表参数
export interface FileListParams {
  page: number
  size: number
  fileName?: string
  fileType?: string
  uploadUser?: string
  appType?: string
  startTime?: string
  endTime?: string
}

// 分页响应
export interface PagedResponse<T> {
  items: T[]
  total: number
  page: number
  size: number
}

// 清理结果
export interface CleanResult {
  deletedCount: number
  freedSpace: number
}

// 批量删除结果
export interface BatchDeleteResult {
  successCount: number
  failCount: number
  failedIds: number[]
}

// 文件上传参数
export interface FileUploadParams {
  file: File
  description?: string
  appType?: string
}

// 文件上传响应
export interface FileUploadResponse {
  id: number
  fileName: string
  fileSize: number
  uploadTime: string
  message: string
}

// 文件API
// 文件API
export const fileApi = {
  // 获取文件列表
  getFileList: (params: FileListParams) => {
    return request.get<PagedResponse<FileRecord>>('/api/file', { params })
  },

  // 根据ID获取文件详情
  getFileById: (id: number) => {
    return request.get<FileRecord>(`/api/file/${id}`)
  },

  // 下载文件
  downloadFile: (id: number) => {
    return request.get(`/api/file/${id}/download`, { responseType: 'blob' })
  },

  // 删除文件
  deleteFile: (id: number) => {
    return request.delete(`/api/file/${id}`)
  },

  // 批量删除文件
  batchDeleteFiles: (ids: number[]) => {
    return request.post<BatchDeleteResult>('/api/file/batch-delete', ids)
  },

  // 清理过期文件
  cleanExpiredFiles: (days: number = 30) => {
    return request.post<CleanResult>('/api/file/clean-expired', null, { params: { days } })
  },

  // 保存文件记录
  saveFileRecord: (fileRecord: Partial<FileRecord>) => {
    return request.post<boolean>('/api/file', fileRecord)
  },

  // 上传文件
  uploadFile: (params: FileUploadParams) => {
    const formData = new FormData()
    formData.append('file', params.file)
    if (params.description) {
      formData.append('description', params.description)
    }
    if (params.appType) {
      formData.append('appType', params.appType)
    }
    
    return request.post<FileUploadResponse>('/api/file/upload', formData, {
      headers: {
        'Content-Type': 'multipart/form-data'
      }
    })
  },

  // 批量上传文件
  batchUploadFiles: (files: File[], description?: string, appType?: string) => {
    const formData = new FormData()
    files.forEach((file, index) => {
      formData.append(`files`, file)
    })
    if (description) {
      formData.append('description', description)
    }
    if (appType) {
      formData.append('appType', appType)
    }
    
    return request.post<FileUploadResponse[]>('/api/file/batch-upload', formData, {
      headers: {
        'Content-Type': 'multipart/form-data'
      }
    })
  }
}