<template>
  <el-dialog
    :model-value="visible"
    title="通道极值"
    width="500px"
    draggable
    :modal="false"
    :close-on-click-modal="false"
    :modal-class="'non-modal-dialog'"
    class="stats-dialog"
    @update:model-value="updateVisible"
  >
    <el-table :data="stats" border stripe size="small" max-height="400">
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
</template>

<script setup lang="ts">
defineProps<{
  visible: boolean
  stats: { name: string; max: number; min: number }[]
}>()

const emit = defineEmits<{
  (e: 'update:visible', val: boolean): void
}>()

const updateVisible = (val: boolean) => {
  emit('update:visible', val)
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
