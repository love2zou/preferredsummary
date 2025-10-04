<template>
  <div class="booking-list">
    <div class="hero-header">
      <div class="left-actions" @click="goBack">
        <el-icon><ArrowLeft /></el-icon>
      </div>
      <div class="app-title">我的预约</div>
      <div class="right-actions"></div>
    </div>

    <div class="content">
      <div v-if="loading" class="hint">正在加载...</div>
      <div v-else-if="items.length === 0" class="hint">暂无预约记录</div>
      <div v-else class="list">
        <div class="item" v-for="it in items" :key="it.id">
          <div class="line1">
            <span class="date">{{ it.bookDate }}</span>
            <span class="time">{{ it.startTime }} - {{ it.endTime }}</span>
          </div>
          <div class="line2">
            <span class="coach">教练：{{ it.coachName }}</span>
            <el-tag :type="it.status === 9 ? 'info' : 'success'">{{ it.status === 9 ? '已取消' : '已预约' }}</el-tag>
          </div>
          <div class="actions">
            <el-button v-if="it.status !== 9" size="small" type="danger" @click="cancel(it.id)">取消</el-button>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { BookingItem, bookingService } from '@/services/bookingService'
import { useUserStore } from '@/stores/user'
import { ArrowLeft } from '@element-plus/icons-vue'
import { onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'

const router = useRouter()
const userStore = useUserStore()
const items = ref<BookingItem[]>([])
const loading = ref(false)

const goBack = () => router.back()

// 方法：loadList
const loadList = async () => {
  loading.value = true
  try {
    const memberId = userStore.user?.id || 0
    const { data } = await bookingService.list(memberId)
    items.value = data.data
  } finally {
    loading.value = false
  }
}

const cancel = async (id: number) => {
  await bookingService.cancel(id)
  await loadList()
}

onMounted(loadList)
</script>

<style scoped>
.booking-list { display: flex; flex-direction: column; min-height: 100vh; }
.hero-header { display: flex; align-items: center; height: 56px; padding: 0 12px; border-bottom: 1px solid var(--el-border-color); }
.left-actions { width: 48px; display: flex; align-items: center; cursor: pointer; }
.app-title { flex: 1; text-align: center; font-weight: 600; }
.right-actions { width: 48px; }
.content { flex: 1; padding: 12px; }
.list { display: flex; flex-direction: column; gap: 12px; }
.item { background: var(--el-bg-color); border: 1px solid var(--el-border-color); border-radius: 8px; padding: 12px; }
.line1 { display: flex; justify-content: space-between; font-weight: 600; }
.line2 { display: flex; justify-content: space-between; align-items: center; margin-top: 6px; }
.date, .time, .coach { font-size: 14px; }
.actions { margin-top: 8px; display: flex; justify-content: flex-end; }
.hint { color: var(--el-text-color-secondary); font-size: 13px; }
</style>