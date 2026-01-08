<template>
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
              v-model="checkAllDigital"
              :indeterminate="isIndeterminateDigital"
              @change="handleCheckAllDigitalChange"
              size="small"
            >
              全选
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
</template>

<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { Search } from '@element-plus/icons-vue'
import type { ChannelDto } from '@/services/zwavService'

const props = defineProps<{
  analogChannels: ChannelDto[]
  digitalChannels: ChannelDto[]
  selectedChannels: number[]
  selectedDigitalChannels: number[]
}>()

const emit = defineEmits<{
  (e: 'update:selectedChannels', val: number[]): void
  (e: 'update:selectedDigitalChannels', val: number[]): void
}>()

// Use internal state for v-models to allow easy emit updates, or just use props if readonly?
// V-model needs writable computed
const selectedChannels = computed({
  get: () => props.selectedChannels,
  set: (val) => emit('update:selectedChannels', val)
})

const selectedDigitalChannels = computed({
  get: () => props.selectedDigitalChannels,
  set: (val) => emit('update:selectedDigitalChannels', val)
})

const channelSearchKeyword = ref('')
const checkAllAnalog = ref(false)
const isIndeterminateAnalog = ref(false)
const checkAllDigital = ref(false)
const isIndeterminateDigital = ref(false)

const filteredAnalogChannels = computed(() => {
  if (!channelSearchKeyword.value) return props.analogChannels
  const kw = channelSearchKeyword.value.toLowerCase()
  return props.analogChannels.filter(
    (c) => c.channelName.toLowerCase().includes(kw) || c.channelIndex.toString().includes(kw)
  )
})

const filteredDigitalChannels = computed(() => {
  if (!channelSearchKeyword.value) return props.digitalChannels
  const kw = channelSearchKeyword.value.toLowerCase()
  return props.digitalChannels.filter(
    (c) => c.channelName.toLowerCase().includes(kw) || c.channelIndex.toString().includes(kw)
  )
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
}

const handleDigitalChange = (value: number[]) => {
  const checkedCount = value.length
  const totalCount = filteredDigitalChannels.value.length
  checkAllDigital.value = checkedCount === totalCount && totalCount > 0
  isIndeterminateDigital.value = checkedCount > 0 && checkedCount < totalCount
}

// Watchers to sync checkbox state when props change externally (e.g. init)
watch(() => props.selectedChannels, (newVal) => {
  handleAnalogChange(newVal)
})
watch(() => props.selectedDigitalChannels, (newVal) => {
  handleDigitalChange(newVal)
})
</script>

<style scoped>
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
</style>
