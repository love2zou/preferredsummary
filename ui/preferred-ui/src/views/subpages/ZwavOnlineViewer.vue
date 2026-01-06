<template>
  <div class="viewer-container" v-loading="loading">
    <div class="header">
      <div class="title">
        <h2>录波在线浏览</h2>
        <span class="file-name" v-if="detailData?.file">{{ detailData.file.originalName }}</span>
        <span class="sub-title" v-if="cfgData">({{ cfgData.stationName }} - {{ cfgData.deviceId }})</span>
      </div>
    </div>

    <div class="content">
      <!-- 左侧波形显示 -->
      <div class="left-panel">
        <!-- 顶部工具栏 -->
        <div class="chart-controls">
          <el-button type="primary" @click="fetchWaveData">加载/刷新波形</el-button>
          <span class="tip">提示：数据量大时请减少通道数</span>
          <div style="flex: 1"></div>

          <el-tooltip content="导出CSV数据" placement="top">
            <el-icon class="stats-icon" @click="handleExport">
              <Download />
            </el-icon>
          </el-tooltip>

          <el-tooltip content="坐标轴高度设置" placement="top">
            <el-icon class="stats-icon" @click="openAxisSettings">
              <Setting />
            </el-icon>
          </el-tooltip>

          <el-tooltip content="通道极值" placement="top">
            <el-icon
              class="stats-icon"
              :class="{ disabled: channelStats.length === 0 }"
              @click="channelStats.length > 0 && (showStatsDialog = true)"
            >
              <Histogram />
            </el-icon>
          </el-tooltip>

          <!-- ✅ 稳定版：按钮触发，必然响应点击 -->
        <el-tooltip content="故障时序图" placement="top" :disabled="!canOpenSequence">
          <el-button
            link
            class="icon-btn"
            :disabled="!canOpenSequence"
            @click.stop="openSequenceDialog"
            title="故障时序图"
          >
            <el-icon><Timer /></el-icon>
          </el-button>
        </el-tooltip>

          <el-tooltip :content="isFullScreen ? '退出全屏' : '全屏显示'" placement="top">
            <el-icon class="stats-icon" @click="toggleFullScreen">
              <FullScreen />
            </el-icon>
          </el-tooltip>
        </div>

        <div class="main-body">
          <!-- 通道选择侧边栏 -->
          <div class="channel-sidebar">
            <div class="sidebar-header">
              <el-input
                v-model="channelSearchKeyword"
                placeholder="通道名称"
                clearable
                size="small"
                prefix-icon="Search"
              />
            </div>

            <div class="channel-section">
              <div class="section-title">模拟通道信号</div>
              <div class="channel-group">
                <div class="group-header group-header-row">
                  <el-checkbox
                    v-model="checkAllAnalog"
                    :indeterminate="isIndeterminateAnalog"
                    @change="handleCheckAllAnalogChange"
                  >
                    全选
                  </el-checkbox>
                  <span class="count">已选{{ selectedChannels.length }}个</span>
                </div>
                <el-scrollbar>
                  <div class="channel-list">
                    <el-checkbox-group v-model="selectedChannels" @change="handleAnalogChange">
                      <div v-for="ch in filteredAnalogChannels" :key="ch.channelIndex" class="channel-item">
                        <el-checkbox :label="ch.channelIndex">
                          <span :title="`${ch.channelIndex}. ${ch.channelName}`">
                            {{ ch.channelIndex }}.{{ ch.channelName }}
                          </span>
                        </el-checkbox>
                      </div>
                    </el-checkbox-group>
                  </div>
                </el-scrollbar>
              </div>
            </div>

            <div class="channel-section">
              <div class="section-title">数字通道信号</div>
              <div class="channel-group">
                <div class="group-header group-header-row">
                  <div class="filter-options">
                    <el-checkbox
                      v-model="checkHasAction"
                      :indeterminate="isIndeterminateHasAction"
                      @change="handleHasActionChange"
                      size="small"
                    >
                      有动作
                    </el-checkbox>
                    <el-checkbox
                      v-model="checkNoAction"
                      :indeterminate="isIndeterminateNoAction"
                      @change="handleNoActionChange"
                      size="small"
                    >
                      无动作
                    </el-checkbox>
                  </div>
                  <div class="selection-count">
                    <span class="count">已选{{ selectedDigitalChannels.length }}个</span>
                  </div>
                </div>
                <el-scrollbar>
                  <div class="channel-list">
                    <el-checkbox-group v-model="selectedDigitalChannels" @change="handleDigitalChange">
                      <div v-for="ch in filteredDigitalChannels" :key="ch.channelIndex" class="channel-item">
                        <el-checkbox :label="ch.channelIndex">
                          <span :title="`${ch.channelIndex}. ${ch.channelName}`">
                            {{ ch.channelIndex }}.{{ ch.channelName }}
                          </span>
                        </el-checkbox>
                      </div>
                    </el-checkbox-group>
                  </div>
                </el-scrollbar>
              </div>
            </div>
          </div>

          <!-- 图表区域 -->
          <div class="chart-wrapper">
            <div class="chart-scroll-container">
              <div ref="chartRef" class="chart-content"></div>
            </div>
          </div>
        </div>

        <!-- 综合设置浮窗 -->
        <el-dialog
          v-model="showAxisSettingDialog"
          title="设置"
          width="500px"
          draggable
          :close-on-click-modal="false"
        >
          <el-tabs>
            <el-tab-pane label="视图设置">
              <div style="margin-bottom: 20px;">
                <div style="margin-bottom: 10px; color: #666; font-size: 12px;">
                  统一设置每个通道波形的绘图高度（像素），默认 50px。
                </div>
                <div style="display: flex; align-items: center; gap: 10px;">
                  <span>单个波形高度:</span>
                  <el-input-number v-model="gridHeightSetting" :min="20" :max="300" :step="5" controls-position="right" />
                  <span>px</span>
                </div>
              </div>
            </el-tab-pane>

            <el-tab-pane label="参数设置">
              <el-form label-width="120px" size="small">
                <el-form-item label="起始采样点">
                  <el-input-number v-model="searchFromSample" :min="0" controls-position="right" style="width: 100%" />
                  <div style="font-size: 12px; color: #999;">fromSample: 起始采样点编号 (默认0)</div>
                </el-form-item>

                <el-form-item label="结束采样点">
                  <el-input-number v-model="searchToSample" :min="0" controls-position="right" style="width: 100%" />
                  <div style="font-size: 12px; color: #999;">toSample: 结束采样点编号 (默认10000)</div>
                </el-form-item>

                <el-form-item label="限制点数">
                  <el-input-number v-model="searchLimit" :min="100" :step="1000" controls-position="right" style="width: 100%" />
                  <div style="font-size: 12px; color: #999;">limit: 限制返回点数，避免浏览器卡死 (默认50000)</div>
                </el-form-item>

                <el-form-item label="抽样倍率">
                  <el-input-number v-model="searchDownSample" :min="1" controls-position="right" style="width: 100%" />
                  <div style="font-size: 12px; color: #999;">downSample: 抽样间隔，1为不抽样</div>
                </el-form-item>
              </el-form>
            </el-tab-pane>
          </el-tabs>

          <template #footer>
            <span class="dialog-footer">
              <el-button @click="showAxisSettingDialog = false">取消</el-button>
              <el-button type="primary" @click="applyAxisSettings">应用</el-button>
            </span>
          </template>
        </el-dialog>

        <!-- 通道极值浮窗 -->
        <el-dialog
          v-model="showStatsDialog"
          title="通道极值"
          width="500px"
          draggable
          :modal="false"
          :close-on-click-modal="false"
          :modal-class="'non-modal-dialog'"
          class="stats-dialog"
        >
          <el-table :data="channelStats" border stripe size="small" max-height="400">
            <el-table-column type="index" label="序号" width="60" align="center" />
            <el-table-column prop="name" label="通道名称" min-width="150" show-overflow-tooltip />
            <el-table-column prop="max" label="最大值" width="120" align="left">
              <template #default="{ row }">
                <span style="color: #f56c6c">{{ row.max.toFixed(3) }}</span>
              </template>
            </el-table-column>
            <el-table-column prop="min" label="最小值" width="120" align="left">
              <template #default="{ row }">
                <span style="color: #409eff">{{ row.min.toFixed(3) }}</span>
              </template>
            </el-table-column>
          </el-table>
        </el-dialog>

        <!-- 故障时序图浮窗 -->
        <el-dialog
          v-model="showSequenceDialog"
          title="故障时序图"
          width="1400px"
          draggable
          append-to-body
          :close-on-click-modal="false"
          class="sequence-dialog"
          @opened="renderSequenceDiagram"
        >
          <div class="seq-layout">
            <!-- 左侧树形结构 -->
            <div class="seq-sidebar">
              <div class="sidebar-actions">
                <el-button type="primary" size="small" :icon="Plus" @click="openFileSelectDialog">
                  添加对比
                </el-button>
              </div>
              <el-tree
                ref="seqTreeRef"
                :data="seqTreeData"
                :props="{ label: 'label', children: 'children' }"
                show-checkbox
                check-strictly
                check-on-click-node
                node-key="id"
                default-expand-all
                highlight-current
                :default-checked-keys="defaultCheckedKeys"
                @check="handleTreeCheck"
              >
                <template #default="{ node, data }">
                  <span class="custom-tree-node" :title="node.label">
                    {{ node.label }}
                  </span>
                </template>
              </el-tree>
            </div>

            <!-- 右侧时序图 -->
            <div class="seq-content">
              <div class="seq-container" ref="seqContainerRef">
                  <div class="left-info" :style="{ top: leftInfoTop + 'px' }">
                    <div class="fault-time">{{ currentMainFaultTime }}</div>
                    <div class="sub-info">故障发生时间</div>
                  </div>
                  <svg ref="seqSvgRef" class="seq-svg"></svg>
              </div>
            </div>
          </div>
        </el-dialog>

        <!-- 文件选择弹窗 -->
        <el-dialog
          v-model="fileSelectDialogVisible"
          title="选择录波文件"
          width="800px"
          append-to-body
          :close-on-click-modal="false"
        >
          <div class="file-select-header">
            <el-input
              v-model="fileSearchKeyword"
              placeholder="文件名"
              style="width: 200px"
              clearable
              @keyup.enter="fetchFileList"
            />
            <el-button type="primary" :icon="Search" @click="fetchFileList">查询</el-button>
          </div>
          
          <el-table
            v-loading="fileLoading"
            :data="fileTableData"
            border
            stripe
            height="400"
            @row-click="handleFileSelect"
            class="file-table"
          >
            <el-table-column prop="originalName" label="文件名" min-width="200" show-overflow-tooltip />
            <el-table-column prop="crtTime" label="创建时间" width="160" />
            <el-table-column label="操作" width="80">
              <template #default>
                <el-icon><Plus /></el-icon>
              </template>
            </el-table-column>
          </el-table>

          <div class="pagination-container">
            <el-pagination
              v-model:current-page="filePagination.page"
              v-model:page-size="filePagination.pageSize"
              :total="filePagination.total"
              layout="total, prev, pager, next"
              @current-change="fetchFileList"
            />
          </div>
        </el-dialog>
      </div>

      <!-- 右侧信息显示 -->
      <div class="right-panel">
        <el-tabs type="border-card" class="info-tabs">
          <el-tab-pane label="CFG 信息">
            <el-scrollbar height="calc(100vh - 180px)">
              <div v-if="cfgData" class="info-list">
                <div class="info-item"><label>厂站名:</label> <span>{{ cfgData.stationName }}</span></div>
                <div class="info-item"><label>设备ID:</label> <span>{{ cfgData.deviceId }}</span></div>
                <div class="info-item"><label>版本:</label> <span>{{ cfgData.revision }}</span></div>
                <div class="info-item"><label>模拟量数:</label> <span>{{ cfgData.analogCount }}</span></div>
                <div class="info-item"><label>开关量数:</label> <span>{{ cfgData.digitalCount }}</span></div>
                <div class="info-item"><label>频率:</label> <span>{{ cfgData.frequencyHz }} Hz</span></div>
                <div class="info-item"><label>时间倍率:</label> <span>{{ cfgData.timeMul }}</span></div>
                <div class="info-item"><label>启动时间:</label> <span>{{ cfgData.startTimeRaw }}</span></div>
                <div class="info-item"><label>触发时间:</label> <span>{{ cfgData.triggerTimeRaw }}</span></div>
                <div class="info-item"><label>数据格式:</label> <span>{{ cfgData.formatType }}</span></div>
                <div class="info-item"><label>数据类型:</label> <span>{{ cfgData.dataType }}</span></div>

                <div class="info-section" v-if="cfgData.sampleRateJson">
                  <h4>采样率配置</h4>
                  <pre>{{ formatJson(cfgData.sampleRateJson) }}</pre>
                </div>

                <div class="info-section" v-if="cfgData.fullCfgText">
                  <h4>CFG 全文</h4>
                  <pre>{{ cfgData.fullCfgText }}</pre>
                </div>
              </div>
            </el-scrollbar>
          </el-tab-pane>

          <el-tab-pane label="HDR 信息">
            <el-scrollbar height="calc(100vh - 180px)">
              <div v-if="hdrData" class="info-list">
                <el-collapse v-model="activeHdrNames" class="hdr-collapse">
                  <el-collapse-item title="设备信息" name="1" v-if="hdrData.deviceInfoJson && hdrData.deviceInfoJson.length">
                    <el-table :data="hdrData.deviceInfoJson" size="small" border>
                      <el-table-column type="index" label="序号" width="50" align="center" />
                      <el-table-column prop="name" label="名称" width="120" />
                      <el-table-column prop="value" label="值" />
                    </el-table>
                  </el-collapse-item>

                  <el-collapse-item title="故障信息" name="3" v-if="hdrData.faultInfoJson && hdrData.faultInfoJson.length">
                    <el-table :data="hdrData.faultInfoJson" size="small" border>
                      <el-table-column type="index" label="序号" width="50" align="center" />
                      <el-table-column prop="name" label="名称" width="160" />
                      <el-table-column label="值">
                        <template #default="{ row }">
                          {{ row.value }} {{ row.unit }}
                        </template>
                      </el-table-column>
                    </el-table>
                  </el-collapse-item>

                  <el-collapse-item title="保护动作信息" name="2" v-if="tripInfoArray.length">
                    <div style="margin-bottom: 10px; padding: 0 10px;">
                      <div class="info-item"><label>故障开始时间:</label> <span>{{ hdrData.faultStartTime }}</span></div>
                      <div class="info-item"><label>故障持续时间:</label> <span>{{ hdrData.faultKeepingTime }}</span></div>
                    </div>
                    <el-table :data="tripInfoArray" size="small" border>
                      <el-table-column type="index" label="序号" width="50" align="center" />
                      <el-table-column prop="time" label="时间" width="70" />
                      <el-table-column prop="phase" label="相位" width="50" align="center" />
                      <el-table-column label="保护动作">
                        <template #default="{ row }">
                          {{ row.name }} {{ row.value === '1' ? '动作' : (row.value === '0' ? '复归' : row.value) }}
                        </template>
                      </el-table-column>
                    </el-table>
                  </el-collapse-item>

                  <el-collapse-item title="启动时切换状态" name="4" v-if="hdrData.digitalStatusJson && hdrData.digitalStatusJson.length">
                    <el-table :data="hdrData.digitalStatusJson" size="small" border>
                      <el-table-column type="index" label="序号" width="50" align="center" />
                      <el-table-column prop="name" label="名称" width="180" />
                      <el-table-column prop="value" label="状态" align="center" />
                    </el-table>
                  </el-collapse-item>

                  <el-collapse-item title="启动后变化信息" name="5" v-if="hdrData.digitalEventJson && hdrData.digitalEventJson.length">
                    <el-table :data="hdrData.digitalEventJson" size="small" border>
                      <el-table-column type="index" label="序号" width="50" align="center" />
                      <el-table-column prop="time" label="时间" width="70" />
                      <el-table-column prop="name" label="名称" width="120" />
                      <el-table-column label="状态">
                        <template #default="{ row }">
                          {{ row.value === '1' ? '0 => 1' : (row.value === '0' ? '1 => 0' : row.value) }}
                        </template>
                      </el-table-column>
                    </el-table>
                  </el-collapse-item>

                  <el-collapse-item title="设备设置信息" name="6" v-if="hdrData.settingValueJson && hdrData.settingValueJson.length">
                    <el-table :data="hdrData.settingValueJson" size="small" border>
                      <el-table-column type="index" label="序号" width="50" align="center" />
                      <el-table-column prop="name" label="名称" width="160" />
                      <el-table-column label="值">
                        <template #default="{ row }">
                          {{ row.value }} {{ row.unit }}
                        </template>
                      </el-table-column>
                    </el-table>
                  </el-collapse-item>

                  <el-collapse-item
                    title="继电保护“软压板”投入状态值"
                    name="7"
                    v-if="hdrData.relayEnaValueJSON && hdrData.relayEnaValueJSON.length"
                  >
                    <el-table :data="hdrData.relayEnaValueJSON" size="small" border>
                      <el-table-column type="index" label="序号" width="60" align="center" />
                      <el-table-column prop="name" label="名称" width="180" />
                      <el-table-column prop="value" label="值" />
                    </el-table>
                  </el-collapse-item>
                </el-collapse>
              </div>
              <div v-else class="empty">暂无 HDR 信息</div>
            </el-scrollbar>
          </el-tab-pane>
        </el-tabs>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { zwavService, type AnalysisDetailDto, type CfgDto, type ChannelDto, type HdrDto, type WaveDataPageDto, type ZwavFileAnalysis } from '@/services/zwavService'
