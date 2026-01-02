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
      <!-- 左侧波形显示 (80%) -->
      <div class="left-panel">
        <div class="chart-controls">
          <el-select v-model="selectedChannels" multiple collapse-tags placeholder="选择模拟通道" style="width: 300px" @change="refreshChart">
            <el-option
              v-for="ch in analogChannels"
              :key="ch.channelIndex"
              :label="`${ch.channelIndex}. ${ch.channelName}`"
              :value="ch.channelIndex"
            />
          </el-select>
          <el-button type="primary" @click="fetchWaveData">加载/刷新波形</el-button>
          <span class="tip">提示：数据量大时请减少通道数</span>
          <div style="flex: 1"></div>
          <el-tooltip content="通道极值" placement="top">
            <el-icon 
              class="stats-icon" 
              :class="{ 'disabled': channelStats.length === 0 }"
              @click="channelStats.length > 0 && (showStatsDialog = true)"
            >
              <Histogram />
            </el-icon>
          </el-tooltip>
        </div>
        
        <div ref="chartRef" class="chart-container"></div>

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
            <el-table-column prop="max" label="最大值 (A)" width="120" align="left">
              <template #default="{ row }">
                <span style="color: #f56c6c">{{ row.max.toFixed(3) }}</span>
              </template>
            </el-table-column>
            <el-table-column prop="min" label="最小值 (A)" width="120" align="left">
              <template #default="{ row }">
                <span style="color: #409eff">{{ row.min.toFixed(3) }}</span>
              </template>
            </el-table-column>
          </el-table>
        </el-dialog>
      </div>

      <!-- 右侧信息显示 (20%) -->
      <div class="right-panel">
        <el-tabs type="border-card" class="info-tabs">
          <el-tab-pane label="CFG 信息">
            <el-scrollbar height="calc(100vh - 180px)">
              <div v-if="cfgData" class="info-list">
                <div class="info-item">
                  <label>厂站名:</label> <span>{{ cfgData.stationName }}</span>
                </div>
                <div class="info-item">
                  <label>设备ID:</label> <span>{{ cfgData.deviceId }}</span>
                </div>
                <div class="info-item">
                  <label>版本:</label> <span>{{ cfgData.revision }}</span>
                </div>
                <div class="info-item">
                  <label>模拟量数:</label> <span>{{ cfgData.analogCount }}</span>
                </div>
                <div class="info-item">
                  <label>开关量数:</label> <span>{{ cfgData.digitalCount }}</span>
                </div>
                <div class="info-item">
                  <label>频率:</label> <span>{{ cfgData.frequencyHz }} Hz</span>
                </div>
                <div class="info-item">
                  <label>时间倍率:</label> <span>{{ cfgData.timeMul }}</span>
                </div>
                <div class="info-item">
                  <label>启动时间:</label> <span>{{ cfgData.startTimeRaw }}</span>
                </div>
                <div class="info-item">
                  <label>触发时间:</label> <span>{{ cfgData.triggerTimeRaw }}</span>
                </div>
                <div class="info-item">
                  <label>数据格式:</label> <span>{{ cfgData.formatType }}</span>
                </div>
                <div class="info-item">
                  <label>数据类型:</label> <span>{{ cfgData.dataType }}</span>
                </div>
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
                
                <!-- 结构化数据展示 (折叠面板) -->
                <el-collapse v-model="activeHdrNames" class="hdr-collapse">
                  <el-collapse-item title="设备信息" name="1" v-if="hdrData.deviceInfoJson && hdrData.deviceInfoJson.length">
                    <el-table :data="hdrData.deviceInfoJson" size="small" border>
                      <el-table-column type="index" label="序号" width="60" align="center" />
                      <el-table-column prop="name" label="名称" width="120"/>
                      <el-table-column prop="value" label="值" />
                    </el-table>
                  </el-collapse-item>

                  <el-collapse-item title="故障信息" name="3" v-if="hdrData.faultInfoJson && hdrData.faultInfoJson.length">
                    <div style="margin-bottom: 10px; padding: 0 10px;">
                      <div class="info-item">
                        <label>故障开始时间:</label> <span>{{ hdrData.faultStartTime }}</span>
                      </div>
                      <div class="info-item">
                        <label>故障持续时间:</label> <span>{{ hdrData.faultKeepingTime }}</span>
                      </div>
                    </div>
                    <el-table :data="hdrData.faultInfoJson" size="small" border>
                      <el-table-column type="index" label="序号" width="60" align="center" />
                      <el-table-column prop="name" label="名称" />
                      <el-table-column label="值">
                        <template #default="{ row }">
                          {{ row.value }} {{ row.unit }}
                        </template>
                      </el-table-column>
                    </el-table>
                  </el-collapse-item>

                  <el-collapse-item title="保护动作信息" name="2" v-if="hdrData.tripInfoJSON && hdrData.tripInfoJSON.length">
                    <div style="margin-bottom: 10px; padding: 0 10px;">
                      <div class="info-item">
                        <label>故障开始:</label> <span>{{ hdrData.faultStartTime }}</span>
                      </div>
                      <div class="info-item">
                        <label>故障持续:</label> <span>{{ hdrData.faultKeepingTime }}</span>
                      </div>
                    </div>
                    <el-table :data="hdrData.tripInfoJSON" size="small" border>
                      <el-table-column type="index" label="序号" width="60" align="center" />
                      <el-table-column prop="time" label="时间" width="80" />
                      <el-table-column label="保护动作">
                        <template #default="{ row }">
                          {{ row.name }} {{ row.value === '1' ? '动作' : (row.value === '0' ? '复归' : row.value) }}
                        </template>
                      </el-table-column>
                      <el-table-column prop="phase" label="相位" />
                    </el-table>
                  </el-collapse-item>

                  <el-collapse-item title="启动时切换状态" name="4" v-if="hdrData.digitalStatusJson && hdrData.digitalStatusJson.length">
                    <el-table :data="hdrData.digitalStatusJson" size="small" border>
                      <el-table-column type="index" label="序号" width="60" align="center" />
                      <el-table-column prop="name" label="名称" />
                      <el-table-column prop="value" label="状态" />
                    </el-table>
                  </el-collapse-item>

                  <el-collapse-item title="启动后变化信息" name="5" v-if="hdrData.digitalEventJson && hdrData.digitalEventJson.length">
                    <el-table :data="hdrData.digitalEventJson" size="small" border>
                      <el-table-column type="index" label="序号" width="60" align="center" />
                      <el-table-column prop="time" label="时间" width="80" />
                      <el-table-column prop="name" label="名称" min-width="150" />
                      <el-table-column label="状态">
                        <template #default="{ row }">
                          {{ row.value === '1' ? '0 => 1' : (row.value === '0' ? '1 => 0' : row.value) }}
                        </template>
                      </el-table-column>
                    </el-table>
                  </el-collapse-item>

                  <el-collapse-item title="设备设置信息" name="6" v-if="hdrData.settingValueJson && hdrData.settingValueJson.length">
                    <el-table :data="hdrData.settingValueJson" size="small" border>
                      <el-table-column type="index" label="序号" width="60" align="center" />
                      <el-table-column prop="name" label="名称" />
                       <el-table-column label="值">
                        <template #default="{ row }">
                          {{ row.value }} {{ row.unit }}
                        </template>
                      </el-table-column>
                    </el-table>
                  </el-collapse-item>

                  <el-collapse-item title="继电保护“软压板”投入状态值" name="7" v-if="hdrData.relayEnaValueJSON && hdrData.relayEnaValueJSON.length">
                    <el-table :data="hdrData.relayEnaValueJSON" size="small" border>
                      <el-table-column type="index" label="序号" width="60" align="center" />
                      <el-table-column prop="name" label="名称" />
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
import * as echarts from 'echarts'
import { ElMessage } from 'element-plus'
import { onMounted, onUnmounted, ref } from 'vue'
import { useRoute } from 'vue-router'

