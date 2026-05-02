<template>
  <div class="app-page trainer-list-page">
    <header class="page-header trainer-list-header">
      <button class="icon-button" type="button" @click="router.push({ name: 'home' })">
        <el-icon><ArrowLeft /></el-icon>
      </button>

      <label class="trainer-search">
        <el-icon><Search /></el-icon>
        <input v-model="keyword" type="text" placeholder="搜索教练 / 擅长方向" />
      </label>

      <button class="icon-button" type="button" @click="showHint('筛选抽屉可继续补充，当前先保留顶部快捷筛选')">
        <el-icon><Filter /></el-icon>
      </button>
    </header>

    <section class="filter-strip">
      <div class="chip-row">
        <button
          v-for="item in clubOptions"
          :key="item.label"
          class="chip"
          :class="{ 'is-active': clubFilter === item.value }"
          type="button"
          @click="clubFilter = item.value"
        >
          {{ item.label }}
        </button>
      </div>

      <div class="chip-row">
        <button
          v-for="item in goalOptions"
          :key="item"
          class="chip"
          :class="{ 'is-active': goalFilter === item }"
          type="button"
          @click="goalFilter = item"
        >
          {{ item }}
        </button>

        <button
          v-for="item in genderOptions"
          :key="item.label"
          class="chip"
          :class="{ 'is-active': genderFilter === item.value }"
          type="button"
          @click="genderFilter = item.value"
        >
          {{ item.label }}
        </button>

        <button
          v-for="item in timeOptions"
          :key="item"
          class="chip"
          :class="{ 'is-active': timeFilter === item }"
          type="button"
          @click="timeFilter = item"
        >
          {{ item }}
        </button>

        <button
          v-for="item in priceOptions"
          :key="item"
          class="chip"
          :class="{ 'is-active': priceFilter === item }"
          type="button"
          @click="priceFilter = item"
        >
          {{ item }}
        </button>
      </div>
    </section>

    <section class="trainer-list">
      <article
        v-for="trainer in visibleTrainers"
        :key="trainer.id"
        class="surface-card trainer-item"
        @click="router.push({ name: 'trainer-detail', params: { id: trainer.id } })"
      >
        <TrainerArtwork
          :name="trainer.name"
          :tone="trainer.heroTone"
          :accent="trainer.accentTone"
          :photo-url="trainer.photoUrl"
          variant="thumb"
        />

        <div class="trainer-item__content">
          <div class="trainer-item__top">
            <div class="trainer-item__title">
              <div class="trainer-item__name-row">
                <strong>{{ trainer.name }}</strong>
                <div class="trainer-item__badges">
                  <span
                    v-for="badge in trainer.badges.slice(0, 2)"
                    :key="badge"
                    class="tag"
                    :class="badgeClass(badge)"
                  >
                    {{ badge }}
                  </span>
                </div>
              </div>

              <div class="trainer-item__rating">
                <el-icon><StarFilled /></el-icon>
                <span>{{ trainer.rating.toFixed(1) }}</span>
                <small>{{ trainer.reviewCount }}条评价</small>
              </div>
            </div>

            <div class="trainer-item__price">¥{{ trainer.price }}<small>/节</small></div>
          </div>

          <p>{{ trainer.goals.join(' / ') }} / {{ trainer.specialties.join(' / ') }}</p>

          <div class="trainer-item__metrics">
            <span>{{ trainer.years }}年</span>
            <span>教龄</span>
            <span>{{ trainer.servedClients }}</span>
            <span>服务人数</span>
          </div>

          <div class="trainer-item__slots">
            <span v-for="slot in trainer.nextSlots.slice(0, 3)" :key="slot">{{ slot }}</span>
          </div>
        </div>

        <div class="trainer-item__action">
          <button class="secondary-button" type="button" @click.stop="openBooking(trainer.id)">预约</button>
        </div>
      </article>

      <div v-if="!visibleTrainers.length" class="surface-card trainer-empty">
        <strong>当前筛选下暂无可预约教练</strong>
        <span>可以切换目标、时间或价格继续看看。</span>
      </div>
    </section>
  </div>
</template>

<script setup lang="ts">
import TrainerArtwork from '@/components/TrainerArtwork.vue'
import { useReservationStore } from '@/stores/reservationStore'
import { ArrowLeft, Filter, Search, StarFilled } from '@element-plus/icons-vue'
import { ElMessage } from 'element-plus'
import { storeToRefs } from 'pinia'
import { computed, onMounted, ref, watch } from 'vue'
import { useRouter } from 'vue-router'

const router = useRouter()
const store = useReservationStore()
const { trainers, clubs } = storeToRefs(store)

const keyword = ref('')
const clubFilter = ref<number | undefined>(undefined)
const goalFilter = ref<string>('目标')
const genderFilter = ref<string | undefined>(undefined)
const timeFilter = ref('时间')
const priceFilter = ref('价格')

const goalOptions = ['目标', '减脂', '增肌', '体能提升']
const genderOptions = [
  { label: '性别', value: undefined },
  { label: '男', value: '男' },
  { label: '女', value: '女' }
]
const timeOptions = ['时间', '今天可约', '明天可约']
const priceOptions = ['价格', '300以下', '300以上']