import { Download, FullScreen, Histogram, Plus, Search, Setting, Timer } from '@element-plus/icons-vue'
import * as echarts from 'echarts'
import { ElMessage, ElMessageBox } from 'element-plus'
import { computed, nextTick, onMounted, onUnmounted, reactive, ref } from 'vue'
import { useRoute } from 'vue-router'

const route = useRoute()
const analysisGuid = route.params.guid as string

const loading = ref(false)
const chartRef = ref<HTMLElement>()
let myChart: echarts.ECharts | null = null

// Data
const analogChannels = ref<ChannelDto[]>([])
const digitalChannels = ref<ChannelDto[]>([])
const selectedChannels = ref<number[]>([]) // Analog selection
const selectedDigitalChannels = ref<number[]>([]) // Digital selection
const cfgData = ref<CfgDto | null>(null)
const hdrData = ref<HdrDto | null>(null)
const detailData = ref<AnalysisDetailDto | null>(null)
const activeHdrNames = ref(['1', '2', '3', '4', '5', '6', '7'])
const channelStats = ref<{ name: string; max: number; min: number }[]>([])
const showStatsDialog = ref(false)
const showSequenceDialog = ref(false)
const isFullScreen = ref(false)

// Sidebar Logic
const channelSearchKeyword = ref('')
const checkAllAnalog = ref(false)
const isIndeterminateAnalog = ref(false)
const checkAllDigital = ref(false)
const isIndeterminateDigital = ref(false)
const checkHasAction = ref(false)
const isIndeterminateHasAction = ref(false)
const checkNoAction = ref(false)
const isIndeterminateNoAction = ref(false)

