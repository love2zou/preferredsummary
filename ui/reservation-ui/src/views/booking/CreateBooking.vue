<template>
  <div class="booking-page">
    <div class="hero-header">
      <div class="left-actions" @click="goBack">
        <el-icon><ArrowLeft /></el-icon>
      </div>
      <div class="app-title">健身预约</div>
      <div class="right-actions"></div>
    </div>

    <div class="content">
      <div class="mini-card">
        <div class="mini-card-header">
          <h3>选择教练</h3>
        </div>
        <template v-if="hasBoundCoach">
          <!-- 删除冗余的步骤提示 -->
          <div class="coach-list">
            <div
              v-for="c in boundCoaches"
              :key="c.coachId"
              :class="['coach-card', { selected: selectedCoachId === c.coachId }]"
              @click="selectCoach(c.coachId)"
            >
              <div class="coach-card-content">
                <div class="avatar">
                  <img
                    v-if="safeAvatarUrl(c) && !brokenAvatar[c.coachId]"
                    :src="safeAvatarUrl(c)"
                    :alt="c.coachName || 'avatar'"
                    @error="onImgError(c.coachId)"
                  />
                  <div v-else class="avatar-fallback">{{ (c.coachName || '').toUpperCase() }}</div>
                </div>
                <div class="name">{{ c.coachName }}</div>
              </div>
            </div>
          </div>
        </template>
        <template v-else>
          <div class="hint" style="margin-bottom:8px">尚未绑定教练，请联系教练在“我的会员”页面为您完成绑定</div>
        </template>
      </div>

      <div class="mini-card">
        <h3>选择日期</h3>
        <div class="date-pills">
          <el-button :type="dateKey === 'today' ? 'primary' : 'default'" @click="setDate('today')">
            <el-icon style="margin-right: 6px"><Calendar /></el-icon>今天 ({{ todayLabel }})
          </el-button>
          <el-button :type="dateKey === 'tomorrow' ? 'primary' : 'default'" @click="setDate('tomorrow')">
            <el-icon style="margin-right: 6px"><Calendar /></el-icon>明天 ({{ tomorrowLabel }})
          </el-button>
        </div>
      </div>

      <div class="mini-card">
        <h3>可预约时段</h3>
        <div v-if="!selectedCoachId" class="hint">请先选择教练</div>
        <div v-else>
          <div v-if="loadingSlots" class="hint">正在加载可预约时段...</div>
          <div v-else-if="filteredSlots.length === 0" class="hint">暂无预约时段</div>
          <div v-else class="slots-grid">
            <el-tag
              v-for="s in filteredSlots"
              :key="s.slotId"
              :type="isSelected(s) ? 'success' : (s.isAvailable ? 'info' : 'danger')"
              :class="['slot-tag', { disabled: !s.isAvailable, selected: isSelected(s) }]"
              @click="toggleSelect(s)"
            >
              <div class="slot-content">
                <div class="slot-time">{{ s.startTime }} - {{ s.endTime }}</div>
                <div class="slot-right" v-if="reservedMap[`${s.startTime}-${s.endTime}`]">
                  <span class="slot-name">{{ reservedMap[`${s.startTime}-${s.endTime}`] }}</span>
                  <span class="slot-status">已预约</span>
                </div>
                <div class="slot-right" v-else-if="!s.isAvailable">
                  <span class="slot-status">已预约</span>
                </div>
              </div>
            </el-tag>
          </div>
        </div>
      </div>

      <div class="fixed-actions">
        <el-button type="primary" :disabled="!canSubmit" @click="submitBooking">确认预约</el-button>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { AvailableSlot, bookingService, BoundCoach } from '@/services/bookingService'
import { useUserStore } from '@/stores/user'
import { ArrowLeft, Calendar } from '@element-plus/icons-vue'
import { ElMessage } from 'element-plus'
import { computed, onMounted, ref, watch } from 'vue'
import { useRouter } from 'vue-router'

const router = useRouter()
const userStore = useUserStore()

const boundCoaches = ref<BoundCoach[]>([])
const selectedCoachId = ref<number | null>(null)
const dateKey = ref<'today' | 'tomorrow'>('today')
const loadingSlots = ref(false)
const availableSlots = ref<AvailableSlot[]>([])
const selectedSlots = ref<{ startTime: string; endTime: string }[]>([])
const reservedMap = ref<Record<string, string>>({})

const goBack = () => router.back()

