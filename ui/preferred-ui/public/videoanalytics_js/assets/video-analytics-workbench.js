import { ALGO_FIELDS, ALGO_FIELD_GROUPS, DEFAULT_ALGO_PARAMS, DEFAULT_ALGO_PARAMS_JSON, parseAlgoParamsJson, toAlgoParamsJson } from './video-algo-params.js'
import { ALGO_STORAGE_KEY, createTag, getJobStatusText, getStatusType, getVideoStatusText, pushJobHistory, removeJobHistory, showToast, toNum } from './video-analytics-shared.js'
import { bindDialog, byId, closeDialog, downloadBlob, escapeHtml, formatTime, getApiBaseUrl, openDialog, qs, setApiBaseUrl, setHtml, setText } from './common.js'
import { videoAnalyticsApi } from './video-analytics-api.js'

const state = {
  activeTab: 'list',
  currentJob: null,
  allEvents: [],
  currentFileId: null,
  currentTime: 0,
  filterConf: 0,
  filterHasEvents: false,
  filterType: 'ALL',
  onlyAbnormal: false,
  listPageNo: 1,
  gridMode: 9,
  listSelectedId: null,
  uploadClosed: false,
  closing: false,
  deleting: false,
  selectedFileIds: new Set(),
  reanalyzing: false,
  snapshotCache: {},
  snapshotLoading: {},
  uploadQueue: [],
  pollTimer: null,
  algoParams: parseAlgoParamsJson(localStorage.getItem(ALGO_STORAGE_KEY) || DEFAULT_ALGO_PARAMS_JSON)
}

function persistAlgoParams() {
  localStorage.setItem(ALGO_STORAGE_KEY, toAlgoParamsJson(state.algoParams))
}

function getEnrichedVideos() {
  const videos = state.currentJob?.videos || []
  return videos.map((file) => {
    const events = state.allEvents.filter((item) => Number(item.videoFileId) === Number(file.id))
    const stats = {
      spark: 0,
      flash: 0,
      total: events.length,
      maxConf: 0,
      firstEventTime: 0
    }
    let firstTime = Infinity
    events.forEach((item) => {
      if (Number(item.eventType) === 2) stats.spark += 1
      if (Number(item.eventType) === 1) stats.flash += 1
      stats.maxConf = Math.max(stats.maxConf, toNum(item.confidence))
      firstTime = Math.min(firstTime, toNum(item.peakTimeSec))
    })
    stats.firstEventTime = firstTime === Infinity ? 0 : firstTime
    return { ...file, stats, events }
  })
}

function getFilteredVideos() {
  let list = getEnrichedVideos()
  if (state.filterHasEvents) list = list.filter((item) => item.stats.total > 0)
  if (state.filterType === 'Spark') list = list.filter((item) => item.stats.spark > 0)
  if (state.filterType === 'Flash') list = list.filter((item) => item.stats.flash > 0)
  if (state.filterConf > 0) list = list.filter((item) => item.stats.maxConf >= state.filterConf)
  return list.slice().sort((a, b) => {
    if (b.stats.total !== a.stats.total) return b.stats.total - a.stats.total
    if (b.stats.maxConf !== a.stats.maxConf) return b.stats.maxConf - a.stats.maxConf
    if (a.stats.total > 0 && b.stats.total > 0 && a.stats.firstEventTime !== b.stats.firstEventTime) {
      return a.stats.firstEventTime - b.stats.firstEventTime
    }
    return Number(a.id) - Number(b.id)
  })
}

function getListAllVideos() {
  let list = getEnrichedVideos().slice().sort((a, b) => Number(a.id) - Number(b.id))
  if (state.onlyAbnormal) list = list.filter((item) => item.stats.total > 0)
  return list
}

function getListPageSize() {
  return Number(state.gridMode)
}

function getListPageCount() {
  return Math.max(1, Math.ceil(getListAllVideos().length / getListPageSize()))
}

function getListPageVideos() {
  const start = (state.listPageNo - 1) * getListPageSize()
  return getListAllVideos().slice(start, start + getListPageSize())
}

function getAllFilteredEvents() {
  let events = state.allEvents.slice()
  if (state.filterType === 'Spark') events = events.filter((item) => Number(item.eventType) === 2)
  if (state.filterType === 'Flash') events = events.filter((item) => Number(item.eventType) === 1)
  events = events.filter((item) => toNum(item.confidence) >= state.filterConf)
  return events.sort((a, b) => Number(a.videoFileId) - Number(b.videoFileId) || Number(a.peakTimeSec) - Number(b.peakTimeSec))
}