const filteredAnalogChannels = computed(() => {
  if (!channelSearchKeyword.value) return analogChannels.value
  const kw = channelSearchKeyword.value.toLowerCase()
  return analogChannels.value.filter(
    (c) => c.channelName.toLowerCase().includes(kw) || c.channelIndex.toString().includes(kw)
  )
})

const filteredDigitalChannels = computed(() => {
  if (!channelSearchKeyword.value) return digitalChannels.value
  const kw = channelSearchKeyword.value.toLowerCase()
  return digitalChannels.value.filter(
    (c) => c.channelName.toLowerCase().includes(kw) || c.channelIndex.toString().includes(kw)
  )
})

/** ✅ tripInfoJSON 可能是数组，也可能是 JSON 字符串；统一转换为数组供界面/时序图使用 */
const tripInfoArray = computed<any[]>(() => {
  const t: any = hdrData.value?.tripInfoJSON
  if (!t) return []
  if (Array.isArray(t)) return t
  if (typeof t === 'string') {
    try {
      const arr = JSON.parse(t)
      return Array.isArray(arr) ? arr : []
    } catch {
      return []
    }
  }
  return []
})

/** ✅ 是否可打开故障时序图 */
const canOpenSequence = computed(() => tripInfoArray.value.length > 0)

/**
 * 判断通道是否有动作
 * 规则：
 * 1. 在 digitalStatusJson 中值为 '1' (初始闭合)
 * 2. 在 digitalEventJson 中存在记录 (认为有变化/有动作)
 */
const actionChannelIndices = computed(() => {
  const indices = new Set<number>()
  if (!hdrData.value) return indices

  if (hdrData.value.digitalStatusJson) {
    hdrData.value.digitalStatusJson.forEach((item: any) => {
      if (String(item.value) === '1') {
        const ch = digitalChannels.value.find(
          (c) => c.channelName === item.name || c.channelName.trim() === String(item.name).trim()
        )
        if (ch) indices.add(ch.channelIndex)
      }
    })
  }

  if (hdrData.value.digitalEventJson) {
    hdrData.value.digitalEventJson.forEach((item: any) => {
      const ch = digitalChannels.value.find(
        (c) => c.channelName === item.name || c.channelName.trim() === String(item.name).trim()
      )
      if (ch) indices.add(ch.channelIndex)
    })
  }

  return indices
})

// 无动作 = 所有 - 有动作
const noActionChannelIndices = computed(() => {
  const allIndices = digitalChannels.value.map((c) => c.channelIndex)
  const actionIndices = actionChannelIndices.value
  return new Set(allIndices.filter((idx) => !actionIndices.has(idx)))
})

const handleCheckAllAnalogChange = (val: boolean) => {
  selectedChannels.value = val ? filteredAnalogChannels.value.map((c) => c.channelIndex) : []
  isIndeterminateAnalog.value = false
}
const handleAnalogChange = (value: number[]) => {
  const checkedCount = value.length
  checkAllAnalog.value = checkedCount === filteredAnalogChannels.value.length && filteredAnalogChannels.value.length > 0
  isIndeterminateAnalog.value = checkedCount > 0 && checkedCount < filteredAnalogChannels.value.length
}

const handleCheckAllDigitalChange = (val: boolean) => {
  selectedDigitalChannels.value = val ? filteredDigitalChannels.value.map((c) => c.channelIndex) : []
  isIndeterminateDigital.value = false
  updateActionCheckboxes()
}

const handleHasActionChange = (val: boolean) => {
  const actionIndices = Array.from(actionChannelIndices.value)
  if (val) {
    const newSet = new Set(selectedDigitalChannels.value)
    actionIndices.forEach((idx) => newSet.add(idx))
    selectedDigitalChannels.value = Array.from(newSet)
  } else {
    const newSet = new Set(selectedDigitalChannels.value)
    actionIndices.forEach((idx) => newSet.delete(idx))
    selectedDigitalChannels.value = Array.from(newSet)
  }
  updateMainDigitalCheckbox()
}

