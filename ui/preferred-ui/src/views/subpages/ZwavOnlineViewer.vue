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
                    <div style="margin-bottom: 10px; padding: 0 10px;">
                      <div class="info-item"><label>故障开始时间:</label> <span>{{ hdrData.faultStartTime }}</span></div>
                      <div class="info-item"><label>故障持续时间:</label> <span>{{ hdrData.faultKeepingTime }}</span></div>
                    </div>
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

                  <el-collapse-item title="保护动作信息" name="2" v-if="hdrData.tripInfoJSON && hdrData.tripInfoJSON.length">
                    <el-table :data="hdrData.tripInfoJSON" size="small" border>
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
import { zwavService, type AnalysisDetailDto, type CfgDto, type ChannelDto, type HdrDto, type WaveDataPageDto } from '@/services/zwavService'
import { Download, Histogram, Setting } from '@element-plus/icons-vue'
import * as echarts from 'echarts'
import { ElMessage, ElMessageBox } from 'element-plus'
import { computed, onMounted, onUnmounted, ref } from 'vue'
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

/**
 * 判断通道是否有动作
 * 规则：
 * 1. 在 digitalStatusJson 中值为 '1' (初始闭合)
 * 2. 在 digitalEventJson 中存在记录且值为 '1' (变位闭合)
 */
const actionChannelIndices = computed(() => {
  const indices = new Set<number>()
  if (!hdrData.value) return indices

  // 1. Check Status (Init)
   if (hdrData.value.digitalStatusJson) {
     hdrData.value.digitalStatusJson.forEach(item => {
       if (String(item.value) === '1') {
         // 使用更宽松的匹配
         const ch = digitalChannels.value.find(c => 
           c.channelName === item.name || 
           c.channelName.trim() === item.name.trim()
         )
         if (ch) indices.add(ch.channelIndex)
       }
     })
   }
 
   // 2. Check Events
   // 只要有事件（无论是变0还是变1），都意味着存在变化或状态非恒定0（假设初始0变1，或者初始1变0）
   // 如果事件是 0->0 (冗余)，虽然还是0，但通常不会记录这种事件。
   // 所以简单认为：只要在 EventJson 中出现，或者 StatusJson 中为 1，即为“有动作/存在1”
   if (hdrData.value.digitalEventJson) {
     hdrData.value.digitalEventJson.forEach(item => {
       // 只要有记录，就认为是有动作（曾有过1，或变成了1）
       const ch = digitalChannels.value.find(c => 
         c.channelName === item.name || 
         c.channelName.trim() === item.name.trim()
       )
       if (ch) indices.add(ch.channelIndex)
     })
   }
   
   return indices
 })
 
 // 无动作 = 所有 - 有动作
 const noActionChannelIndices = computed(() => {
   // 必须基于 filteredDigitalChannels 还是 digitalChannels? 
   // 逻辑上应该是基于所有数字通道进行分类，不受搜索关键词影响（或者受影响？）
   // 通常分类是基于全量的。
   const allIndices = digitalChannels.value.map(c => c.channelIndex)
   const actionIndices = actionChannelIndices.value
   return new Set(allIndices.filter(idx => !actionIndices.has(idx)))
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
    // Add all action channels
    const newSet = new Set(selectedDigitalChannels.value)
    actionIndices.forEach(idx => newSet.add(idx))
    selectedDigitalChannels.value = Array.from(newSet)
  } else {
    // Remove all action channels
    const newSet = new Set(selectedDigitalChannels.value)
    actionIndices.forEach(idx => newSet.delete(idx))
    selectedDigitalChannels.value = Array.from(newSet)
  }
  updateMainDigitalCheckbox()
}

const handleNoActionChange = (val: boolean) => {
  const noActionIndices = Array.from(noActionChannelIndices.value)
  if (val) {
    // Add all no-action channels
    const newSet = new Set(selectedDigitalChannels.value)
    noActionIndices.forEach(idx => newSet.add(idx))
    selectedDigitalChannels.value = Array.from(newSet)
  } else {
    // Remove all no-action channels
    const newSet = new Set(selectedDigitalChannels.value)
    noActionIndices.forEach(idx => newSet.delete(idx))
    selectedDigitalChannels.value = Array.from(newSet)
  }
  updateMainDigitalCheckbox()
}

const handleDigitalChange = (value: number[]) => {
  updateMainDigitalCheckbox()
  updateActionCheckboxes()
}

// 这里的 updateMainDigitalCheckbox 主要用于更新“有动作”和“无动作”的状态
// 因为移除了“全选”按钮，checkAllDigital 变量虽然还在代码中，但不再绑定界面元素，
// 不过为了保持代码完整性，先保留计算逻辑，或者可以删除它。
// 这里仅保留 updateActionCheckboxes 的调用
const updateMainDigitalCheckbox = () => {
  // const checkedCount = selectedDigitalChannels.value.length
  // const totalCount = filteredDigitalChannels.value.length
  // checkAllDigital.value = checkedCount === totalCount && totalCount > 0
  // isIndeterminateDigital.value = checkedCount > 0 && checkedCount < totalCount
  updateActionCheckboxes()
}

