<template>
  <div class="app-page member-center-page">
    <header class="page-header">
      <div class="page-header__title">
        <strong>会员资料中心</strong>
        <span>个人信息、身体数据、训练记录与计划</span>
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

    <section v-if="activeScene === 'profile'" class="surface-card panel">
      <div class="member-head">
        <img :src="profile.avatarUrl || avatarFallback" alt="avatar" />
        <div>
          <strong>{{ profile.name }}</strong>
          <span>{{ profile.membershipName }}</span>
          <small>{{ profile.phoneNumber }}</small>
        </div>
      </div>

      <div class="section-title"><h3>基础信息</h3></div>
      <div class="info-list">
        <div v-for="item in basicInfo" :key="item.label">
          <span>{{ item.label }}</span>
          <strong>{{ item.value }}</strong>
        </div>
      </div>

      <div class="note-box">
        <strong>健康备注</strong>
        <p>{{ profile.healthRemark || '暂无健康备注' }}</p>
      </div>

      <button class="primary-button" type="button" @click="setScene('edit')">编辑资料</button>
    </section>

    <section v-else-if="activeScene === 'edit'" class="surface-card panel">
      <div class="form-list">
        <div v-for="item in editFields" :key="item.label" class="form-row">
          <span>{{ item.label }}</span>
          <strong>{{ item.value }}</strong>
        </div>
      </div>
      <button class="primary-button" type="button">保存资料</button>
    </section>

    <section v-else-if="activeScene === 'goals'" class="surface-card panel">
      <div class="section-title">
        <h3>主要目标</h3>
      </div>
      <div class="goal-grid">
        <article v-for="goal in mainGoals" :key="goal.label" class="goal-card" :class="{ 'is-active': goal.active }">
          <strong>{{ goal.label }}</strong>
          <span>{{ goal.hint }}</span>
        </article>
      </div>

      <div class="section-title">
        <h3>次要目标</h3>
      </div>
      <div class="chip-grid">
        <button v-for="goal in sideGoals" :key="goal.label" class="chip" :class="{ 'is-active': goal.active }" type="button">
          {{ goal.label }}
        </button>
      </div>

      <div class="info-list info-list--compact">
        <div><span>当前体重</span><strong>{{ latestMetric.weightKg }} kg</strong></div>
        <div><span>当前体脂</span><strong>{{ latestMetric.bodyFatRate }}%</strong></div>
        <div><span>计划周期</span><strong>{{ planRange }}</strong></div>
        <div><span>训练计划</span><strong>{{ trainingPlanName }}</strong></div>
      </div>

      <button class="primary-button" type="button">保存目标</button>
    </section>

    <section v-else-if="activeScene === 'body'" class="surface-card panel">
      <div class="metric-grid">
        <article v-for="metric in bodyMetricCards" :key="metric.label">
          <span>{{ metric.label }}</span>
          <strong>{{ metric.value }}</strong>
          <small :class="metric.className">{{ metric.trend }}</small>
        </article>
      </div>

      <div class="section-title">
        <h3>最近测量记录</h3>
        <span>{{ bodyMetrics.length }} 次</span>
      </div>
      <div class="history-list">
        <article v-for="item in bodyMetrics" :key="item.measureTime" class="history-row">
          <strong>{{ item.measureTime }}</strong>
          <span>{{ item.weightKg }} kg</span>
          <span>{{ item.bodyFatRate }}%</span>
          <span>{{ item.bmi }}</span>
          <span>{{ item.muscleKg }} kg</span>
        </article>
      </div>
    </section>

    <section v-else-if="activeScene === 'trend'" class="surface-card panel">
      <div class="section-title">
        <h3>近 30 天体重趋势</h3>
        <span>{{ weightDeltaText }}</span>
      </div>

      <div class="trend-card">
        <div class="trend-card__axis">
          <span>80</span>
          <span>75</span>
          <span>70</span>
          <span>65</span>
        </div>
        <div class="trend-card__plot">
          <div class="trend-line">
            <span
              v-for="point in trendPoints"
              :key="point.label"
              class="trend-point"
              :style="{ left: point.left, top: point.top }"
            ></span>
          </div>
          <div class="trend-labels">
            <small v-for="point in trendPoints" :key="point.label">{{ point.label }}</small>
          </div>
        </div>
      </div>

      <div class="stats-row">
        <article>
          <strong>{{ latestMetric.weightKg }} kg</strong>
          <span>最新</span>
        </article>
        <article>
          <strong>{{ oldestMetric.weightKg }} kg</strong>
          <span>起始</span>
        </article>
        <article>
          <strong>{{ weightDeltaValue }}</strong>
          <span>变化</span>
        </article>
      </div>
    </section>

    <section v-else-if="activeScene === 'records'" class="surface-card panel">
      <div class="sub-tabs">
        <button
          v-for="tab in recordTabs"
          :key="tab"
          class="sub-tabs__item"
          :class="{ 'is-active': activeRecordTab === tab }"
          type="button"
          @click="activeRecordTab = tab"
        >
          {{ tab }}
        </button>
      </div>

      <article v-for="record in filteredRecords" :key="record.recordTime + record.title" class="record-card">
        <img :src="profile.avatarUrl || avatarFallback" alt="member" />
        <div class="record-card__body">
          <div class="record-card__head">
            <strong>{{ record.title }}</strong>
            <span class="status-tag">已完成</span>
          </div>
          <small>{{ record.recordTime }} · 教练：{{ record.coachName }} · {{ record.durationMinutes }} 分钟</small>
          <div class="record-card__foot">
            <span>{{ record.locationName }}</span>
            <strong>{{ record.calories }} kcal</strong>
          </div>
        </div>
      </article>
    </section>

    <section v-else-if="activeScene === 'plan'" class="surface-card panel">
      <div class="section-title">
        <h3>训练计划</h3>
        <span>{{ trainingPlan?.progressText || '暂无计划' }}</span>
      </div>

      <div class="week-strip">
        <div v-for="item in planItems" :key="item.dayLabel" class="week-strip__item" :class="{ 'is-active': item.isCompleted }">
          <span>{{ item.dayLabel }}</span>
          <strong>{{ item.durationMinutes }}m</strong>
        </div>
      </div>

      <article v-for="task in planItems" :key="task.dayLabel + task.title" class="plan-card">
        <div>
          <strong>{{ task.title }}</strong>
          <small>{{ task.durationMinutes }} 分钟 · {{ task.caloriesTarget }} kcal</small>
        </div>
        <span class="plan-check" :class="{ 'is-done': task.isCompleted }"></span>
      </article>

      <button class="primary-button" type="button">开始训练</button>
    </section>

    <section v-else class="surface-card panel">
      <div class="sub-tabs">
        <button
          v-for="tab in notificationTabs"
          :key="tab"
          class="sub-tabs__item"
          :class="{ 'is-active': activeNotificationTab === tab }"
          type="button"
          @click="activeNotificationTab = tab"
        >
          {{ tab }}
        </button>
      </div>

      <article v-for="notice in filteredNotifications" :key="notice.title + notice.sendTime" class="notice-card">
        <span class="notice-dot" :style="{ background: notice.color }"></span>
        <div class="notice-card__body">
          <strong>{{ notice.title }}</strong>
          <p>{{ notice.content }}</p>
        </div>
        <small>{{ notice.sendTime }}</small>
      </article>
    </section>
  </div>
