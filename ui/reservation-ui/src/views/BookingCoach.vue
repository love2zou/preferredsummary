<template>
  <div v-if="bookingPage && trainer && selectedSession && selectedDate" class="app-page booking-page">
    <header class="page-header booking-header">
      <button class="icon-button" type="button" @click="router.back()">
        <el-icon><ArrowLeft /></el-icon>
      </button>
      <div class="page-header__title">
        <strong>预约教练</strong>
      </div>
      <div class="booking-header__spacer"></div>
    </header>

    <section class="surface-card booking-summary">
      <TrainerArtwork
        :name="trainer.name"
        :tone="trainer.heroTone"
        :accent="trainer.accentTone"
        :photo-url="trainer.photoUrl"
        variant="thumb"
      />

      <div class="booking-summary__body">
        <div class="booking-summary__top">
          <div class="booking-summary__name-row">
            <strong>{{ trainer.name }}</strong>
            <span class="tag tag--brand">{{ trainer.badges[0] }}</span>
            <span class="booking-summary__score">
              <el-icon><StarFilled /></el-icon>
              {{ trainer.rating.toFixed(1) }}
            </span>
          </div>
          <div class="booking-summary__price">¥{{ selectedSession.price }}<small>/节</small></div>
        </div>
        <small>{{ trainer.goals.join(' / ') }} / {{ trainer.specialties.join(' / ') }}</small>
      </div>
    </section>

    <section class="surface-card booking-panel">
      <div class="section-head">
        <h3>课程类型</h3>
      </div>
      <div class="booking-type-grid">
        <button
          v-for="session in trainer.sessionTypes"
          :key="session.id"
          class="booking-type"
          :class="{ 'is-active': selectedSessionId === session.id }"
          type="button"
          @click="selectedSessionId = session.id"
        >
          {{ session.label }}
        </button>
      </div>

      <div class="booking-panel__section booking-row">
        <div>
          <span>选择门店</span>
          <strong>{{ trainer.club }}</strong>
        </div>
        <button class="booking-row__link" type="button" @click="showHint('当前先展示教练所属门店，后续可支持多门店切换')">
          {{ trainer.club }}
          <el-icon><ArrowRight /></el-icon>
        </button>
      </div>

      <div class="booking-panel__section">
        <div class="section-head">
          <h3>选择日期</h3>
        </div>
        <div class="booking-date-grid">
          <button
            v-for="item in trainer.availableDates"
            :key="item.key"
            class="booking-date"
            :class="{ 'is-active': selectedDateKey === item.key }"
            type="button"
            @click="selectedDateKey = item.key"
          >
            <strong>{{ item.label }}</strong>
            <span>{{ item.subLabel }}</span>
            <small v-if="item.moreLabel">{{ item.moreLabel }}</small>
          </button>
        </div>
      </div>

      <div class="booking-panel__section">
        <div class="section-head">
          <h3>选择时间</h3>
        </div>
        <div class="booking-time-grid">
          <button
            v-for="time in selectedDate.times"
            :key="time"
            class="booking-time"
            :class="{ 'is-active': selectedTime === time }"
            type="button"
            @click="selectedTime = time"
          >
            {{ time }}
          </button>
        </div>
      </div>

      <label class="booking-note">
        <span>备注（选填）</span>
        <textarea v-model="note" rows="3" placeholder="如有特殊需求，请留言给教练"></textarea>
      </label>
    </section>

    <footer class="surface-card booking-footer">
      <div class="booking-footer__summary">
        <div>
          <span>剩余课包</span>
          <strong>{{ bookingPage.remainingSessions }} 节</strong>
        </div>
        <div class="booking-footer__total">
          <span>合计</span>
          <strong>¥{{ selectedSession.price }}</strong>
        </div>
      </div>
      <button class="primary-button" type="button" @click="confirmBooking">确认预约</button>
    </footer>
  </div>
</template>

