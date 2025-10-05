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
        <div
          v-for="it in items"
          :key="it.id"
          :class="['item', { past: isPast(it) }]"
        >
          <div class="line1">
            <span class="date">{{ it.bookDate }}</span>
            <span class="time">{{ it.startTime }} - {{ it.endTime }}</span>
          </div>
          <div class="line2">
            <span class="coach">
              {{ userStore.isTrainer ? `会员：${it.memberName || it.memberId}` : `教练：${it.coachName}` }}
            </span>
            <!-- 教练视图不显示状态标签（reserved-by-date 无状态字段） -->
            <el-tag v-if="!userStore.isTrainer" :type="it.status === 9 ? 'info' : 'success'">
              {{ it.status === 9 ? '已取消' : '已预约' }}
            </el-tag>
          </div>
          <div class="actions">
            <!-- 教练不展示取消按钮；会员按原逻辑展示 -->
            <el-button v-if="!userStore.isTrainer && it.status !== 9 && !isPast(it)" size="small" type="danger" @click="cancel(it.id)">取消</el-button>
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

const toYMD = (d: Date) => {
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`
}

// 方法：loadList
const loadList = async () => {
  loading.value = true
  try {
    const userId = userStore.user?.id || 0
    const request = userStore.user?.role === 'jiaolian'
      ? bookingService.listByCoach(userId)
      : bookingService.list(userId)

    const { data } = await request
    items.value = Array.isArray(data)
      ? data
      : (Array.isArray((data as any)?.data) ? (data as any).data : [])
  } finally {
    loading.value = false
  }
}

const cancel = async (id: number) => {
  await bookingService.cancel(id)
  await loadList()
}

onMounted(loadList)

// 辅助函数：判断预约是否已结束（历史日期/已过结束时间）
const isPast = (it: BookingItem) => {
  const end = new Date(`${it.bookDate}T${it.endTime}:00`)
  const now = new Date()
  return end.getTime() < now.getTime()
}
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
/* 过期预约的卡片配色（更淡、更灰），与正常区分开 */
.item.past {
  background: var(--el-fill-color-light);
  border-color: var(--el-border-color);
  opacity: 0.92;
}
.item.past .date,
.item.past .time,
.item.past .coach {
  color: var(--el-text-color-secondary);
}
</style>