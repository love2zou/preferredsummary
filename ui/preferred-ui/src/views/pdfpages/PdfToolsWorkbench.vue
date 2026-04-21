<template>
  <div class="pdf-workbench-page">
    <header class="workbench-header">
      <el-button link type="primary" @click="goIntro">
        <el-icon><ArrowLeft /></el-icon>
        返回介绍页
      </el-button>
      <div>
        <h1>PDF 工具台</h1>
        <p>本地完成 PDF 拆分和合并，处理完成后直接下载。</p>
      </div>
    </header>

    <main class="workbench-body">
      <el-tabs v-model="activeTab" class="pdf-tabs">
        <el-tab-pane label="PDF 拆分" name="split">
          <section class="panel">
            <div class="panel-title">
              <h2>上传 PDF</h2>
              <span v-if="splitPageCount">共 {{ splitPageCount }} 页</span>
            </div>
            <el-upload
              class="upload-zone"
              drag
              accept="application/pdf,.pdf"
              :auto-upload="false"
              :show-file-list="false"
              :on-change="handleSplitFileChange"
            >
              <el-icon class="upload-icon"><UploadFilled /></el-icon>
              <div class="upload-text">拖拽 PDF 到这里，或点击选择文件</div>
            </el-upload>
            <div v-if="splitFileName" class="file-chip">
              <span>{{ splitFileName }}</span>
              <el-button link type="danger" @click="clearSplit">移除</el-button>
            </div>
          </section>

          <section class="panel">
            <h2>拆分方式</h2>
            <el-radio-group v-model="splitMode" class="mode-group">
              <el-radio label="range">按页码范围拆分</el-radio>
              <el-radio label="single">每一页拆成单独 PDF</el-radio>
              <el-radio label="chunk">每 N 页拆分</el-radio>
            </el-radio-group>

            <el-alert v-if="splitMode === 'range'" class="usage-alert" type="info" :closable="false" show-icon>
              <template #title>页码从 1 开始。输入 1-42 会生成一个包含第 1 到 42 页的 PDF；输入 1-3,8,10-12 会按每一段分别生成多个 PDF。</template>
            </el-alert>

            <div v-if="splitMode === 'range'" class="setting-row">
              <label>页码范围</label>
              <div class="input-with-help">
                <el-input v-model="pageRanges" placeholder="例如：1-3, 8, 10-12" />
                <p>多个页码段用英文逗号分隔，每一段会拆出一个独立 PDF。页码不能超过当前 PDF 总页数。</p>
              </div>
            </div>

            <div v-if="splitMode === 'chunk'" class="setting-row compact">
              <label>每组页数</label>
              <div class="input-with-help">
                <el-input-number v-model="chunkSize" :min="1" :max="999" />
                <p>例如输入 5，会生成第 1-5 页、第 6-10 页这样的多个 PDF。</p>
              </div>
            </div>
          </section>

          <section class="actions-bar">
            <el-button @click="clearSplit">清空</el-button>
            <el-button type="primary" :loading="processing" :disabled="!splitFile" @click="splitPdf">
              生成拆分 PDF
            </el-button>
          </section>
        </el-tab-pane>

        <el-tab-pane label="PDF 合并" name="merge">
          <section class="panel">
            <h2>上传 PDF</h2>
            <el-alert class="usage-alert" type="info" :closable="false" show-icon>
              <template #title>选择两个或更多 PDF，按列表顺序合并。可以用上移、下移调整顺序，再点击生成合并 PDF。</template>
            </el-alert>
            <el-upload
              class="upload-zone"
              drag
              multiple
              accept="application/pdf,.pdf"
              :auto-upload="false"
              :show-file-list="false"
              :on-change="handleMergeFileChange"
            >
              <el-icon class="upload-icon"><UploadFilled /></el-icon>
              <div class="upload-text">拖拽多个 PDF 到这里，或点击选择文件</div>
            </el-upload>
          </section>

          <section class="panel">
            <div class="panel-title">
              <h2>合并顺序</h2>
              <span>{{ mergeFiles.length }} 个文件</span>
            </div>
            <el-empty v-if="!mergeFiles.length" description="还没有选择 PDF 文件" />
            <div v-else class="merge-list">
              <div v-for="(item, index) in mergeFiles" :key="item.id" class="merge-item">
                <div class="merge-index">{{ index + 1 }}</div>
                <div class="merge-info">
                  <strong>{{ item.name }}</strong>
                  <span>{{ formatSize(item.size) }}</span>
                </div>
                <div class="merge-actions">
                  <el-button size="small" :disabled="index === 0" @click="moveMergeFile(index, -1)">上移</el-button>
                  <el-button size="small" :disabled="index === mergeFiles.length - 1" @click="moveMergeFile(index, 1)">下移</el-button>
                  <el-button size="small" type="danger" plain @click="removeMergeFile(index)">删除</el-button>
                </div>
              </div>
            </div>
          </section>

          <section class="panel">
            <div class="setting-row">
              <label>输出文件名</label>
              <el-input v-model="mergeOutputName" placeholder="merged.pdf" />
            </div>
          </section>

          <section class="actions-bar">
            <el-button @click="clearMerge">清空</el-button>
            <el-button type="primary" :loading="processing" :disabled="mergeFiles.length < 2" @click="mergePdfs">
              生成合并 PDF
            </el-button>
          </section>
        </el-tab-pane>
      </el-tabs>
    </main>
  </div>