function getEventGroups() {
  const videoMap = new Map(getEnrichedVideos().map((item) => [Number(item.id), item]))
  const groupMap = new Map()
  getAllFilteredEvents().forEach((item) => {
    const videoId = Number(item.videoFileId)
    if (!groupMap.has(videoId)) groupMap.set(videoId, [])
    groupMap.get(videoId).push(item)
  })

  let groups = Array.from(groupMap.entries()).map(([videoId, events]) => {
    const video = videoMap.get(videoId)
    let spark = 0
    let flash = 0
    let maxConf = 0
    let firstEventTime = Infinity
    events.forEach((item) => {
      if (Number(item.eventType) === 2) spark += 1
      if (Number(item.eventType) === 1) flash += 1
      maxConf = Math.max(maxConf, toNum(item.confidence))
      firstEventTime = Math.min(firstEventTime, toNum(item.peakTimeSec))
    })
    return {
      videoId,
      fileName: video?.fileName || `视频 ${videoId}`,
      status: Number(video?.status || 0),
      spark,
      flash,
      total: events.length,
      maxConf,
      firstEventTime: firstEventTime === Infinity ? 0 : firstEventTime,
      events
    }
  })

  if (state.filterHasEvents) groups = groups.filter((item) => item.total > 0)
  return groups.sort((a, b) => {
    if (b.total !== a.total) return b.total - a.total
    if (b.maxConf !== a.maxConf) return b.maxConf - a.maxConf
    return a.firstEventTime - b.firstEventTime
  })
}

function currentVideo() {
  return getEnrichedVideos().find((item) => Number(item.id) === Number(state.currentFileId)) || null
}

function parseServerDateTime(value) {
  if (!value) return null
  if (value instanceof Date) return Number.isNaN(value.getTime()) ? null : value
  const text = String(value).trim()
  if (!text) return null
  const normalized = text.includes('T') ? text : text.replace(' ', 'T')
  const date = new Date(normalized)
  return Number.isNaN(date.getTime()) ? null : date
}

function getUploadExpireAtMs(job = state.currentJob) {
  const expireAt = parseServerDateTime(job?.uploadExpireTime || job?.crtTime)
  if (!expireAt) return 0
  if (job?.uploadExpireTime) return expireAt.getTime()
  return expireAt.getTime() + 60 * 60 * 1000
}

function formatSessionExpireText() {
  const expireMs = getUploadExpireAtMs()
  if (!expireMs) return ''
  const remainMs = expireMs - Date.now()
  if (remainMs <= 0) return '会话已自动关闭'
  const totalSec = Math.ceil(remainMs / 1000)
  const hh = String(Math.floor(totalSec / 3600)).padStart(2, '0')
  const mm = String(Math.floor((totalSec % 3600) / 60)).padStart(2, '0')
  const ss = String(totalSec % 60).padStart(2, '0')
  return `会话将在 ${hh}:${mm}:${ss} 后自动关闭`
}

function pruneSelection() {
  const validIds = new Set((state.currentJob?.videos || []).map((item) => Number(item.id)))
  state.selectedFileIds = new Set(Array.from(state.selectedFileIds).filter((id) => validIds.has(Number(id))))
  if (state.currentFileId && !validIds.has(Number(state.currentFileId))) {
    state.currentFileId = state.currentJob?.videos?.[0]?.id ?? null
  }
  if (state.listSelectedId && !validIds.has(Number(state.listSelectedId))) {
    state.listSelectedId = state.currentJob?.videos?.[0]?.id ?? null
  }
}
function syncUploadClosed() {
  state.uploadClosed = Boolean(state.currentJob?.uploadClosed) || Date.now() >= getUploadExpireAtMs()
}

function setActiveTab(tab) {
  state.activeTab = tab
  byId('tabListBtn').classList.toggle('is-active', tab === 'list')
  byId('tabDetailBtn').classList.toggle('is-active', tab === 'detail')
  byId('tabListPanel').classList.toggle('is-hidden', tab !== 'list')
  byId('tabDetailPanel').classList.toggle('is-hidden', tab !== 'detail')
}

function renderHeader() {
  setText('currentJobNo', state.currentJob?.jobNo || '-')
  setHtml('currentJobStatus', state.currentJob ? createTag(escapeHtml(getJobStatusText(state.currentJob)), getStatusType(state.currentJob.status)) : '')
  setHtml('uploadClosedTag', state.uploadClosed ? createTag('已关闭上传', 'info') : '')
  setText('sessionExpireText', formatSessionExpireText())
}

