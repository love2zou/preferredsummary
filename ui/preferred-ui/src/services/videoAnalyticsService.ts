import api from './api'

export interface ApiResponse<T> {
  success: boolean
  message: string
  data: T
}

export interface CreateVideoJobRequest {
  algoParamsJson?: string
}

export interface CreateJobResultDto {
  jobId: number
  jobNo: string
  status: number
  totalVideoCount: number
}

export interface UploadVideoResultDto {
  jobNo: string
  fileId: number
  fileName: string
  status: number
}

export interface JobVideoDto {
  id: number
  fileName: string
  status: number
  errorMessage: string | null

  analyzeSec?: number | null

  // 注意：你当前字段更像“视频时长”，不是“分析耗时”
  durationSec?: number | null

  width?: number | null
  height?: number | null
  seqNo: number
}

export interface JobDetailDto {
  jobId: number
  jobNo: string
  status: number
  progress: number
  algoCode: string
  algoParamsJson?: string | null
  errorMessage: string | null
  startTime?: string | null
  finishTime?: string | null
  totalVideoCount: number
  finishedVideoCount: number
  totalEventCount: number
  files: JobVideoDto[] // Backend returns 'files'
  videos?: JobVideoDto[] // Frontend uses 'videos'
}

export interface EventDto {
  id: number
  videoFileId: number
  eventType: number // 1 Flash, 2 Spark
  startTimeSec: number
  endTimeSec: number
  peakTimeSec: number
  frameIndex: number
  confidence: any // decimal -> 前端用 Number()
  bboxJson: string
  seqNo: number
}

export interface SnapshotDto {
  id: number
  eventId: number
  imagePath: string
  timeSec: number
  frameIndex: number
  imageWidth: number
  imageHeight: number
  seqNo: number
}

export interface ReanalyzeVideoRequest {
  fileIds: number[]
  algoParamsJson?: string
}

export interface ReanalyzeResultDto {
  requeuedCount: number
  clearedEventCount: number
  clearedSnapshotCount: number
}

// Use global api instance
// const http = axios.create(...)

export const videoAnalyticsService = {
  // ================= 新接口：持续上传会话（Job） =================
  async createJob(algoParamsJson?: string) {
    const body: CreateVideoJobRequest = {
      algoParamsJson: algoParamsJson && algoParamsJson.trim() ? algoParamsJson : undefined
    }
    const res = await api.post<any, ApiResponse<CreateJobResultDto>>('/api/VideoAnalytics/job', body)
    return res
  },

  async uploadOne(jobNo: string, file: File) {
    const form = new FormData()
    form.append('file', file)

    const res = await api.post<any, ApiResponse<UploadVideoResultDto>>(
      `/api/VideoAnalytics/job/${encodeURIComponent(jobNo)}/upload`,
      form
    )
    return res
  },

  async closeJob(jobNo: string) {
    const res = await api.post<any, ApiResponse<any>>(
      `/api/VideoAnalytics/job/${encodeURIComponent(jobNo)}/close`,
      {}
    )
    return res
  },

  async reanalyze(jobNo: string, fileIds: number[], algoParamsJson?: string) {
    const body: ReanalyzeVideoRequest = {
      fileIds: Array.from(new Set((fileIds || []).filter(x => Number(x) > 0))).map(Number),
      algoParamsJson: algoParamsJson && algoParamsJson.trim() ? algoParamsJson : undefined
    }
    const res = await api.post<any, ApiResponse<ReanalyzeResultDto>>(
      `/api/VideoAnalytics/job/${encodeURIComponent(jobNo)}/reanalyze`,
      body
    )
    return res
  },

  async deleteJob(jobNo: string) {
    const res = await api.delete<any, ApiResponse<any>>(
      `/api/VideoAnalytics/job/${encodeURIComponent(jobNo)}`
    )
    return res
  },

  // ================= 查询/事件/快照 =================
  async getJob(jobNo: string) {
    const res = await api.get<any, ApiResponse<JobDetailDto>>(
      `/api/VideoAnalytics/job/${encodeURIComponent(jobNo)}`
    )
    // res is ApiResponse (unwrapped by interceptor)
    if (res.success && res.data) {
      // Normalize: backend returns 'files', frontend expects 'videos'
      if (res.data.files && !res.data.videos) {
        res.data.videos = res.data.files
      }

      if (res.data.videos && Array.isArray(res.data.videos)) {
        res.data.videos.forEach((v: any) => {
          if (v && v.analyzeSec === undefined && v.AnalyzeSec !== undefined) v.analyzeSec = v.AnalyzeSec
          if (v && v.durationSec === undefined && v.DurationSec !== undefined) v.durationSec = v.DurationSec
          if (v && v.fileName === undefined && v.FileName !== undefined) v.fileName = v.FileName
          if (v && v.errorMessage === undefined && v.ErrorMessage !== undefined) v.errorMessage = v.ErrorMessage
          if (v && v.seqNo === undefined && v.SeqNo !== undefined) v.seqNo = v.SeqNo
          if (v && v.width === undefined && v.Width !== undefined) v.width = v.Width
          if (v && v.height === undefined && v.Height !== undefined) v.height = v.Height
          if (v && v.status === undefined && v.Status !== undefined) v.status = v.Status
          if (v && v.id === undefined && v.Id !== undefined) v.id = v.Id
        })
      }
    }
    return res
  },

  async getJobEvents(jobNo: string) {
    const res = await api.get<any, ApiResponse<EventDto[]>>(
      `/api/VideoAnalytics/job/${encodeURIComponent(jobNo)}/events`
    )
    return res
  },

  async getEventSnapshots(eventId: number) {
    const res = await api.get<any, ApiResponse<SnapshotDto[]>>(
      `/api/VideoAnalytics/event/${eventId}/snapshots`
    )
    return res
  },

  // ================= 可选：你原来已有，保留 =================
  async cancelJob(jobNo: string) {
    const res = await api.post<any, ApiResponse<any>>(
      `/api/VideoAnalytics/job/${encodeURIComponent(jobNo)}/cancel`,
      {}
    )
    return res
  },

  getSnapshotUrl(snapshotId: number) {
    return `${api.defaults.baseURL || ''}/api/VideoAnalytics/snapshot/${snapshotId}/download`
  },

  async downloadSnapshot(snapshotId: number) {
    return await api.get(`/api/VideoAnalytics/snapshot/${snapshotId}/download`, { responseType: 'blob' })
  },

  getVideoContentUrl(fileId: number) {
    return `${api.defaults.baseURL || ''}/api/VideoAnalytics/video/${fileId}/stream`
  }
}
