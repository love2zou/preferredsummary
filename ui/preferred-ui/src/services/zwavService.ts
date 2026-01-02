import api from './api'

export interface ApiResponse<T> {
  success: boolean
  message: string
  data: T
}

export interface FileDto {
  id: number
  originalName: string
  fileSize: number
  storagePath: string
  extractPath: string
}

export interface CfgSummaryDto {
  stationName: string
  deviceId: string
  analogCount: number
  digitalCount: number
  frequencyHz: number | null
  timeMul: number | null
  startTimeRaw: string
  triggerTimeRaw: string
}

export interface AnalysisDetailDto {
  analysisGuid: string
  status: string
  progress: number
  errorMessage: string | null
  totalRecords: number
  recordSize: number
  digitalWords: number
  crtTime: string
  updTime: string
  startTime: string | null
  finishTime: string | null
  file: FileDto | null
  cfgSummary: CfgSummaryDto | null
}

export interface ZwavFileAnalysis {
  analysisGuid: string
  status: string
  progress: number
  errorMessage: string | null
  originalName: string
  fileSize: number
  crtTime: string
  updTime: string
  startTime: string | null
  finishTime: string | null
}

export interface ZwavListParams {
  status?: string
  keyword?: string
  fromUtc?: string
  toUtc?: string
  page?: number
  pageSize?: number
  orderBy?: string
}

export interface UploadResponse {
  fileId: number
  originalName: string
  storagePath: string
  fileSizeBytes: number
  ext: string
  crtTimeUtc: string
}

export interface CreateAnalysisResponse {
  analysisGuid: string
  status: string
  progressUrl: string
}

export interface PagedResult<T> {
  data: T[]
  total: number
  page: number
  pageSize: number
  totalPages: number
}

export interface ChannelDto {
  channelIndex: number
  channelType: string
  channelCode: string
  channelName: string
  phase: string
  unit: string
  ratioA: number | null
  offsetB: number | null
  skew: number | null
  isEnable: number
}

export interface CfgDto {
  stationName: string
  deviceId: string
  analogCount: number
  digitalCount: number
  frequencyHz: number | null
  timeMul: number | null
  startTimeRaw: string
  triggerTimeRaw: string
  revision: string
  formatType: string
  dataType: string
  sampleRateJson: string
  fullCfgText: string
}

export interface NameValue {
  name: string
  value: string
}

export interface TripInfo {
  time: string
  name: string
  phase: string
  value: string
}

export interface FaultInfo {
  name: string
  value: string
  unit: string
}

export interface DigitalEvent {
  time: string
  name: string
  value: string
}

export interface SettingValue {
  name: string
  value: string
  unit: string
}

export interface RelayEnaValue {
  name: string
  value: string
}

export interface HdrDto {
  faultStartTime: string
  faultKeepingTime: string
  deviceInfoJson: NameValue[]
  tripInfoJSON: TripInfo[]
  faultInfoJson: FaultInfo[]
  digitalStatusJson: NameValue[]
  digitalEventJson: DigitalEvent[]
  settingValueJson: SettingValue[]
  relayEnaValueJSON: RelayEnaValue[]
}

export interface WaveDataRowDto {
  sampleNo: number
  timeRaw: number
  analog: number[]
  digital: number[]
}

export interface WaveDataPageDto {
  fromSample: number
  toSample: number
  downSample: number
  channels: number[]
  digitals: number[]
  rows: WaveDataRowDto[]
}

export const zwavService = {
  // ... existing methods ...
  // 上传文件
  uploadFile(file: File) {
    const formData = new FormData()
    formData.append('file', file)
    return api.post<any, ApiResponse<UploadResponse>>('/api/ZwavAnalyses/upload', formData, {
      headers: {
        'Content-Type': 'multipart/form-data'
      }
    })
  },

  // 创建解析任务
  createAnalysis(fileId: number, forceRecreate: boolean = false) {
    return api.post<any, ApiResponse<CreateAnalysisResponse>>('/api/ZwavAnalyses/create', {
      fileId,
      forceRecreate
    })
  },

  // 获取列表
  getList(params: ZwavListParams) {
    return api.get<any, ApiResponse<PagedResult<ZwavFileAnalysis>>>('/api/ZwavAnalyses', { params })
  },

  // 获取状态
  getStatus(analysisGuid: string) {
    return api.get<any, ApiResponse<ZwavFileAnalysis>>(`/api/ZwavAnalyses/${analysisGuid}`)
  },

  // 删除任务
  deleteAnalysis(analysisGuid: string, deleteFile: boolean = false) {
    return api.delete<any, ApiResponse<null>>(`/api/ZwavAnalyses/${analysisGuid}`, {
      params: { deleteFile }
    })
  },

  // 获取通道
  getChannels(analysisGuid: string, type: string = 'All', enabledOnly: boolean = true) {
    return api.get<any, ApiResponse<ChannelDto[]>>(`/api/ZwavAnalyses/${analysisGuid}/channels`, {
      params: { type, enabledOnly }
    })
  },

  // 获取详情
  getDetail(analysisGuid: string) {
    return api.get<any, ApiResponse<AnalysisDetailDto>>(`/api/ZwavAnalyses/${analysisGuid}/detail`)
  },

  // 获取 CFG
  getCfg(analysisGuid: string, includeText: boolean = false) {
    return api.get<any, ApiResponse<CfgDto>>(`/api/ZwavAnalyses/${analysisGuid}/cfg`, {
      params: { includeText }
    })
  },

  // 获取 HDR
  getHdr(analysisGuid: string) {
    return api.get<any, ApiResponse<HdrDto>>(`/api/ZwavAnalyses/${analysisGuid}/hdr`)
  },

  // 获取波形数据
  getWaveData(analysisGuid: string, params: {
    fromSample?: number
    toSample?: number
    offset?: number
    limit?: number
    channels?: string
    digitals?: string
    downSample?: number
  }) {
    return api.get<any, ApiResponse<WaveDataPageDto>>(`/api/ZwavAnalyses/${analysisGuid}/get-wavedata`, {
      params
    })
  },

  // 下载原文件
  downloadFile(analysisGuid: string) {
    return api.get(`/api/ZwavAnalyses/${analysisGuid}/download`, {
      responseType: 'blob'
    })
  }
}

