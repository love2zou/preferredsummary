<template>
  <div v-if="trainer" class="trainer-detail-page">
    <section class="trainer-detail__hero">
      <TrainerArtwork
        :name="trainer.name"
        :tone="trainer.heroTone"
        :accent="trainer.accentTone"
        :photo-url="trainer.photoUrl"
        variant="hero"
      />

      <div class="trainer-detail__overlay">
        <button class="icon-button icon-button--glass" type="button" @click="router.back()">
          <el-icon><ArrowLeft /></el-icon>
        </button>
        <button class="icon-button icon-button--glass" type="button" @click="shareTrainer">
          <el-icon><Share /></el-icon>
        </button>
      </div>
    </section>

    <div class="app-page trainer-detail__body">
      <section class="surface-card trainer-summary">
        <div class="trainer-summary__head">
          <div>
            <div class="trainer-summary__name">
              <h1>{{ trainer.name }}</h1>
              <span class="tag tag--brand">{{ trainer.badges[0] }}</span>
            </div>
            <div class="trainer-summary__rating">
              <el-icon><StarFilled /></el-icon>
              <span>{{ trainer.rating.toFixed(1) }}</span>
              <small>（{{ trainer.reviewCount }}条评价）</small>
            </div>
          </div>

          <button class="trainer-summary__follow" type="button" :class="{ 'is-active': followed }" @click="toggleFollow">
            <el-icon><Star /></el-icon>
            {{ followed ? '已关注' : '关注' }}
          </button>
        </div>

        <div class="trainer-summary__stats">
          <article>
            <strong>{{ trainer.years }}年</strong>
            <span>教龄</span>
          </article>
          <article>
            <strong>{{ trainer.servedClients }}人</strong>
            <span>服务人数</span>
          </article>
          <article>
            <strong>{{ trainer.satisfaction }}%</strong>
            <span>好评率</span>
          </article>
          <article>
            <strong>¥{{ trainer.price }}/节</strong>
            <span>课程价格</span>
          </article>
        </div>
      </section>

      <section class="surface-card detail-card">
        <div class="section-head">
          <h3>擅长方向</h3>
          <button class="detail-link" type="button" @click="showHint('后续可以接更多标签筛选和教练对比')">
            <el-icon><ArrowRight /></el-icon>
          </button>
        </div>
        <div class="detail-chip-row">
          <span v-for="item in trainer.specialties" :key="item" class="chip">{{ item }}</span>
        </div>
      </section>

      <section class="surface-card detail-card">
        <div class="section-head">
          <h3>教练简介</h3>
        </div>
        <p class="detail-copy">{{ trainer.introduction }}</p>
        <p v-if="showFullIntro" class="detail-copy detail-copy--sub">{{ trainer.story }}</p>
        <button class="detail-expand" type="button" @click="showFullIntro = !showFullIntro">
          {{ showFullIntro ? '收起' : '展开' }}
        </button>
      </section>

      <section class="surface-card detail-card">
        <div class="section-head">
          <h3>资格认证</h3>
        </div>
        <div class="cert-grid">
          <span v-for="item in trainer.certifications" :key="item">{{ item }}</span>
        </div>
      </section>

      <section class="surface-card detail-card">
        <div class="section-head">
          <h3>用户评价（{{ trainer.reviewCount }}）</h3>
          <button class="detail-text-link" type="button" @click="showHint('评价列表已接局部展示，完整列表可继续补分页')">
            查看全部
          </button>
        </div>

        <article v-for="review in trainer.reviews" :key="review.id" class="trainer-review">
          <div class="trainer-review__head">
            <div>
              <strong>{{ review.author }}</strong>
              <div class="trainer-review__score">
                <el-icon><StarFilled /></el-icon>
                {{ review.rating.toFixed(1) }}
              </div>
            </div>
            <span class="tag tag--orange">{{ review.tag }}</span>
          </div>
          <p>{{ review.content }}</p>
        </article>
      </section>
    </div>

    <footer class="trainer-detail__footer">
      <button class="ghost-button trainer-detail__ghost" type="button" @click="showHint('在线咨询入口先保留，后续可接 IM 能力')">
        <el-icon><ChatDotRound /></el-icon>
        在线咨询
      </button>
      <button
        class="primary-button trainer-detail__book"
        type="button"
        @click="router.push({ name: 'booking', params: { id: trainer.id } })"
      >
        立即预约
      </button>
    </footer>
  </div>
</template>