const route = useRoute()
const analysisGuid = route.params.guid as string

const loading = ref(false)
const chartRef = ref<HTMLElement>()
let myChart: echarts.ECharts | null = null

// Data
const analogChannels = ref<ChannelDto[]>([])
const selectedChannels = ref<number[]>([]) // 选中的通道索引
const cfgData = ref<CfgDto | null>(null)
const hdrData = ref<HdrDto | null>(null)
const detailData = ref<AnalysisDetailDto | null>(null)
const activeHdrNames = ref(['1', '2', '3', '4', '5', '6', '7']) // 默认展开所有折叠面板
const channelStats = ref<{name: string, max: number, min: number}[]>([]) // 通道统计信息
const showStatsDialog = ref(false)

// Init
onMounted(async () => {
  if (!analysisGuid) {
    ElMessage.error('缺少任务ID')
    return
  }
  
  loading.value = true
  try {
    // 并行获取元数据
    const [channelsRes, cfgRes, hdrRes, detailRes] = await Promise.all([
      zwavService.getChannels(analysisGuid, 'Analog', true),
      zwavService.getCfg(analysisGuid, true), // includeText = true
      zwavService.getHdr(analysisGuid).catch(() => ({ data: null })), // HDR 可能不存在
      zwavService.getDetail(analysisGuid).catch(() => ({ data: null }))
    ])

    if (channelsRes.success) {
      analogChannels.value = channelsRes.data || []
      // 默认选中前 3 个通道
      selectedChannels.value = analogChannels.value.slice(0, 3).map(c => c.channelIndex)
    }

    if (cfgRes.success) {
      cfgData.value = cfgRes.data
    }

    if (hdrRes && (hdrRes as any).success) {
      hdrData.value = (hdrRes as any).data
    }

    if (detailRes && (detailRes as any).success) {
      detailData.value = (detailRes as any).data
    }

    // 初始化图表
    initChart()
    // 加载初始波形
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
  if (myChart) myChart.dispose()
  window.removeEventListener('resize', handleResize)
})

const handleResize = () => {
  myChart?.resize()
}

const initChart = () => {
  if (chartRef.value) {
    myChart = echarts.init(chartRef.value)
    myChart.setOption({
      tooltip: {
        trigger: 'axis',
        axisPointer: { type: 'cross' }
      },
      legend: {
        data: []
      },
      grid: {
        left: '3%',
        right: '4%',
        bottom: '3%',
        containLabel: true
      },
      xAxis: {
        type: 'category',
        boundaryGap: false,
        data: []
      },
      yAxis: {
        type: 'value',
        scale: true
      },
      dataZoom: [
        { type: 'slider', start: 0, end: 100, height: 100, bottom: 10,}
      ],
      series: []
    })
  }
}

const refreshChart = () => {
  // 仅当用户手动切换通道时触发，如果需要重新请求数据，可以调用 fetchWaveData
  // 但为了性能，如果数据已经在前端，可以只更新 series。
  // 这里简化处理：直接重新请求数据（因为可能涉及大量数据，前端全量缓存可能不合适，按需请求更好）
  // 或者我们可以先请求下来所有选中通道的数据，这里先做重新请求。
  // fetchWaveData() 
  // 暂时不自动刷新，让用户点击按钮刷新
}

const fetchWaveData = async () => {
  if (selectedChannels.value.length === 0) {
    ElMessage.warning('请至少选择一个通道')
    return
  }

  myChart?.showLoading()
  try {
    // 构建 channels 参数 "1,2,3"
    const channelsStr = selectedChannels.value.join(',')
    
    // 获取波形数据
    // 注意：limit 默认 2000，如果文件很大，可能需要分段或抽样。
    // 这里为了演示，我们假设请求前 10000 个点，或者不传 limit (后端默认限制)
    // 根据实际情况调整 limit 和 downSample
    const res: any = await zwavService.getWaveData(analysisGuid, {
      channels: channelsStr,
      limit: 5000, // 限制点数，避免浏览器卡死
      downSample: 1 // 不抽样
    })

    if (res.success && res.data) {
      updateChart(res.data)
    } else {
      ElMessage.error(res.message || '获取波形数据失败')
    }
  } catch (err) {
    ElMessage.error('获取波形数据异常')
  } finally {
    myChart?.hideLoading()
  }
}

const updateChart = (data: WaveDataPageDto) => {
  if (!myChart) return

  // X 轴数据 (TimeRaw 或 SampleNo)
  // 如果 cfgData 有频率，可以计算时间 ms
  // 这里直接用 SampleNo，实际可以根据 frequencyHz 转换
  // 假设频率 50Hz，点间隔 1000/SampleRate
  // 这里暂时直接展示 SampleNo，后续可优化为时间
  const xAxisData = data.rows.map(r => r.sampleNo)
  
  const stats: {name: string, max: number, min: number}[] = []

  const series = selectedChannels.value.map(chIdx => {
    const chInfo = analogChannels.value.find(c => c.channelIndex === chIdx)
    const name = chInfo ? `${chInfo.channelName}` : `CH${chIdx}`
    
    // 从 rows 中提取对应通道的数据
    const dataIndex = data.channels.indexOf(chIdx)
    const channelData = data.rows.map(r => r.analog[dataIndex])

    // 计算极值
    let max = -Infinity
    let min = Infinity
    for(const val of channelData) {
        if(val > max) max = val
        if(val < min) min = val
    }
    stats.push({ name, max, min })
    
    return {
      name: name,
      type: 'line',
      symbol: 'none', // 不显示点，提高性能
      sampling: 'lttb', // 降采样策略
      data: channelData
    }
  })
  
  channelStats.value = stats
  // 如果之前没有显示过统计，或者用户手动关闭了，这里不自动打开
  // 如果想每次加载都自动打开，可以取消注释下面这行
  // if (stats.length > 0) showStatsDialog.value = true

  const option = {
    tooltip: {
      trigger: 'axis',
      axisPointer: { type: 'cross' }
    },
    toolbox: {
      feature: {
        dataZoom: {
          yAxisIndex: 'none'
        },
        restore: {},
        saveAsImage: {}
      }
    },
    legend: {
      data: series.map(s => s.name),
      top: 0
    },
    grid: {
      left: '2%',
      right: '2%', // 增加右侧边距，给X轴单位留出空间
      top: '8%',
      bottom: '8%', // 使用像素值固定底部高度，确保能容纳下轴标签和滚动条
      containLabel: true
    },
    xAxis: {
      type: 'category',
      boundaryGap: false,
      name: '时间 (ms)', // 横轴单位
      nameLocation: 'middle', // 放置在轴末尾（最右侧）
      nameGap: 20,
      data: xAxisData
    },
    yAxis: {
      type: 'value',
      scale: true,
      name: '幅值 (A)' // 纵轴单位
    },
    dataZoom: [
      {
        type: 'inside', // 支持鼠标滚轮缩放和拖拽平移
        start: 0,
        end: 100,
        zoomOnMouseWheel: true, // 开启滚轮缩放
        moveOnMouseWheel: false, // 滚轮仅缩放，不平移
        moveOnMouseMove: true // 鼠标拖拽平移
      },
      {
        type: 'slider',
        start: 0,
        end: 100,
        height: 24,
        bottom: 10,
        borderColor: '#dcdfe6',
        fillerColor: 'rgba(64, 158, 255, 0.2)',
        handleStyle: {
          color: '#409eff',
          shadowBlur: 3,
          shadowColor: 'rgba(0, 0, 0, 0.2)',
          shadowOffsetX: 1,
          shadowOffsetY: 1
        },
        moveHandleStyle: {
          color: '#dcdfe6'
        },
        selectedDataBackground: {
          lineStyle: { color: '#409eff' },
          areaStyle: { color: '#409eff', opacity: 0.1 }
        },
        textStyle: {
          color: '#909399'
        }
      }
    ],
    series: series
  }

  myChart.setOption(option, true)
}

const formatJson = (jsonStr: string) => {
  try {
    if (!jsonStr) return ''
    const obj = JSON.parse(jsonStr)
    return JSON.stringify(obj, null, 2)
  } catch (e) {
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
  box-shadow: 0 1px 4px rgba(0,0,0,0.1);
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
  flex: 3; /* 3/4 */
  background-color: #fff;
  border-radius: 4px;
  display: flex;
  flex-direction: column;
  padding: 10px;
  box-shadow: 0 2px 12px 0 rgba(0,0,0,0.1);
}

.right-panel {
  flex: 1; /* 1/4 */
  background-color: #fff;
  border-radius: 4px;
  box-shadow: 0 2px 12px 0 rgba(0,0,0,0.1);
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.chart-controls {
  margin-bottom: 10px;
  display: flex;
  align-items: center;
  gap: 10px;
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

.stats-dialog :deep(.el-dialog__body) {
  padding: 10px 20px;
}

.chart-container {
  flex: 1;
  width: 100%;
  min-height: 400px;
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
  border-bottom: 1px solid #EBEEF5;
  padding-bottom: 5px;
}

.info-item label {
  color: #909399;
  width: 80px;
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
  border-left: 3px solid #409EFF;
  padding-left: 8px;
}

/* 移除原有的 .info-section 上边距，由 collapse 控制间距 */
.info-section {
  margin-top: 0;
}

.hdr-collapse {
  border-top: none;
  border-bottom: none;
}

.hdr-collapse :deep(.el-collapse-item__header) {
  font-size: 14px;
  font-weight: bold;
  color: #303133;
  border-bottom: 1px solid #EBEEF5;
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
