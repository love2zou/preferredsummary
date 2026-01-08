<template>
  <div class="chart-controls">
    <el-button type="primary" :loading="waveLoading" @click="emit('fetchWaveData')">加载/刷新波形</el-button>
    <span class="tip">提示：数据量大时请减少通道数</span>
    <div style="flex: 1"></div>

    <el-tooltip content="导出CSV数据" placement="top">
      <el-icon class="stats-icon" @click="emit('handleExport')">
        <Download />
      </el-icon>
    </el-tooltip>

    <el-tooltip content="坐标轴高度设置" placement="top">
      <el-icon class="stats-icon" @click="emit('openAxisSettings')">
        <Setting />
      </el-icon>
    </el-tooltip>

    <el-tooltip content="通道极值" placement="top">
      <el-icon
        class="stats-icon"
        :class="{ disabled: !hasStats }"
        @click="hasStats && emit('openStatsDialog')"
      >
        <Histogram />
      </el-icon>
    </el-tooltip>

    <el-tooltip content="故障时序图" placement="top" :disabled="!canOpenSequence">
      <el-button
        link
        class="icon-btn"
        :disabled="!canOpenSequence"
        @click.stop="emit('openSequenceDialog')"
        title="故障时序图"
      >
        <el-icon><Timer /></el-icon>
      </el-button>
    </el-tooltip>

    <el-tooltip :content="isFullScreen ? '退出全屏' : '全屏显示'" placement="top">
      <el-icon class="stats-icon" @click="emit('toggleFullScreen')">
        <FullScreen />
      </el-icon>
    </el-tooltip>
  </div>
</template>

<script setup lang="ts">
import { Download, FullScreen, Histogram, Setting, Timer } from '@element-plus/icons-vue'

defineProps<{
  waveLoading: boolean
  hasStats: boolean
  canOpenSequence: boolean
  isFullScreen: boolean
}>()

const emit = defineEmits<{
  (e: 'fetchWaveData'): void
  (e: 'handleExport'): void
  (e: 'openAxisSettings'): void
  (e: 'openStatsDialog'): void
  (e: 'openSequenceDialog'): void
  (e: 'toggleFullScreen'): void
}>()
</script>

<style scoped>
.chart-controls {
  padding: 10px;
  border-bottom: 1px solid #ebeef5;
  display: flex;
  align-items: center;
  gap: 10px;
  background-color: #fff;
  flex-shrink: 0;
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

.icon-btn {
  font-size: 20px;
  color: #909399;
  padding: 0;
  height: auto;
}
.icon-btn:hover {
  color: #409eff;
}
.icon-btn.is-disabled {
  color: #c0c4cc;
}
</style>
