<template>
  <div class="app-page flow-page">
    <header class="page-header">
      <button class="icon-button" type="button" @click="router.back()">
        <el-icon><ArrowLeft /></el-icon>
      </button>
      <div class="page-header__title">
        <strong>预约服务流程</strong>
        <span>预约成功、签到、评价、联系教练</span>
      </div>
      <button class="icon-button" type="button" @click="router.push({ name: 'experience-map' })">
        <el-icon><Grid /></el-icon>
      </button>
    </header>

    <div class="scene-tabs">
      <button
        v-for="item in scenes"
        :key="item.key"
        class="scene-tabs__item"
        :class="{ 'is-active': activeScene === item.key }"
        type="button"
        @click="setScene(item.key)"
      >
        {{ item.label }}
      </button>
    </div>

    <section v-if="activeScene === 'success'" class="surface-card panel panel--center">
      <div class="result-mark">
        <el-icon><Check /></el-icon>
      </div>
      <h2>预约成功</h2>
      <p>课程、门店和沟通信息已经同步到你的预约记录。</p>

      <div class="summary-card">
        <div class="summary-card__head">
          <div>
            <strong>{{ coachName }}</strong>
            <span>{{ sessionLabel }}</span>
          </div>
          <img :src="coachPhoto" alt="coach" />
        </div>
        <div class="kv-list">
          <div><span>上课时间</span><strong>{{ upcomingTime }}</strong></div>
          <div><span>上课门店</span><strong>{{ clubName }}</strong></div>
          <div><span>训练区域</span><strong>{{ areaName }}</strong></div>
        </div>
      </div>

      <div class="button-stack">
        <button class="primary-button" type="button" @click="router.push({ name: 'reservations' })">查看我的预约</button>
        <button class="ghost-button" type="button" @click="setScene('chat')">联系教练</button>
      </div>
    </section>

    <section v-else-if="activeScene === 'cancel'" class="surface-card panel">
      <div class="inline-profile">
        <img :src="coachPhoto" alt="coach" />
        <div>
          <strong>{{ coachName }}</strong>
          <p>{{ upcomingTime }} / {{ clubName }}</p>
        </div>
      </div>

      <div class="section-title">
        <h3>取消预约</h3>
        <span>请选择取消原因</span>
      </div>

      <div class="chip-grid">
        <button
          v-for="reason in cancelReasons"
          :key="reason"
          class="chip"
          :class="{ 'is-active': cancelReason === reason }"
          type="button"
          @click="cancelReason = reason"
        >
          {{ reason }}
        </button>
      </div>

      <label class="field-block">
        <span>补充说明</span>
        <textarea v-model="cancelNote" rows="4" placeholder="可填写更具体的原因，方便门店安排排班。"></textarea>
      </label>

      <div class="hint-box">开课前 2 小时之外可免费取消，临近课程开始时段将按门店规则处理。</div>
      <button class="primary-button" type="button" @click="setScene('cancelled')">确认取消预约</button>
    </section>

    <section v-else-if="activeScene === 'cancelled'" class="surface-card panel panel--center">
      <div class="result-mark">
        <el-icon><Check /></el-icon>
      </div>
      <h2>已取消预约</h2>
      <p>本次课程已从你的预约列表移除，可重新选择教练和时间。</p>
      <div class="metric-strip">
        <strong>课时已返还</strong>
        <span>剩余课时以会员课包页为准</span>
      </div>
      <div class="button-stack">
        <button class="primary-button" type="button" @click="router.push({ name: 'trainers' })">重新预约</button>
        <button class="ghost-button" type="button" @click="router.push({ name: 'reservations' })">返回预约列表</button>
      </div>
    </section>

    <section v-else-if="activeScene === 'checkin'" class="surface-card panel">
      <div class="countdown">
        <span>签到方式</span>
        <strong>{{ checkInMethod }}</strong>
        <small>{{ checkInTime }}</small>
      </div>

      <div class="qr-box">
        <div class="qr-box__grid">
          <span v-for="cell in 81" :key="cell" :class="{ dark: qrPattern.has(cell) }"></span>
        </div>
      </div>

      <div class="method-grid">
        <article v-for="method in methods" :key="method.label" class="method-card">
          <el-icon><component :is="method.icon" /></el-icon>
          <strong>{{ method.label }}</strong>
        </article>
      </div>

      <div class="kv-list kv-list--block">
        <div><span>门店</span><strong>{{ clubName }}</strong></div>
        <div><span>区域</span><strong>{{ areaName }}</strong></div>
        <div><span>课程</span><strong>{{ sessionLabel }}</strong></div>
      </div>

      <button class="primary-button" type="button" @click="setScene('checked')">模拟签到成功</button>
    </section>

    <section v-else-if="activeScene === 'checked'" class="surface-card panel panel--center">
      <div class="result-mark">
        <el-icon><Select /></el-icon>
      </div>
      <h2>签到成功</h2>
      <p>训练已开始，课程完成后可在这里回看评价与沟通记录。</p>
      <div class="kv-list kv-list--block">
        <div><span>签到时间</span><strong>{{ checkInTime }}</strong></div>
        <div><span>教练</span><strong>{{ checkInCoach }}</strong></div>
        <div><span>训练区域</span><strong>{{ areaName }}</strong></div>
      </div>
      <div class="button-stack">
        <button class="primary-button" type="button" @click="setScene('complete')">查看课程完成页</button>
        <button class="ghost-button" type="button" @click="setScene('chat')">继续沟通</button>
      </div>
    </section>

    <section v-else-if="activeScene === 'complete'" class="surface-card panel">
      <div class="inline-profile">
        <img :src="coachPhoto" alt="coach" />
        <div>
          <strong>{{ completedCoach }}</strong>
          <p>{{ completedTime }}</p>
        </div>
      </div>

      <div class="metric-grid">
        <article v-for="metric in completeMetrics" :key="metric.label">
          <strong>{{ metric.value }}</strong>
          <span>{{ metric.label }}</span>
        </article>
      </div>

      <div class="section-title">
        <h3>训练回顾</h3>
        <span>{{ completedSession }}</span>
      </div>

      <ul class="bullet-list">
        <li>{{ completedNote || '本次训练数据已同步，建议根据教练反馈安排下一次预约。' }}</li>
        <li>到店门店: {{ clubName }}</li>
        <li>训练区域: {{ areaName }}</li>
      </ul>

      <div class="button-row">
        <button class="primary-button" type="button" @click="setScene('review')">去评价</button>
        <button class="ghost-button" type="button" @click="router.push({ name: 'booking', params: { id: upcomingTrainerId || 1 } })">再约一节</button>
      </div>
    </section>

    <section v-else-if="activeScene === 'review'" class="surface-card panel">
      <div class="inline-profile">
        <img :src="coachPhoto" alt="coach" />
        <div>
          <strong>{{ completedCoach }}</strong>
          <p>请为本次课程打分</p>
        </div>
      </div>

      <div class="rating-list">
        <div v-for="item in reviewMetrics" :key="item.label" class="rating-row">
          <span>{{ item.label }}</span>
          <div class="stars">
            <el-icon v-for="star in 5" :key="star" :class="{ active: star <= item.score }"><StarFilled /></el-icon>
          </div>
          <small>{{ item.text }}</small>
        </div>
      </div>

      <div class="chip-grid">
        <button
          v-for="tag in reviewTags"
          :key="tag"
          class="chip"
          :class="{ 'is-active': selectedReviewTags.includes(tag) }"
          type="button"
          @click="toggleTag(tag)"
        >
          {{ tag }}
        </button>
      </div>

      <label class="field-block">
        <span>文字评价</span>
        <textarea rows="4" placeholder="写下你的训练感受，帮助教练持续优化课程安排。"></textarea>
      </label>

      <button class="primary-button" type="button">提交评价</button>
    </section>

    <section v-else class="surface-card panel">
      <div class="chat-header">
        <img :src="coachPhoto" alt="coach" />
        <div>
          <strong>{{ coachName }}</strong>
          <span>{{ clubName }}</span>
        </div>
      </div>

      <div class="chat-list">
        <article
          v-for="message in chatMessages"
          :key="`${message.time}-${message.content}`"
          class="chat-line"
          :class="message.side"
        >
          <div class="chat-bubble">{{ message.content }}</div>
          <small>{{ message.time }}</small>
        </article>
      </div>

      <div class="chip-grid">
        <button v-for="reply in quickReplies" :key="reply" class="chip" type="button">{{ reply }}</button>
      </div>

      <div class="chat-input">
        <button class="icon-button" type="button">
          <el-icon><Microphone /></el-icon>
        </button>
        <input type="text" placeholder="输入消息..." />
        <button class="icon-button" type="button">
          <el-icon><CirclePlus /></el-icon>
        </button>
      </div>
    </section>
  </div>