function renderListTab() {
  const pageCount = getListPageCount()
  if (state.listPageNo > pageCount) state.listPageNo = pageCount
  const listAll = getListAllVideos()
  const pageVideos = getListPageVideos()

  setText('listSummaryText', `（共 ${listAll.length} 条）`)
  setText('gridSummaryText', `(${pageVideos.length} / ${getListPageSize()})`)
  setText('listPagerText', `每页 ${getListPageSize()} 条，网格数量随布局变化`)
  setText('listPageText', `${state.listPageNo} / ${pageCount}`)
  byId('btnListPrev').disabled = state.listPageNo <= 1
  byId('btnListNext').disabled = state.listPageNo >= pageCount

  if (!state.listSelectedId) state.listSelectedId = pageVideos[0]?.id ?? null

  if (!pageVideos.length) {
    setHtml('listTableBody', '<tr><td colspan="6" class="cell-empty">当前条件下暂无视频</td></tr>')
  } else {
    const rows = pageVideos.map((row, index) => {
      const selected = Number(state.listSelectedId) === Number(row.id) ? 'is-selected-row' : ''
      return `
        <tr class="${selected}" data-action="select-list-row" data-file-id="${row.id}">
          <td class="cell-center">${(state.listPageNo - 1) * getListPageSize() + index + 1}</td>
          <td class="file-name-cell">${escapeHtml(row.fileName || '-')}</td>
          <td>${row.stats.total > 0 ? createTag('异常', 'danger') : createTag('正常', 'success')}</td>
          <td>
            <span class="mini-kpi">
              <span class="kpi kpi-danger">火花 ${row.stats.spark}</span>
              <span class="kpi kpi-warning">闪光 ${row.stats.flash}</span>
            </span>
          </td>
          <td>${row.stats.maxConf > 0 ? `${(row.stats.maxConf * 100).toFixed(0)}%` : '<span class="text-gray">-</span>'}</td>
          <td><button class="el-button is-link" data-action="open-detail" data-file-id="${row.id}" type="button">查看详情</button></td>
        </tr>
      `
    }).join('')
    setHtml('listTableBody', rows)
  }

  const gridClass = state.gridMode === 4 ? 'grid-2' : state.gridMode === 9 ? 'grid-3' : 'grid-4'
  const emptyCount = Math.max(0, getListPageSize() - pageVideos.length)
  const cells = pageVideos.map((item) => `
    <div class="grid-cell ${Number(state.listSelectedId) === Number(item.id) ? 'active' : ''}" data-action="select-grid-video" data-file-id="${item.id}">
      ${item.stats.total > 0 ? '<div class="grid-flame" title="存在异常事件">⚠</div>' : ''}
      <video class="grid-video" controls muted playsinline preload="metadata" src="${escapeHtml(videoAnalyticsApi.getVideoContentUrl(item.id))}"></video>
    </div>
  `).join('')
  const empty = Array.from({ length: emptyCount }, () => '<div class="grid-cell empty"><div class="empty-tip">空</div></div>').join('')
  setHtml('videoGrid', `<div class="video-grid-inner ${gridClass}">${cells}${empty}</div>`)
  document.querySelectorAll('[data-grid]').forEach((node) => {
    node.classList.toggle('is-active', Number(node.getAttribute('data-grid')) === Number(state.gridMode))
  })
}

