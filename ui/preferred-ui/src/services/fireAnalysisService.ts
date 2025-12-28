import api, { API_CONFIG } from '@/services/api'

export interface FireUploadResult {
  fileId: string
  fileName: string
  size: number
}

export interface FireHit {
  url: string
  type: 'flash' | 'fire'
  time: number
  score: number
  w: number
  h: number
}

export interface FireKpi {
  fps: string
  candPct: string
  elapsed: string
}

export interface FireAnalysisResult {
  logs: string[]
  kpi: FireKpi
  gallery: FireHit[]
}

export const fireAnalysisService = {
  async uploadVideo(file: File): Promise<FireUploadResult> {
    const form = new FormData()
    form.append('file', file)
    const res = await api.post('/api/FireAnalysis/upload', form, { headers: { 'Content-Type': 'multipart/form-data' } })
    return res.data
  },
  async analyze(fileId: string, params: Record<string, any>): Promise<FireAnalysisResult> {
    const res = await api.post('/api/FireAnalysis/analyze', { fileId, params })
    return res.data
  }
}
