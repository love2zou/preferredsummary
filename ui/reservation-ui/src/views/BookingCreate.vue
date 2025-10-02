<template>
  <div class="booking-create-page">
    <!-- 顶部栏 -->
    <header class="app-header">
      <button class="back-btn" @click="router.back()">返回</button>
      <div class="app-title">新建约课</div>
      <div style="width:64px"></div>
    </header>

    <!-- 教练选择 -->
    <section class="mini-card">
      <h3 class="section-title">选择教练</h3>
      <div class="coach-grid">
        <button
          v-for="c in coaches"
          :key="c.id"
          class="coach-item"
          :class="{ active: c.id === selectedCoachId }"
          @click="selectedCoachId = c.id"
        >
          <div class="coach-avatar">{{ c.avatar }}</div>
          <div class="coach-name">{{ c.name }}</div>
          <div class="coach-tag">{{ c.tag }}</div>
        </button>
      </div>
    </section>

    <!-- 日期选择 + 时间段 -->
    <section class="mini-card">
      <div class="day-tabs">
        <button
          class="day-tab"
          :class="{ active: selectedDay === 'today' }"
          @click="selectedDay = 'today'"
        >
          今天（{{ todayLabel }}）
        </button>
        <button
          class="day-tab"
          :class="{ active: selectedDay === 'tomorrow' }"
          @click="selectedDay = 'tomorrow'"
        >
          明天（{{ tomorrowLabel }}）
        </button>
      </div>

      <!-- 时间段：两列栅格，选择后在格子内显示预约用户名 -->
      <div class="slots-grid">
        <button
          v-for="slot in slotItems"
          :key="slot.key"
          class="time-slot"
          :class="{
            selected: selectedSlot?.key === slot.key,
            booked: !!slot.bookedBy
          }"
          :disabled="!selectedCoachId"
          @click="selectSlot(slot)"
        >
          <div class="slot-label">{{ slot.label }}</div>
          <div v-if="slot.bookedBy" class="slot-booked">已预约：{{ slot.bookedBy }}</div>
        </button>
      </div>

      <p v-if="!selectedCoachId" class="hint">
        请先选择教练后，再选择预约时间段
      </p>
    </section>

    <!-- 底部预约按钮（固定） -->
    <div class="booking-actions">
      <button 
        class="mini-button primary booking-btn" 
        :disabled="!canSubmit" 
        @click="handleBooking"
      >
        确认预约
      </button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { useUserStore } from '@/stores/user'
import { useBookingStore } from '@/stores/booking'

const router = useRouter()
const userStore = useUserStore()
const bookingStore = useBookingStore()

// 教练选择
const coaches = [
  { id: 1, name: '王教练', tag: '增肌/力量', avatar: '🏋️' },
  { id: 2, name: '李教练', tag: '减脂/有氧', avatar: '🔥' },
  { id: 3, name: '张教练', tag: '塑形/核心', avatar: '💪' }
]
const selectedCoachId = ref<number | null>(null)
const selectedCoach = computed(() => coaches.find(c => c.id === selectedCoachId.value))

// 日期选择
type DayKey = 'today' | 'tomorrow'
const selectedDay = ref<DayKey>('today')
const todayLabel = computed(() => {
  const d = new Date()
  return `${d.getMonth() + 1}/${d.getDate()}`
})
const tomorrowLabel = computed(() => {
  const d = new Date()
  d.setDate(d.getDate() + 1)
  return `${d.getMonth() + 1}/${d.getDate()}`
})

// 时间段（两列栅格用的数据源，支持显示预约用户名）
type SlotItem = { key: string; label: string; start: Date; end: Date; bookedBy?: string }
const slotItems = ref<SlotItem[]>([])
const selectedSlot = ref<SlotItem | null>(null)

function generateSlots(day: DayKey): SlotItem[] {
  const base = new Date()
  if (day === 'tomorrow') base.setDate(base.getDate() + 1)
  const hours = Array.from({ length: 11 }, (_, i) => 10 + i) // 10..20
  return hours.map(h => {
    const start = new Date(base); start.setHours(h, 0, 0, 0)
    const end = new Date(base);   end.setHours(h + 1, 0, 0, 0)
    return {
      key: `${day}-${h}`,
      label: `${pad(h)}:00 - ${pad(h + 1)}:00`,
      start, end
    }
  })
}
function pad(n: number) { return n.toString().padStart(2, '0') }