</template>

<script setup lang="ts">
import { Grid } from '@element-plus/icons-vue'
import { ElMessage } from 'element-plus'
import { computed, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/authStore'
import {
  reservationPortalApi,
  type ReservationBodyMetric,
  type ReservationMemberCenterData,
  type ReservationMemberNotification,
  type ReservationTrainingPlanItem,
  type ReservationTrainingRecord
} from '@/api/reservationPortal'

type SceneKey = 'profile' | 'edit' | 'goals' | 'body' | 'trend' | 'records' | 'plan' | 'notifications'

const router = useRouter()
const route = useRoute()
const authStore = useAuthStore()

const scenes: Array<{ key: SceneKey; label: string }> = [
  { key: 'profile', label: '个人资料' },
  { key: 'edit', label: '编辑资料' },
  { key: 'goals', label: '健身目标' },
  { key: 'body', label: '身体数据' },
  { key: 'trend', label: '数据趋势' },
  { key: 'records', label: '训练记录' },
  { key: 'plan', label: '训练计划' },
  { key: 'notifications', label: '消息通知' }
]

const sceneFromQuery = computed<SceneKey>(() => {
  const value = String(route.query.scene || 'profile')
  return (scenes.find((item) => item.key === value)?.key ?? 'profile') as SceneKey
})

const centerData = ref<ReservationMemberCenterData | null>(null)
const activeScene = ref<SceneKey>(sceneFromQuery.value)
const activeRecordTab = ref('全部')
const activeNotificationTab = ref('全部')
const avatarFallback = 'https://images.unsplash.com/photo-1566753323558-f4e0952af115?auto=format&fit=crop&w=320&q=80'
const recordTabs = ['全部', '私教课', '自主训练']
const notificationTabs = ['全部', '系统通知', '预约消息']

const profile = computed(
  () =>
    centerData.value?.profile || {
      memberId: 0,
      name: '--',
      phoneNumber: '',
      avatarUrl: '',
      gender: '',
      age: 0,
      heightCm: 0,
      weightKg: 0,
      birthday: '',
      city: '',
      membershipName: '',
      healthRemark: '',
      primaryGoal: '',
      secondaryGoals: []
    }
)
const bodyMetrics = computed(() => centerData.value?.bodyMetrics ?? [])
const records = computed(() => centerData.value?.trainingRecords ?? [])
const trainingPlan = computed(() => centerData.value?.trainingPlan ?? null)
const notifications = computed(() => centerData.value?.notifications ?? [])

const latestMetric = computed<ReservationBodyMetric>(() => bodyMetrics.value[0] || { measureTime: '', weightKg: 0, bodyFatRate: 0, bmi: 0, muscleKg: 0 })
const oldestMetric = computed<ReservationBodyMetric>(() => bodyMetrics.value[bodyMetrics.value.length - 1] || latestMetric.value)
const planItems = computed<ReservationTrainingPlanItem[]>(() => trainingPlan.value?.items ?? [])

const basicInfo = computed(() => [
  { label: '性别', value: profile.value.gender || '--' },
  { label: '年龄', value: String(profile.value.age || '--') },
  { label: '身高', value: `${profile.value.heightCm || '--'} cm` },
  { label: '体重', value: `${profile.value.weightKg || latestMetric.value.weightKg || '--'} kg` },
  { label: '生日', value: profile.value.birthday || '--' },
  { label: '所在城市', value: profile.value.city || '--' }
])

const editFields = computed(() => [
  { label: '头像', value: profile.value.name || '--' },
  { label: '姓名', value: profile.value.name || '--' },
  { label: '生日', value: profile.value.birthday || '--' },
  { label: '身高(cm)', value: String(profile.value.heightCm || '--') },
  { label: '体重(kg)', value: String(profile.value.weightKg || latestMetric.value.weightKg || '--') },
  { label: '联系电话', value: profile.value.phoneNumber || '--' },
  { label: '所在城市', value: profile.value.city || '--' }
])

const mainGoals = computed(() => {
  const active = profile.value.primaryGoal
  return [
    { label: '减脂塑形', hint: '体脂与线条优化', active: active === '减脂塑形' },
    { label: '增肌增量', hint: '提升力量与围度', active: active === '增肌增量' },
    { label: '体态矫正', hint: '改善姿态稳定性', active: active === '体态矫正' }
  ]
})

const sideGoals = computed(() => {
  const selected = new Set(profile.value.secondaryGoals || [])
  return ['康复训练', '提升体能', '运动表现', '柔韧提升', '健康管理', '备赛'].map((label) => ({
    label,
    active: selected.has(label)
  }))
})

const bodyMetricCards = computed(() => {
  const prev = bodyMetrics.value[1]
  const weightDiff = prev ? Number((latestMetric.value.weightKg - prev.weightKg).toFixed(1)) : 0
  const fatDiff = prev ? Number((latestMetric.value.bodyFatRate - prev.bodyFatRate).toFixed(1)) : 0
  const muscleDiff = prev ? Number((latestMetric.value.muscleKg - prev.muscleKg).toFixed(1)) : 0

  return [
    {
      label: '当前体重',
      value: `${latestMetric.value.weightKg} kg`,
      trend: weightDiff <= 0 ? `${weightDiff} kg` : `+${weightDiff} kg`,
      className: weightDiff <= 0 ? 'metric-down' : 'metric-up'
    },
    {
      label: '体脂率',
      value: `${latestMetric.value.bodyFatRate}%`,
      trend: fatDiff <= 0 ? `${fatDiff}%` : `+${fatDiff}%`,
      className: fatDiff <= 0 ? 'metric-down' : 'metric-up'
    },
    {
      label: 'BMI',
      value: `${latestMetric.value.bmi}`,
      trend: latestMetric.value.bmi <= 24 ? '正常' : '偏高',
      className: 'metric-steady'
    },
    {
      label: '肌肉量',
      value: `${latestMetric.value.muscleKg} kg`,
      trend: muscleDiff >= 0 ? `+${muscleDiff} kg` : `${muscleDiff} kg`,
      className: muscleDiff >= 0 ? 'metric-up' : 'metric-down'
    }
  ]
})

const trendPoints = computed(() => {
  const list = [...bodyMetrics.value].reverse()
  if (!list.length) return []
  const max = Math.max(...list.map((item) => item.weightKg), 80)
  const min = Math.min(...list.map((item) => item.weightKg), 65)
  const span = Math.max(max - min, 1)
  return list.map((item, index) => ({
    label: item.measureTime.slice(5, 10).replace('-', '/'),
    left: `${list.length === 1 ? 50 : (index / (list.length - 1)) * 88 + 2}%`,
    top: `${18 + ((max - item.weightKg) / span) * 52}%`
  }))
})

const filteredRecords = computed<ReservationTrainingRecord[]>(() => {
  if (activeRecordTab.value === '全部') return records.value
  if (activeRecordTab.value === '自主训练') {
    return records.value.filter((item) => !item.coachName)
  }
  return records.value.filter((item) => Boolean(item.coachName))
})

const filteredNotifications = computed(() => {
  const list = notifications.value.map((item: ReservationMemberNotification) => ({
    ...item,
    color:
      item.notifyType === 'Reservation'
        ? '#22c55e'
        : item.notifyType === 'Reminder'
          ? '#f59e0b'
          : '#2563eb'
  }))

  if (activeNotificationTab.value === '全部') return list
  if (activeNotificationTab.value === '系统通知') return list.filter((item) => item.notifyType === 'System')
  return list.filter((item) => item.notifyType !== 'System')
})

const trainingPlanName = computed(() => trainingPlan.value?.planName || '暂无计划')
const planRange = computed(() => {
  if (!trainingPlan.value) return '--'
  return `${trainingPlan.value.startDate} 至 ${trainingPlan.value.endDate}`
})
const weightDelta = computed(() => Number((latestMetric.value.weightKg - oldestMetric.value.weightKg).toFixed(1)))
const weightDeltaText = computed(() => (weightDelta.value <= 0 ? `较起始下降 ${Math.abs(weightDelta.value)} kg` : `较起始上升 ${weightDelta.value} kg`))
const weightDeltaValue = computed(() => `${weightDelta.value > 0 ? '+' : ''}${weightDelta.value} kg`)

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
    const response = await reservationPortalApi.getMemberCenter(memberId)
    centerData.value = response.data
  } catch {
    ElMessage.error('会员中心数据加载失败')
  }
})

