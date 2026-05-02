<template>
  <div class="app-page courses-page">
    <header class="page-header">
      <div class="page-header__title">
        <strong>我的课程</strong>
        <span>会员课包与预约入口</span>
      </div>
      <button class="icon-button" type="button" @click="router.push({ name: 'commerce-center' })">
        <el-icon><ShoppingCart /></el-icon>
      </button>
    </header>

    <div class="course-tabs">
      <button
        v-for="item in tabs"
        :key="item.key"
        class="course-tabs__item"
        :class="{ 'is-active': activeTab === item.key }"
        type="button"
        @click="activeTab = item.key"
      >
        {{ item.label }}
      </button>
    </div>

    <section v-if="activeTab === 'courses'" class="course-stack">
      <article v-for="item in coursePackages" :key="item.name" class="surface-card course-package-card">
        <div class="course-package-card__head">
          <div>
            <strong>{{ item.name }}</strong>
            <small>{{ item.status }}</small>
          </div>
          <span class="status-pill" :class="item.statusClass">{{ item.badge }}</span>
        </div>

        <div class="course-package-card__metrics">
          <div>
            <span>剩余</span>
            <strong>{{ item.remaining }} 节</strong>
          </div>
          <div>
            <span>有效期至</span>
            <strong>{{ item.expireAt }}</strong>
          </div>
        </div>

        <div class="course-package-card__actions">
          <button class="primary-button" type="button" @click="router.push({ name: 'trainers' })">去预约</button>
          <button class="ghost-button" type="button" @click="router.push({ name: 'profile' })">查看详情</button>
        </div>
      </article>

      <div v-if="!coursePackages.length" class="surface-card empty-card">
        <strong>当前还没有可用课包信息</strong>
        <span>可以先到课程商城购买，或联系门店确认会员权益。</span>
      </div>
    </section>

    <section v-else-if="activeTab === 'orders'" class="surface-card order-card">
      <div class="section-head">
        <h3>课程订单</h3>
        <button class="header-link" type="button" @click="router.push({ name: 'commerce-center', query: { scene: 'orders' } })">
          查看全部
        </button>
      </div>

      <article v-for="order in orders" :key="order.no" class="order-card__item">
        <div>
          <strong>{{ order.name }}</strong>
          <small>{{ order.time }}</small>
        </div>
        <div class="order-card__side">
          <span :class="['status-pill', order.statusClass]">{{ order.status }}</span>
          <button class="ghost-button order-card__btn" type="button" @click="router.push({ name: 'commerce-center', query: { scene: 'order-detail' } })">
            详情
          </button>
        </div>
      </article>

      <div v-if="!orders.length" class="empty-inline">
        还没有课程订单，可以先去商城看看课包。
      </div>
    </section>

    <section v-else class="surface-card reminder-card">
      <div class="section-head">
        <h3>到期提醒</h3>
        <button class="header-link" type="button" @click="router.push({ name: 'commerce-center', query: { scene: 'coupon' } })">
          优惠券
        </button>
      </div>

      <article v-for="reminder in reminders" :key="reminder.title" class="reminder-card__item">
        <span class="reminder-card__icon">{{ reminder.icon }}</span>
        <div>
          <strong>{{ reminder.title }}</strong>
          <p>{{ reminder.content }}</p>
        </div>
      </article>

      <div v-if="!reminders.length" class="empty-inline">
        当前没有新的课程提醒。
      </div>

      <button class="primary-button" type="button" @click="router.push({ name: 'commerce-center', query: { scene: 'market' } })">
        购买新课包
      </button>
    </section>
  </div>
</template>

<script setup lang="ts">
import { ShoppingCart } from '@element-plus/icons-vue'
import { ElMessage } from 'element-plus'
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/authStore'
import { useReservationStore } from '@/stores/reservationStore'
import { reservationPortalApi, type ReservationCommerceData } from '@/api/reservationPortal'

const router = useRouter()
const authStore = useAuthStore()
const reservationStore = useReservationStore()
const activeTab = ref<'courses' | 'orders' | 'reminders'>('courses')
const commerceData = ref<ReservationCommerceData | null>(null)

const tabs = [
  { key: 'courses', label: '我的课程' },
  { key: 'orders', label: '课程订单' },
  { key: 'reminders', label: '到期提醒' }
] as const

const statusLabelMap: Record<string, string> = {
  PendingPayment: '待支付',
  Paid: '已支付',
  Completed: '已完成',
  Closed: '已关闭'
}

const statusClassMap: Record<string, string> = {
  PendingPayment: 'status-pill--warning',
  Paid: 'status-pill--success',
  Completed: 'status-pill--muted',
  Closed: 'status-pill--muted'
}

const profileUser = computed(() => reservationStore.profile?.user ?? null)
const nextReservation = computed(() => reservationStore.home?.nextReservation ?? null)
const availableCoupons = computed(() =>
  (commerceData.value?.coupons ?? []).filter((item) => item.statusCode === 'Available')
)