function renderDetailVideoList() {
  const videos = getFilteredVideos()
  const hit = videos.filter((item) => state.selectedFileIds.has(Number(item.id))).length
  const allBox = byId('selectAllVideosToggle')
  allBox.checked = videos.length > 0 && hit === videos.length
  allBox.indeterminate = hit > 0 && hit < videos.length

  setText('detailVideoSummary', `视频列表 (${videos.length})`)
  byId('btnReanalyzeSelected').disabled = state.selectedFileIds.size === 0 || state.reanalyzing || state.deleting || !state.currentJob
  byId('btnReanalyzeSelected').textContent = `重新分析 (${state.selectedFileIds.size})`
  byId('btnDeleteSelected').disabled = state.selectedFileIds.size === 0 || state.reanalyzing || state.deleting || !state.currentJob
  byId('btnDeleteSelected').textContent = state.deleting ? `删除中 (${state.selectedFileIds.size})` : `批量删除 (${state.selectedFileIds.size})`

  if (!videos.length) {
    setHtml('detailVideoList', '<div class="empty-panel">当前筛选条件下没有视频</div>')
    return
  }

  const html = videos.map((vid) => `
    <div class="video-card ${Number(state.currentFileId) === Number(vid.id) ? 'active' : ''}" data-action="select-detail-video" data-file-id="${vid.id}">
      <div class="vc-row1">
        <div class="vc-left">
          <input type="checkbox" data-action="toggle-select-video" data-file-id="${vid.id}" ${state.selectedFileIds.has(Number(vid.id)) ? 'checked' : ''} />
          <span class="vc-name">${escapeHtml(vid.fileName || '-')}</span>
        </div>
        <div class="vc-right">
          ${vid.stats.total > 0 ? createTag('异常', 'danger') : createTag('正常', 'success')}
          ${createTag(escapeHtml(getVideoStatusText(vid.status)), 'info')}
        </div>
      </div>

      <div class="vc-row2">
        <div class="vc-kpi-left">
          ${vid.stats.spark > 0 ? createTag(`火花 ${vid.stats.spark}`, 'danger') : ''}
          ${vid.stats.flash > 0 ? createTag(`闪光 ${vid.stats.flash}`, 'warning') : ''}
          ${vid.stats.total === 0 ? '<span class="text-gray">无异常事件</span>' : ''}
          <span class="vc-conf-inline ${vid.stats.maxConf > 0 ? '' : 'text-gray'}">置信度 ${vid.stats.maxConf > 0 ? `${(vid.stats.maxConf * 100).toFixed(0)}%` : '-'}</span>
        </div>
        <div class="vc-kpi-right">
          <span class="vc-meta">分析时长: ${formatAnalyzeSec(vid.analyzeSec)}</span>
        </div>
      </div>

      <div class="vc-row3">
        <span class="vc-meta">${Number(vid.status) === 1 ? '处理中...' : ''}</span>
      </div>

      ${vid.errorMessage ? `<div class="vc-error">${escapeHtml(vid.errorMessage)}</div>` : ''}
    </div>
  `).join('')
  setHtml('detailVideoList', html)
}
function renderEventWall() {
  const groups = getEventGroups()
  setText('eventWallSummary', `（筛选后共 ${getAllFilteredEvents().length} 个事件，${groups.length} 个视频分组）`)

  if (!groups.length) {
    setHtml('eventWall', '<div class="empty-panel">当前筛选条件下无事件</div>')
    return
  }

  const html = groups.map((group) => `
    <div class="group-block">
      <div class="group-header" data-action="select-group-video" data-video-id="${group.videoId}">
        <div class="gh-left">
          <span class="gh-title">${escapeHtml(group.fileName)}</span>
          ${createTag(escapeHtml(getVideoStatusText(group.status)), 'info')}
          ${createTag('异常', 'danger')}
        </div>
        <div class="gh-right">
          ${group.spark > 0 ? createTag(`火花 ${group.spark}`, 'danger') : ''}
          ${group.flash > 0 ? createTag(`闪光 ${group.flash}`, 'warning') : ''}
          <span class="gh-meta">事件 ${group.total}</span>
          ${group.maxConf > 0 ? `<span class="gh-meta strong">置信度 ${(group.maxConf * 100).toFixed(0)}%</span>` : ''}
        </div>
      </div>

      <div class="event-grid">
        ${group.events.map((evt) => `
          <div class="event-card ${Number(state.currentFileId) === Number(group.videoId) ? 'selected' : ''}" data-action="seek-event" data-video-id="${group.videoId}" data-event-id="${evt.id}">
            <div class="ec-thumb">
              ${state.snapshotCache[evt.id]
                ? `<img class="ec-img" src="${escapeHtml(state.snapshotCache[evt.id])}" alt="snapshot-${evt.id}" />`
                : `<div class="ec-placeholder">${state.snapshotLoading[evt.id] ? '加载中...' : '暂无截图'}</div>`}
              <div class="ec-time">${escapeHtml(formatTime(evt.peakTimeSec))}</div>
              <button class="ec-dl" title="下载截图" data-action="download-snapshot" data-event-id="${evt.id}" type="button">下载</button>
            </div>
            <div class="ec-info">
              <div class="ec-row">
                ${createTag(Number(evt.eventType) === 2 ? '火花' : '闪光', Number(evt.eventType) === 2 ? 'danger' : 'warning')}
                <span class="ec-conf">${(Number(evt.confidence) * 100).toFixed(1)}%</span>
              </div>
              <div class="progress-line"><span style="width:${Math.round(Number(evt.confidence) * 100)}%"></span></div>
            </div>
          </div>
        `).join('')}
      </div>
    </div>
  `).join('')

  setHtml('eventWall', html)
}

function renderPlayerAndUpload() {
  const video = currentVideo()
  const player = byId('detailVideoPlayer')
  const src = video ? videoAnalyticsApi.getVideoContentUrl(video.id) : ''
  setText('playerTitle', `播放器 - ${video?.fileName || '-'}`)
  if (player.getAttribute('src') !== src) {
    if (src) {
      player.src = src
      player.load()
    } else {
      player.removeAttribute('src')
      player.load()
    }
  }

  setText('playerTimeText', formatTime(state.currentTime))
  setText('videoErrorText', video?.errorMessage ? video.errorMessage : '当前视频暂无错误信息')
  byId('videoErrorPanel').classList.toggle('has-error', Boolean(video?.errorMessage))

  const disabled = state.uploadClosed || !state.currentJob
  byId('btnOpenFilePicker').disabled = disabled
  byId('btnCloseUpload').disabled = disabled || state.closing
  byId('btnCloseUpload').textContent = state.closing ? '关闭中...' : '关闭上传'
  byId('uploadDropzone').classList.toggle('is-disabled', disabled)

  const videoCount = (state.currentJob?.videos || []).length
  const finished = (state.currentJob?.videos || []).filter((item) => Number(item.status) === 2).length
  const failed = (state.currentJob?.videos || []).filter((item) => Number(item.status) === 3).length
  setText('uploadStatsText', `已上传 ${videoCount} 个`)
  setText('finishStatsText', `完成/失败: ${finished}/${failed}`)

  if (!state.uploadQueue.length) {
    setHtml('uploadQueueList', '<div class="upload-queue-empty">暂无本地上传记录</div>')
  } else {
    setHtml('uploadQueueList', state.uploadQueue.map((item) => `
      <div class="upload-queue-item">
        <span class="file-name-cell">${escapeHtml(item.name)}</span>
        ${createTag(item.status === 'success' ? '成功' : item.status === 'error' ? '失败' : '上传中', item.status === 'success' ? 'success' : item.status === 'error' ? 'danger' : 'primary')}
      </div>
    `).join(''))
  }
}
function renderAlgoDialog() {
  const html = ALGO_FIELD_GROUPS.map((group) => `
    <div class="algo-group">
      <div class="algo-group-title">${group}</div>
      <div class="algo-grid">
        ${ALGO_FIELDS.filter((item) => item.group === group).map((field) => `
          <label class="algo-field">
            <span class="algo-label">${field.labelZh}</span>
            <input
              class="el-input"
              type="number"
              data-action="algo-input"
              data-key="${field.key}"
              min="${field.min}"
              max="${field.max}"
              step="${field.step}"
              value="${state.algoParams[field.key]}"
            />
            <small>${field.desc}</small>
          </label>
        `).join('')}
      </div>
    </div>
  `).join('')
  setHtml('algoGroupsWrap', html)
  setText('algoJsonPreview', toAlgoParamsJson(state.algoParams))
}

