<template>
  <div class="app-page commerce-page">
    <header class="page-header">
      <div class="page-header__title">
        <strong>课程商城</strong>
        <span>课包购买、订单确认、支付结果</span>
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

    <section v-if="activeScene === 'market'" class="surface-card panel">
      <div class="sub-tabs">
        <button
          v-for="tab in courseTabs"
          :key="tab"
          class="sub-tabs__item"
          :class="{ 'is-active': activeCourseTab === tab }"
          type="button"
          @click="activeCourseTab = tab"
        >
          {{ tab }}
        </button>
      </div>

      <article v-for="item in filteredPackages" :key="item.id" class="package-card">
        <img :src="item.coverImageUrl" :alt="item.packageName" />
        <div class="package-card__body">
          <div class="package-card__head">
            <strong>{{ item.packageName }}</strong>
            <span class="badge">{{ item.isRecommended ? '推荐' : item.categoryName }}</span>
          </div>
          <small>{{ item.summary }}</small>
          <div class="package-card__meta">
            <span>{{ item.clubScope }}</span>
            <span>{{ item.validDays }} 天有效</span>
          </div>
          <div class="package-card__foot">
            <div>
              <del>¥{{ item.originalPrice }}</del>
              <strong>¥{{ item.salePrice }}</strong>
            </div>
            <button class="buy-button" type="button" @click="selectPackage(item.id, 'detail')">购买</button>
          </div>
        </div>
      </article>
    </section>

    <section v-else-if="activeScene === 'detail'" class="surface-card panel">
      <div class="hero">
        <img :src="selectedPackage.coverImageUrl" :alt="selectedPackage.packageName" />
        <div class="hero__overlay">
          <strong>{{ selectedPackage.packageName }}</strong>
          <span>{{ selectedPackage.categoryName }} · {{ selectedPackage.coachLevel }}</span>
        </div>
      </div>

      <div class="price-head">
        <div>
          <strong>¥{{ selectedPackage.salePrice }}</strong>
          <del>¥{{ selectedPackage.originalPrice }}</del>
        </div>
        <span class="price-head__tag">{{ selectedPackage.totalSessions }} 节</span>
      </div>

      <div class="metric-grid">
        <article>
          <strong>{{ selectedPackage.clubScope || '--' }}</strong>
          <span>适用门店</span>
        </article>
        <article>
          <strong>{{ selectedPackage.coachLevel || '--' }}</strong>
          <span>适用教练</span>
        </article>
        <article>
          <strong>{{ selectedPackage.validDays }} 天</strong>
          <span>有效期</span>
        </article>
      </div>

      <div class="section-title">
        <h3>课程介绍</h3>
      </div>
      <p class="copy">{{ selectedPackage.summary || '课包权益与适用范围以门店实际发布信息为准。' }}</p>
      <ul class="bullet-list">
        <li>总课时：{{ selectedPackage.totalSessions }} 节</li>
        <li>状态：{{ selectedPackage.statusCode }}</li>
        <li>适用范围：{{ selectedPackage.clubScope || '全门店通用' }}</li>
      </ul>

      <div class="button-row">
        <button class="ghost-button" type="button">加入购物车</button>
        <button class="primary-button" type="button" @click="setScene('confirm')">立即购买</button>
      </div>
    </section>

    <section v-else-if="activeScene === 'confirm'" class="surface-card panel">
      <div class="section-title">
        <h3>确认订单</h3>
        <span>请核对课包与优惠信息</span>
      </div>

      <div class="order-card">
        <div class="order-card__head">
          <strong>{{ selectedPackage.packageName }}</strong>
          <span>¥{{ selectedPackage.salePrice }}</span>
        </div>
        <small>{{ selectedPackage.coachLevel }} · {{ selectedPackage.clubScope }}</small>
      </div>

      <div class="form-list">
        <button class="form-list__row" type="button" @click="setScene('coupon')">
          <span>优惠券</span>
          <strong>{{ selectedCouponText }}</strong>
        </button>
        <div class="form-list__row">
          <span>积分抵扣</span>
          <strong>¥{{ pointDiscount }}</strong>
        </div>
        <div class="form-list__row">
          <span>备注</span>
          <strong>选填</strong>
        </div>
      </div>

      <div class="amount-list">
        <div><span>原价</span><strong>¥{{ selectedPackage.originalPrice }}</strong></div>
        <div><span>券后优惠</span><strong class="minus">-¥{{ selectedCouponAmount }}</strong></div>
        <div><span>积分抵扣</span><strong class="minus">-¥{{ pointDiscount }}</strong></div>
        <div class="total"><span>实付金额</span><strong>¥{{ confirmPayAmount }}</strong></div>
      </div>

      <button class="primary-button" type="button" @click="setScene('payment')">提交订单</button>
    </section>

    <section v-else-if="activeScene === 'payment'" class="surface-card panel">
      <div class="payment-total">
        <span>订单金额</span>
        <strong>¥{{ confirmPayAmount }}</strong>
      </div>

      <div class="payment-list">
        <button
          v-for="item in paymentMethods"
          :key="item.name"
          class="payment-item"
          :class="{ 'is-active': selectedPayment === item.name }"
          type="button"
          @click="selectedPayment = item.name"
        >
          <span class="payment-item__icon" :style="{ background: item.color }">{{ item.short }}</span>
          <div>
            <strong>{{ item.name }}</strong>
            <small>{{ item.hint }}</small>
          </div>
          <span class="payment-item__radio"></span>
        </button>
      </div>

      <button class="primary-button" type="button" @click="setScene('paid')">确认支付</button>
    </section>

    <section v-else-if="activeScene === 'paid'" class="surface-card panel panel--center">
      <div class="result-mark">
        <el-icon><Check /></el-icon>
      </div>
      <h2>支付成功</h2>
      <p>课程包已加入你的会员权益，接下来可以直接去预约教练。</p>
      <div class="amount-list amount-list--plain">
        <div><span>订单号</span><strong>{{ selectedOrder.orderNo || '--' }}</strong></div>
        <div><span>支付方式</span><strong>{{ selectedPayment }}</strong></div>
        <div><span>支付金额</span><strong>¥{{ paidAmount }}</strong></div>
      </div>
      <div class="button-stack">
        <button class="primary-button" type="button" @click="setScene('orders')">查看订单</button>
        <button class="ghost-button" type="button" @click="router.push({ name: 'training' })">去预约</button>
      </div>
    </section>

    <section v-else-if="activeScene === 'orders'" class="surface-card panel">
      <div class="sub-tabs">
        <button
          v-for="tab in orderTabs"
          :key="tab"
          class="sub-tabs__item"
          :class="{ 'is-active': activeOrderTab === tab }"
          type="button"
          @click="activeOrderTab = tab"
        >
          {{ tab }}
        </button>
      </div>

      <article v-for="item in filteredOrders" :key="item.id" class="order-item">
        <img :src="item.coverImageUrl || selectedPackage.coverImageUrl" :alt="item.packageName" />
        <div class="order-item__body">
          <div class="order-item__head">
            <strong>{{ item.packageName }}</strong>
            <span :class="['status-tag', statusClassMap[item.statusCode] || '']">{{ statusLabelMap[item.statusCode] || item.statusCode }}</span>
          </div>
          <small>下单时间：{{ item.orderTime }}</small>
          <div class="order-item__foot">
            <strong>¥{{ item.payAmount }}</strong>
            <button class="ghost-button" type="button" @click="selectOrder(item.id, 'order-detail')">
              {{ item.statusCode === 'PendingPayment' ? '去支付' : '查看详情' }}
            </button>
          </div>
        </div>
      </article>
    </section>

    <section v-else-if="activeScene === 'order-detail'" class="surface-card panel">
      <div class="section-title">
        <h3>订单详情</h3>
        <span>{{ statusLabelMap[selectedOrder.statusCode] || selectedOrder.statusCode || '订单' }}</span>
      </div>

      <div class="timeline">
        <div v-for="step in orderTimeline" :key="step.label" class="timeline__item" :class="{ 'is-active': step.active }">
          <span class="timeline__dot"></span>
          <div>
            <strong>{{ step.label }}</strong>
            <small>{{ step.time }}</small>
          </div>
        </div>
      </div>

      <div class="order-card">
        <div class="order-card__head">
          <strong>{{ selectedOrder.packageName || selectedPackage.packageName }}</strong>
          <span>¥{{ selectedOrder.payAmount || confirmPayAmount }}</span>
        </div>
        <div class="amount-list">
          <div><span>原始金额</span><strong>¥{{ selectedOrder.originAmount || selectedPackage.originalPrice }}</strong></div>
          <div><span>优惠金额</span><strong class="minus">-¥{{ selectedOrder.discountAmount || selectedCouponAmount }}</strong></div>
          <div><span>积分抵扣</span><strong class="minus">-¥{{ selectedOrder.pointDiscountAmount || pointDiscount }}</strong></div>
          <div class="total"><span>实付金额</span><strong>¥{{ selectedOrder.payAmount || confirmPayAmount }}</strong></div>
        </div>
      </div>

      <div class="button-row">
        <button class="ghost-button" type="button">联系客服</button>
        <button class="ghost-button" type="button">申请退款</button>
        <button class="primary-button" type="button" @click="router.push({ name: 'training' })">去预约</button>
      </div>
    </section>

    <section v-else class="surface-card panel">
      <div class="sub-tabs">
        <button
          v-for="tab in couponTabs"
          :key="tab"
          class="sub-tabs__item"
          :class="{ 'is-active': activeCouponTab === tab }"
          type="button"
          @click="activeCouponTab = tab"
        >
          {{ tab }}
        </button>
      </div>

      <article
        v-for="coupon in filteredCoupons"
        :key="coupon.couponId"
        class="coupon-card"
        :class="{ 'is-selected': selectedCouponId === coupon.couponId }"
        @click="selectedCouponId = coupon.couponId"
      >
        <div class="coupon-card__amount">{{ couponDisplay(coupon) }}</div>
        <div class="coupon-card__body">
          <strong>{{ coupon.title }}</strong>
          <small>{{ coupon.ruleText }}</small>
          <p>{{ coupon.startDate }} - {{ coupon.endDate }}</p>
        </div>
        <span class="coupon-card__check"></span>
      </article>

      <div class="coupon-footer">
        <span>当前优惠</span>
        <strong>-¥{{ selectedCouponAmount }}</strong>
      </div>
      <button class="primary-button" type="button" @click="setScene('confirm')">确认使用</button>
    </section>
  </div>
