<template>
  <div class="app-page reservations-page">
    <header class="page-header reservation-header">
      <div class="page-header__title">
        <strong>我的预约</strong>
      </div>
    </header>

    <div class="reservation-tabs">
      <button
        v-for="item in statusTabs"
        :key="item.key"
        class="reservation-tabs__item"
        :class="{ 'is-active': activeTab === item.key }"
        type="button"
        @click="activeTab = item.key"
      >
        {{ item.label }}
      </button>
    </div>

    <section class="reservation-list">
      <article
        v-for="reservation in currentReservations"
        :key="reservation.id"
        class="surface-card reservation-card"
      >
        <TrainerArtwork
          :name="reservation.trainerName"
          tone="#eef5ef"
          accent="#16a34a"
          :photo-url="reservation.trainerPhotoUrl"
          variant="thumb"
        />

        <div class="reservation-card__content">
          <div class="reservation-card__head">
            <div class="reservation-card__topline">
              <strong>{{ reservation.sessionLabel }}</strong>
              <small>{{ reservation.tag }}</small>
            </div>
            <h3>{{ reservation.dateLabel }} {{ reservation.timeRange }}</h3>
          </div>

          <p>{{ reservation.club }} · {{ reservation.area }}</p>

          <div class="reservation-card__actions">
            <button
              v-if="reservation.status === 'upcoming'"
              class="ghost-button"
              type="button"
              @click="openFlow('cancel', reservation.id)"
            >
              取消预约
            </button>
            <button class="secondary-button" type="button" @click="openFlow('chat', reservation.id)">
              联系教练
            </button>
            <button class="ghost-button" type="button" @click="openFlow(reservation.status === 'completed' ? 'review' : 'checkin', reservation.id)">
              {{ reservation.status === 'completed' ? '去评价' : '签到' }}
            </button>
          </div>
        </div>
      </article>

      <div v-if="!currentReservations.length" class="surface-card reservation-empty">
        <strong>{{ emptyTitle }}</strong>
        <span>{{ emptyText }}</span>
      </div>
    </section>
  </div>
</template>

<script setup lang="ts">
import TrainerArtwork from '@/components/TrainerArtwork.vue'
import { useReservationStore } from '@/stores/reservationStore'
import { ElMessage } from 'element-plus'
import { storeToRefs } from 'pinia'
import { computed, onMounted, ref, watch } from 'vue'
import { useRouter } from 'vue-router'

const router = useRouter()
const store = useReservationStore()
const { upcomingReservations, completedReservations, cancelledReservations } = storeToRefs(store)
const activeTab = ref<'upcoming' | 'completed' | 'cancelled'>('upcoming')

const statusTabs = [
  { key: 'upcoming', label: '待上课' },
  { key: 'completed', label: '已完成' },
  { key: 'cancelled', label: '已取消' }
] as const

const currentReservations = computed(() => {
  if (activeTab.value === 'completed') return completedReservations.value
  if (activeTab.value === 'cancelled') return cancelledReservations.value
  return upcomingReservations.value
})

const emptyTitle = computed(() => {
  if (activeTab.value === 'completed') return '还没有已完成课程'
  if (activeTab.value === 'cancelled') return '还没有取消记录'
  return '还没有待上课预约'
})

const emptyText = computed(() => {
  if (activeTab.value === 'completed') return '完成一节课后，这里会自动沉淀你的训练安排。'
  if (activeTab.value === 'cancelled') return '取消后的预约会出现在这里，方便继续回看。'
  return '去挑一位教练，安排下一次训练吧。'
})

const fetchTab = async (status: 'upcoming' | 'completed' | 'cancelled'): Promise<void> => {
  try {
    await store.loadReservations(status)
  } catch {
    ElMessage.error('预约列表加载失败')
  }
}

watch(activeTab, (value) => {
  void fetchTab(value)
})

onMounted(async () => {
  await fetchTab(activeTab.value)
})

const openFlow = (scene: 'cancel' | 'chat' | 'checkin' | 'review', reservationId: number): void => {
  void router.push({
    name: 'reservation-flow',
    query: {
      scene,
      reservationId: String(reservationId)
    }
  })
}
</script>

<style scoped>
.reservations-page {
  padding-top: 2px;
  background: linear-gradient(180deg, #ffffff 0%, #f8faf9 100%);
}

.reservation-header {
  margin-bottom: 8px;
}

.reservation-tabs {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  margin-bottom: 12px;
  padding-bottom: 2px;
  border-bottom: 1px solid rgba(15, 23, 42, 0.06);
}

.reservation-tabs__item {
  position: relative;
  border: 0;
  padding: 10px 4px 12px;
  color: var(--muted);
  background: transparent;
  font-size: 13px;
  font-weight: 700;
}

.reservation-tabs__item.is-active {
  color: var(--brand-deep);
}

.reservation-tabs__item.is-active::after {
  content: '';
  position: absolute;
  left: 50%;
  bottom: -1px;
  width: 28px;
  height: 3px;
  border-radius: 999px;
  background: var(--brand);
  transform: translateX(-50%);
}

.reservation-list {
  display: grid;
  gap: 10px;
}

.reservation-card {
  display: grid;
  grid-template-columns: 72px minmax(0, 1fr);
  gap: 10px;
  padding: 10px;
}

.reservation-card :deep(.trainer-art--thumb) {
  width: 72px;
  height: 90px;
  border-radius: 14px;
}

.reservation-card__content {
  min-width: 0;
}

.reservation-card__topline {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 10px;
}

.reservation-card__topline strong {
  font-size: 13px;
}

.reservation-card__topline small {
  color: var(--orange);
  font-size: 10px;
  font-weight: 700;
  white-space: nowrap;
}

.reservation-card__head h3 {
  margin: 6px 0 0;
  font-size: 28px;
  line-height: 1.1;
  letter-spacing: -0.03em;
}

.reservation-card p {
  margin: 8px 0 0;
  color: var(--muted);
  font-size: 11px;
}

.reservation-card__actions {
  display: flex;
  gap: 8px;
  flex-wrap: wrap;
  margin-top: 12px;
}

.reservation-card__actions .ghost-button,
.reservation-card__actions .secondary-button {
  padding: 8px 12px;
  font-size: 11px;
}

.reservation-empty {
  padding: 18px;
  text-align: center;
}

.reservation-empty strong,
.reservation-empty span {
  display: block;
}

.reservation-empty span {
  margin-top: 8px;
  color: var(--muted);
  font-size: 12px;
  line-height: 1.6;
}
</style>