function render() {
  renderHeader()
  renderListTab()
  renderDetailVideoList()
  renderEventWall()
  renderPlayerAndUpload()
  renderAlgoDialog()
  setActiveTab(state.activeTab)
}

function formatAnalyzeSec(sec) {
  if (sec === null || sec === undefined) return '-'
  const value = Number(sec)
  if (!Number.isFinite(value)) return '-'
  if (value === 0) return '0s'
  const totalSec = Math.round(value)
  if (totalSec < 60) return `${totalSec}s`
  const mm = Math.floor(totalSec / 60)
  const ss = totalSec % 60
  return `${mm}m ${ss}s`
}

async function refreshDetail() {
  if (!state.currentJob?.jobNo) return
  try {
    const [jobRes, evtRes] = await Promise.all([
      videoAnalyticsApi.getJob(state.currentJob.jobNo),
      videoAnalyticsApi.getJobEvents(state.currentJob.jobNo)
    ])
    if (jobRes?.success) {
      state.currentJob = jobRes.data
      syncUploadClosed()
    }
    if (evtRes?.success) {
      state.allEvents = evtRes.data
    }
    if (!state.currentFileId) {
      const first = state.currentJob?.videos?.[0]
      if (first?.id) selectVideo(first.id, false)
    }
    if (state.listPageNo > getListPageCount()) state.listPageNo = getListPageCount()
    pruneSelection()
    render()
    preloadSnapshots(getAllFilteredEvents().slice(0, 60))
    if (![0, 1].includes(Number(state.currentJob?.status))) stopPolling()
  } catch (e) {
    console.error(e)
  }
}

function startPolling() {
  if (state.pollTimer) return
  state.pollTimer = window.setInterval(refreshDetail, 3000)
}

function stopPolling() {
  if (state.pollTimer) {
    clearInterval(state.pollTimer)
    state.pollTimer = null
  }
}

function selectVideo(fileId, preload = true) {
  state.currentFileId = Number(fileId)
  state.listSelectedId = Number(fileId)

  const v = getEnrichedVideos().find((item) => Number(item.id) === Number(fileId))
  if (!v) {
    renderPlayerAndUpload()
    return
  }
  if (preload && v.events.length) {
    const evts = v.events
      .filter((item) => toNum(item.confidence) >= state.filterConf)
      .filter((item) => state.filterType === 'ALL' ? true : state.filterType === 'Spark' ? Number(item.eventType) === 2 : Number(item.eventType) === 1)
      .sort((a, b) => Number(a.peakTimeSec) - Number(b.peakTimeSec))
      .slice(0, 30)
    preloadSnapshots(evts)
  }
  const evts = v.events
    .filter((item) => toNum(item.confidence) >= state.filterConf)
    .filter((item) => state.filterType === 'ALL' ? true : state.filterType === 'Spark' ? Number(item.eventType) === 2 : Number(item.eventType) === 1)
  if (evts.length) {
    const best = evts.reduce((prev, curr) => toNum(curr.confidence) > toNum(prev.confidence) ? curr : prev, evts[0])
    seekToEvent(best, false)
  }
  render()
}

function seekToEvent(evt, autoPlay = true) {
  const player = byId('detailVideoPlayer')
  const targetTime = Math.max(0, Number(evt.peakTimeSec) - 2)
  player.currentTime = targetTime
  state.currentTime = targetTime
  setText('playerTimeText', formatTime(state.currentTime))
  if (autoPlay) {
    player.play().catch(() => {})
    window.setTimeout(() => {
      if (!player.paused) player.pause()
    }, 4000)
  }
}

async function seekToEventFromWall(videoId, eventId) {
  const evt = state.allEvents.find((item) => Number(item.id) === Number(eventId))
  if (!evt) return
  if (Number(state.currentFileId) !== Number(videoId)) {
    selectVideo(videoId, false)
    await new Promise((resolve) => setTimeout(resolve, 50))
  }
  seekToEvent(evt, true)
}