</template>

<script setup lang="ts">
import { ArrowLeft, UploadFilled } from '@element-plus/icons-vue'
import type { UploadFile } from 'element-plus'
import { ElMessage } from 'element-plus'
import { PDFDocument } from 'pdf-lib'
import { ref } from 'vue'
import { useRouter } from 'vue-router'

type MergeFile = {
  id: string
  name: string
  size: number
  file: File
}

type PageRange = {
  start: number
  end: number
}

const router = useRouter()
const activeTab = ref<'split' | 'merge'>('split')
const processing = ref(false)

const splitFile = ref<File | null>(null)
const splitFileName = ref('')
const splitPageCount = ref(0)
const splitMode = ref<'range' | 'single' | 'chunk'>('range')
const pageRanges = ref('1')
const chunkSize = ref(5)

const mergeFiles = ref<MergeFile[]>([])
const mergeOutputName = ref('merged.pdf')
let mergeFileIdSeed = 0

const goIntro = () => {
  void router.push('/pdf-tools')
}

const getRawFile = (uploadFile: UploadFile): File | null => {
  return uploadFile.raw instanceof File ? uploadFile.raw : null
}

const ensurePdf = (file: File) => {
  return file.type === 'application/pdf' || file.name.toLowerCase().endsWith('.pdf')
}

const createMergeFileId = (file: File) => {
  const randomId = globalThis.crypto?.randomUUID?.() ??
    `${Date.now()}-${mergeFileIdSeed += 1}-${Math.random().toString(36).slice(2)}`
  return `${file.name}-${file.size}-${file.lastModified}-${randomId}`
}

const handleSplitFileChange = async (uploadFile: UploadFile) => {
  const file = getRawFile(uploadFile)
  if (!file) return
  if (!ensurePdf(file)) {
    ElMessage.warning('请选择 PDF 文件')
    return
  }

  try {
    const pdf = await PDFDocument.load(await file.arrayBuffer(), { ignoreEncryption: true })
    splitFile.value = file
    splitFileName.value = file.name
    splitPageCount.value = pdf.getPageCount()
    pageRanges.value = splitPageCount.value ? `1-${splitPageCount.value}` : '1'
  } catch {
    ElMessage.error('PDF 读取失败，请确认文件未加密且格式正确')
  }
}

const handleMergeFileChange = (uploadFile: UploadFile) => {
  const file = getRawFile(uploadFile)
  if (!file) return
  if (!ensurePdf(file)) {
    ElMessage.warning('已跳过非 PDF 文件')
    return
  }

  mergeFiles.value.push({
    id: createMergeFileId(file),
    name: file.name,
    size: file.size,
    file
  })
}

const clearSplit = () => {
  splitFile.value = null
  splitFileName.value = ''
  splitPageCount.value = 0
  pageRanges.value = '1'
}