const clubOptions = ref([{ label: '门店', value: undefined as number | undefined }])

const reloadTrainers = async (): Promise<void> => {
  try {
    await store.loadTrainers({
      keyword: keyword.value || undefined,
      clubId: clubFilter.value,
      goal: goalFilter.value === '目标' ? undefined : goalFilter.value,
      gender: genderFilter.value
    })
  } catch {
    ElMessage.error('教练列表加载失败')
  }
}

const visibleTrainers = computed(() => {
  return trainers.value.filter((trainer) => {
    if (timeFilter.value === '今天可约' && !trainer.nextSlots.some((slot) => slot.includes('今天'))) {
      return false
    }

    if (timeFilter.value === '明天可约' && !trainer.nextSlots.some((slot) => slot.includes('明天'))) {
      return false
    }

    if (priceFilter.value === '300以下' && trainer.price > 300) {
      return false
    }

    if (priceFilter.value === '300以上' && trainer.price < 300) {
      return false
    }

    return true
  })
})

watch([keyword, clubFilter, goalFilter, genderFilter], () => {
  void reloadTrainers()
})

onMounted(async () => {
  try {
    await store.loadClubs()
    clubOptions.value = [{ label: '门店', value: undefined }].concat(
      clubs.value.map((item) => ({ label: item.clubName, value: item.id }))
    )
    await reloadTrainers()
  } catch {
    ElMessage.error('筛选数据加载失败')
  }
})

const badgeClass = (badge: string): string => (badge.includes('认证') ? 'tag--orange' : 'tag--brand')

const showHint = (message: string): void => {
  ElMessage.success(message)
}

const openBooking = (trainerId: number): void => {
  router.push({ name: 'booking', params: { id: trainerId } })
}
</script>

<style scoped>
.trainer-list-page {
  padding-top: 2px;
  background: linear-gradient(180deg, #ffffff 0%, #f7f9f8 100%);
}

.trainer-list-header {
  gap: 10px;
  margin-bottom: 12px;
}

.trainer-search {
  flex: 1;
  min-width: 0;
  display: inline-flex;
  align-items: center;
  gap: 8px;
  height: 36px;
  padding: 0 12px;
  border-radius: 999px;
  color: #a0a6af;
  background: #f5f6f7;
}

.trainer-search input {
  width: 100%;
  border: 0;
  outline: 0;
  color: var(--ink);
  background: transparent;
  font-size: 12px;
}

.filter-strip {
  display: grid;
  gap: 8px;
  margin-bottom: 12px;
}

.trainer-list {
  display: grid;
  gap: 10px;
}

.trainer-item {
  display: grid;
  grid-template-columns: 72px minmax(0, 1fr) auto;
  gap: 10px;
  padding: 10px;
  align-items: start;
}

.trainer-item :deep(.trainer-art--thumb) {
  width: 72px;
  height: 96px;
  border-radius: 14px;
}

.trainer-item__content {
  min-width: 0;
}

.trainer-item__top {
  display: flex;
  justify-content: space-between;
  gap: 10px;
}

.trainer-item__name-row {
  display: flex;
  align-items: center;
  gap: 6px;
  flex-wrap: wrap;
}

.trainer-item__title strong {
  font-size: 19px;
  line-height: 1.1;
}

.trainer-item__badges {
  display: flex;
  gap: 4px;
  flex-wrap: wrap;
}

.trainer-item__price {
  padding-top: 26px;
  color: var(--orange);
  font-size: 18px;
  font-weight: 800;
  white-space: nowrap;
}

.trainer-item__price small {
  font-size: 11px;
}

.trainer-item__rating {
  display: flex;
  align-items: center;
  gap: 4px;
  margin-top: 5px;
  color: var(--orange);
  font-size: 11px;
  font-weight: 700;
}

.trainer-item__rating small {
  color: var(--muted);
  font-weight: 500;
}

.trainer-item p {
  margin: 6px 0 0;
  color: #596271;
  font-size: 12px;
  line-height: 1.5;
}

.trainer-item__metrics {
  display: flex;
  gap: 4px;
  flex-wrap: wrap;
  margin-top: 6px;
  color: #98a2b3;
  font-size: 11px;
}

.trainer-item__slots {
  display: flex;
  gap: 6px;
  flex-wrap: wrap;
  margin-top: 9px;
}

.trainer-item__slots span {
  padding: 5px 8px;
  border-radius: 9px;
  color: #5b6470;
  background: #f6f7f8;
  font-size: 10px;
  font-weight: 600;
}

.trainer-item__action {
  display: flex;
  align-items: end;
  min-height: 100%;
}

.trainer-item__action .secondary-button {
  min-width: 56px;
  padding: 7px 0;
  font-size: 11px;
}

.trainer-empty {
  padding: 18px;
  text-align: center;
}

.trainer-empty strong,
.trainer-empty span {
  display: block;
}

.trainer-empty span {
  margin-top: 8px;
  color: var(--muted);
  font-size: 12px;
}
</style>