async function preloadSnapshots(events) {
  const pending = events.filter((item) => !state.snapshotCache[item.id] && !state.snapshotLoading[item.id])
  if (!pending.length) return
  const chunkSize = 6
  for (let i = 0; i < pending.length; i += chunkSize) {
    const chunk = pending.slice(i, i + chunkSize)
    await Promise.all(chunk.map(async (evt) => {
      state.snapshotLoading[evt.id] = true
      renderEventWall()
      try {
        const res = await videoAnalyticsApi.getEventSnapshots(evt.id)
        if (res?.success && res.data.length > 0) {
          state.snapshotCache[evt.id] = videoAnalyticsApi.getSnapshotUrl(res.data[0].id)
        } else {
          state.snapshotCache[evt.id] = ''
        }
      } catch {
        state.snapshotCache[evt.id] = ''
      } finally {
        state.snapshotLoading[evt.id] = false
        renderEventWall()
      }
    }))
  }
}

async function ensureSnapshotId(eventId) {
  if (state.snapshotLoading[eventId]) return null
  state.snapshotLoading[eventId] = true
  renderEventWall()
  try {
    const res = await videoAnalyticsApi.getEventSnapshots(eventId)
    if (!res?.success || !res.data.length) {
      state.snapshotCache[eventId] = ''
      return null
    }
    const best = res.data.slice().sort((a, b) => Number(b.confidence || 0) - Number(a.confidence || 0))[0]
    const snapshotId = Number(best?.id || 0)
    if (!snapshotId) return null
    state.snapshotCache[eventId] = videoAnalyticsApi.getSnapshotUrl(snapshotId)
    return snapshotId
  } catch {
    state.snapshotCache[eventId] = ''
    return null
  } finally {
    state.snapshotLoading[eventId] = false
    renderEventWall()
  }
}

async function downloadEventSnapshot(eventId) {
  const snapshotId = await ensureSnapshotId(eventId)
  if (!snapshotId) {
    showToast('暂无截图可下载', 'warning')
    return
  }
  try {
    const { blob } = await videoAnalyticsApi.downloadSnapshot(snapshotId)
    if (!blob || blob.size <= 0) {
      showToast('截图内容为空', 'warning')
      return
    }
    downloadBlob(blob, `snapshot_${snapshotId}.jpg`)
  } catch {
    showToast('下载失败', 'error')
  }
}

async function deleteSelectedVideos() {
  if (!state.currentJob?.jobNo) return
  const ids = Array.from(state.selectedFileIds).filter((item) => Number(item) > 0).map(Number)
  if (!ids.length) return
  if (!window.confirm(`确认删除选中的 ${ids.length} 个视频吗？这会同时删除视频文件、事件和截图。`)) return
  state.deleting = true
  renderDetailVideoList()
  try {
    const res = await videoAnalyticsApi.deleteVideos(state.currentJob.jobNo, ids)
    if (!res?.success) {
      showToast(res?.message || '删除失败', 'error')
      return
    }
    const deletedCount = Number(res.data?.deletedVideoCount || 0)
    const skippedCount = Number(res.data?.skippedProcessingCount || 0)
    state.selectedFileIds = new Set(Array.from(state.selectedFileIds).filter((id) => !ids.includes(Number(id))))
    state.snapshotCache = {}
    state.snapshotLoading = {}
    await refreshDetail()
    pruneSelection()
    render()
    if (deletedCount > 0 && skippedCount > 0) {
      showToast(`已删除 ${deletedCount} 个视频，另有 ${skippedCount} 个处理中视频未删除`, 'warning')
    } else if (deletedCount > 0) {
      showToast(`已删除 ${deletedCount} 个视频`, 'success')
    } else if (skippedCount > 0) {
      showToast(`有 ${skippedCount} 个视频正在处理中，暂未删除`, 'warning')
    } else {
      showToast('没有可删除的视频', 'warning')
    }
  } catch {
    showToast('删除失败', 'error')
  } finally {
    state.deleting = false
    renderDetailVideoList()
  }
}
async function reanalyzeSelected() {
  if (!state.currentJob?.jobNo) return
  const ids = Array.from(state.selectedFileIds).filter((item) => Number(item) > 0).map(Number)
  if (!ids.length) return
  if (!window.confirm(`确认重新分析 ${ids.length} 个视频吗？`)) return
  state.reanalyzing = true
  renderDetailVideoList()
  try {
    const res = await videoAnalyticsApi.reanalyze(state.currentJob.jobNo, ids, toAlgoParamsJson(state.algoParams))
    if (!res?.success) {
      showToast(res?.message || '重新分析失败', 'error')
      return
    }
    state.selectedFileIds = new Set()
    showToast(`已重新入队 ${res.data?.requeuedCount ?? ids.length} 个视频`, 'success')
    await refreshDetail()
    startPolling()
  } catch {
    showToast('重新分析失败', 'error')
  } finally {
    state.reanalyzing = false
    renderDetailVideoList()
  }
}

async function closeUploadTask() {
  if (!state.currentJob?.jobNo) return
  if (!window.confirm('关闭上传后将不再接收新视频，是否继续？')) return
  state.closing = true
  renderPlayerAndUpload()
  try {
    const res = await videoAnalyticsApi.closeJob(state.currentJob.jobNo)
    if (!res?.success) {
      showToast(res?.message || '关闭失败', 'error')
      return
    }
    state.uploadClosed = true
    showToast('已关闭上传，后续不再接收新视频。', 'success')
    await refreshDetail()
    startPolling()
  } catch {
    showToast('关闭失败', 'error')
  } finally {
    state.closing = false
    renderPlayerAndUpload()
  }
}