const setScene = (scene: SceneKey): void => {
  activeScene.value = scene
  void router.replace({ query: { scene } })
}
</script>

<style scoped>
.member-center-page {
  background:
    radial-gradient(circle at top right, rgba(59, 130, 246, 0.08), transparent 24%),
    linear-gradient(180deg, #fbfdff 0%, #f4f8ff 100%);
}

.scene-tabs,
.sub-tabs,
.chip-grid,
.week-strip {
  display: flex;
  gap: 8px;
  overflow-x: auto;
  scrollbar-width: none;
}

.scene-tabs::-webkit-scrollbar,
.sub-tabs::-webkit-scrollbar,
.chip-grid::-webkit-scrollbar,
.week-strip::-webkit-scrollbar {
  display: none;
}

.scene-tabs {
  margin-bottom: 12px;
}

.scene-tabs__item,
.sub-tabs__item,
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
.sub-tabs__item.is-active,
.chip.is-active {
  color: #2563eb;
  background: rgba(37, 99, 235, 0.08);
}

.panel {
  padding: 16px;
}

.member-head {
  display: grid;
  grid-template-columns: 72px minmax(0, 1fr);
  gap: 12px;
  align-items: center;
}

.member-head img,
.record-card img {
  width: 72px;
  height: 72px;
  border-radius: 24px;
  object-fit: cover;
}

.member-head strong,
.member-head span,
.member-head small {
  display: block;
}

.member-head span,
.member-head small,
.note-box p,
.goal-card span,
.record-card small,
.notice-card p,
.notice-card small {
  color: var(--muted);
}

.member-head span {
  margin-top: 4px;
  color: #2563eb;
  font-size: 12px;
  font-weight: 700;
}

.member-head small {
  margin-top: 5px;
  font-size: 12px;
}

.section-title {
  margin: 16px 0 12px;
}

.section-title h3,
.section-title span {
  margin: 0;
}

.info-list,
.form-list,
.history-list {
  display: grid;
  gap: 10px;
}

.info-list div,
.form-row,
.history-row,
.record-card__head,
.record-card__foot,
.notice-card {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 10px;
}

.info-list span,
.form-row span,
.history-row span {
  color: var(--muted);
  font-size: 12px;
}

.info-list strong,
.form-row strong,
.history-row strong {
  font-size: 12px;
}

.info-list--compact {
  margin: 14px 0;
}

.note-box {
  margin: 16px 0;
  padding: 14px;
  border-radius: 18px;
  background: #f8fbff;
}

.note-box strong,
.note-box p {
  display: block;
  margin: 0;
}

.note-box p {
  margin-top: 6px;
  font-size: 12px;
  line-height: 1.6;
}

.goal-grid {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 10px;
}

.goal-card {
  padding: 14px 10px;
  border-radius: 18px;
  background: #fff;
  text-align: center;
  box-shadow: inset 0 0 0 1px rgba(15, 23, 42, 0.06);
}

.goal-card.is-active {
  background: rgba(37, 99, 235, 0.08);
  box-shadow: inset 0 0 0 1px rgba(37, 99, 235, 0.24);
}

.goal-card strong,
.goal-card span {
  display: block;
}

.goal-card span {
  margin-top: 6px;
  font-size: 11px;
}

.metric-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 10px;
}

