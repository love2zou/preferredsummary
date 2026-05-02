<template>
  <div class="app-page home-page">
    <header class="page-header home-header">
      <button class="home-pill home-location" type="button" @click="showHint('当前默认展示上海门店，可继续接门店切换能力')">
        <el-icon><Location /></el-icon>
        <span>{{ user?.city || '上海' }}</span>
        <el-icon class="home-location__caret"><ArrowDown /></el-icon>
      </button>

      <button class="home-search" type="button" @click="router.push({ name: 'trainers' })">
        <el-icon><Search /></el-icon>
        <span>搜索教练 / 课程 / 门店</span>
      </button>

      <button class="icon-button home-notice" type="button" @click="showHint('消息中心稍后接入，这里先保留提醒入口')">
        <el-icon><Bell /></el-icon>
      </button>
    </header>

    <section class="surface-card hero-card">
      <div class="hero-card__copy">
        <p class="hero-card__greeting">Hi，{{ user?.name || '张小力' }} <span>👋</span></p>
        <h1>目标：{{ featuredTrainer?.highlight || '减脂塑形' }}</h1>
        <span>{{ welcomeNote }}</span>
      </div>

      <TrainerArtwork
        v-if="featuredTrainer"
        :name="featuredTrainer.name"
        :tone="featuredTrainer.heroTone"
        :accent="featuredTrainer.accentTone"
        :photo-url="featuredTrainer.photoUrl"
        variant="card"
      />
    </section>

    <section class="surface-card summary-card">
      <article class="summary-card__cell">
        <span>剩余私教课时</span>
        <strong>{{ user?.remainingSessions || 0 }}</strong>
        <small>节</small>
      </article>

      <article class="summary-card__cell summary-card__cell--next">
        <span>下一节课</span>
        <strong>{{ nextLessonLabel }}</strong>
        <div class="summary-card__coach">
          <div>
            <small>{{ nextTrainer?.name || '先预约一位教练吧' }}</small>
            <p>{{ nextReservation?.club || '世纪大道店' }}</p>
          </div>
          <div class="summary-card__coach-side">
            <img
              v-if="nextTrainer?.photoUrl"
              :src="nextTrainer.photoUrl"
              :alt="nextTrainer.name"
              class="summary-card__coach-avatar"
            />
            <button class="summary-card__jump" type="button" @click="openNextBooking">
              <el-icon><ArrowRight /></el-icon>
            </button>
          </div>
        </div>
      </article>
    </section>

    <section class="quick-grid">
      <button
        v-for="action in actions"
        :key="action.key"
        class="quick-card"
        type="button"
        @click="openAction(action.routeName, action.label)"
      >
        <span class="quick-card__icon" :class="`quick-card__icon--${action.key}`">
          <el-icon><component :is="action.icon" /></el-icon>
        </span>
        <strong>{{ action.label }}</strong>
      </button>
    </section>

    <section class="section-head home-section-head">
      <h3>推荐教练</h3>
      <button class="home-link" type="button" @click="router.push({ name: 'trainers' })">
        更多
        <el-icon><ArrowRight /></el-icon>
      </button>
    </section>

    <section class="coach-grid">
      <article
        v-for="trainer in highlightedTrainers"
        :key="trainer.id"
        class="surface-card coach-card"
        @click="router.push({ name: 'trainer-detail', params: { id: trainer.id } })"
      >
        <TrainerArtwork
          :name="trainer.name"
          :tone="trainer.heroTone"
          :accent="trainer.accentTone"
          :photo-url="trainer.photoUrl"
          variant="thumb"
        />
        <div class="coach-card__main">
          <div class="coach-card__head">
            <strong>{{ trainer.name }}</strong>
            <span>
              <el-icon><StarFilled /></el-icon>
              {{ trainer.rating.toFixed(1) }}
            </span>
          </div>
          <p>{{ trainer.badges[0] }}</p>
          <small>{{ trainer.specialties.join(' / ') }}</small>
          <div class="coach-card__bottom">
            <strong>¥{{ trainer.price }}<span>/节</span></strong>
            <button class="secondary-button" type="button" @click.stop="openBooking(trainer.id)">立即预约</button>
          </div>
        </div>
      </article>
    </section>
  </div>
</template>