</template>

<script setup lang="ts">
import {
  ArrowLeft,
  Camera,
  Check,
  Check as Select,
  CirclePlus,
  Grid,
  LocationFilled,
  Microphone,
  StarFilled,
  Tickets
} from '@element-plus/icons-vue'
import { ElMessage } from 'element-plus'
import { computed, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/authStore'
import { reservationApi, type ReservationOrderItem } from '@/api/reservation'
import {
  reservationPortalApi,
  type ReservationFlowData
} from '@/api/reservationPortal'

type SceneKey = 'success' | 'cancel' | 'cancelled' | 'checkin' | 'checked' | 'complete' | 'review' | 'chat'
type ReservationStatusKey = 'upcoming' | 'completed' | 'cancelled'

const router = useRouter()
const route = useRoute()
const authStore = useAuthStore()

const scenes: Array<{ key: SceneKey; label: string }> = [
  { key: 'success', label: '预约成功' },
  { key: 'cancel', label: '取消预约' },
  { key: 'cancelled', label: '取消结果' },
  { key: 'checkin', label: '到店签到' },
  { key: 'checked', label: '签到成功' },
  { key: 'complete', label: '课程完成' },
  { key: 'review', label: '课程评价' },
  { key: 'chat', label: '联系教练' }
]

const sceneFromQuery = computed<SceneKey>(() => {
  const value = String(route.query.scene || 'success')
  return (scenes.find((item) => item.key === value)?.key ?? 'success') as SceneKey
})

const reservationId = computed(() => Number(route.query.reservationId || 0))
const portalData = ref<ReservationFlowData | null>(null)
const reservationRecord = ref<ReservationOrderItem | null>(null)
const activeScene = ref<SceneKey>(sceneFromQuery.value)
const cancelReason = ref('')
const cancelNote = ref('')
const selectedReviewTags = ref<string[]>([])
const actionLoading = ref(false)

const methods = [
  { label: 'QR check-in', icon: Tickets },
  { label: 'Front desk', icon: Camera },
  { label: 'Location check-in', icon: LocationFilled }
]

const quickReplies = ['Training feedback', 'View schedule', 'Plan advice', 'FAQ']
const defaultCoachPhoto = 'https://images.unsplash.com/photo-1567013127542-490d757e51fc?auto=format&fit=crop&w=320&q=80'
const completeMetrics = [
  { label: 'Calories', value: '362 kcal' },
  { label: 'Duration', value: '60 min' },
  { label: 'Completion', value: '100%' }
]

const qrPattern = new Set([
  1, 2, 3, 4, 5, 7, 9, 10, 14, 16, 17, 18, 19, 21, 22, 23, 24, 26, 28, 30, 31,
  33, 34, 35, 37, 41, 43, 44, 45, 48, 50, 53, 54, 55, 56, 59, 61, 63, 65, 67, 70,
  71, 72, 74, 76, 77, 79, 81
])

const upcomingReservation = computed(() => {
  if (reservationRecord.value && reservationRecord.value.status !== 'completed') {
    return reservationRecord.value
  }
  return portalData.value?.upcomingReservation ?? null
})

const completedReservation = computed(() => {
  if (reservationRecord.value?.status === 'completed') {
    return reservationRecord.value
  }
  return portalData.value?.completedReservation ?? null
})

const activeReservation = computed(() => {
  if (reservationRecord.value) {
    return reservationRecord.value
  }
  if (activeScene.value === 'complete' || activeScene.value === 'review') {
    return completedReservation.value
  }
  return upcomingReservation.value ?? completedReservation.value
})

const checkInRecord = computed(() => portalData.value?.checkIn ?? null)
const cancelReasons = computed(() => portalData.value?.cancelReasons ?? [])
const reviewMetrics = computed(() =>
  (portalData.value?.reviewMetrics ?? []).map((item) => ({
    label: item.label,
    score: item.score,
    text: item.description
  }))
)
const reviewTags = computed(() => portalData.value?.reviewTags ?? [])
const chatMessages = computed(() =>
  (portalData.value?.messages ?? []).map((message) => ({
    side: message.senderRole === 'coach' ? 'right' : 'left',
    content: message.content,
    time: message.sentTime
  }))
)

const coachPhoto = computed(
  () =>
    activeReservation.value?.trainerPhotoUrl ||
    completedReservation.value?.trainerPhotoUrl ||
    defaultCoachPhoto
)
const coachName = computed(
  () =>
    activeReservation.value?.trainerName ||
    checkInRecord.value?.coachName ||
    'Coach'
)
const upcomingTrainerId = computed(() => activeReservation.value?.trainerId ?? 0)
const upcomingTime = computed(() => {
  const item = upcomingReservation.value
  return item ? `${item.calendarDate} ${item.timeRange}` : '--'
})
const completedTime = computed(() => {
  const item = completedReservation.value
  return item ? `${item.calendarDate} ${item.timeRange}` : '--'
})
const clubName = computed(() => activeReservation.value?.club || checkInRecord.value?.clubName || '--')
const areaName = computed(() => activeReservation.value?.area || checkInRecord.value?.areaName || '--')
const sessionLabel = computed(() => activeReservation.value?.sessionLabel || '--')
const completedSession = computed(() => completedReservation.value?.sessionLabel || sessionLabel.value)
const completedNote = computed(() => completedReservation.value?.note || activeReservation.value?.note || '')
const checkInMethod = computed(() => checkInRecord.value?.checkInMethod || 'QR check-in')
const checkInTime = computed(() => checkInRecord.value?.checkInTime || '--')
const checkInCoach = computed(() => `${checkInRecord.value?.coachName || coachName.value} / Senior coach`)
const completedCoach = computed(() => `${completedReservation.value?.trainerName || coachName.value} / Senior coach`)

const getMemberId = (): number => authStore.user?.userId ?? 0

const loadPortalData = async (memberId: number): Promise<void> => {
  const response = await reservationPortalApi.getMemberFlow(memberId)
  portalData.value = response.data
  cancelReason.value = response.data.cancelReasons?.[0] || ''
  selectedReviewTags.value = response.data.reviewTags?.slice(0, 3) || []
}

const resolveStatusesForScene = (scene: SceneKey): ReservationStatusKey[] => {
  if (scene === 'complete' || scene === 'review') {
    return ['completed']
  }
  if (scene === 'cancelled') {
    return ['cancelled', 'upcoming', 'completed']
  }
  return ['upcoming', 'completed']
}

const loadReservationRecord = async (memberId: number): Promise<void> => {
  if (!reservationId.value) {
    reservationRecord.value = null
    return
  }

  const responses = await Promise.all(
    resolveStatusesForScene(activeScene.value).map((status) =>
      reservationApi.getReservations(memberId, status)
    )
  )

  reservationRecord.value =
    responses
      .flatMap((response) => response.data ?? [])
      .find((item) => item.id === reservationId.value) ?? null
}

const initializePage = async (): Promise<void> => {
  const memberId = getMemberId()
  if (!memberId) {
    ElMessage.warning('Please sign in with a member account first.')
    await router.push({ name: 'login' })
    return
  }

  try {
    await Promise.all([loadPortalData(memberId), loadReservationRecord(memberId)])
  } catch {
    ElMessage.error('Failed to load the reservation flow data.')
  }
}

watch(
  () => route.query.scene,
  () => {
    activeScene.value = sceneFromQuery.value
  }
)

watch(
  () => [route.query.scene, route.query.reservationId],
  () => {
    const memberId = getMemberId()
    if (!memberId) return
    void loadReservationRecord(memberId)
  }
)

onMounted(async () => {
  await initializePage()
})

const confirmCancelReservation = async (): Promise<void> => {
  const memberId = getMemberId()
  if (!memberId || !reservationId.value) {
    ElMessage.warning('No reservation was found to cancel.')
    return
  }

  if (actionLoading.value) {
    return
  }

  actionLoading.value = true
  try {
    await reservationApi.cancelReservation(memberId, reservationId.value)
    activeScene.value = 'cancelled'
    await router.replace({
      query: {
        ...route.query,
        scene: 'cancelled'
      }
    })
    await Promise.all([loadPortalData(memberId), loadReservationRecord(memberId)])
    ElMessage.success('Reservation cancelled.')
  } catch {
    ElMessage.error('Failed to cancel the reservation. Please try again.')
  } finally {
    actionLoading.value = false
  }
}

const setScene = (scene: SceneKey): void => {
  if (scene === 'cancelled' && reservationId.value) {
    void confirmCancelReservation()
    return
  }

  activeScene.value = scene
  void router.replace({
    query: {
      ...route.query,
      scene
    }
  })
}

const toggleTag = (tag: string): void => {
  selectedReviewTags.value = selectedReviewTags.value.includes(tag)
    ? selectedReviewTags.value.filter((item) => item !== tag)
    : [...selectedReviewTags.value, tag]
}
</script>

<style scoped>
.flow-page {
  background:
    radial-gradient(circle at top right, rgba(37, 99, 235, 0.1), transparent 24%),
    linear-gradient(180deg, #fbfdff 0%, #f3f7ff 100%);
}

.scene-tabs,
.chip-grid,
.method-grid {
  display: flex;
  gap: 8px;
  overflow-x: auto;
  scrollbar-width: none;
}

.scene-tabs::-webkit-scrollbar,
.chip-grid::-webkit-scrollbar,
.method-grid::-webkit-scrollbar {
  display: none;
}

.scene-tabs {
  margin-bottom: 12px;
}

.scene-tabs__item,
.chip {
  flex: 0 0 auto;
  border: 0;
  border-radius: 999px;
  padding: 8px 12px;
  background: #fff;
  color: #6b7280;
  box-shadow: inset 0 0 0 1px rgba(37, 99, 235, 0.08);
  font-size: 12px;
  font-weight: 700;
}

.scene-tabs__item.is-active,
.chip.is-active {
  color: #2563eb;
  background: rgba(37, 99, 235, 0.08);
}

.panel {
  padding: 16px;
}

.panel--center {
  text-align: center;
}

.panel h2,
.panel p,
.section-title h3,
.section-title span {
  margin: 0;
}

.panel p,
.section-title span,
.inline-profile p,
.chat-header span {
  color: var(--muted);
}

.result-mark {
  width: 74px;
  height: 74px;
  margin: 0 auto 14px;
  border-radius: 50%;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  color: #fff;
  font-size: 34px;
  background: linear-gradient(135deg, #38bdf8, #2563eb);
  box-shadow: 0 16px 24px rgba(37, 99, 235, 0.22);
}

.summary-card,
.hint-box,
.metric-strip,
.countdown,
.kv-list--block {
  margin-top: 16px;
  border-radius: 18px;
}

.summary-card,
.hint-box,
.countdown,
.kv-list--block {
  padding: 14px;
  background: #f8fbff;
  border: 1px solid rgba(37, 99, 235, 0.08);
}

.summary-card__head,
.inline-profile,
.kv-list div,
.button-row,
.chat-input,
.chat-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
}

.summary-card__head img,
.inline-profile img,
.chat-header img {
  width: 54px;
  height: 54px;
  border-radius: 18px;
  object-fit: cover;
}

.summary-card__head strong,
.summary-card__head span,
.inline-profile strong,
.inline-profile p,
.chat-header strong,
.chat-header span {
  display: block;
}

.summary-card__head span,
.kv-list span,
.rating-row small {
  font-size: 12px;
}

.summary-card__head strong,
.kv-list strong,
.inline-profile strong,
.chat-header strong {
  font-size: 14px;
}

.kv-list {
  display: grid;
  gap: 10px;
  margin-top: 12px;
}

.kv-list span {
  color: var(--muted);
}

.button-stack,
.rating-list {
  display: grid;
  gap: 12px;
  margin-top: 16px;
}

.field-block {
  display: grid;
  gap: 8px;
  margin-top: 16px;
}

.field-block span {
  font-size: 12px;
  font-weight: 700;
}

.field-block textarea,
.chat-input input {
  width: 100%;
  border: 1px solid rgba(15, 23, 42, 0.08);
  border-radius: 16px;
  background: #fff;
  font-size: 14px;
}

.field-block textarea {
  padding: 12px 14px;
  resize: none;
}

.hint-box {
  color: #64748b;
  font-size: 12px;
  line-height: 1.7;
}

.metric-strip {
  padding: 16px;
  background: linear-gradient(135deg, rgba(37, 99, 235, 0.08), rgba(56, 189, 248, 0.06));
}

.metric-strip strong,
.metric-strip span {
  display: block;
}

.countdown {
  text-align: center;
}

.countdown strong {
  display: block;
  margin-top: 6px;
  font-size: 28px;
}

.countdown small {
  display: block;
  margin-top: 6px;
  color: var(--muted);
}

.qr-box {
  display: flex;
  justify-content: center;
  margin: 18px 0;
}

.qr-box__grid {
  width: 214px;
  height: 214px;
  padding: 14px;
  display: grid;
  grid-template-columns: repeat(9, 1fr);
  gap: 4px;
  background: #fff;
  border-radius: 24px;
  box-shadow: inset 0 0 0 1px rgba(15, 23, 42, 0.06);
}

.qr-box__grid span {
  border-radius: 3px;
  background: #e2e8f0;
}

.qr-box__grid span.dark {
  background: #0f172a;
}

.method-grid {
  margin-bottom: 12px;
}

.method-card,
.metric-grid article {
  flex: 1 0 0;
  min-width: 92px;
  padding: 14px 10px;
  border-radius: 18px;
  background: #fff;
  text-align: center;
  box-shadow: inset 0 0 0 1px rgba(15, 23, 42, 0.06);
}

.method-card strong,
.metric-grid strong,
.metric-grid span {
  display: block;
}

.method-card .el-icon {
  margin-bottom: 8px;
  color: #2563eb;
  font-size: 18px;
}

.metric-grid {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 10px;
  margin: 16px 0;
}

.section-title {
  margin: 16px 0 12px;
}

.bullet-list {
  margin: 0;
  padding-left: 18px;
  color: #475569;
  line-height: 1.7;
}

.button-row {
  margin-top: 16px;
}

.button-row .primary-button,
.button-row .ghost-button {
  flex: 1 1 0;
}

.rating-row {
  display: grid;
  grid-template-columns: 92px minmax(0, 1fr) 72px;
  gap: 8px;
  align-items: center;
}

.stars {
  display: flex;
  gap: 4px;
  color: #cbd5e1;
}

.stars .active {
  color: #f59e0b;
}

.chat-list {
  display: grid;
  gap: 12px;
  margin: 16px 0;
}

.chat-line {
  display: grid;
  gap: 6px;
}

.chat-line.left {
  justify-items: start;
}

.chat-line.right {
  justify-items: end;
}

.chat-bubble {
  max-width: 86%;
  padding: 12px 14px;
  border-radius: 18px;
  line-height: 1.6;
}

.chat-line.left .chat-bubble {
  background: #f1f5f9;
  color: #0f172a;
}

.chat-line.right .chat-bubble {
  background: linear-gradient(135deg, #3b82f6, #2563eb);
  color: #fff;
}

.chat-line small {
  color: #94a3b8;
}

.chat-input {
  margin-top: 16px;
  padding: 8px;
  border-radius: 18px;
  background: #fff;
  box-shadow: inset 0 0 0 1px rgba(15, 23, 42, 0.06);
}

.chat-input input {
  flex: 1 1 auto;
  height: 40px;
  border: 0;
  padding: 0 12px;
}
</style>