async function uploadFiles(fileList) {
  if (!state.currentJob?.jobNo) return
  if (state.uploadClosed) {
    showToast('已关闭上传，不能再上传新视频', 'warning')
    return
  }
  const files = Array.from(fileList || [])
  for (const file of files) {
    const item = { name: file.name, status: 'uploading' }
    state.uploadQueue = [item, ...state.uploadQueue].slice(0, 20)
    renderPlayerAndUpload()
    try {
      const res = await videoAnalyticsApi.uploadOne(state.currentJob.jobNo, file)
      if (!res?.success) {
        item.status = 'error'
        showToast(res?.message || `上传失败：${file.name}`, 'error')
      } else {
        item.status = 'success'
        showToast(`上传成功：${file.name}`, 'success')
        await refreshDetail()
        const last = state.currentJob?.videos?.[state.currentJob.videos.length - 1]
        if (last?.id) selectVideo(last.id, false)
        startPolling()
      }
    } catch {
      item.status = 'error'
      showToast(`上传失败：${file.name}`, 'error')
    }
    renderPlayerAndUpload()
  }
}

function resetAlgoParams() {
  state.algoParams = { ...DEFAULT_ALGO_PARAMS }
  persistAlgoParams()
  renderAlgoDialog()
}

function updateAlgoField(input) {
  const key = input.getAttribute('data-key')
  if (!key || !Object.prototype.hasOwnProperty.call(state.algoParams, key)) return
  state.algoParams[key] = Number(input.value)
  state.algoParams = parseAlgoParamsJson(toAlgoParamsJson(state.algoParams))
  persistAlgoParams()
  renderAlgoDialog()
}

function handleKeyboard(e) {
  const player = byId('detailVideoPlayer')
  if (!player) return
  if (['INPUT', 'TEXTAREA'].includes(e.target?.tagName)) return
  if (e.code === 'Space') {
    e.preventDefault()
    player.paused ? player.play().catch(() => {}) : player.pause()
  }
  if (e.code === 'ArrowLeft') {
    e.preventDefault()
    player.currentTime = Math.max(0, player.currentTime + (e.shiftKey ? -5 : -1))
  }
  if (e.code === 'ArrowRight') {
    e.preventDefault()
    player.currentTime = Math.max(0, player.currentTime + (e.shiftKey ? 5 : 1))
  }
}

async function deleteCurrentJob() {
  const jobNo = state.currentJob?.jobNo
  if (!jobNo) return
  if (!window.confirm(`确认删除任务 ${jobNo} 吗？`)) return
  try {
    const res = await videoAnalyticsApi.deleteJob(jobNo)
    if (!res?.success) {
      showToast(res?.message || '删除失败', 'error')
      return
    }
    removeJobHistory(jobNo)
    showToast('删除成功', 'success')
    window.location.href = './VideoAnalytics.html'
  } catch {
    showToast('删除失败', 'error')
  }
}

