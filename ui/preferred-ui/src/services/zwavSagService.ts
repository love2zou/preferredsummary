import api from './api'
import type { ApiResponse, PagedResult } from './zwavService'

/**
 * 暂降事件列表 DTO
 * 对齐后端 ZwavSagListItemDto
 */
export type ZwavSagListItemDto = {
  id: number
  fileId: number
  originalName?: string
  status: number
  hasSag: boolean
  eventType: string
  eventCount: number
  startTime?: string | null
  finishTime?: string | null
  costMs?: number | null
  worstPhase?: string
  residualVoltagePct?: number
  triggerPhase?: string
  endPhase?: string
  crtTime?: string
}

/**
 * 暂降事件相别明细 DTO
 * 对齐后端 ZwavSagPhaseDto
 */
export type ZwavSagPhaseDto = {
  phase: string
  startTimeUtc: string
  endTimeUtc: string
  durationMs: number
  referenceVoltage: number
  residualVoltage: number
  residualVoltagePct: number
  sagDepth: number
  sagPercent: number
  isTriggerPhase: boolean
  isEndPhase: boolean
  isWorstPhase: boolean
}

/**
 * 暂降事件详情 DTO
 * 对齐后端 ZwavSagDetailDto
 */
export type ZwavSagDetailDto = {
  id: number
  fileId: number
  originalName?: string | null
  status: number
  errorMessage?: string | null
  hasSag: boolean
  eventType: string
  eventCount: number
  startTime?: string | null
  finishTime?: string | null
  costMs?: number | null
  startTimeUtc?: string | null
  endTimeUtc?: string | null
  occurTimeUtc?: string | null
  durationMs?: number | null
  triggerPhase?: string
  endPhase?: string
  worstPhase?: string
  referenceType?: string
  referenceVoltage?: number
  residualVoltage?: number
  residualVoltagePct?: number
  sagDepth?: number
  sagPercent?: number
  startAngleDeg?: number
  phaseJumpDeg?: number
  sagThresholdPct?: number
  interruptThresholdPct?: number
  hysteresisPct?: number
  isMergedStatEvent?: boolean
  mergeGroupId?: string | null
  rawEventCount?: number
  remark?: string
  crtTime?: string
}

/**
 * 发起暂降分析请求 DTO
 * 对齐后端 AnalyzeZwavSagRequest
 */
export type AnalyzeZwavSagRequest = {
  fileIds?: number[]
  analysisGuids?: string[]
  forceRebuild?: boolean
  referenceType?: 'Declared' | 'Sliding' | string
  referenceVoltage?: number
  sagThresholdPct?: number
  interruptThresholdPct?: number
  hysteresisPct?: number
  minDurationMs?: number
}

/**
 * 暂降分析响应 DTO
 * 对齐后端 AnalyzeZwavSagResponse
 */
export type AnalyzeZwavSagResponse = {
  analyzedCount: number
  createdEventCount: number
  createdPhaseCount: number
  createdRmsPointCount: number
}

/**
 * 暂降过程页使用的电压通道 DTO
 */
export type ZwavSagVoltageChannelDto = {
  channelIndex: number
  phase: string
  channelCode?: string
  channelName?: string
  unit?: string
}

/**
 * RMS点 DTO
 */
export type ZwavSagRmsPointDto = {
  channelIndex: number
  phase: string
  sampleNo: number
  timeMs: number
  rms: number
  rmsPct: number
  referenceVoltage: number
  seqNo: number
}

/**
 * 过程图关键标记 DTO
 *
 * kind 建议规范为：
 * - event-start
 * - event-end
 * - phase-start
 * - phase-end
 * - min-rms
 * - recover
 */
export type ZwavSagMarkerDto = {
  kind: string
  phase?: string
  timeMs: number
  label?: string
  value?: number | null
}

/**
 * 过程页事件 DTO
 * 这部分建议后端尽量补齐，便于过程页直接展示，不再靠前端猜字段。
 */
export type ZwavSagComputedEventDto = {
  eventType: string
  phase: string

  /**
   * 可选：所属通道信息
   */
  channelIndex?: number
  channelName?: string

  /**
   * 时间信息
   */
  occurTimeUtc?: string
  startTimeUtc?: string
  endTimeUtc?: string
  startMs: number
  endMs: number
  durationMs: number

  /**
   * 参考与结果
   */
  referenceVoltage?: number
  residualVoltage?: number
  residualVoltagePct: number

  /**
   * 暂降幅值(%) = 100 - residualVoltagePct
   */
  sagMagnitudePct: number

  /**
   * 最低点时间（相对波形起点，ms）
   * 用于在过程图中定位“最小点”
   */
  minTimeMs?: number

  /**
   * 相别角色标识
   */
  isTriggerPhase?: boolean
  isEndPhase?: boolean
  isWorstPhase?: boolean
}

/**
 * 暂降分析过程 DTO
 */
export type ZwavSagProcessDto = {
  /**
   * 当前主事件
   */
  event: ZwavSagDetailDto

  /**
   * 相别明细
   */
  phases: ZwavSagPhaseDto[]

  /**
   * 关联的解析任务
   */
  analysisId: number
  analysisGuid: string

  /**
   * 波形基础信息
   */
  waveStartTimeUtc: string
  frequencyHz: number
  timeMul: number

  /**
   * 识别出的电压通道
   */
  voltageChannels: ZwavSagVoltageChannelDto[]

  /**
   * RMS点序列
   */
  rmsPoints: ZwavSagRmsPointDto[]

  /**
   * 关键标记线/点
   */
  markers: ZwavSagMarkerDto[]

  /**
   * 过程页事件结果
   */
  computedEvents: ZwavSagComputedEventDto[]

  /**
   * 推荐展示区间
   */
  suggestedFromSample?: number | null
  suggestedToSample?: number | null
}