</template>

<script setup lang="ts">
import { Check, Grid } from '@element-plus/icons-vue'
import { ElMessage } from 'element-plus'
import { computed, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/authStore'
import {
  reservationPortalApi,
  type ReservationCommerceData,
  type ReservationCourseOrder,
  type ReservationCoursePackage,
  type ReservationMemberCoupon
} from '@/api/reservationPortal'

type SceneKey = 'market' | 'detail' | 'confirm' | 'payment' | 'paid' | 'orders' | 'order-detail' | 'coupon'

const route = useRoute()
const router = useRouter()
const authStore = useAuthStore()

const scenes: Array<{ key: SceneKey; label: string }> = [
  { key: 'market', label: '课程购买' },
  { key: 'detail', label: '课包详情' },
  { key: 'confirm', label: '订单确认' },
  { key: 'payment', label: '支付方式' },
  { key: 'paid', label: '支付结果' },
  { key: 'orders', label: '我的订单' },
  { key: 'order-detail', label: '订单详情' },
  { key: 'coupon', label: '优惠券' }
]

const sceneFromQuery = computed<SceneKey>(() => {
  const value = String(route.query.scene || 'market')
  return (scenes.find((item) => item.key === value)?.key ?? 'market') as SceneKey
})

const commerceData = ref<ReservationCommerceData | null>(null)
const activeScene = ref<SceneKey>(sceneFromQuery.value)
const activeCourseTab = ref('私教课包')
const activeOrderTab = ref('全部')
const activeCouponTab = ref('可用')
const selectedPayment = ref('微信支付')
const selectedPackageId = ref<number>(0)
const selectedOrderId = ref<number>(0)
const selectedCouponId = ref<number>(0)

const courseTabs = ['私教课包', '体验课', '康复训练', '小团课']
const orderTabs = ['全部', '待支付', '已支付', '已完成']
const couponTabs = ['可用', '不可用']
const pointDiscount = 80
const paymentMethods = [
  { name: '微信支付', hint: '推荐使用微信快捷支付', short: '微', color: '#22c55e' },
  { name: '支付宝', hint: '推荐使用支付宝支付', short: '支', color: '#3b82f6' },
  { name: '银行卡', hint: '支持储蓄卡 / 信用卡', short: '卡', color: '#fb923c' },
  { name: '会员余额', hint: '当前可用余额 ¥320.00', short: '余', color: '#f59e0b' }
]
const statusLabelMap: Record<string, string> = {
  PendingPayment: '待支付',
  Paid: '已支付',
  Completed: '已完成',
  Closed: '已关闭'
}
const statusClassMap: Record<string, string> = {
  PendingPayment: 'status-tag--warning',
  Paid: 'status-tag--success',
  Completed: 'status-tag--muted',
  Closed: 'status-tag--muted'
}

const packages = computed(() => commerceData.value?.packages ?? [])
const orders = computed(() => commerceData.value?.orders ?? [])
const coupons = computed(() => commerceData.value?.coupons ?? [])

const selectedPackage = computed<ReservationCoursePackage>(() => {
  return (
    packages.value.find((item) => item.id === selectedPackageId.value) ||
    commerceData.value?.featuredPackage ||
    packages.value[0] || {
      id: 0,
      packageCode: '',
      packageName: '--',
      categoryName: '',
      summary: '',
      coverImageUrl: '',
      originalPrice: 0,
      salePrice: 0,
      totalSessions: 0,
      validDays: 0,
      coachLevel: '',
      clubScope: '',
      isRecommended: false,
      statusCode: ''
    }
  )
})

const selectedOrder = computed<ReservationCourseOrder>(() => {
  return (
    orders.value.find((item) => item.id === selectedOrderId.value) ||
    orders.value[0] || {
      id: 0,
      orderNo: '',
      packageName: '',
      coverImageUrl: '',
      originAmount: 0,
      discountAmount: 0,
      pointDiscountAmount: 0,
      payAmount: 0,
      paymentMethod: '',
      statusCode: '',
      orderTime: ''
    }
  )
})

const selectedCoupon = computed<ReservationMemberCoupon | null>(() => {
  return coupons.value.find((item) => item.couponId === selectedCouponId.value) ?? null
})

const filteredPackages = computed(() => {
  if (activeCourseTab.value !== '私教课包') {
    return packages.value.filter((item) => item.categoryName.includes(activeCourseTab.value.replace('课', '')))
  }
  return packages.value
})

const filteredOrders = computed(() => {
  if (activeOrderTab.value === '全部') return orders.value
  const status = activeOrderTab.value === '待支付'
    ? 'PendingPayment'
    : activeOrderTab.value === '已支付'
      ? 'Paid'
      : 'Completed'
  return orders.value.filter((item) => item.statusCode === status)
})

const filteredCoupons = computed(() => {
  const wantAvailable = activeCouponTab.value === '可用'
  return coupons.value.filter((item) => wantAvailable ? item.statusCode === 'Available' : item.statusCode !== 'Available')
})

const selectedCouponAmount = computed(() => {
  const coupon = selectedCoupon.value
  if (!coupon) return 0
  if (coupon.couponType === 'Discount') {
    return Number((selectedPackage.value.salePrice * (1 - coupon.discountValue)).toFixed(2))
  }
  return coupon.discountValue
})

const selectedCouponText = computed(() => {
  if (!selectedCoupon.value) return '选择优惠券'
  return `${selectedCoupon.value.title} -${selectedCouponAmount.value}`
})

const confirmPayAmount = computed(() => {
  const amount = selectedPackage.value.originalPrice - selectedCouponAmount.value - pointDiscount
  return Number(Math.max(amount, 0).toFixed(2))
})

const paidAmount = computed(() => selectedOrder.value.payAmount || confirmPayAmount.value)

const orderTimeline = computed(() => {
  const order = selectedOrder.value
  return [
    { label: '已下单', time: order.orderTime || '--', active: Boolean(order.orderNo) },
    { label: '已支付', time: order.statusCode === 'PendingPayment' ? '--' : order.orderTime || '--', active: order.statusCode !== 'PendingPayment' },
    { label: '已生效', time: order.statusCode === 'Completed' ? order.orderTime || '--' : '--', active: order.statusCode === 'Completed' }
  ]
})

watch(
  () => route.query.scene,
  () => {
    activeScene.value = sceneFromQuery.value
  }
)

onMounted(async () => {
  const memberId = authStore.user?.userId ?? 0
  if (!memberId) {
    ElMessage.warning('请先登录会员账号')
    await router.push({ name: 'login' })
    return
  }

  try {
    const response = await reservationPortalApi.getCommerce(memberId)
    commerceData.value = response.data
    selectedPackageId.value = response.data.featuredPackage?.id || response.data.packages?.[0]?.id || 0
    selectedOrderId.value = response.data.orders?.[0]?.id || 0
    selectedCouponId.value = response.data.coupons?.find((item) => item.statusCode === 'Available')?.couponId || 0
  } catch {
    ElMessage.error('课程商城数据加载失败')
  }
})

const setScene = (scene: SceneKey): void => {
  activeScene.value = scene
  void router.replace({ query: { scene } })
}

const selectPackage = (packageId: number, scene: SceneKey): void => {
  selectedPackageId.value = packageId
  setScene(scene)
}

const selectOrder = (orderId: number, scene: SceneKey): void => {
  selectedOrderId.value = orderId
  setScene(scene)
}

const couponDisplay = (coupon: ReservationMemberCoupon): string => {
  if (coupon.couponType === 'Discount') {
    return `${Number((coupon.discountValue * 10).toFixed(1))} 折`
  }
  return `¥${coupon.discountValue}`
}
</script>

<style scoped>
.commerce-page {
  background:
    radial-gradient(circle at top left, rgba(37, 99, 235, 0.08), transparent 22%),
    linear-gradient(180deg, #fafdff 0%, #f4f8ff 100%);
}

.scene-tabs,
.sub-tabs {
  display: flex;
  gap: 8px;
  overflow-x: auto;
  scrollbar-width: none;
}

.scene-tabs::-webkit-scrollbar,
.sub-tabs::-webkit-scrollbar {
  display: none;
}

.scene-tabs {
  margin-bottom: 12px;
}

.scene-tabs__item,
.sub-tabs__item {
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
.sub-tabs__item.is-active {
  color: #2563eb;
  background: rgba(37, 99, 235, 0.08);
}

.panel {
  padding: 16px;
}

.package-card,
.order-item {
  display: grid;
  grid-template-columns: 92px minmax(0, 1fr);
  gap: 12px;
  margin-top: 14px;
}

.package-card img,
.order-item img,
.hero img {
  width: 100%;
  border-radius: 18px;
  object-fit: cover;
}

.package-card img,
.order-item img {
  height: 92px;
}

.package-card__head,
.package-card__foot,
.package-card__meta,
.order-item__head,
.order-item__foot,
.form-list__row,
.amount-list div,
.coupon-footer,
.button-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 10px;
}

.package-card__body small,
.order-item small,
.copy,
.hero__overlay span {
  color: var(--muted);
  font-size: 12px;
  line-height: 1.6;
}

.badge,
.status-tag {
  padding: 4px 8px;
  border-radius: 999px;
  font-size: 10px;
  font-weight: 700;
}

.badge {
  color: #ea580c;
  background: #fff1e6;
}

.buy-button {
  border: 0;
  border-radius: 14px;
  padding: 10px 16px;
  background: linear-gradient(135deg, #3b82f6, #1d4ed8);
  color: #fff;
  font-size: 12px;
  font-weight: 700;
}

.hero {
  position: relative;
}

.hero img {
  height: 188px;
}

.hero__overlay {
  position: absolute;
  inset: auto 0 0 0;
  padding: 18px;
  color: #fff;
  background: linear-gradient(180deg, transparent, rgba(15, 23, 42, 0.78));
}

.hero__overlay strong,
.hero__overlay span {
  display: block;
}

.price-head,
.payment-total {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  margin-top: 16px;
}

.price-head strong,
.payment-total strong {
  font-size: 28px;
}

.price-head del {
  margin-left: 8px;
  color: #94a3b8;
}

.price-head__tag {
  padding: 8px 12px;
  border-radius: 999px;
  color: #2563eb;
  background: rgba(37, 99, 235, 0.08);
  font-size: 12px;
  font-weight: 700;
}

.metric-grid {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 10px;
  margin: 16px 0;
}

.metric-grid article {
  padding: 14px 10px;
  border-radius: 18px;
  background: #f8fbff;
  text-align: center;
}

.metric-grid strong,
.metric-grid span {
  display: block;
}

.metric-grid span {
  margin-top: 4px;
  color: var(--muted);
  font-size: 12px;
}

.section-title {
  margin: 16px 0 12px;
}

.section-title h3,
.section-title span {
  margin: 0;
}

.section-title span {
  color: var(--muted);
}

.bullet-list {
  margin: 0;
  padding-left: 18px;
  line-height: 1.7;
  color: #475569;
}

.button-row {
  margin-top: 16px;
}

.button-row .primary-button,
.button-row .ghost-button {
  flex: 1 1 0;
}

.order-card,
.form-list,
.amount-list,
.timeline,
.payment-list {
  display: grid;
  gap: 12px;
}

.order-card,
.amount-list--plain {
  margin-top: 14px;
  padding: 14px;
  border-radius: 18px;
  background: #f8fbff;
}

.order-card__head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 10px;
}

.form-list {
  margin-top: 16px;
}

.form-list__row {
  padding: 14px;
  border-radius: 18px;
  background: #fff;
  box-shadow: inset 0 0 0 1px rgba(15, 23, 42, 0.06);
}

.amount-list {
  margin: 16px 0;
}

.amount-list span {
  color: var(--muted);
  font-size: 12px;
}

.amount-list .minus {
  color: #ef4444;
}

.amount-list .total {
  padding-top: 12px;
  border-top: 1px solid rgba(15, 23, 42, 0.08);
}

.payment-item {
  border: 0;
  border-radius: 18px;
  padding: 14px;
  display: flex;
  align-items: center;
  gap: 12px;
  background: #fff;
  box-shadow: inset 0 0 0 1px rgba(15, 23, 42, 0.06);
}

.payment-item.is-active {
  box-shadow: inset 0 0 0 1px rgba(37, 99, 235, 0.28);
  background: rgba(37, 99, 235, 0.04);
}

.payment-item__icon {
  width: 38px;
  height: 38px;
  border-radius: 12px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  color: #fff;
  font-weight: 700;
}

.payment-item strong,
.payment-item small {
  display: block;
  text-align: left;
}

.payment-item small {
  color: var(--muted);
}

.payment-item__radio {
  margin-left: auto;
  width: 18px;
  height: 18px;
  border-radius: 50%;
  box-shadow: inset 0 0 0 1px #cbd5e1;
}

.payment-item.is-active .payment-item__radio {
  background: #2563eb;
  box-shadow: inset 0 0 0 4px #fff;
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
}

.button-stack {
  display: grid;
  gap: 12px;
  margin-top: 16px;
}

.timeline__item {
  display: grid;
  grid-template-columns: 18px minmax(0, 1fr);
  gap: 10px;
  align-items: start;
}

.timeline__dot {
  width: 12px;
  height: 12px;
  margin-top: 4px;
  border-radius: 50%;
  background: #cbd5e1;
}

.timeline__item.is-active .timeline__dot {
  background: #2563eb;
}

.timeline__item small {
  color: var(--muted);
}

.coupon-card {
  margin-top: 14px;
  padding: 16px;
  border-radius: 20px;
  display: grid;
  grid-template-columns: 96px minmax(0, 1fr) 18px;
  gap: 12px;
  background: #fff7ed;
  box-shadow: inset 0 0 0 1px rgba(251, 146, 60, 0.18);
}

.coupon-card.is-selected {
  box-shadow: inset 0 0 0 2px rgba(37, 99, 235, 0.22);
  background: #f8fbff;
}

.coupon-card__amount {
  font-size: 26px;
  font-weight: 800;
  color: #ea580c;
}

.coupon-card__body strong,
.coupon-card__body small,
.coupon-card__body p {
  display: block;
  margin: 0;
}

.coupon-card__body small,
.coupon-card__body p {
  color: var(--muted);
  font-size: 12px;
  line-height: 1.6;
}

.coupon-card__check {
  width: 18px;
  height: 18px;
  margin-top: 4px;
  border-radius: 50%;
  box-shadow: inset 0 0 0 1px #cbd5e1;
}

.coupon-card.is-selected .coupon-card__check {
  background: #2563eb;
  box-shadow: inset 0 0 0 4px #fff;
}

.coupon-footer {
  margin: 16px 0;
}

.status-tag--warning {
  color: #ea580c;
  background: #fff1e6;
}

.status-tag--success {
  color: #15803d;
  background: #ebf8ef;
}

.status-tag--muted {
  color: #64748b;
  background: #f1f5f9;
}
</style>