<script setup lang="ts">
import TrainerArtwork from '@/components/TrainerArtwork.vue'
import { useReservationStore } from '@/stores/reservationStore'
import { ArrowLeft, ArrowRight, StarFilled } from '@element-plus/icons-vue'
import { ElMessage } from 'element-plus'
import { computed, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'

const route = useRoute()
const router = useRouter()
const store = useReservationStore()

const trainerId = computed(() => Number(route.params.id))
const bookingPage = computed(() => store.bookingPages[trainerId.value] ?? null)
const trainer = computed(() => bookingPage.value?.trainer ?? null)

const selectedSessionId = ref<number | null>(null)
const selectedDateKey = ref('')
const selectedTime = ref('')
const note = ref('')

watch(
  trainer,
  (value) => {
    if (!value) return
    selectedSessionId.value = value.sessionTypes[0]?.id ?? null
    selectedDateKey.value = value.availableDates[0]?.key ?? ''
    selectedTime.value = value.availableDates[0]?.times[0] ?? ''
  },
  { immediate: true }
)

const selectedSession = computed(
  () => trainer.value?.sessionTypes.find((item) => item.id === selectedSessionId.value) ?? null
)
const selectedDate = computed(
  () => trainer.value?.availableDates.find((item) => item.key === selectedDateKey.value) ?? null
)

watch(selectedDate, (value) => {
  if (!value) {
    selectedTime.value = ''
    return
  }

  if (!value.times.includes(selectedTime.value)) {
    selectedTime.value = value.times[0] ?? ''
  }
})

onMounted(async () => {
  try {
    await store.loadBookingPage(trainerId.value)
  } catch {
    ElMessage.error('预约页数据加载失败')
  }
})

const showHint = (message: string): void => {
  ElMessage.success(message)
}

const confirmBooking = async (): Promise<void> => {
  if (!trainer.value || !selectedSession.value || !selectedDate.value || !selectedTime.value) {
    ElMessage.warning('请先完整选择课程时间')
    return
  }

  try {
    const result = await store.bookTrainer({
      trainerId: trainer.value.id,
      sessionTypeId: selectedSession.value.id,
      calendarDate: selectedDate.value.key,
      time: selectedTime.value,
      note: note.value.trim()
    })

    ElMessage.success(`已预约 ${trainer.value.name} · ${selectedDate.value.label} ${selectedTime.value}`)
    await router.push({
      name: 'reservation-flow',
      query: {
        scene: 'success',
        reservationId: String(result.reservationId)
      }
    })
  } catch {
    ElMessage.error('预约创建失败，请稍后重试')
  }
}
</script>

<style scoped>
.booking-page {
  padding-top: 2px;
  background: linear-gradient(180deg, #ffffff 0%, #f8faf9 100%);
}

.booking-header {
  margin-bottom: 12px;
}

.booking-header__spacer {
  width: 30px;
}

.booking-summary {
  display: grid;
  grid-template-columns: 72px minmax(0, 1fr);
  gap: 10px;
  padding: 10px;
  margin-bottom: 10px;
}

.booking-summary :deep(.trainer-art--thumb) {
  width: 72px;
  height: 88px;
  border-radius: 14px;
}

.booking-summary__body {
  min-width: 0;
}

.booking-summary__top {
  display: flex;
  justify-content: space-between;
  gap: 8px;
}

.booking-summary__name-row {
  display: flex;
  align-items: center;
  gap: 4px;
  flex-wrap: wrap;
}

.booking-summary__name-row strong {
  font-size: 18px;
}

.booking-summary__score {
  display: inline-flex;
  align-items: center;
  gap: 3px;
  color: var(--orange);
  font-size: 11px;
  font-weight: 700;
}

.booking-summary__price {
  color: var(--orange);
  font-size: 18px;
  font-weight: 800;
  white-space: nowrap;
}

.booking-summary__price small {
  font-size: 11px;
}

.booking-summary small:last-child {
  display: block;
  margin-top: 6px;
  color: #697281;
  font-size: 11px;
  line-height: 1.45;
}

.booking-panel {
  padding: 14px;
}

.booking-type-grid,
.booking-date-grid,
.booking-time-grid {
  display: grid;
  gap: 8px;
}

.booking-type-grid {
  grid-template-columns: repeat(3, minmax(0, 1fr));
}

.booking-type {
  height: 36px;
  border: 1px solid rgba(15, 23, 42, 0.05);
  border-radius: 10px;
  background: #f5f6f7;
  color: #636b77;
  font-size: 12px;
  font-weight: 700;
}

.booking-type.is-active,
.booking-date.is-active,
.booking-time.is-active {
  color: var(--brand-deep);
  border-color: rgba(15, 138, 67, 0.22);
  background: var(--brand-soft);
}

.booking-panel__section {
  margin-top: 16px;
}

.booking-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 10px;
  padding: 12px 0 4px;
}

.booking-row span,
.booking-row strong {
  display: block;
}

.booking-row span {
  color: var(--muted);
  font-size: 11px;
}

.booking-row strong {
  margin-top: 4px;
  font-size: 13px;
}

.booking-row__link {
  border: 0;
  display: inline-flex;
  align-items: center;
  gap: 4px;
  color: #4b5563;
  background: transparent;
  font-size: 12px;
  font-weight: 700;
}

.booking-date-grid {
  grid-template-columns: repeat(4, minmax(0, 1fr));
}

.booking-date {
  padding: 10px 6px;
  border: 1px solid rgba(15, 23, 42, 0.05);
  border-radius: 10px;
  background: #f7f8f8;
  text-align: center;
}

.booking-date strong,
.booking-date span,
.booking-date small {
  display: block;
}

.booking-date strong {
  font-size: 12px;
}

.booking-date span,
.booking-date small {
  margin-top: 3px;
  color: var(--muted);
  font-size: 10px;
}

.booking-time-grid {
  grid-template-columns: repeat(4, minmax(0, 1fr));
}

.booking-time {
  height: 36px;
  border: 1px solid rgba(15, 23, 42, 0.05);
  border-radius: 9px;
  background: #f7f8f8;
  color: #59616c;
  font-size: 12px;
  font-weight: 700;
}

.booking-note {
  display: grid;
  gap: 8px;
  margin-top: 16px;
}

.booking-note span {
  font-size: 12px;
  font-weight: 700;
}

.booking-note textarea {
  width: 100%;
  resize: none;
  border: 0;
  outline: 0;
  border-radius: 12px;
  padding: 12px;
  background: #f5f6f7;
  color: var(--ink);
  font-size: 12px;
}

.booking-footer {
  margin-top: 12px;
  padding: 12px 14px 14px;
}

.booking-footer__summary {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  margin-bottom: 12px;
}

.booking-footer__summary span,
.booking-footer__summary strong {
  display: block;
}

.booking-footer__summary span {
  color: var(--muted);
  font-size: 10px;
}

.booking-footer__summary strong {
  margin-top: 4px;
  font-size: 16px;
}

.booking-footer__total {
  text-align: right;
}

.booking-footer__total strong {
  color: var(--orange);
  font-size: 20px;
  font-weight: 800;
}

.booking-footer .primary-button {
  width: 100%;
}
</style>