const handleNoActionChange = (val: boolean) => {
  const noActionIndices = Array.from(noActionChannelIndices.value)
  if (val) {
    const newSet = new Set(selectedDigitalChannels.value)
    noActionIndices.forEach((idx) => newSet.add(idx))
    selectedDigitalChannels.value = Array.from(newSet)
  } else {
    const newSet = new Set(selectedDigitalChannels.value)
    noActionIndices.forEach((idx) => newSet.delete(idx))
    selectedDigitalChannels.value = Array.from(newSet)
  }
  updateMainDigitalCheckbox()
}

const handleDigitalChange = (_value: number[]) => {
  updateMainDigitalCheckbox()
  updateActionCheckboxes()
}

const updateMainDigitalCheckbox = () => {
  updateActionCheckboxes()
}

const updateActionCheckboxes = () => {
  const selectedSet = new Set(selectedDigitalChannels.value)

  const actionIndices = Array.from(actionChannelIndices.value)
  if (actionIndices.length === 0) {
    checkHasAction.value = false
    isIndeterminateHasAction.value = false
  } else {
    const selectedActionCount = actionIndices.filter((idx) => selectedSet.has(idx)).length
    checkHasAction.value = selectedActionCount === actionIndices.length
    isIndeterminateHasAction.value = selectedActionCount > 0 && selectedActionCount < actionIndices.length
  }

  const noActionIndices = Array.from(noActionChannelIndices.value)
  if (noActionIndices.length === 0) {
    checkNoAction.value = false
    isIndeterminateNoAction.value = false
  } else {
    const selectedNoActionCount = noActionIndices.filter((idx) => selectedSet.has(idx)).length
    checkNoAction.value = selectedNoActionCount === noActionIndices.length
    isIndeterminateNoAction.value = selectedNoActionCount > 0 && selectedNoActionCount < noActionIndices.length
  }
}

// 坐标轴设置
const showAxisSettingDialog = ref(false)
const gridHeightSetting = ref(50)

// 查询参数设置
const searchFromSample = ref(0)
const searchToSample = ref(10000)
const searchLimit = ref(50000)
const searchDownSample = ref(1)

const openAxisSettings = () => {
  showAxisSettingDialog.value = true
}
const applyAxisSettings = () => {
  showAxisSettingDialog.value = false
  fetchWaveData()
}

const seqTreeData = ref<any[]>([])
const seqTreeRef = ref()
const defaultCheckedKeys = ref<string[]>([])
const currentMainFaultTime = ref('')

// File Selection Dialog
const fileSelectDialogVisible = ref(false)
const fileSearchKeyword = ref('')
const fileLoading = ref(false)
const fileTableData = ref<ZwavFileAnalysis[]>([])
const filePagination = reactive({
  page: 1,
  pageSize: 10,
  total: 0
})

// Store loaded trip info for comparison
// Key: analysisGuid (or fileId/name), Value: TripInfo[]
const loadedTripInfos = new Map<string, any[]>()
const loadedFileNames = new Map<string, string>()
const loadedFileColors = new Map<string, string>()

// Colors for different files
const SERIES_COLORS = ['#1677ff', '#b56be6', '#f6a04b', '#52c41a', '#ff4d4f']

/** ✅ 打开故障时序图：先打开 dialog，再 nextTick + 双 RAF 绘制，确保尺寸可用 */
const openSequenceDialog = async () => {
  if (!hdrData.value || tripInfoArray.value.length === 0) {
    ElMessage.warning('暂无故障时序数据')
    return
  }

  // Init with current file
  const guid = analysisGuid
  const fileName = detailData.value?.file?.originalName || '当前文件'
  const startTime = hdrData.value.faultStartTime || '未知时间'
  currentMainFaultTime.value = startTime

  // Use computed tripInfoArray which handles parsing
  const tripInfo = tripInfoArray.value

  // Cache current data
  loadedTripInfos.set(guid, tripInfo)
  loadedFileNames.set(guid, fileName)
  loadedFileColors.set(guid, SERIES_COLORS[0])

  // Build tree node for current file
  const rootId = guid
  const tripNodes = tripInfo.map((item, index) => {
    const valText = item.value === '1' ? '动作' : (item.value === '0' ? '复归' : item.value)
    return {
      label: `${item.time}ms ${item.name} ${valText}`,
      id: `${rootId}-trip-${index}`,
      disabled: true // Leaf nodes not checkable for simplicity, or checkable to filter? usually check root to show/hide file
    }
  })

  seqTreeData.value = [
    {
      label: fileName,
      id: rootId,
      children: [
        { label: `启动时间: ${startTime}`, id: `${rootId}-start-time`, disabled: true },
        ...tripNodes
      ]
    }
  ]
  
  defaultCheckedKeys.value = [rootId]

  showSequenceDialog.value = true

  await nextTick()
  requestAnimationFrame(() => {
    requestAnimationFrame(() => {
      renderSequenceDiagram()
    })
  })
}

const openFileSelectDialog = () => {
  fileSelectDialogVisible.value = true
  fetchFileList()
}

const fetchFileList = async () => {
  fileLoading.value = true
  try {
    const params = {
      page: filePagination.page,
      pageSize: filePagination.pageSize,
      keyword: fileSearchKeyword.value,
      status: 'Completed' // Only completed files have HDR
    }
    const res: any = await zwavService.getList(params)
    if (res.success) {
      fileTableData.value = res.data.data.filter((f: any) => f.analysisGuid !== analysisGuid) // Exclude current
      filePagination.total = res.data.total
    }
  } catch (err) {
    console.error(err)
  } finally {
    fileLoading.value = false
  }
}

const handleFileSelect = async (row: ZwavFileAnalysis) => {
  if (loadedTripInfos.has(row.analysisGuid)) {
    ElMessage.info('该文件已在列表中')
    return
  }

  try {
    const res: any = await zwavService.getHdr(row.analysisGuid)
    if (res.success && res.data && res.data.tripInfoJSON) {
      const guid = row.analysisGuid
      const fileName = row.originalName
      const startTime = res.data.faultStartTime
      
      let tripInfo: any[] = []
      const t = res.data.tripInfoJSON
      if (Array.isArray(t)) {
        tripInfo = t
      } else if (typeof t === 'string') {
        try {
          tripInfo = JSON.parse(t)
        } catch {
          tripInfo = []
        }
      }

      if (!Array.isArray(tripInfo) || tripInfo.length === 0) {
         ElMessage.warning('该文件没有保护动作信息')
         return
      }

      // Cache
      loadedTripInfos.set(guid, tripInfo)
      loadedFileNames.set(guid, fileName)
      // Assign color
      const colorIndex = loadedTripInfos.size % SERIES_COLORS.length
      loadedFileColors.set(guid, SERIES_COLORS[colorIndex])

      // Add to Tree
      const tripNodes = tripInfo.map((item: any, index: number) => {
        const valText = item.value === '1' ? '动作' : (item.value === '0' ? '复归' : item.value)
        return {
          label: `${item.time}ms ${item.name} ${valText}`,
          id: `${guid}-trip-${index}`,
          disabled: true
        }
      })

      const newNode = {
        label: fileName,
        id: guid,
        children: [
          { label: `启动时间: ${startTime}`, id: `${guid}-start-time`, disabled: true },
          ...tripNodes
        ]
      }
      seqTreeData.value = [...seqTreeData.value, newNode]

      // Auto check new file
      defaultCheckedKeys.value = [...defaultCheckedKeys.value, guid]
      // Need to manually set checked keys because binding might not update immediately for new nodes
      nextTick(() => {
        seqTreeRef.value?.setCheckedKeys(defaultCheckedKeys.value)
        renderSequenceDiagram()
      })

      ElMessage.success('添加成功')
      // Don't close dialog to allow multiple select? Or close? User said "batch add", maybe keep open.
      // But user said "I can batch add... from ZwavAnalysis table", usually implies a picker.
      // Let's keep picker open.
    } else {
      ElMessage.warning('该文件没有保护动作信息')
    }
  } catch (err) {
    console.error(err)
    ElMessage.error('加载文件信息失败')
  }
}

