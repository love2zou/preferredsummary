<template>
  <el-dialog
    :model-value="visible"
    title="设置"
    width="500px"
    draggable
    :close-on-click-modal="false"
    @update:model-value="updateVisible"
  >
    <el-tabs>
      <el-tab-pane label="视图设置">
        <div style="margin-bottom: 20px;">
          <div style="margin-bottom: 10px; color: #666; font-size: 12px;">
            统一设置每个通道波形的绘图高度（像素），默认 50px。
          </div>
          <div style="display: flex; align-items: center; gap: 10px;">
            <span>单个波形高度:</span>
            <el-input-number v-model="localGridHeight" :min="20" :max="300" :step="5" controls-position="right" />
            <span>px</span>
          </div>
        </div>
      </el-tab-pane>

      <el-tab-pane label="参数设置">
        <el-form label-width="120px" size="small">
          <el-form-item label="起始采样点">
            <el-input-number v-model="localSearchParams.fromSample" :min="0" controls-position="right" style="width: 100%" />
            <div style="font-size: 12px; color: #999;">fromSample: 起始采样点编号 (默认0)</div>
          </el-form-item>

          <el-form-item label="结束采样点">
            <el-input-number v-model="localSearchParams.toSample" :min="0" controls-position="right" style="width: 100%" />
            <div style="font-size: 12px; color: #999;">toSample: 结束采样点编号 (默认10000)</div>
          </el-form-item>

          <el-form-item label="限制点数">
            <el-input-number v-model="localSearchParams.limit" :min="100" :step="1000" controls-position="right" style="width: 100%" />
            <div style="font-size: 12px; color: #999;">limit: 限制返回点数，避免浏览器卡死 (默认50000)</div>
          </el-form-item>

          <el-form-item label="抽样倍率">
            <el-input-number v-model="localSearchParams.downSample" :min="1" controls-position="right" style="width: 100%" />
            <div style="font-size: 12px; color: #999;">downSample: 抽样间隔，1为不抽样</div>
          </el-form-item>
        </el-form>
      </el-tab-pane>
    </el-tabs>

    <template #footer>
      <span class="dialog-footer">
        <el-button @click="updateVisible(false)">取消</el-button>
        <el-button type="primary" @click="handleApply">应用</el-button>
      </span>
    </template>
  </el-dialog>
</template>

<script setup lang="ts">
import { ref, watch } from 'vue'

const props = defineProps<{
  visible: boolean
  gridHeight: number
  searchParams: {
    fromSample: number
    toSample: number
    limit: number
    downSample: number
  }
}>()

const emit = defineEmits<{
  (e: 'update:visible', val: boolean): void
  (e: 'apply', gridHeight: number, params: any): void
}>()

const localGridHeight = ref(50)
const localSearchParams = ref({
  fromSample: 0,
  toSample: 10000,
  limit: 50000,
  downSample: 1
})

watch(() => props.visible, (val) => {
  if (val) {
    localGridHeight.value = props.gridHeight
    localSearchParams.value = { ...props.searchParams }
  }
})

const updateVisible = (val: boolean) => {
  emit('update:visible', val)
}

const handleApply = () => {
  emit('apply', localGridHeight.value, localSearchParams.value)
  updateVisible(false)
}
</script>
