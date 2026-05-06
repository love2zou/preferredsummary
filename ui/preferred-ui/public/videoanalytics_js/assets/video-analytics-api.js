import { apiBlob, apiForm, apiJson, buildUrl, getApiBaseUrl } from './common.js'

function normalizeVideo(video) {
  const item = { ...(video || {}) }
  if (item.analyzeSec === undefined && item.AnalyzeSec !== undefined) item.analyzeSec = item.AnalyzeSec
  if (item.durationSec === undefined && item.DurationSec !== undefined) item.durationSec = item.DurationSec
  if (item.fileName === undefined && item.FileName !== undefined) item.fileName = item.FileName
  if (item.errorMessage === undefined && item.ErrorMessage !== undefined) item.errorMessage = item.ErrorMessage
  if (item.seqNo === undefined && item.SeqNo !== undefined) item.seqNo = item.SeqNo
  if (item.width === undefined && item.Width !== undefined) item.width = item.Width
  if (item.height === undefined && item.Height !== undefined) item.height = item.Height
  if (item.status === undefined && item.Status !== undefined) item.status = item.Status
  if (item.id === undefined && item.Id !== undefined) item.id = item.Id
  item.id = Number(item.id || 0)
  item.status = Number(item.status || 0)
  return item
}

function normalizeJob(job) {
  const item = { ...(job || {}) }
  if (item.files && !item.videos) item.videos = item.files
  item.videos = Array.isArray(item.videos) ? item.videos.map(normalizeVideo) : []
  item.totalVideoCount = Number(item.totalVideoCount ?? item.TotalVideoCount ?? 0)
  item.finishedVideoCount = Number(item.finishedVideoCount ?? item.FinishedVideoCount ?? 0)
  item.totalEventCount = Number(item.totalEventCount ?? item.TotalEventCount ?? 0)
  item.progress = Number(item.progress ?? item.Progress ?? 0)
  item.status = Number(item.status ?? item.Status ?? 0)
  return item
}

function normalizeEvent(event) {
  const item = { ...(event || {}) }
  item.id = Number(item.id ?? item.Id ?? 0)
  item.videoFileId = Number(item.videoFileId ?? item.VideoFileId ?? 0)
  item.eventType = Number(item.eventType ?? item.EventType ?? 0)
  item.startTimeSec = Number(item.startTimeSec ?? item.StartTimeSec ?? 0)
  item.endTimeSec = Number(item.endTimeSec ?? item.EndTimeSec ?? 0)
  item.peakTimeSec = Number(item.peakTimeSec ?? item.PeakTimeSec ?? 0)
  item.frameIndex = Number(item.frameIndex ?? item.FrameIndex ?? 0)
  item.confidence = Number(item.confidence ?? item.Confidence ?? 0)
  item.seqNo = Number(item.seqNo ?? item.SeqNo ?? 0)
  return item
}

function normalizeSnapshot(snapshot) {
  const item = { ...(snapshot || {}) }
  item.id = Number(item.id ?? item.Id ?? 0)
  item.eventId = Number(item.eventId ?? item.EventId ?? 0)
  item.confidence = Number(item.confidence ?? item.Confidence ?? 0)
  return item
}

export const videoAnalyticsApi = {
  async createJob(algoParamsJson = '') {
    const res = await apiJson('/api/VideoAnalytics/job', {
      method: 'POST',
      body: {
        algoParamsJson: algoParamsJson && String(algoParamsJson).trim() ? algoParamsJson : undefined
      }
    })
    return res
  },

  async uploadOne(jobNo, file) {
    const form = new FormData()
    form.append('file', file)
    return apiForm(`/api/VideoAnalytics/job/${encodeURIComponent(jobNo)}/upload`, form)
  },

  async closeJob(jobNo) {
    return apiJson(`/api/VideoAnalytics/job/${encodeURIComponent(jobNo)}/close`, {
      method: 'POST',
      body: {}
    })
  },

  async reanalyze(jobNo, fileIds, algoParamsJson) {
    return apiJson(`/api/VideoAnalytics/job/${encodeURIComponent(jobNo)}/reanalyze`, {
      method: 'POST',
      body: {
        fileIds: Array.from(new Set((fileIds || []).filter((x) => Number(x) > 0))).map(Number),
        algoParamsJson: algoParamsJson && String(algoParamsJson).trim() ? algoParamsJson : undefined
      }
    })
  },

  async deleteJob(jobNo) {
    return apiJson(`/api/VideoAnalytics/job/${encodeURIComponent(jobNo)}`, {
      method: 'DELETE'
    })
  },

  async getJob(jobNo) {
    const res = await apiJson(`/api/VideoAnalytics/job/${encodeURIComponent(jobNo)}`)
    if (res && res.success && res.data) {
      res.data = normalizeJob(res.data)
    }
    return res
  },

  async getJobEvents(jobNo) {
    const res = await apiJson(`/api/VideoAnalytics/job/${encodeURIComponent(jobNo)}/events`)
    if (res && res.success && Array.isArray(res.data)) {
      res.data = res.data.map(normalizeEvent)
    }
    return res
  },

  async getEventSnapshots(eventId) {
    const res = await apiJson(`/api/VideoAnalytics/event/${eventId}/snapshots`)
    if (res && res.success && Array.isArray(res.data)) {
      res.data = res.data.map(normalizeSnapshot)
    }
    return res
  },

  async downloadSnapshot(snapshotId) {
    return apiBlob(`/api/VideoAnalytics/snapshot/${snapshotId}/download`)
  },

  getSnapshotUrl(snapshotId) {
    return buildUrl(getApiBaseUrl(), `/api/VideoAnalytics/snapshot/${snapshotId}/download`)
  },

  getVideoContentUrl(fileId) {
    return buildUrl(getApiBaseUrl(), `/api/VideoAnalytics/video/${fileId}/stream`)
  }
}