watch(selectedDay, () => {
  slotItems.value = generateSlots(selectedDay.value)
  selectedSlot.value = null
}, { immediate: true })

function selectSlot(slot: SlotItem) {
  if (!selectedCoachId.value) return
  selectedSlot.value = slot
}

const canSubmit = computed(() => !!selectedCoachId.value && !!selectedSlot.value)

// 点击“确认预约”：调用 store，并在格子内标记用户名
async function handleBooking() {
  if (!canSubmit.value || !selectedSlot.value || !selectedCoach.value) return
  const dateStr = selectedSlot.value.start.toISOString().split('T')[0]
  const bookingData = {
    trainerId: selectedCoach.value.id,
    trainerName: selectedCoach.value.name,
    date: dateStr,
    timeSlot: selectedSlot.value.label.replace(/\s/g, '')
  }
  try {
    await bookingStore.createBooking(bookingData)
    // 在选中时间段格子内显示预约会员用户名
    const username = (userStore.user as any)?.username ?? (userStore.user as any)?.name ?? '我'
    selectedSlot.value.bookedBy = username
  } catch (err) {
    console.error('预约失败', err)
    alert('预约失败，请稍后再试')
  }
}
</script>

<style scoped>
.booking-create-page { padding: 16px; }

/* 顶部栏 */
.app-header {
  height: 48px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0 12px;
  background: #fff;
  border-radius: var(--radius);
  box-shadow: var(--shadow);
  border: 1px solid var(--border-color);
  margin-bottom: 12px;
  position: sticky; top: 0; z-index: 10;
}
.app-title { font-size: 16px; font-weight: 600; color: var(--text-color); }
.back-btn { background: transparent; border: 0; color: var(--text-secondary); font-size: 14px; cursor: pointer; }

/* 分区标题 */
.section-title { margin: 0 0 8px; font-size: 15px; font-weight: 600; color: var(--text-color); }

/* 教练选择 */
.coach-grid { display: grid; grid-template-columns: repeat(3, 1fr); gap: 12px; }
.coach-item { border: 1px solid var(--border-color); background: #fff; border-radius: var(--radius); box-shadow: var(--shadow); padding: 12px 8px; text-align: center; cursor: pointer; }
.coach-item.active { border-color: var(--primary-color); box-shadow: 0 0 0 2px rgba(16, 185, 129, 0.1); }
.coach-avatar { font-size: 24px; margin-bottom: 6px; }
.coach-name { font-size: 14px; font-weight: 600; color: var(--text-color); }
.coach-tag { font-size: 12px; color: var(--text-secondary); }

/* 日期切换 */
.day-tabs { display: flex; gap: 8px; margin-bottom: 12px; }
.day-tab { flex: 1; height: 36px; border-radius: var(--radius); border: 1px solid var(--border-color); background: #fff; color: var(--text-color); cursor: pointer; }
.day-tab.active { border-color: var(--primary-color); background: var(--primary-color); color: #fff; }

/* 时间段：两列栅格 + 状态样式 */
.slots-grid { display: grid; grid-template-columns: repeat(2, 1fr); gap: 8px; }
.time-slot { 
  min-height: 48px; 
  border-radius: var(--radius); 
  border: 1px solid var(--border-color); 
  background: #fff; 
  color: var(--text-color); 
  cursor: pointer; 
  padding: 8px;
  text-align: left;
}
.time-slot.selected { border-color: var(--primary-color); box-shadow: 0 0 0 2px rgba(16,185,129,0.12); }
.time-slot.booked { background: rgba(16,185,129,0.08); border-color: var(--primary-color); }
.time-slot:disabled { cursor: not-allowed; opacity: 0.6; }
.slot-label { font-size: 14px; font-weight: 600; margin-bottom: 4px; }
.slot-booked { font-size: 12px; color: var(--primary-color-700); }

/* 底部预约按钮 */
.booking-actions { 
  position: fixed; bottom: 0; left: 0; right: 0; 
  padding: 12px 16px; background: var(--card-bg); 
  border-top: 1px solid var(--border-color);
}
.booking-btn { width: 100%; height: 44px; font-size: 15px; }
.booking-btn:disabled { opacity: .6; cursor: not-allowed; }

/* hint */
.hint { margin-top: 8px; font-size: 12px; color: var(--text-secondary); }
</style>