const coursePackages = computed(() => {
  if (!profileUser.value) {
    return []
  }

  return [
    {
      name: profileUser.value.membership,
      status: `${profileUser.value.homeClub || '默认门店'} / 当前会员权益`,
      badge: profileUser.value.remainingSessions > 0 ? '生效中' : '待续费',
      statusClass: profileUser.value.remainingSessions > 0 ? 'status-pill--success' : 'status-pill--warning',
      remaining: profileUser.value.remainingSessions,
      expireAt: profileUser.value.expireAt || '--'
    }
  ]
})

const orders = computed(() =>
  (commerceData.value?.orders ?? []).map((item) => ({
    no: item.orderNo,
    name: item.packageName,
    time: `下单时间: ${item.orderTime}`,
    status: statusLabelMap[item.statusCode] || item.statusCode,
    statusClass: statusClassMap[item.statusCode] || 'status-pill--muted'
  }))
)

const reminders = computed(() => {
  const list: Array<{ icon: string; title: string; content: string }> = []

  if (profileUser.value?.expireAt) {
    list.push({
      icon: '!',
      title: '会员课包有效期提醒',
      content: `当前课包有效期至 ${profileUser.value.expireAt}，剩余 ${profileUser.value.remainingSessions} 节。`
    })
  }

  if (availableCoupons.value.length > 0) {
    list.push({
      icon: '%',
      title: '优惠券待使用',
      content: `当前可用优惠券 ${availableCoupons.value.length} 张，可到课程商城查看具体规则。`
    })
  }

  if (nextReservation.value) {
    list.push({
      icon: '>',
      title: '下一节课程已安排',
      content: `${nextReservation.value.dateLabel} ${nextReservation.value.timeRange} / ${nextReservation.value.trainerName}`
    })
  }

  return list
})

onMounted(async () => {
  const memberId = authStore.user?.userId ?? 0
  if (!memberId) {
    ElMessage.warning('请先登录会员账号')
    await router.push({ name: 'login' })
    return
  }

  try {
    await Promise.all([
      reservationStore.loadProfile(),
      reservationStore.loadHome()
    ])

    const response = await reservationPortalApi.getCommerce(memberId)
    commerceData.value = response.data
  } catch {
    ElMessage.error('课程中心数据加载失败')
  }
})
</script>

<style scoped>
.courses-page {
  background:
    radial-gradient(circle at top left, rgba(22, 163, 74, 0.08), transparent 20%),
    linear-gradient(180deg, #fbfdfb 0%, #f3f6f4 100%);
}

.course-tabs {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 8px;
  margin-bottom: 12px;
}

.course-tabs__item {
  border: 0;
  border-radius: 999px;
  padding: 9px 0;
  color: #6b7280;
  background: #fff;
  box-shadow: inset 0 0 0 1px rgba(15, 23, 42, 0.05);
  font-size: 12px;
  font-weight: 700;
}

.course-tabs__item.is-active {
  color: var(--brand-deep);
  background: var(--brand-soft);
}

.course-stack {
  display: grid;
  gap: 12px;
}

.course-package-card,
.order-card,
.reminder-card,
.empty-card {
  padding: 16px;
}

.course-package-card__head,
.course-package-card__metrics,
.course-package-card__actions,
.order-card__item,
.order-card__side,
.reminder-card__item {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
}

.course-package-card__head strong,
.course-package-card__metrics strong,
.order-card__item strong,
.reminder-card__item strong,
.empty-card strong {
  display: block;
}

.course-package-card__head small,
.course-package-card__metrics span,
.order-card__item small,
.reminder-card__item p,
.empty-card span,
.empty-inline {
  color: var(--muted);
  font-size: 11px;
}

.course-package-card__metrics {
  margin-top: 14px;
  padding: 14px;
  border-radius: 18px;
  background: #f8fbf9;
}

.course-package-card__actions {
  margin-top: 14px;
}

.course-package-card__actions button,
.order-card__btn {
  min-width: 108px;
}

.header-link {
  border: 0;
  color: var(--brand-deep);
  background: transparent;
  font-size: 12px;
  font-weight: 700;
}

.order-card__item + .order-card__item,
.reminder-card__item + .reminder-card__item {
  margin-top: 14px;
  padding-top: 14px;
  border-top: 1px solid rgba(15, 23, 42, 0.06);
}

.order-card__side {
  flex-direction: column;
  align-items: flex-end;
}

.status-pill {
  padding: 4px 8px;
  border-radius: 999px;
  font-size: 10px;
  font-weight: 700;
}

.status-pill--success {
  color: #15803d;
  background: #ebf8ef;
}

.status-pill--warning {
  color: #ea580c;
  background: #fff1e6;
}

.status-pill--muted {
  color: #64748b;
  background: #f1f5f9;
}

.reminder-card__item {
  align-items: flex-start;
}

.reminder-card__icon {
  width: 40px;
  height: 40px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  border-radius: 14px;
  background: var(--brand-soft);
  font-size: 20px;
  flex-shrink: 0;
}

.reminder-card p {
  margin: 6px 0 0;
  line-height: 1.6;
}

.reminder-card .primary-button {
  width: 100%;
  margin-top: 16px;
}

.empty-card,
.empty-inline {
  text-align: center;
}

.empty-card span {
  margin-top: 8px;
  line-height: 1.6;
}

.empty-inline {
  margin-top: 14px;
}
</style>
