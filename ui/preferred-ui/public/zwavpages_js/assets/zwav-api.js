 import { apiJson, apiBlob, buildUrl, getApiBaseUrl, getAuthHeaders } from './common.js'
 
 export const zwavApi = {
   uploadFile(file, onProgress) {
     const baseUrl = getApiBaseUrl()
     const url = buildUrl(baseUrl, '/api/ZwavAnalyses/upload', {})
     return new Promise((resolve, reject) => {
       const xhr = new XMLHttpRequest()
       xhr.open('POST', url, true)
       const headers = getAuthHeaders()
       Object.keys(headers).forEach((k) => xhr.setRequestHeader(k, headers[k]))
 
       xhr.upload.onprogress = (evt) => {
         if (!evt.lengthComputable) return
         const pct = Math.floor((evt.loaded * 100) / evt.total)
         if (typeof onProgress === 'function') onProgress(pct)
       }
 
       xhr.onload = () => {
         try {
           const text = xhr.responseText || ''
           const json = text ? JSON.parse(text) : null
           if (xhr.status >= 200 && xhr.status < 300) resolve(json)
           else reject(new Error((json && json.message) || `HTTP ${xhr.status}`))
         } catch (e) {
           reject(e)
         }
       }
       xhr.onerror = () => reject(new Error('Network Error'))
 
       const fd = new FormData()
       fd.append('file', file)
       xhr.send(fd)
     })
   },
 
   createAnalysis(fileId, forceRecreate) {
     return apiJson('/api/ZwavAnalyses/create', { method: 'POST', body: { fileId, forceRecreate: !!forceRecreate } })
   },
 
   getList(params) {
     return apiJson('/api/ZwavAnalyses', { params })
   },
 
   deleteAnalysis(analysisGuid, deleteFile) {
     return apiJson(`/api/ZwavAnalyses/${encodeURIComponent(analysisGuid)}`, {
       method: 'DELETE',
       params: { deleteFile: deleteFile ? 'true' : 'false' }
     })
   },
 
   downloadFile(analysisGuid) {
     return apiBlob(`/api/ZwavAnalyses/${encodeURIComponent(analysisGuid)}/download`)
   },
 
   exportWaveData(analysisGuid, enabledOnly) {
     return apiBlob(`/api/ZwavAnalyses/${encodeURIComponent(analysisGuid)}/export`, {
       params: { enabledOnly: enabledOnly ? 'true' : 'false' }
     })
   },
 
   getDetail(analysisGuid) {
     return apiJson(`/api/ZwavAnalyses/${encodeURIComponent(analysisGuid)}/detail`)
   },
 
   getCfg(analysisGuid, includeText) {
     return apiJson(`/api/ZwavAnalyses/${encodeURIComponent(analysisGuid)}/cfg`, { params: { includeText: includeText ? 'true' : 'false' } })
   },
 
   getHdr(analysisGuid) {
     return apiJson(`/api/ZwavAnalyses/${encodeURIComponent(analysisGuid)}/hdr`)
   },
 
   getChannels(analysisGuid, type, enabledOnly) {
     return apiJson(`/api/ZwavAnalyses/${encodeURIComponent(analysisGuid)}/channels`, {
       params: { type: type || 'All', enabledOnly: enabledOnly ? 'true' : 'false' }
     })
   },
 
   getWaveData(analysisGuid, params) {
     return apiJson(`/api/ZwavAnalyses/${encodeURIComponent(analysisGuid)}/get-wavedata`, { params })
   }
 }
 