<script setup lang="ts">
import TrainerArtwork from '@/components/TrainerArtwork.vue'
import { useReservationStore } from '@/stores/reservationStore'
import { ArrowLeft, ArrowRight, ChatDotRound, Share, Star, StarFilled } from '@element-plus/icons-vue'
import { ElMessage } from 'element-plus'
import { computed, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'

const FOLLOW_STORAGE_KEY = 'reservation-ui-followed-trainers'

const route = useRoute()
const router = useRouter()
const store = useReservationStore()

const trainerId = computed(() => Number(route.params.id))
const trainer = computed(() => store.trainerDetails[trainerId.value] ?? null)
const showFullIntro = ref(false)
const followedIds = ref<number[]>([])

const readFollowed = (): number[] => {
  try {
    const raw = localStorage.getItem(FOLLOW_STORAGE_KEY)
    return raw ? (JSON.parse(raw) as number[]) : []
  } catch {
    return []
  }
}

const persistFollowed = (): void => {
  localStorage.setItem(FOLLOW_STORAGE_KEY, JSON.stringify(followedIds.value))
}

const followed = computed(() => followedIds.value.includes(trainerId.value))

onMounted(async () => {
  followedIds.value = readFollowed()
  try {
    await store.loadTrainerDetail(trainerId.value)
  } catch {
    ElMessage.error('教练详情加载失败')
  }
})

const toggleFollow = (): void => {
  if (followed.value) {
    followedIds.value = followedIds.value.filter((id) => id !== trainerId.value)
    ElMessage.success('已取消关注')
  } else {
    followedIds.value = followedIds.value.concat(trainerId.value)
    ElMessage.success('已关注该教练')
  }
  persistFollowed()
}

const shareTrainer = (): void => {
  ElMessage.success('分享入口已预留，后续可接微信或链接分享')
}

const showHint = (message: string): void => {
  ElMessage.success(message)
}
</script>

<style scoped>
.trainer-detail-page {
  min-height: 100%;
  background: #f5f7f6;
  position: relative;
  padding-bottom: 100px;
}

.trainer-detail__hero {
  position: relative;
  overflow: hidden;
}

.trainer-detail__overlay {
  position: absolute;
  inset: 12px 16px auto;
  display: flex;
  justify-content: space-between;
}

.icon-button--glass {
  color: #fff;
  background: rgba(15, 23, 42, 0.28);
  box-shadow: none;
}

.trainer-detail__body {
  position: relative;
  z-index: 1;
  margin-top: -16px;
}

.trainer-summary {
  padding: 16px;
  border-top-left-radius: 22px;
  border-top-right-radius: 22px;
}

.trainer-summary__head {
  display: flex;
  justify-content: space-between;
  gap: 12px;
}

.trainer-summary__name {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
}

.trainer-summary__name h1 {
  margin: 0;
  font-size: 19px;
}

.trainer-summary__rating {
  display: flex;
  align-items: center;
  gap: 4px;
  margin-top: 6px;
  color: var(--orange);
  font-size: 13px;
  font-weight: 700;
}

.trainer-summary__rating small {
  color: var(--muted);
  font-weight: 500;
}

.trainer-summary__follow {
  height: 32px;
  padding: 0 12px;
  border: 1px solid rgba(15, 23, 42, 0.06);
  border-radius: 999px;
  background: #fff;
  color: var(--muted);
  display: inline-flex;
  align-items: center;
  gap: 4px;
}

.trainer-summary__follow.is-active {
  color: var(--brand-deep);
  border-color: rgba(15, 138, 67, 0.18);
  background: var(--brand-soft);
}

.trainer-summary__stats {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: 6px;
  margin-top: 14px;
  padding-top: 14px;
  border-top: 1px solid rgba(15, 23, 42, 0.06);
}

.trainer-summary__stats article {
  text-align: center;
}

.trainer-summary__stats strong,
.trainer-summary__stats span {
  display: block;
}

.trainer-summary__stats strong {
  font-size: 15px;
}

.trainer-summary__stats span {
  margin-top: 4px;
  color: var(--muted);
  font-size: 10px;
}

.detail-card {
  margin-top: 10px;
  padding: 14px 16px;
}

.detail-link,
.detail-text-link,
.detail-expand {
  border: 0;
  background: transparent;
}

.detail-link {
  width: 26px;
  height: 26px;
  border-radius: 50%;
  color: var(--muted);
  display: inline-flex;
  align-items: center;
  justify-content: center;
}

.detail-chip-row {
  display: flex;
  gap: 8px;
  flex-wrap: wrap;
}

.detail-chip-row .chip {
  border: 0;
  background: #f5f6f7;
  color: #56616d;
}

.detail-copy {
  margin: 0;
  color: #3f4954;
  font-size: 13px;
  line-height: 1.72;
}

.detail-copy--sub {
  margin-top: 8px;
  color: var(--muted);
}

.detail-expand {
  margin-top: 10px;
  padding: 0;
  color: var(--brand-deep);
  font-size: 12px;
  font-weight: 700;
}

.cert-grid {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 10px;
}

.cert-grid span {
  padding: 12px 8px;
  border-radius: 12px;
  background: #f6f7f8;
  text-align: center;
  font-size: 11px;
  font-weight: 800;
  letter-spacing: 0.06em;
}

.detail-text-link {
  color: var(--muted);
  font-size: 12px;
  font-weight: 700;
}

.trainer-review + .trainer-review {
  margin-top: 14px;
  padding-top: 14px;
  border-top: 1px solid rgba(15, 23, 42, 0.05);
}

.trainer-review__head {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 8px;
}

.trainer-review__head strong {
  font-size: 13px;
}

.trainer-review__score {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  margin-top: 5px;
  color: var(--orange);
  font-size: 11px;
  font-weight: 700;
}

.trainer-review p {
  margin: 8px 0 0;
  color: #4b5563;
  font-size: 12px;
  line-height: 1.65;
}

.trainer-detail__footer {
  position: absolute;
  left: 16px;
  right: 16px;
  bottom: 16px;
  display: grid;
  grid-template-columns: 134px minmax(0, 1fr);
  gap: 10px;
}

.trainer-detail__ghost {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 5px;
}

.trainer-detail__book {
  width: 100%;
}
</style>