function bindEvents() {
  bindDialog('helpDialog')
  bindDialog('algoDialog')

  byId('btnBackList').addEventListener('click', () => {
    window.location.href = './VideoAnalytics.html'
  })
  byId('tabListBtn').addEventListener('click', () => setActiveTab('list'))
  byId('tabDetailBtn').addEventListener('click', () => setActiveTab('detail'))
  byId('btnOpenHelpDialog').addEventListener('click', () => openDialog('helpDialog'))
  byId('btnOpenAlgoDialog').addEventListener('click', () => {
    renderAlgoDialog()
    openDialog('algoDialog')
  })
  byId('btnResetAlgoParams').addEventListener('click', resetAlgoParams)
  byId('btnReanalyzeSelected').addEventListener('click', reanalyzeSelected)
  byId('btnDeleteSelected').addEventListener('click', deleteSelectedVideos)
  byId('btnCloseUpload').addEventListener('click', closeUploadTask)

  byId('onlyAbnormalToggle').addEventListener('change', (e) => {
    state.onlyAbnormal = !!e.target.checked
    state.listPageNo = 1
    state.listSelectedId = null
    renderListTab()
  })
  byId('btnListPrev').addEventListener('click', () => {
    if (state.listPageNo > 1) state.listPageNo--
    state.listSelectedId = getListPageVideos()[0]?.id ?? null
    renderListTab()
  })
  byId('btnListNext').addEventListener('click', () => {
    if (state.listPageNo < getListPageCount()) state.listPageNo++
    state.listSelectedId = getListPageVideos()[0]?.id ?? null
    renderListTab()
  })
  document.querySelectorAll('[data-grid]').forEach((node) => {
    node.addEventListener('click', () => {
      state.gridMode = Number(node.getAttribute('data-grid'))
      state.listPageNo = 1
      state.listSelectedId = null
      renderListTab()
    })
  })

  byId('filterConfRange').addEventListener('input', (e) => {
    state.filterConf = Number(e.target.value)
    setText('filterConfText', `${Math.round(state.filterConf * 100)}%`)
    render()
    preloadSnapshots(getAllFilteredEvents().slice(0, 60))
  })
  byId('filterHasEventsToggle').addEventListener('change', (e) => {
    state.filterHasEvents = !!e.target.checked
    render()
  })
  document.querySelectorAll('input[name="filterType"]').forEach((node) => {
    node.addEventListener('change', (e) => {
      state.filterType = e.target.value
      render()
      preloadSnapshots(getAllFilteredEvents().slice(0, 60))
    })
  })

  byId('selectAllVideosToggle').addEventListener('change', (e) => {
    const ids = getFilteredVideos().map((item) => Number(item.id))
    if (e.target.checked) {
      ids.forEach((id) => state.selectedFileIds.add(id))
    } else {
      ids.forEach((id) => state.selectedFileIds.delete(id))
    }
    renderDetailVideoList()
  })

  byId('detailVideoPlayer').addEventListener('timeupdate', (e) => {
    state.currentTime = e.target.currentTime
    setText('playerTimeText', formatTime(state.currentTime))
  })
  byId('btnSeekMinus5').addEventListener('click', () => {
    const player = byId('detailVideoPlayer')
    player.currentTime = Math.max(0, player.currentTime - 5)
  })
  byId('btnSeekPlus5').addEventListener('click', () => {
    const player = byId('detailVideoPlayer')
    player.currentTime = Math.max(0, player.currentTime + 5)
  })

  byId('btnOpenFilePicker').addEventListener('click', () => {
    if (!state.uploadClosed) byId('uploadInput').click()
  })
  byId('uploadInput').addEventListener('change', async (e) => {
    await uploadFiles(e.target.files)
    e.target.value = ''
  })

  const dropzone = byId('uploadDropzone')
  ;['dragenter', 'dragover'].forEach((name) => {
    dropzone.addEventListener(name, (e) => {
      e.preventDefault()
      if (!state.uploadClosed) dropzone.classList.add('is-dragover')
    })
  })
  ;['dragleave', 'drop'].forEach((name) => {
    dropzone.addEventListener(name, (e) => {
      e.preventDefault()
      dropzone.classList.remove('is-dragover')
    })
  })
  dropzone.addEventListener('click', () => {
    if (!state.uploadClosed) byId('uploadInput').click()
  })
  dropzone.addEventListener('drop', async (e) => {
    await uploadFiles(e.dataTransfer?.files || [])
  })

  document.addEventListener('click', async (e) => {
    const target = e.target
    if (!(target instanceof HTMLElement)) return
    const actionEl = target.closest('[data-action]')
    if (!actionEl) return
    const action = actionEl.getAttribute('data-action')
    const fileId = Number(actionEl.getAttribute('data-file-id') || 0)
    const videoId = Number(actionEl.getAttribute('data-video-id') || 0)
    const eventId = Number(actionEl.getAttribute('data-event-id') || 0)

    if (action === 'select-list-row' || action === 'select-grid-video') {
      state.listSelectedId = fileId
      renderListTab()
    }
    if (action === 'open-detail') {
      setActiveTab('detail')
      selectVideo(fileId)
    }
    if (action === 'select-detail-video') {
      selectVideo(fileId)
    }
    if (action === 'toggle-select-video') {
      e.stopPropagation()
      if (target instanceof HTMLInputElement) {
        if (target.checked) state.selectedFileIds.add(fileId)
        else state.selectedFileIds.delete(fileId)
        renderDetailVideoList()
      }
    }
    if (action === 'select-group-video') {
      selectVideo(videoId)
    }
    if (action === 'seek-event') {
      await seekToEventFromWall(videoId, eventId)
    }
    if (action === 'download-snapshot') {
      e.stopPropagation()
      await downloadEventSnapshot(eventId)
    }
  })

  byId('algoGroupsWrap').addEventListener('input', (e) => {
    const target = e.target
    if (target instanceof HTMLInputElement && target.getAttribute('data-action') === 'algo-input') {
      updateAlgoField(target)
    }
  })

  window.addEventListener('keydown', handleKeyboard)
  window.addEventListener('beforeunload', stopPolling)
}

async function init() {
  bindEvents()
  const jobNo = qs('jobNo')
  if (!jobNo) {
    showToast('缺少 jobNo 参数，正在返回任务列表', 'warning')
    window.setTimeout(() => {
      window.location.href = './VideoAnalytics.html'
    }, 1200)
    return
  }
  try {
    const res = await videoAnalyticsApi.getJob(jobNo)
    if (!res?.success || !res.data) {
      showToast('任务加载失败', 'error')
      return
    }
    state.currentJob = res.data
    pushJobHistory(jobNo)
    syncUploadClosed()
    render()
    await refreshDetail()
    if ([0, 1].includes(Number(state.currentJob?.status))) startPolling()
  } catch {
    showToast('任务加载失败', 'error')
  }

  document.title = `${state.currentJob?.jobNo || '工作台'} - 视频分析工作台`
  byId('btnDeleteCurrentJob')?.addEventListener('click', deleteCurrentJob)
}

init()