const updateActionCheckboxes = () => {
  const selectedSet = new Set(selectedDigitalChannels.value)
  
  // Update Has Action
  const actionIndices = Array.from(actionChannelIndices.value)
  if (actionIndices.length === 0) {
    checkHasAction.value = false
    isIndeterminateHasAction.value = false
  } else {
    const selectedActionCount = actionIndices.filter(idx => selectedSet.has(idx)).length
    checkHasAction.value = selectedActionCount === actionIndices.length
    isIndeterminateHasAction.value = selectedActionCount > 0 && selectedActionCount < actionIndices.length
  }

  // Update No Action
  const noActionIndices = Array.from(noActionChannelIndices.value)
  if (noActionIndices.length === 0) {
    checkNoAction.value = false
    isIndeterminateNoAction.value = false
  } else {
    const selectedNoActionCount = noActionIndices.filter(idx => selectedSet.has(idx)).length
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

onMounted(async () => {
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
  ElMessageBox.confirm(
    '确定要导出当前分析的波形数据吗？文件可能较大，请耐心等待。',
    '导出确认',
    {
      confirmButtonText: '确定',
      cancelButtonText: '取消',
      type: 'warning'
    }/*  */
  )
    .then(async () => {
      try {
        loading.value = true
        const res = await zwavService.exportWaveData(analysisGuid)
        // res is axios response object
        const blob = new Blob([res.data], { type: 'text/csv;charset=utf-8;' })
        const link = document.createElement('a')
        const url = window.URL.createObjectURL(blob)
        link.href = url
        
        let fileName = 'wave_data.csv'
        // Try to get filename from content-disposition
        const contentDisposition = res.headers['content-disposition']
        if (contentDisposition) {
          const fileNameMatch = contentDisposition.match(/filename[^;=\n]*=((['"]).*?\2|[^;\n]*)/)
          if (fileNameMatch != null && fileNameMatch[1]) {
            fileName = fileNameMatch[1].replace(/['"]/g, '')
          }
        }
        // Fallback
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
    .catch(() => {
      // Cancelled
    })
}

/**
 * 数字通道绘制规则（按你的需求）：
 * - 0：绿色线（阶梯）
 * - 1：红色虚化矩形区间（area）
 * - 0<->1：自然形成“线 + 区间”
 * 技术实现：一个数字通道拆成两个 series：
 *  - 绿色线 series：data = (v==0?0:null)
 *  - 红色区 series：data = (v==1?1:null)，lineStyle.width=0 + areaStyle
 */
type SeriesItem = {
  name: string
  isDigital: boolean
  unit?: string
  analogData?: number[]
  digitalRaw?: number[]        // 0/1 原始值
  digitalLine0?: Array<number | null>  // 0 时绿色线
  digitalArea1?: Array<number | null>  // 1 时红色区
}

const updateChart = (data: WaveDataPageDto) => {
  if (!myChart || !chartRef.value) return

  // X 轴数据
  const xAxisData = data.rows.map((r) => r.timeRaw)

  const stats: { name: string; max: number; min: number }[] = []

  // 用于 tooltip：通过 dataIndex 精准取数字通道原始 0/1
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

    seriesItems.push({
      name,
      isDigital: false,
      unit,
      analogData: channelData
    })
  })

  // ---------- Digital（核心：0 绿线 + 1 红色虚化区） ----------
  selectedDigitalChannels.value.forEach((chIdx) => {
    const chInfo = digitalChannels.value.find((c) => c.channelIndex === chIdx)
    const name = chInfo ? `${chInfo.channelIndex}. ${chInfo.channelName}` : `D${chIdx}`

    const dataIndex = data.digitals ? data.digitals.indexOf(chIdx) : -1
    if (dataIndex === -1) return

    // raw: 强制归一为 0/1
    const raw = data.rows.map((r) => {
      const v = r.digital && r.digital[dataIndex] !== undefined ? r.digital[dataIndex] : 0
      return v === 1 ? 1 : 0
    })

    // tooltip 用：保存原始 0/1
    digitalRawMap.set(name, raw)

    // 0：绿色线（非 0 用 null 断开）
    const line0 = raw.map((v) => (v === 0 ? 0 : null))
    // 1：红色虚化区（非 1 用 null 断开）
    const area1 = raw.map((v) => (v === 1 ? 1 : null))

    // stats（0/1）
    let max = 0
    let min = 1
    for (const v of raw) {
      if (v > max) max = v
      if (v < min) min = v
    }
    stats.push({ name, max, min })

    seriesItems.push({
      name,
      isDigital: true,
      digitalLine0: line0,
      digitalArea1: area1
    })
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

  // tooltip：数字通道通过 dataIndex 从 rawMap 取真实 0/1（不会“固定不变”）
  const tooltipFormatter = (params: any[]) => {
    if (!params || params.length === 0) return ''

    const xVal = params[0].axisValue
    const dataIndex = params[0].dataIndex

    let html = `<div>时间(ms): ${xVal}</div>`

    // 去重：数字通道有 -0/-1 两条 series，只显示一行
    const seen = new Set<string>()

    for (const p of params) {
      const sName: string = p.seriesName || ''
      const baseName =
        sName.endsWith('-0') || sName.endsWith('-1') ? sName.slice(0, -2) : sName

      if (seen.has(baseName)) continue
      seen.add(baseName)

      // 数字通道：查 raw
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

      // 模拟量
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
      // 模拟量
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

    // 数字量：两条 series（绿线 + 红区）
    series.push({
      name: `${item.name}-0`,
      type: 'line',
      xAxisIndex: idx,
      yAxisIndex: idx,
      data: item.digitalLine0 || [],
      step: 'start',
      symbol: 'none',
      lineStyle: { color: DIGITAL_GREEN, width: 2 },
      tooltip: { show: true }, // tooltip 只依赖 formatter（此处保持 true）
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
      tooltip: { show: false }, // 禁用此条 tooltip，避免重复
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
    axisPointer: {
      link: { xAxisIndex: 'all' }
    },
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
    // 不使用 visualMap，避免与红色虚化区冲突
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

.group-header-col {
  display: flex;
  flex-direction: column;
  gap: 5px;
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
</style>