const handleTreeCheck = () => {
  // Simple debounce
  if ((window as any)._seqRenderTimer) clearTimeout((window as any)._seqRenderTimer)
  ;(window as any)._seqRenderTimer = setTimeout(() => {
    renderSequenceDiagram()
  }, 100)
}

/* =========================
 * 故障时序图绘制逻辑 (SVG)
 * ========================= */
const seqSvgRef = ref<SVGElement | null>(null)
const seqContainerRef = ref<HTMLElement | null>(null)
const leftInfoTop = ref(0)

const NS = 'http://www.w3.org/2000/svg'
const LANES = [60, 100, 140]
const SWAYS = [-20, 0, 20]
const GAP = 22

function el(name: string, attrs: any = {}, text?: string) {
  const n = document.createElementNS(NS, name)
  for (const [k, v] of Object.entries(attrs)) n.setAttribute(k, String(v))
  if (text != null) n.textContent = text
  return n
}

function clearSvg() {
  if (seqSvgRef.value) {
    while (seqSvgRef.value.firstChild) seqSvgRef.value.removeChild(seqSvgRef.value.firstChild)
  }
}

function setViewBox(W: number, H: number) {
  if (seqSvgRef.value) {
    seqSvgRef.value.setAttribute('viewBox', `0 0 ${W} ${H}`)
    ;(seqSvgRef.value as any).style.height = `${H}px`
  }
}

function parseMs(str: any) {
  if (str == null) return NaN
  const s = String(str).trim()
  const m = s.match(/(\d+(\.\d+)?)/)
  return m ? Number(m[1]) : NaN
}

function splitTextByChars(text: string, maxChars: number) {
  const s = String(text ?? '').trim()
  if (!s) return ['']
  const out: string[] = []
  let i = 0
  while (i < s.length) {
    out.push(s.slice(i, i + maxChars))
    i += maxChars
  }
  return out
}

function appendWrappedText(
  parentG: Element,
  x: number,
  y: number,
  anchor: string,
  fill: string,
  text: string,
  maxChars = 16,
  lineHeight = 16
) {
  const lines = splitTextByChars(text, maxChars)
  const t = el('text', { x, y, fill, class: 'label-text', 'text-anchor': anchor, 'font-size': '12', 'font-weight': 'bold' })
  lines.forEach((ln, idx) => {
    const ts = el('tspan', { x, dy: idx === 0 ? '0' : String(lineHeight) }, ln)
    t.appendChild(ts)
  })
  parentG.appendChild(t)
}

function buildNodesFromTripInfoMap(selectedTripInfos: Map<string, any[]>) {
  const map = new Map<number, any>()

  for (const [guid, tripInfo] of selectedTripInfos) {
    const color = loadedFileColors.get(guid) || '#1677ff'
    
    for (const r of tripInfo) {
      const t = parseMs(r.time)
      if (!Number.isFinite(t)) continue

      if (!map.has(t)) map.set(t, { timeMs: t, labels: [] as any[] })
      const node = map.get(t)

      const valText = r.value === '1' ? '动作' : r.value === '0' ? '复归' : r.value
      const txt = `${r.name} ${valText}`
      node.labels.push({ text: txt, color, guid })
    }
  }

  const nodes = [...map.values()].sort((a, b) => a.timeMs - b.timeMs)

  // 交替上下
  let flip = false
  for (const n of nodes) {
    const side = flip ? 'bottom' : 'top'
    flip = !flip
    n.labels.forEach((x: any) => (x.side = side))
  }
  return nodes
}

function estimateGroupExtent(itemsCount: number, maxLines: number) {
  const dotsSpan = itemsCount <= 1 ? 0 : (itemsCount - 1) * GAP
  const textSpan = maxLines <= 1 ? 0 : (maxLines - 1) * 16
  return dotsSpan + textSpan + 20
}

function estimateVerticalNeeds(nodes: any[]) {
  let needTop = 0
  let needBot = 0

  nodes.forEach((node, idx) => {
    const arr = node.labels
    if (!arr || !arr.length) return

    const isTop = (arr[0].side || 'top') === 'top'
    const lane = idx % 3
    const laneDist = LANES[lane]

    const maxChars = arr.length > 1 ? 14 : 16
    const maxLines = Math.max(...arr.map((x: any) => splitTextByChars(x.text, maxChars).length))

    const extent = laneDist + estimateGroupExtent(arr.length, maxLines)
    if (isTop) needTop = Math.max(needTop, extent)
    else needBot = Math.max(needBot, extent)
  })

  needTop = Math.max(needTop, 100)
  needBot = Math.max(needBot, 100)
  return { needTop, needBot }
}

function drawLabelGroup(cx: number, axisY: number, items: any[], isTop: boolean, color: string, idx: number) {
  if (!seqSvgRef.value) return

  const dir = isTop ? -1 : 1
  const lane = idx % 3
  const laneDist = LANES[lane]
  const sway = SWAYS[lane] * (idx % 2 === 0 ? 1 : -1)

  const anchorX = cx + sway
  const anchorY = axisY + dir * laneDist

  const g = el('g', {})
  seqSvgRef.value.appendChild(g)

  // 轴线到锚点的斜线：颜色也用主题色，避免“线颜色不对”
  g.appendChild(
    el('line', {
      x1: cx,
      y1: axisY,
      x2: anchorX,
      y2: anchorY,
      stroke: color,
      'stroke-width': 1.5
    })
  )

  const goLeft = anchorX > cx
  const hxLen = 30

  const ys: number[] = []
  for (let i = 0; i < items.length; i++) ys.push(anchorY + dir * GAP * i)

  // 多条时画竖线
  if (items.length > 1) {
    g.appendChild(
      el('line', {
        x1: anchorX,
        y1: Math.min(...ys),
        x2: anchorX,
        y2: Math.max(...ys),
        stroke: color,
        'stroke-width': 2
      })
    )
  }

  for (let i = 0; i < items.length; i++) {
    const y = ys[i]
    // Use item's specific color
    const itemColor = items[i].color || color

    g.appendChild(el('circle', { cx: anchorX, cy: y, r: 3, fill: itemColor }))

    const hx = anchorX + (goLeft ? -hxLen : hxLen)
    g.appendChild(
      el('line', {
        x1: anchorX,
        y1: y,
        x2: hx,
        y2: y,
        stroke: itemColor,
        'stroke-width': 1.5
      })
    )

    const textX = hx + (goLeft ? -6 : 6)
    appendWrappedText(g, textX, y + 4, goLeft ? 'end' : 'start', '#333', items[i].text, 18, 16)
  }
}

function computeXPositions(times: number[], x0: number, x1: number) {
  const n = times.length
  if (n === 0) return []
  if (n === 1) return [(x0 + x1) / 2]

  const sorted = [...times].sort((a, b) => a - b)
  const weights = sorted.map((t) => Math.log(1 + Math.max(0, t)))
  const minW = Math.min(...weights)
  const maxW = Math.max(...weights)
  const spanW = Math.max(1e-6, maxW - minW)

  const xs = new Map<number, number>()
  for (let i = 0; i < n; i++) {
    const eq = i / (n - 1)
    const wq = (weights[i] - minW) / spanW
    const mix = 0.8 * eq + 0.2 * wq
    xs.set(sorted[i], x0 + mix * (x1 - x0))
  }
  return times.map((t) => xs.get(t) as number)
}