<script setup lang="ts">
import TrainerArtwork from '@/components/TrainerArtwork.vue'
import { quickActions } from '@/data/appConstants'
import { useReservationStore } from '@/stores/reservationStore'
import {
  ArrowDown,
  ArrowRight,
  Bell,
  Calendar,
  CollectionTag,
  Location,
  Notebook,
  Search,
  StarFilled,
  TrendCharts
} from '@element-plus/icons-vue'
import { ElMessage } from 'element-plus'
import { storeToRefs } from 'pinia'
import { computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'

const router = useRouter()
const store = useReservationStore()
const { home, user, nextReservation } = storeToRefs(store)

const featuredTrainer = computed(() => home.value?.recommendedTrainers?.[0] || null)
const highlightedTrainers = computed(() => home.value?.recommendedTrainers?.slice(0, 2) || [])
const nextTrainer = computed(() => {
  const reservation = nextReservation.value
  return reservation ? store.getTrainerById(reservation.trainerId) : null
})

const welcomeNote = computed(() => {
  if (nextReservation.value) {
    return '坚持训练，遇见更好的自己！'
  }
  return '坚持训练，遇见更好的自己！'
})

const nextLessonLabel = computed(() => {
  if (!nextReservation.value) return '待安排'
  return `${nextReservation.value.dateLabel} ${nextReservation.value.timeRange.slice(0, 5)}`
})

const iconMap = {
  reserve: Calendar,
  courses: CollectionTag,
  'my-courses': Notebook,
  records: TrendCharts
}

const actions = quickActions.map((item) => ({
  ...item,
  icon: iconMap[item.key as keyof typeof iconMap] ?? Calendar
}))

onMounted(async () => {
  try {
    await store.loadHome()
  } catch {
    ElMessage.error('首页数据加载失败')
  }
})

const showHint = (message: string): void => {
  ElMessage.success(message)
}

const openAction = (routeName: string, label: string): void => {
  if (routeName === 'commerce-center') {
    router.push({ name: routeName, query: { scene: 'market' } })
    return
  }

  if (routeName === 'member-center' && label === '训练记录') {
    router.push({ name: routeName, query: { scene: 'records' } })
    return
  }

  router.push({ name: routeName })
}

const openNextBooking = (): void => {
  if (!nextReservation.value) {
    router.push({ name: 'trainers' })
    return
  }
  router.push({ name: 'reservations' })
}

const openBooking = (trainerId: number): void => {
  router.push({ name: 'booking', params: { id: trainerId } })
}
</script>

<style scoped>
.home-page {
  padding-top: 2px;
  background:
    radial-gradient(circle at top right, rgba(226, 245, 231, 0.82), transparent 24%),
    linear-gradient(180deg, #fcfdfc 0%, #f6f8f7 100%);
}

.home-header {
  gap: 10px;
  margin-bottom: 12px;
}

.home-pill,
.home-search {
  border: 0;
  display: inline-flex;
  align-items: center;
  background: rgba(255, 255, 255, 0.95);
  box-shadow: 0 10px 20px rgba(15, 23, 42, 0.04);
}

.home-location {
  gap: 4px;
  min-width: 68px;
  height: 34px;
  padding: 0 10px;
  border-radius: 999px;
  color: #27313f;
  font-size: 13px;
  font-weight: 700;
}

.home-location__caret {
  color: var(--muted);
  font-size: 12px;
}

.home-search {
  flex: 1;
  min-width: 0;
  gap: 8px;
  height: 34px;
  padding: 0 12px;
  border-radius: 999px;
  color: #a0a6af;
  font-size: 12px;
}

.home-notice {
  position: relative;
}

.home-notice::after {
  content: '';
  position: absolute;
  top: 8px;
  right: 6px;
  width: 7px;
  height: 7px;
  border-radius: 50%;
  background: #ef4444;
  border: 2px solid #fff;
}

.hero-card {
  display: grid;
  grid-template-columns: minmax(0, 1fr) 114px;
  gap: 10px;
  padding: 12px 14px;
  margin-bottom: 12px;
  background:
    radial-gradient(circle at 86% 20%, rgba(255, 255, 255, 0.68), transparent 18%),
    linear-gradient(135deg, #f6fbf7 0%, #eef8f1 48%, #e6f4ea 100%);
  min-height: 108px;
}

.hero-card__copy {
  display: flex;
  flex-direction: column;
  justify-content: center;
  gap: 6px;
}

.hero-card__greeting,
.hero-card__copy h1,
.hero-card__copy span:last-child {
  margin: 0;
}

.hero-card__greeting {
  font-size: 14px;
  font-weight: 800;
  letter-spacing: -0.01em;
}

.hero-card__copy h1 {
  font-size: 15px;
  line-height: 1.3;
}

.hero-card__copy span:last-child {
  color: #95a0ad;
  font-size: 10px;
  line-height: 1.45;
}

.summary-card {
  display: grid;
  grid-template-columns: 0.9fr 1.1fr;
  margin-bottom: 14px;
  padding: 0;
  overflow: hidden;
}

.summary-card__cell {
  padding: 14px 14px 12px;
}

.summary-card__cell + .summary-card__cell {
  border-left: 1px solid rgba(15, 23, 42, 0.06);
}

.summary-card__cell span,
.summary-card__cell small,
.summary-card__coach p {
  color: var(--muted);
}

.summary-card__cell span {
  display: block;
  font-size: 11px;
}

.summary-card__cell strong {
  display: inline-block;
  margin-top: 8px;
  font-size: 20px;
  font-weight: 800;
}

.summary-card__cell > small {
  display: block;
  margin-top: 2px;
  font-size: 11px;
}

.summary-card__cell--next strong {
  font-size: 16px;
  letter-spacing: -0.02em;
}

.summary-card__coach {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 10px;
  margin-top: 8px;
}

.summary-card__coach-side {
  display: flex;
  align-items: center;
  gap: 8px;
}

.summary-card__coach-avatar {
  width: 28px;
  height: 28px;
  border-radius: 50%;
  object-fit: cover;
  border: 2px solid #fff;
  box-shadow: 0 4px 10px rgba(15, 23, 42, 0.08);
}

.summary-card__coach small,
.summary-card__coach p {
  display: block;
  margin: 0;
}

.summary-card__coach small {
  color: #26303d;
  font-size: 12px;
  font-weight: 700;
}

.summary-card__coach p {
  margin-top: 3px;
  font-size: 10px;
}

.summary-card__jump {
  width: 28px;
  height: 28px;
  border: 0;
  border-radius: 50%;
  color: var(--brand-deep);
  background: #eefaf1;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
}

.quick-grid {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: 8px;
  margin-bottom: 18px;
}

.quick-card {
  border: 0;
  border-radius: 16px;
  padding: 10px 4px 8px;
  background: rgba(255, 255, 255, 0.98);
  box-shadow: 0 10px 20px rgba(15, 23, 42, 0.03);
  display: grid;
  justify-items: center;
  gap: 5px;
}

.quick-card__icon {
  width: 34px;
  height: 34px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  border-radius: 12px;
  color: #fff;
  font-size: 16px;
}

.quick-card__icon--reserve {
  background: linear-gradient(135deg, #67c87f, #16a34a);
}

.quick-card__icon--courses {
  background: linear-gradient(135deg, #ff9667, #ff7a21);
}

.quick-card__icon--my-courses {
  background: linear-gradient(135deg, #57d4c6, #2eb6aa);
}

.quick-card__icon--records {
  background: linear-gradient(135deg, #ffbf61, #ffa22d);
}

.quick-card strong {
  font-size: 11px;
  font-weight: 700;
}

.home-section-head {
  margin-bottom: 10px;
}

.home-link {
  border: 0;
  display: inline-flex;
  align-items: center;
  gap: 3px;
  color: var(--muted);
  background: transparent;
  font-size: 12px;
  font-weight: 700;
}

.coach-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 10px;
}

.coach-card {
  padding: 10px;
  display: grid;
  gap: 8px;
}

.coach-card :deep(.trainer-art--thumb) {
  width: 100%;
  height: 84px;
  border-radius: 12px;
}

.coach-card__main {
  min-width: 0;
}

.coach-card__head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
}

.coach-card__head strong {
  font-size: 15px;
}

.coach-card__head span {
  display: inline-flex;
  align-items: center;
  gap: 3px;
  color: var(--orange);
  font-size: 11px;
  font-weight: 800;
}

.coach-card p,
.coach-card small {
  display: block;
}

.coach-card p {
  margin: 4px 0 0;
  color: #394453;
  font-size: 11px;
  font-weight: 700;
}

.coach-card small {
  margin-top: 4px;
  color: var(--muted);
  font-size: 9px;
  line-height: 1.4;
}

.coach-card__bottom {
  display: grid;
  gap: 8px;
  margin-top: 10px;
}

.coach-card__bottom strong {
  color: #252f3c;
  font-size: 16px;
  font-weight: 800;
  letter-spacing: -0.03em;
}

.coach-card__bottom strong span {
  font-size: 11px;
  color: var(--muted);
}

.coach-card__bottom .secondary-button {
  width: 100%;
  padding: 8px 0;
  font-size: 11px;
}
</style>
