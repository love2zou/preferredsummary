import api from './api'
import type { ApiResponse, PagedResult } from './zwavService'

/**
 * 暂降事件列表 DTO
 * 对齐后端 ZwavSagListItemDto
 */
export type ZwavSagListItemDto = {
  id: number
  analysisId: number
  analysisGuid?: string
  originalName?: string
  stationName?: string
  deviceId?: string

  eventType: string

  occurTimeUtc: string
  startTimeUtc: string
  endTimeUtc: string

  durationMs: number

  worstPhase?: string
  residualVoltage?: number
  residualVoltagePct?: number
  sagDepth?: number
  sagPercent: number

  triggerPhase?: string
  endPhase?: string

  rawEventCount?: number
  isMergedStatEvent?: boolean

  crtTime?: string
}

/**
 * 暂降事件相别明细 DTO
 * 对齐后端 ZwavSagPhaseDto
 */
export type ZwavSagPhaseDto = {
  id: number
  sagEventId: number
  analysisId: number

  phase: string

  startTimeUtc: string
  endTimeUtc: string
  durationMs: number

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

  isTriggerPhase?: boolean
  isEndPhase?: boolean
  isWorstPhase?: boolean

  remark?: string
}

/**
 * 暂降事件详情 DTO
 * 对齐后端 ZwavSagDetailDto
 */
export type ZwavSagDetailDto = {
  id: number
  analysisId: number

  analysisGuid?: string
  fileName?: string

  stationName?: string
  deviceId?: string
  frequencyHz?: number

  eventType: string

  occurTimeUtc: string
  startTimeUtc: string
  endTimeUtc: string

  durationMs: number

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
  mergeGroupId?: string
  rawEventCount?: number

  remark?: string

  phases?: ZwavSagPhaseDto[]
}

/**
 * 发起暂降分析请求 DTO
 * 对齐后端 AnalyzeZwavSagRequest
 */
export type AnalyzeZwavSagRequest = {
  analysisGuids: string[]
  forceRebuild?: boolean
  referenceType?: 'Declared' | 'Sliding' | string
  referenceVoltage?: number
  sagThresholdPct?: number
  interruptThresholdPct?: number
  hysteresisPct?: number
  minDurationMs?: number
  rmsMode?: 'HalfCycle' | 'OneCycle' | string
  channelIndexes?: number[]
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
 * 更新 DTO
 * 当前后端只建议更新备注等有限字段
 */
export type UpdateZwavSagEventRequest = {
  remark?: string
}

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
    return api.get<any, ApiResponse<ZwavSagChannelRuleDto>>(`/api/ZwavSagEvents/channel-rules/${id}`)
  },

  /**
   * 新增暂降通道词库
   */
  async createChannelRule(body: CreateZwavSagChannelRuleRequest) {
    return api.post<any, ApiResponse<ZwavSagChannelRuleDto>>('/api/ZwavSagEvents/channel-rules', body)
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
    return api.get<any, ApiResponse<ZwavSagPhaseDto[]>>(`/api/ZwavSagEvents/${id}/phases`)
  },

  /**
   * 按 AnalysisId 查询暂降结果
   */
  async getByAnalysis(analysisId: number) {
    return api.get<any, ApiResponse<ZwavSagDetailDto[]>>(
      `/api/ZwavSagEvents/by-analysis/${analysisId}`
    )
  },

  async analyze(body: {
    analysisGuids: string[]
    referenceType?: string
    referenceVoltage?: number
    thresholdPercent?: number // 对应后端 SagThresholdPct
    sagThresholdPct?: number // 兼容
    interruptThresholdPct?: number
    hysteresisPct?: number
    minDurationMs?: number
    rmsMode?: string
    forceRebuild?: boolean
  }) {
    // 映射前端参数到后端 DTO
    const req = {
      analysisGuids: body.analysisGuids,
      forceRebuild: body.forceRebuild,
      referenceType: body.referenceType,
      referenceVoltage: body.referenceVoltage,
      sagThresholdPct: body.sagThresholdPct ?? body.thresholdPercent,
      interruptThresholdPct: body.interruptThresholdPct,
      hysteresisPct: body.hysteresisPct,
      minDurationMs: body.minDurationMs,
      rmsMode: body.rmsMode
    }
    return api.post<any, ApiResponse<AnalyzeZwavSagResponse>>('/api/ZwavSagEvents/analyze', req)
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
   * 按解析任务删除全部暂降结果
   */
  async deleteByAnalysis(analysisId: number) {
    return api.delete<any, ApiResponse<any>>(`/api/ZwavSagEvents/by-analysis/${analysisId}`)
  }
}