.metric-grid article,
.stats-row article {
  padding: 14px;
  border-radius: 18px;
  background: #fff;
  box-shadow: inset 0 0 0 1px rgba(15, 23, 42, 0.06);
}

.metric-grid span,
.metric-grid strong,
.metric-grid small,
.stats-row strong,
.stats-row span {
  display: block;
}

.metric-grid small {
  margin-top: 4px;
  font-size: 11px;
}

.metric-up {
  color: #16a34a;
}

.metric-down {
  color: #2563eb;
}

.metric-steady {
  color: #64748b;
}

.trend-card {
  margin: 16px 0;
  display: grid;
  grid-template-columns: 32px minmax(0, 1fr);
  gap: 12px;
}

.trend-card__axis,
.trend-labels {
  display: grid;
}

.trend-card__axis {
  grid-template-rows: repeat(4, 1fr);
  color: #94a3b8;
  font-size: 11px;
}

.trend-card__plot {
  position: relative;
  min-height: 180px;
  padding: 18px 0 24px;
  border-radius: 18px;
  background: linear-gradient(180deg, rgba(37, 99, 235, 0.04), rgba(255, 255, 255, 0.9));
}

.trend-line {
  position: relative;
  height: 138px;
}

.trend-line::before {
  content: '';
  position: absolute;
  inset: 0 12px;
  border-top: 1px dashed rgba(148, 163, 184, 0.35);
  border-bottom: 1px dashed rgba(148, 163, 184, 0.35);
}