const renderSequenceDiagram = () => {
  if (!seqSvgRef.value || !seqContainerRef.value) return
  
  // Get checked keys
  const checkedKeys = seqTreeRef.value?.getCheckedKeys() || defaultCheckedKeys.value
  
  // Filter for GUIDs (which are the root nodes in our tree structure)
  const selectedGuids = checkedKeys.filter((k: string) => loadedTripInfos.has(k))

  clearSvg()

  if (selectedGuids.length === 0) {
    setViewBox(1000, 260)
    seqSvgRef.value.appendChild(
      el('text', { x: 40, y: 80, fill: '#909399', 'font-size': '14', 'font-weight': 'bold' }, '请在左侧勾选要显示的文件')
    )
    leftInfoTop.value = 130
    return
  }

  // Collect trip infos
  const selectedMap = new Map<string, any[]>()
  for (const guid of selectedGuids) {
    const info = loadedTripInfos.get(guid)
    if (info) selectedMap.set(guid, info)
  }

  // Use nextTick to ensure container size is correct
  setTimeout(() => {
    if (!seqContainerRef.value || !seqSvgRef.value) return
    const box = seqContainerRef.value.getBoundingClientRect()
    const W = Math.max(1000, Math.floor(box.width))

    const nodes = buildNodesFromTripInfoMap(selectedMap)
    const { needTop, needBot } = estimateVerticalNeeds(nodes)

    const topPad = 40
    const botPad = 40
    const axisY = topPad + needTop
    leftInfoTop.value = axisY
    const H = axisY + needBot + botPad
    setViewBox(W, H)

    const xLeftPad = 120
    const xRightPad = 80
    const axisStart = xLeftPad
    const xBodyEnd = W - xRightPad

    const axisColor = '#cfe6ff'
    const headColor = '#9dc5f8'

    seqSvgRef.value.appendChild(
      el('line', {
        x1: axisStart,
        y1: axisY,
        x2: xBodyEnd,
        y2: axisY,
        stroke: axisColor,
        'stroke-width': 4,
        'stroke-linecap': 'round'
      })
    )

    const headLen = 30
    const headHalf = 12
    const tipX = xBodyEnd + headLen
    seqSvgRef.value.appendChild(
      el('polygon', {
        points: `${tipX},${axisY} ${xBodyEnd},${axisY - headHalf} ${xBodyEnd},${axisY + headHalf}`,
        fill: headColor
      })
    )
    
    seqSvgRef.value.appendChild(
      el(
        'text',
        {
          x: tipX + 10,
          y: axisY + 4,
          fill: '#909399',
          'font-weight': 'bold',
          'font-size': '12'
        },
        'ms'
      )
    )

    const times = nodes.map((n: any) => n.timeMs)
    const xs = computeXPositions(times, axisStart + 30, xBodyEnd - 30)

    nodes.forEach((node: any, idx: number) => {
      const cx = xs[idx]
      // Use first label's color for node circle
      const firstLabel = node.labels[0]
      const nodeColor = firstLabel ? firstLabel.color : '#1677ff'

      seqSvgRef.value!.appendChild(el('circle', { cx, cy: axisY, r: 5, fill: '#fff', stroke: nodeColor, 'stroke-width': 2 }))
      
      seqSvgRef.value!.appendChild(
        el(
          'text',
          {
            x: cx,
            y: axisY + 18,
            'text-anchor': 'middle',
            fill: '#606266',
            'font-size': '11',
            'font-weight': 'bold'
          },
          String(node.timeMs)
        )
      )

      const arr = node.labels
      if (arr && arr.length) {
        const isTop = (arr[0].side || 'top') === 'top'
        drawLabelGroup(cx, axisY, arr, isTop, nodeColor, idx)
      }
    })
  }, 100)
}

const toggleFullScreen = () => {
  const el = document.querySelector('.left-panel') as any
  if (!el) return

  if (!document.fullscreenElement) {
    el.requestFullscreen().catch((err: any) => {
      ElMessage.error(`无法进入全屏模式: ${err.message}`)
    })
  } else {
    document.exitFullscreen()
  }
}

const onFullScreenChange = () => {
  isFullScreen.value = !!document.fullscreenElement
}

onMounted(async () => {
  document.addEventListener('fullscreenchange', onFullScreenChange)

  if (!analysisGuid) {
    ElMessage.error('缺少任务ID')
    return
  }

  loading.value = true
  try {
    const [analogRes, digitalRes, cfgRes, hdrRes, detailRes] = await Promise.all([
      zwavService.getChannels(analysisGuid, 'Analog', true),
      zwavService.getChannels(analysisGuid, 'Digital', true),
      zwavService.getCfg(analysisGuid, true),
      zwavService.getHdr(analysisGuid).catch(() => ({ success: true, data: null } as any)),
      zwavService.getDetail(analysisGuid).catch(() => ({ success: true, data: null } as any))
    ])

    if (analogRes.success) {
      analogChannels.value = analogRes.data || []
      selectedChannels.value = analogChannels.value.slice(0, 3).map((c) => c.channelIndex)
      handleAnalogChange(selectedChannels.value)
    }
    if (digitalRes.success) {
      digitalChannels.value = digitalRes.data || []
    }
    if (cfgRes.success) cfgData.value = cfgRes.data
    if ((hdrRes as any)?.success) hdrData.value = (hdrRes as any).data
    if ((detailRes as any)?.success) detailData.value = (detailRes as any).data

    initChart()
    await fetchWaveData()
  } catch (err) {
    console.error(err)
    ElMessage.error('初始化数据失败')
  } finally {
    loading.value = false
  }

  window.addEventListener('resize', handleResize)
})

onUnmounted(() => {
  myChart?.dispose()
  window.removeEventListener('resize', handleResize)
  document.removeEventListener('fullscreenchange', onFullScreenChange)
})

const handleResize = () => {
  myChart?.resize()
}

const initChart = () => {
  if (!chartRef.value) return
  myChart = echarts.init(chartRef.value)
  myChart.setOption({
    tooltip: {
      trigger: 'axis',
      axisPointer: { type: 'cross' }
    },
    grid: { left: '3%', right: '4%', bottom: '3%', containLabel: true },
    xAxis: { type: 'category', boundaryGap: false, data: [] },
    yAxis: { type: 'value', scale: true },
    series: []
  })
}

const lastWaveData = ref<WaveDataPageDto | null>(null)

const fetchWaveData = async () => {
  if (selectedChannels.value.length === 0 && selectedDigitalChannels.value.length === 0) {
    ElMessage.warning('请至少选择一个通道')
    return
  }

  myChart?.showLoading()
  try {
    const channelsStr = selectedChannels.value.join(',')
    const digitalsStr = selectedDigitalChannels.value.join(',')

    const res: any = await zwavService.getWaveData(analysisGuid, {
      channels: channelsStr,
      digitals: digitalsStr,
      fromSample: searchFromSample.value,
      toSample: searchToSample.value,
      limit: searchLimit.value,
      downSample: searchDownSample.value
    })

    if (res.success && res.data) {
      lastWaveData.value = res.data
      updateChart(res.data)
    } else {
      ElMessage.error(res.message || '获取波形数据失败')
    }
  } catch (err) {
    console.error(err)
    ElMessage.error('获取波形数据异常')
  } finally {
    myChart?.hideLoading()
  }
}