/**
 * 过程页预览请求
 */
export type ZwavSagProcessPreviewRequest = {
  referenceType?: string
  referenceVoltage?: number
  sagThresholdPct?: number
  interruptThresholdPct?: number
  hysteresisPct?: number
  minDurationMs?: number
}

/**
 * 过程页预览响应
 */
export type ZwavSagProcessPreviewResponse = {
  rmsPoints: ZwavSagRmsPointDto[]
  markers: ZwavSagMarkerDto[]
  computedEvents: ZwavSagComputedEventDto[]
  suggestedFromSample?: number | null
  suggestedToSample?: number | null
}

/**
 * 更新 DTO
 * 当前后端只建议更新备注等有限字段
 */
export type UpdateZwavSagEventRequest = {
  remark?: string
}

/**
 * 通道识别词库 DTO
 */
export type ZwavSagChannelRuleDto = {
  id: number
  ruleName: string
  seqNo: number
  crtTime: string
  updTime: string
}

export type CreateZwavSagChannelRuleRequest = {
  ruleName: string
  seqNo: number
}

export type UpdateZwavSagChannelRuleRequest = {
  ruleName?: string
  seqNo?: number
}

export const zwavSagService = {
  /**
   * 分页查询暂降通道词库
   */
  async getChannelRuleList(params: {
    keyword?: string
    page?: number
    pageSize?: number
  }) {
    return api.get<any, ApiResponse<PagedResult<ZwavSagChannelRuleDto>>>(
      '/api/ZwavSagEvents/channel-rules',
      { params }
    )
  },

  /**
   * 获取暂降通道词库详情
   */
  async getChannelRuleDetail(id: number) {
    return api.get<any, ApiResponse<ZwavSagChannelRuleDto>>(
      `/api/ZwavSagEvents/channel-rules/${id}`
    )
  },

  /**
   * 新增暂降通道词库
   */
  async createChannelRule(body: CreateZwavSagChannelRuleRequest) {
    return api.post<any, ApiResponse<ZwavSagChannelRuleDto>>(
      '/api/ZwavSagEvents/channel-rules',
      body
    )
  },

  /**
   * 修改暂降通道词库
   */
  async updateChannelRule(id: number, body: UpdateZwavSagChannelRuleRequest) {
    return api.put<any, ApiResponse<any>>(`/api/ZwavSagEvents/channel-rules/${id}`, body)
  },

  /**
   * 删除暂降通道词库
   */
  async deleteChannelRule(id: number) {
    return api.delete<any, ApiResponse<any>>(`/api/ZwavSagEvents/channel-rules/${id}`)
  },

  /**
   * 分页查询暂降事件
   */
  async list(params: {
    keyword?: string
    eventType?: string
    phase?: string
    fromUtc?: string
    toUtc?: string
    page?: number
    pageSize?: number
  }) {
    return api.get<any, ApiResponse<PagedResult<ZwavSagListItemDto>>>(
      '/api/ZwavSagEvents',
      { params }
    )
  },

  /**
   * 获取事件详情
   */
  async getDetail(id: number) {
    return api.get<any, ApiResponse<ZwavSagDetailDto>>(`/api/ZwavSagEvents/${id}`)
  },

  /**
   * 获取相别明细
   */
  async getPhases(id: number) {
    return api.get<any, ApiResponse<ZwavSagPhaseDto[]>>(
      `/api/ZwavSagEvents/${id}/phases`
    )
  },

  /**
   * 按 FileId 查询暂降结果
   */
  async getByFile(fileId: number) {
    return api.get<any, ApiResponse<ZwavSagDetailDto[]>>(
      `/api/ZwavSagEvents/by-file/${fileId}`
    )
  },

  /**
   * 发起分析
   */
  async analyze(body: AnalyzeZwavSagRequest) {
    return api.post<any, ApiResponse<AnalyzeZwavSagResponse>>(
      '/api/ZwavSagEvents/analyze',
      body
    )
  },

  /**
   * 重新分析
   */
  async reanalyze(body: AnalyzeZwavSagRequest) {
    return api.post<any, ApiResponse<AnalyzeZwavSagResponse>>(
      '/api/ZwavSagEvents/reanalyze',
      body
    )
  },

  /**
   * 更新事件（当前主要用于备注）
   * 后端是 PATCH，不是 PUT
   */
  async update(id: number, body: UpdateZwavSagEventRequest) {
    return api.patch<any, ApiResponse<any>>(`/api/ZwavSagEvents/${id}`, body)
  },

  /**
   * 删除单个事件
   */
  async delete(id: number) {
    return api.delete<any, ApiResponse<any>>(`/api/ZwavSagEvents/${id}`)
  },

  /**
   * 按录波文件删除全部暂降结果
   */
  async deleteByFile(fileId: number) {
    return api.delete<any, ApiResponse<any>>(`/api/ZwavSagEvents/by-file/${fileId}`)
  },

  /**
   * 获取暂降分析过程
   */
  async getProcess(id: number) {
    return api.get<any, ApiResponse<ZwavSagProcessDto>>(
      `/api/ZwavSagEvents/${id}/process`
    )
  },

  /**
   * 预览过程计算
   */
  async previewProcess(id: number, body: ZwavSagProcessPreviewRequest) {
    return api.post<any, ApiResponse<ZwavSagProcessPreviewResponse>>(
      `/api/ZwavSagEvents/${id}/process/preview`,
      body
    )
  }
}