const clearMerge = () => {
  mergeFiles.value = []
  mergeOutputName.value = 'merged.pdf'
}

const moveMergeFile = (index: number, direction: -1 | 1) => {
  const target = index + direction
  if (target < 0 || target >= mergeFiles.value.length) return
  const next = [...mergeFiles.value]
  const current = next[index]
  next[index] = next[target]
  next[target] = current
  mergeFiles.value = next
}

const removeMergeFile = (index: number) => {
  mergeFiles.value.splice(index, 1)
}

const parsePageRanges = (value: string, total: number): PageRange[] => {
  const ranges: PageRange[] = []
  const parts = value.split(',').map(part => part.trim()).filter(Boolean)

  for (const part of parts) {
    const match = part.match(/^(\d+)(?:\s*-\s*(\d+))?$/)
    if (!match) {
      throw new Error(`页码格式不正确：${part}`)
    }

    const start = Number(match[1])
    const end = Number(match[2] || match[1])
    if (start < 1 || end < start || end > total) {
      throw new Error(`页码超出范围：${part}`)
    }
    ranges.push({ start, end })
  }

  if (!ranges.length) {
    throw new Error('请输入页码范围')
  }
  return ranges
}

const splitPdf = async () => {
  if (!splitFile.value) return

  processing.value = true
  try {
    const sourcePdf = await PDFDocument.load(await splitFile.value.arrayBuffer(), { ignoreEncryption: true })
    const total = sourcePdf.getPageCount()

    if (splitMode.value === 'range') {
      const ranges = parsePageRanges(pageRanges.value, total)
      for (const range of ranges) {
        const result = await PDFDocument.create()
        const pageIndexes = Array.from(
          { length: range.end - range.start + 1 },
          (_, index) => range.start - 1 + index
        )
        const pages = await result.copyPages(sourcePdf, pageIndexes)
        pages.forEach(page => result.addPage(page))
        await downloadPdf(result, buildFileName(splitFile.value.name, `pages-${range.start}-${range.end}`))
      }
      ElMessage.success('拆分 PDF 已生成')
      return
    }

    if (splitMode.value === 'single') {
      for (let index = 0; index < total; index += 1) {
        const result = await PDFDocument.create()
        const [page] = await result.copyPages(sourcePdf, [index])
        result.addPage(page)
        await downloadPdf(result, buildFileName(splitFile.value.name, `page-${index + 1}`))
      }
      ElMessage.success('单页 PDF 已生成')
      return
    }

    for (let start = 0; start < total; start += chunkSize.value) {
      const end = Math.min(start + chunkSize.value, total)
      const result = await PDFDocument.create()
      const pageIndexes = Array.from({ length: end - start }, (_, index) => start + index)
      const pages = await result.copyPages(sourcePdf, pageIndexes)
      pages.forEach(page => result.addPage(page))
      await downloadPdf(result, buildFileName(splitFile.value.name, `pages-${start + 1}-${end}`))
    }
    ElMessage.success('分组 PDF 已生成')
  } catch (error) {
    ElMessage.error(error instanceof Error ? error.message : 'PDF 拆分失败')
  } finally {
    processing.value = false
  }
}

const mergePdfs = async () => {
  if (mergeFiles.value.length < 2) return

  processing.value = true
  try {
    const result = await PDFDocument.create()
    for (const item of mergeFiles.value) {
      const sourcePdf = await PDFDocument.load(await item.file.arrayBuffer(), { ignoreEncryption: true })
      const pages = await result.copyPages(sourcePdf, sourcePdf.getPageIndices())
      pages.forEach(page => result.addPage(page))
    }
    await downloadPdf(result, normalizePdfName(mergeOutputName.value))
    ElMessage.success('合并 PDF 已生成')
  } catch {
    ElMessage.error('PDF 合并失败，请确认文件未加密且格式正确')
  } finally {
    processing.value = false
  }
}

const downloadPdf = async (pdf: PDFDocument, fileName: string) => {
  const bytes = await pdf.save()
  const blob = new Blob([bytes], { type: 'application/pdf' })
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = fileName
  link.click()
  URL.revokeObjectURL(url)
}