const handleExport = () => {
  ElMessageBox.confirm('确定要导出当前分析的波形数据吗？文件可能较大，请耐心等待。', '导出确认', {
    confirmButtonText: '确定',
    cancelButtonText: '取消',
    type: 'warning'
  })
    .then(async () => {
      try {
        loading.value = true
        const res = await zwavService.exportWaveData(analysisGuid)
        const blob = new Blob([res.data], { type: 'text/csv;charset=utf-8;' })
        const link = document.createElement('a')
        const url = window.URL.createObjectURL(blob)
        link.href = url

        let fileName = 'wave_data.csv'
        const contentDisposition = (res as any).headers?.['content-disposition']
        if (contentDisposition) {
          const fileNameMatch = contentDisposition.match(/filename[^;=\n]*=((['"]).*?\2|[^;\n]*)/)
          if (fileNameMatch != null && fileNameMatch[1]) fileName = fileNameMatch[1].replace(/['"]/g, '')
        }
        if (fileName === 'wave_data.csv' && detailData.value?.file?.originalName) {
          fileName = `${detailData.value.file.originalName}.csv`
        }

        link.setAttribute('download', fileName)
        document.body.appendChild(link)
        link.click()
        document.body.removeChild(link)
        window.URL.revokeObjectURL(url)

        ElMessage.success('导出成功')
      } catch (err) {
        console.error(err)
        ElMessage.error('导出失败')
      } finally {
        loading.value = false
      }
    })
    .catch(() => {})
}

const updateChart = (data: WaveDataPageDto) => {
  if (!myChart || !chartRef.value) return

  const xAxisData = data.rows.map((r) => r.timeMs)
  const stats: { name: string; max: number; min: number }[] = []
  const digitalRawMap = new Map<string, number[]>()

  type SeriesItem = {
    name: string
    isDigital: boolean
    unit?: string
    analogData?: number[]
    digitalLine0?: Array<number | null>
    digitalArea1?: Array<number | null>
  }

  const seriesItems: SeriesItem[] = []

  // ---------- Analog ----------
  selectedChannels.value.forEach((chIdx) => {
    const chInfo = analogChannels.value.find((c) => c.channelIndex === chIdx)
    const name = chInfo ? `${chInfo.channelIndex}. ${chInfo.channelName}` : `${chIdx}. CH${chIdx}`
    const unit = chInfo?.unit || 'A'

    const dataIndex = data.channels ? data.channels.indexOf(chIdx) : -1
    if (dataIndex === -1) return

    const channelData = data.rows.map((r) => r.analog[dataIndex])

    let max = -Infinity
    let min = Infinity
    for (const v of channelData) {
      if (v > max) max = v
      if (v < min) min = v
    }
    stats.push({ name, max, min })

    seriesItems.push({ name, isDigital: false, unit, analogData: channelData })
  })

  // ---------- Digital ----------
  selectedDigitalChannels.value.forEach((chIdx) => {
    const chInfo = digitalChannels.value.find((c) => c.channelIndex === chIdx)
    const name = chInfo ? `${chInfo.channelIndex}. ${chInfo.channelName}` : `D${chIdx}`

    const dataIndex = data.digitals ? data.digitals.indexOf(chIdx) : -1
    if (dataIndex === -1) return

    const raw = data.rows.map((r) => {
      const v = r.digital && r.digital[dataIndex] !== undefined ? r.digital[dataIndex] : 0
      return v === 1 ? 1 : 0
    })

    digitalRawMap.set(name, raw)

    const line0 = raw.map((v) => (v === 0 ? 0 : null))
    const area1 = raw.map((v) => (v === 1 ? 1 : null))

    let max = 0
    let min = 1
    for (const v of raw) {
      if (v > max) max = v
      if (v < min) min = v
    }
    stats.push({ name, max, min })

    seriesItems.push({ name, isDigital: true, digitalLine0: line0, digitalArea1: area1 })
  })

  channelStats.value = stats

  // ---------- 动态 Grid ----------
  const count = seriesItems.length
  const grids: any[] = []
  const xAxes: any[] = []
  const yAxes: any[] = []
  const series: any[] = []

  const topMargin = 40
  const bottomMargin = 20
  const gap = 10
  const gridHeight = gridHeightSetting.value

  const totalChartHeight = topMargin + count * (gridHeight + gap) + bottomMargin
  chartRef.value.style.height = `${totalChartHeight}px`
  myChart.resize()

  const DIGITAL_GREEN = '#67C23A'
  const DIGITAL_RED_AREA = 'rgba(245,108,108,0.35)'

  const tooltipFormatter = (params: any[]) => {
    if (!params || params.length === 0) return ''

    const xVal = params[0].axisValue
    const dataIndex = params[0].dataIndex

    let html = `<div>时间(ms): ${xVal}</div>`
    const seen = new Set<string>()

    for (const p of params) {
      const sName: string = p.seriesName || ''
      const baseName = sName.endsWith('-0') || sName.endsWith('-1') ? sName.slice(0, -2) : sName

      if (seen.has(baseName)) continue
      seen.add(baseName)

      if (sName.endsWith('-0') || sName.endsWith('-1')) {
        const rawArr = digitalRawMap.get(baseName)
        if (rawArr && dataIndex >= 0 && dataIndex < rawArr.length) {
          const state = rawArr[dataIndex] === 1 ? 1 : 0
          html += `<div>${baseName}: ${state === 1 ? '1(投入)' : '0(复归)'}</div>`
        } else {
          html += `<div>${baseName}: -</div>`
        }
        continue
      }

      const val = p.value
      if (val === null || val === undefined) continue
      html += `<div style="color:${p.color}">${baseName}: ${Number(val).toFixed(4)}</div>`
    }

    return html
  }

  seriesItems.forEach((item, idx) => {
    const top = topMargin + idx * (gridHeight + gap)

    grids.push({
      left: 160,
      right: '4%',
      top,
      height: gridHeight,
      containLabel: false
    })

    const isFirst = idx === 0
    xAxes.push({
      type: 'category',
      boundaryGap: false,
      data: xAxisData,
      gridIndex: idx,
      position: 'top',
      axisLabel: { show: isFirst },
      axisTick: { show: isFirst },
      axisLine: { show: isFirst },
      splitLine: { show: true, lineStyle: { type: 'dashed', opacity: 0.5 } }
    })

    yAxes.push({
      type: 'value',
      scale: true,
      gridIndex: idx,
      name: item.name,
      nameLocation: 'middle',
      nameGap: 5,
      nameRotate: 0,
      min: item.isDigital ? -0.1 : undefined,
      max: item.isDigital ? 1.1 : undefined,
      interval: item.isDigital ? 1 : undefined,
      nameTextStyle: {
        align: 'right',
        color: '#333',
        fontWeight: 'bold',
        fontSize: 12,
        width: 130,
        overflow: 'break',
        lineHeight: 14
      },
      axisLabel: { show: false },
      axisLine: { show: true, lineStyle: { color: '#ccc' } },
      splitLine: { show: true, lineStyle: { type: 'dashed', opacity: 0.5 } }
    })

    if (!item.isDigital) {
      series.push({
        name: item.name,
        type: 'line',
        xAxisIndex: idx,
        yAxisIndex: idx,
        symbol: 'none',
        sampling: 'lttb',
        data: item.analogData || [],
        markLine: {
          symbol: 'none',
          silent: true,
          data: [{ yAxis: 0 }],
          lineStyle: { color: '#ccc', type: 'solid', width: 1 }
        }
      })
      return
    }

    series.push({
      name: `${item.name}-0`,
      type: 'line',
      xAxisIndex: idx,
      yAxisIndex: idx,
      data: item.digitalLine0 || [],
      step: 'start',
      symbol: 'none',
      lineStyle: { color: DIGITAL_GREEN, width: 2 },
      tooltip: { show: true },
      emphasis: { disabled: true }
    })

    series.push({
      name: `${item.name}-1`,
      type: 'line',
      xAxisIndex: idx,
      yAxisIndex: idx,
      data: item.digitalArea1 || [],
      step: 'start',
      symbol: 'none',
      lineStyle: { width: 0 },
      areaStyle: { color: DIGITAL_RED_AREA },
      tooltip: { show: false },
      emphasis: { disabled: true }
    })
  })

  const option: any = {
    tooltip: {
      trigger: 'axis',
      axisPointer: {
        type: 'cross',
        link: { xAxisIndex: 'all' }
      },
      formatter: tooltipFormatter
    },
    axisPointer: { link: { xAxisIndex: 'all' } },
    grid: grids,
    xAxis: xAxes,
    yAxis: yAxes,
    dataZoom: [
      {
        type: 'inside',
        xAxisIndex: xAxes.map((_: any, i: number) => i),
        start: 0,
        end: 100
      }
    ],
    series
  }

  myChart.setOption(option, true)
}

const formatJson = (jsonStr: string) => {
  try {
    if (!jsonStr) return ''
    const obj = JSON.parse(jsonStr)
    return JSON.stringify(obj, null, 2)
  } catch {
    return jsonStr
  }
}
</script>

<style>
.non-modal-dialog {
  pointer-events: none;
}
.non-modal-dialog .el-dialog {
  pointer-events: auto;
}
</style>

<style scoped>
.viewer-container {
  display: flex;
  flex-direction: column;
  height: 100vh;
  background-color: #f5f7fa;
}

.header {
  background-color: #fff;
  padding: 15px 20px;
  border-bottom: 1px solid #dcdfe6;
  box-shadow: 0 1px 4px rgba(0, 0, 0, 0.1);
  z-index: 10;
}

.title {
  display: flex;
  align-items: center;
}

.title h2 {
  margin: 0;
  font-size: 18px;
  color: #303133;
}

.file-name {
  margin-left: 20px;
  color: #303133;
  font-size: 16px;
  font-weight: bold;
}

.sub-title {
  margin-left: 10px;
  color: #909399;
  font-size: 14px;
}

.content {
  flex: 1;
  display: flex;
  overflow: hidden;
  padding: 10px;
  gap: 10px;
  background-color: #f5f7fa;
}

.left-panel {
  flex: 4;
  background-color: #fff;
  border-radius: 4px;
  display: flex;
  flex-direction: column;
  padding: 0;
  box-shadow: 0 2px 12px 0 rgba(0, 0, 0, 0.1);
  overflow: hidden;
}

.chart-controls {
  padding: 10px;
  border-bottom: 1px solid #ebeef5;
  display: flex;
  align-items: center;
  gap: 10px;
  background-color: #fff;
  flex-shrink: 0;
}

.main-body {
  flex: 1;
  display: flex;
  flex-direction: row;
  overflow: hidden;
}

.channel-sidebar {
  width: 220px;
  border-right: 1px solid #dcdfe6;
  display: flex;
  flex-direction: column;
  background-color: #fcfcfc;
}

.sidebar-header {
  padding: 10px;
  border-bottom: 1px solid #ebeef5;
}

.channel-section {
  flex: 1;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  border-bottom: 1px solid #ebeef5;
}
.channel-section:last-child {
  border-bottom: none;
}

.section-title {
  padding: 8px 10px;
  font-size: 13px;
  font-weight: bold;
  color: #303133;
  background-color: #fff;
  border-bottom: 1px solid #ebeef5;
}

.channel-group {
  flex: 1;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.group-header {
  padding: 5px 10px;
  background-color: #f5f7fa;
  font-size: 12px;
  font-weight: bold;
}

.group-header-row {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.filter-options {
  display: flex;
  flex-wrap: wrap;
  gap: 0px;
}

.selection-count {
  text-align: right;
  font-size: 11px;
}

.count {
  color: #909399;
  font-weight: normal;
}

.channel-list {
  padding: 5px 0;
  height: 100%;
}

.channel-item {
  padding: 2px 10px;
}
.channel-item:hover {
  background-color: #f0f2f5;
}

.channel-item :deep(.el-checkbox__label) {
  font-size: 12px;
  color: #606266;
  width: 200px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  vertical-align: middle;
}

.chart-wrapper {
  flex: 1;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  padding: 10px;
  background-color: #fff;
}

.chart-scroll-container {
  flex: 1;
  width: 100%;
  overflow-y: auto;
  position: relative;
}

.chart-content {
  width: 100%;
}

.tip {
  font-size: 12px;
  color: #909399;
}

.stats-icon {
  font-size: 20px;
  color: #909399;
  cursor: pointer;
  transition: color 0.3s;
}
.stats-icon:hover {
  color: #409eff;
}
.stats-icon.disabled {
  color: #c0c4cc;
  cursor: not-allowed;
}

.right-panel {
  flex: 1;
  background-color: #fff;
  border-radius: 4px;
  box-shadow: 0 2px 12px 0 rgba(0, 0, 0, 0.1);
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.info-tabs {
  height: 100%;
  border: none;
  box-shadow: none;
}

.info-tabs :deep(.el-tabs__content) {
  padding: 15px;
  height: calc(100% - 40px);
  overflow: hidden;
}

.info-list {
  font-size: 13px;
}

.info-item {
  margin-bottom: 10px;
  display: flex;
  border-bottom: 1px solid #ebeeF5;
  padding-bottom: 5px;
}

.info-item label {
  color: #909399;
  width: 100px;
  flex-shrink: 0;
}

.info-item span {
  color: #303133;
  word-break: break-all;
}

.info-section {
  margin-top: 20px;
}
.info-section h4 {
  margin: 0 0 10px;
  font-size: 14px;
  color: #303133;
  border-left: 3px solid #409eff;
  padding-left: 8px;
}

.hdr-collapse {
  border-top: none;
  border-bottom: none;
}

.hdr-collapse :deep(.el-collapse-item__header) {
  font-size: 14px;
  font-weight: bold;
  color: #303133;
  border-bottom: 1px solid #ebeeF5;
}
.hdr-collapse :deep(.el-collapse-item__content) {
  padding-bottom: 20px;
}

pre {
  background-color: #f4f4f5;
  padding: 10px;
  border-radius: 4px;
  font-family: monospace;
  white-space: pre-wrap;
  word-wrap: break-word;
  color: #606266;
  font-size: 12px;
}

.empty {
  color: #909399;
  text-align: center;
  margin-top: 20px;
}

.seq-layout {
  display: flex;
  height: 600px; /* 增加整体高度 */
  border: 1px solid #e5e7eb;
  border-radius: 8px;
  overflow: hidden;
}

.seq-sidebar {
  width: 300px;
  background-color: #fcfcfc;
  border-right: 1px solid #e5e7eb;
  display: flex;
  flex-direction: column;
  padding: 10px;
}

.sidebar-actions {
  margin-bottom: 10px;
  display: flex;
  justify-content: flex-end;
}

.sidebar-actions .el-button {
  width: 100%;
}

.seq-sidebar .el-tree {
  flex: 1;
  overflow-y: auto;
  background: transparent;
}

.seq-content {
  flex: 1;
  background-color: #fff;
  overflow: hidden;
  position: relative;
  display: flex;
  flex-direction: column;
}

.seq-container {
  position: relative;
  width: 100%;
  height: 100%; /* 占满 seq-content */
  background: #fff;
  overflow: auto;
  /* 移除原有的 border 和 radius，因为外层 seq-layout 已经有了 */
  border: none; 
  border-radius: 0;
}

.file-select-header {
  display: flex;
  gap: 10px;
  margin-bottom: 20px;
}

.file-table {
  margin-bottom: 20px;
}

.pagination-container {
  display: flex;
  justify-content: flex-end;
}

.custom-tree-node {
  font-size: 13px;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  display: inline-block;
  width: 100%;
}

.seq-svg {
  display: block;
  min-width: 1000px;
}

.left-info {
  position: absolute;
  left: 30px;
  transform: translateY(-50%);
  pointer-events: none;
  z-index: 10;
}

.fault-time {
  font-size: 16px;
  font-weight: 900;
  color: #374151;
  white-space: nowrap;
}

.sub-info {
  font-size: 12px;
  color: #909399;
  margin-top: 4px;
  font-weight: bold;
}

.sequence-dialog :deep(.el-dialog__body) {
  padding: 20px;
  background-color: #f8fafc;
}
</style>