const today = new Date()
const tomorrow = new Date(today.getTime() + 86400000)
const fmt = (d: Date) => `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
const todayLabel = computed(() => fmt(today))
const tomorrowLabel = computed(() => fmt(tomorrow))
const bookDate = computed(() => (dateKey.value === 'today' ? todayLabel.value : tomorrowLabel.value))

const setDate = (key: 'today' | 'tomorrow') => {
  dateKey.value = key
}

const isSelected = (s: AvailableSlot) => selectedSlots.value.some(x => x.startTime === s.startTime && x.endTime === s.endTime)
const toggleSelect = (s: AvailableSlot) => {
  if (!s.isAvailable) return
  const idx = selectedSlots.value.findIndex(x => x.startTime === s.startTime && x.endTime === s.endTime)
  if (idx >= 0) selectedSlots.value.splice(idx, 1)
  else selectedSlots.value.push({ startTime: s.startTime, endTime: s.endTime })
}

const canSubmit = computed(() => !!selectedCoachId.value && selectedSlots.value.length > 0)

// 方法：loadBoundCoaches、loadAvailableSlots
// 移除：绑定教练候选与状态
// 原：const bindCandidates = ref<AdminUser[]>([])
// 移除：绑定教练候选与状态
// 原：const pendingBindCoachId = ref<number | null>(null)
const hasBoundCoach = computed(() => boundCoaches.value.length > 0)

// 加载已绑定教练
const loadBoundCoaches = async () => {
  const memberId = userStore.user?.id || 0
  const resp = await bookingService.getBoundCoaches(memberId)
  const coaches = Array.isArray(resp?.data) ? resp.data : []
  boundCoaches.value = coaches
  selectedCoachId.value = coaches.length > 0 ? coaches[0].coachId : null
}

const loadAvailableSlots = async () => {
  if (!selectedCoachId.value) return
  loadingSlots.value = true
  try {
    const resp = await bookingService.getAvailableSlots(selectedCoachId.value, bookDate.value)
    availableSlots.value = Array.isArray(resp?.data) ? resp.data : []
    // 同步拉取：教练+日期 下已被预约的姓名
    const r = await bookingService.getReservedByDate(selectedCoachId.value, bookDate.value)
    const arr = Array.isArray(r?.data) ? r.data : []
    const map: Record<string, string> = {}
    for (const it of arr) {
      map[`${it.startTime}-${it.endTime}`] = it.memberName
    }
    reservedMap.value = map
  } finally {
    loadingSlots.value = false
  }
}

// 移除：加载可绑定教练（会员端不再使用）
// 原函数 loadBindableCoaches(...) 已删除

// 移除：绑定教练后继续预约逻辑（会员端不再使用）
// 原函数 bindCoachThenProceed(...) 已删除

onMounted(async () => {
  await loadBoundCoaches()
  await loadAvailableSlots()
})

const submitBooking = async () => {
  if (!canSubmit.value) return
  const memberId = userStore.user?.id || 0
  await bookingService.batchCreate({
    memberId,
    coachId: selectedCoachId.value!,
    bookDate: bookDate.value,
    timeSlots: selectedSlots.value
  })
  ElMessage.success('预约成功')
  // 刷新：清空选择并重新获取当日可约与已约信息
  selectedSlots.value = []
  await loadAvailableSlots()
}

watch([selectedCoachId, bookDate], loadAvailableSlots)
const selectCoach = (id: number) => {
  selectedCoachId.value = id
}
// 移除：跳转绑定教练页面的入口
// 原：const bindSectionEl = ref<HTMLElement | null>(null)
// 原：const openBindSection = () => { router.push('/bind-coach') }

// 统一时段过滤：营业 09:00–21:00，午休 12:00–14:00 不可约
const filteredSlots = computed(() => {
  const businessStart = 9 * 60
  const restStart = 12 * 60
  const restEnd = 14 * 60
  const businessEnd = 21 * 60
  return availableSlots.value.filter(s => {
    const start = toMin(s.startTime)
    const end = toMin(s.endTime)
    if (start < businessStart || end > businessEnd) return false
    if (start < restEnd && end > restStart) return false
    return true
  })
})

// 解析 "HH:mm" 为分钟用于比较
const toMin = (t: string) => {
  const [h, m] = t.split(':').map(Number)
  return h * 60 + (m || 0)
}
const brokenAvatar = ref<Record<number, boolean>>({})

function safeCoachOrigin(base: string) {
  // 去掉可能的 /api 后缀，确保用于图片等非 API 资源
  return base.replace(/\/api\/?$/i, '')
}

function safeAvatarUrl(c: BoundCoach): string | '' {
  const url = (c as any).avatarUrl || (c as any).profilePictureUrl || (c as any).avatar || ''
  if (!url) return ''
  // 绝对地址直接返回
  if (/^https?:\/\//i.test(url)) return url
  // 相对地址：用后端 Origin 拼接
  const base = (import.meta as any).env?.VITE_API_BASE_URL || ''
  const origin = base ? safeCoachOrigin(base) : ''
  // 若没有配置 base，直接返回相对路径以支持同域开发
  if (!origin) return url
  return `${origin}${url.startsWith('/') ? '' : '/'}${url}`
}

function onImgError(coachId: number) {
  brokenAvatar.value[coachId] = true
}
</script>

<style scoped>
.booking-page { display: flex; flex-direction: column; min-height: 100vh; }
.hero-header { display: flex; align-items: center; height: 56px; padding: 0 12px; border-bottom: 1px solid var(--el-border-color); }
.left-actions { width: 48px; display: flex; align-items: center; cursor: pointer; }
.app-title { flex: 1; text-align: center; font-weight: 600; }
.right-actions { width: 48px; }
.content { flex: 1; padding: 12px; }
.mini-card { background: var(--el-bg-color); border: 1px solid var(--el-border-color); border-radius: 8px; padding: 12px; margin-bottom: 12px; }
.bind-tip { margin-top: 8px; color: var(--el-text-color-secondary); font-size: 13px; }
/* 让“今天/明天”在小屏优先避免横向溢出 */
.date-pills { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 10px; margin-top: 16px; }
@media (max-width: 380px) {
  .date-pills { grid-template-columns: 1fr; }
}
/* 修复：相邻按钮的额外左边距，避免“今天/明天”不对齐 */
.date-pills :deep(.el-button + .el-button) { margin-left: 0 !important; }
/* 修复格子在小屏的横向溢出，必要时栈叠 */
.slots-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 10px; margin-top: 16px; }
@media (max-width: 380px) {
  .slots-grid { grid-template-columns: 1fr; }
}
.slot-tag { cursor: pointer; user-select: none; }
.slot-tag.disabled { opacity: 0.5; cursor: not-allowed; }
.slot-tag.selected { box-shadow: 0 0 0 1px var(--el-color-success); }
.fixed-actions { position: sticky; bottom: 0; background: var(--el-bg-color); padding: 12px; border-top: 1px solid var(--el-border-color); display: flex; justify-content: center; }
.step-title { margin-bottom: 6px; font-weight: 600; }
.coach-list { display: grid; grid-template-columns: repeat(auto-fill, minmax(70px, 1fr)); gap: 10px; margin-top: 16px; }
.coach-card { border: 1px solid var(--el-border-color); border-radius: 8px; cursor: pointer; background: var(--el-bg-color); transition: border-color .15s ease; display: flex; align-items: center; justify-content: center; overflow: hidden; }
.coach-card:hover { border-color: var(--el-color-primary-light-8); }
.coach-card.selected { border-color: var(--el-color-primary); }
.coach-card-content { width: 100%; gap: 6px; box-sizing: border-box; }
.avatar { width: 100%; height: 80px; margin: 0; border-radius: 8px 8px 0 0; overflow: hidden; border: none; background: var(--el-fill-color-light); display: block; }
.avatar img { width: 100%; height: 100%; object-fit: cover; display: block; }
.avatar-fallback { width: 100%; height: 100%; display: flex; align-items: center; justify-content: center; color: var(--el-text-color-primary); font-weight: 700; font-size: 20px; }
.coach-card .name { width: 100%; display: flex; align-items: center; justify-content: center; text-align: center; font-weight: 600; font-size: 14px; line-height: 1.2; height: 32px; margin: 0; }

/* 保留：时段姓名的布局与绿色高亮 + 防溢出 */
.slot-tag { display: flex; width: 100%; max-width: 100%; }
.slot-content { display: flex; align-items: center; justify-content: space-between; gap: 8px; width: 100%; min-width: 0; }
.slot-time { flex: 0 1 auto; min-width: 0; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
.slot-right { display: flex; align-items: center; gap: 6px; min-width: 0; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; color: var(--el-text-color-secondary); }
.slot-name { color: var(--el-color-success); font-weight: 600; }
.slot-status { color: var(--el-text-color-secondary); }
</style>