.trend-point {
  position: absolute;
  width: 10px;
  height: 10px;
  margin-left: -5px;
  border-radius: 50%;
  background: #2563eb;
  box-shadow: 0 0 0 4px rgba(37, 99, 235, 0.12);
}

.trend-labels {
  grid-template-columns: repeat(auto-fit, minmax(0, 1fr));
  margin-top: 10px;
  color: #94a3b8;
  font-size: 11px;
  text-align: center;
}

.stats-row {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 10px;
}

.sub-tabs {
  margin-bottom: 12px;
}

.record-card,
.notice-card,
.plan-card {
  margin-top: 12px;
  padding: 14px;
  border-radius: 18px;
  background: #fff;
  box-shadow: inset 0 0 0 1px rgba(15, 23, 42, 0.06);
}

.record-card {
  display: grid;
  grid-template-columns: 56px minmax(0, 1fr);
  gap: 12px;
}

.record-card__body {
  display: grid;
  gap: 8px;
}

.status-tag {
  padding: 4px 8px;
  border-radius: 999px;
  color: #15803d;
  background: #ebf8ef;
  font-size: 10px;
  font-weight: 700;
}

.week-strip {
  margin: 14px 0 10px;
}

.week-strip__item {
  flex: 0 0 auto;
  min-width: 72px;
  padding: 12px 10px;
  border-radius: 18px;
  background: #fff;
  text-align: center;
  box-shadow: inset 0 0 0 1px rgba(15, 23, 42, 0.06);
}

.week-strip__item.is-active {
  background: rgba(37, 99, 235, 0.08);
  box-shadow: inset 0 0 0 1px rgba(37, 99, 235, 0.24);
}

.week-strip__item span,
.week-strip__item strong {
  display: block;
}

.plan-card,
.notice-card {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
}

.plan-check {
  width: 22px;
  height: 22px;
  border-radius: 50%;
  box-shadow: inset 0 0 0 1px #cbd5e1;
}

.plan-check.is-done {
  background: #16a34a;
  box-shadow: inset 0 0 0 5px #fff;
}

.notice-dot {
  width: 10px;
  height: 10px;
  border-radius: 50%;
  flex: 0 0 auto;
}

.notice-card__body {
  flex: 1 1 auto;
}

.notice-card__body strong,
.notice-card__body p {
  display: block;
  margin: 0;
}

.notice-card__body p {
  margin-top: 4px;
  font-size: 12px;
  line-height: 1.6;
}
</style>