const buildFileName = (sourceName: string, suffix: string) => {
  const baseName = sourceName.replace(/\.pdf$/i, '')
  return `${baseName}-${suffix}.pdf`
}

const normalizePdfName = (name: string) => {
  const trimmed = name.trim() || 'merged.pdf'
  return trimmed.toLowerCase().endsWith('.pdf') ? trimmed : `${trimmed}.pdf`
}

const formatSize = (size: number) => {
  if (size < 1024 * 1024) return `${(size / 1024).toFixed(1)} KB`
  return `${(size / 1024 / 1024).toFixed(2)} MB`
}
</script>

<style scoped>
.pdf-workbench-page {
  min-height: 100vh;
  color: #172033;
  background: #f5f7fb;
}

.workbench-header {
  display: flex;
  gap: 22px;
  align-items: flex-start;
  padding: 28px min(6vw, 72px);
  background: #ffffff;
  border-bottom: 1px solid #e5e9f2;
}

.workbench-header h1 {
  margin: 0;
  font-size: 30px;
  letter-spacing: 0;
}

.workbench-header p {
  margin: 8px 0 0;
  color: #5c667a;
}

.workbench-body {
  max-width: 1100px;
  margin: 0 auto;
  padding: 28px 24px 52px;
}

.pdf-tabs {
  padding: 18px 22px 22px;
  background: #ffffff;
  border: 1px solid #e5e9f2;
  border-radius: 8px;
}

.panel {
  margin-top: 18px;
  padding: 22px;
  background: #ffffff;
  border: 1px solid #e5e9f2;
  border-radius: 8px;
}

.panel-title {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
}

.panel-title span {
  color: #5c667a;
  font-size: 14px;
}

.panel h2 {
  margin: 0 0 16px;
  font-size: 20px;
  letter-spacing: 0;
}

.upload-zone {
  width: 100%;
}

.upload-icon {
  color: #1677ff;
  font-size: 42px;
}

.upload-text {
  color: #43516a;
}

.file-chip {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 14px;
  margin-top: 14px;
  padding: 10px 12px;
  background: #f0f4fa;
  border-radius: 8px;
}

.mode-group {
  display: flex;
  flex-wrap: wrap;
  gap: 8px 18px;
  margin-top: 16px;
}

.usage-alert {
  margin-bottom: 16px;
}

.setting-row {
  display: grid;
  grid-template-columns: 110px minmax(0, 1fr);
  gap: 14px;
  align-items: center;
  margin-top: 18px;
}

.setting-row.compact {
  grid-template-columns: 110px 160px;
}

.setting-row label {
  color: #43516a;
  font-weight: 600;
}

.input-with-help {
  display: grid;
  gap: 8px;
}

.input-with-help p {
  margin: 0;
  color: #6f7a8f;
  font-size: 13px;
  line-height: 1.6;
}

.actions-bar {
  display: flex;
  justify-content: flex-end;
  gap: 12px;
  margin-top: 18px;
}

.merge-list {
  display: grid;
  gap: 10px;
}

.merge-item {
  display: grid;
  grid-template-columns: 40px minmax(0, 1fr) auto;
  gap: 12px;
  align-items: center;
  padding: 12px;
  background: #f7f9fc;
  border: 1px solid #e5e9f2;
  border-radius: 8px;
}

.merge-index {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 32px;
  height: 32px;
  color: #ffffff;
  background: #1677ff;
  border-radius: 8px;
  font-weight: 700;
}

.merge-info {
  display: flex;
  flex-direction: column;
  gap: 4px;
  min-width: 0;
}

.merge-info strong {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.merge-info span {
  color: #6f7a8f;
  font-size: 13px;
}

.merge-actions {
  display: flex;
  gap: 8px;
}

@media (max-width: 760px) {
  .workbench-header {
    flex-direction: column;
    padding: 22px 18px;
  }

  .workbench-body {
    padding: 18px 12px 36px;
  }

  .setting-row,
  .setting-row.compact {
    grid-template-columns: 1fr;
  }

  .merge-item {
    grid-template-columns: 34px minmax(0, 1fr);
  }

  .merge-actions {
    grid-column: 1 / -1;
    flex-wrap: wrap;
  }
}
</style>
