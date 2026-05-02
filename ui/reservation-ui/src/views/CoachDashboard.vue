<template>
  <div class="app-page coach-page">
    <header class="page-header coach-header">
      <div class="page-header__title page-header__title--left">
        <strong>{{ dashboard.coachName || '教练工作台' }}</strong>
        <span>{{ dashboard.title || '私教工作台' }}</span>
      </div>
      <div class="coach-header__actions">
        <button class="icon-button" type="button" @click="router.push({ name: 'experience-map' })">
          <el-icon><Grid /></el-icon>
        </button>
        <button class="icon-button" type="button" @click="logout">
          <el-icon><SwitchButton /></el-icon>
        </button>
      </div>
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

    <section v-if="activeScene === 'dashboard'" class="surface-card panel">
      <div class="hero-head">
        <img :src="dashboard.avatarUrl || defaultAvatar" alt="coach" />
        <div>
          <strong>{{ dashboard.coachName || '教练' }}</strong>
          <span>{{ dashboard.title || '高级私教' }}</span>
        </div>
      </div>

      <div class="hero-metrics">
        <article>
          <strong>{{ dashboard.todayReservationCount || 0 }}</strong>
          <span>今日课程</span>
        </article>
        <article>
          <strong>{{ pendingAudits.length }}</strong>
          <span>待审核预约</span>
        </article>
        <article>
          <strong>{{ dashboard.boundMemberCount || members.length }}</strong>
          <span>活跃学员</span>
        </article>
      </div>

      <div class="quick-grid">
        <button v-for="entry in quickEntries" :key="entry.scene" class="quick-card" type="button" @click="setScene(entry.scene)">
          <span class="quick-card__icon">{{ entry.icon }}</span>
          <strong>{{ entry.label }}</strong>
        </button>
      </div>

      <div class="section-title">
        <h3>今日课表</h3>
        <span>{{ schedule.scheduleDate || '今日' }}</span>
      </div>

      <article v-for="item in schedule.items" :key="item.startTime + item.endTime" class="row-card">
        <div>
          <strong>{{ item.startTime }} - {{ item.endTime }}</strong>
          <p>{{ item.isReserved ? `${item.memberName} · ${item.sessionName}` : '空闲时段' }}</p>
        </div>
        <small :class="{ reserved: item.isReserved }">{{ item.isReserved ? '已预约' : '可预约' }}</small>
      </article>
    </section>

    <section v-else-if="activeScene === 'schedule'" class="surface-card panel">
      <div class="section-title">
        <h3>排班管理</h3>
        <span>{{ schedule.scheduleDate || '今日排班' }}</span>
      </div>

      <article v-for="item in schedule.items" :key="item.startTime + item.endTime" class="schedule-row">
        <div>
          <strong>{{ item.startTime }} - {{ item.endTime }}</strong>
          <p>{{ item.sessionName || '开放排班' }}</p>
        </div>
        <span class="pill" :class="{ 'pill--success': item.isReserved }">{{ item.isReserved ? '已预约' : '可预约' }}</span>
      </article>

      <button class="primary-button" type="button">保存排班</button>
    </section>

    <section v-else-if="activeScene === 'audits'" class="surface-card panel">
      <div class="section-title">
        <h3>预约审核</h3>
        <span>{{ pendingAudits.length }} 条待处理</span>
      </div>

      <article v-for="audit in pendingAudits" :key="audit.id" class="audit-card">
        <div class="audit-card__head">
          <img :src="audit.memberAvatarUrl || defaultAvatar" alt="member" />
          <div>
            <strong>{{ audit.memberName }}</strong>
            <p>{{ audit.reservationDate }} · {{ audit.timeRange }}</p>
          </div>
        </div>
        <small>{{ audit.sessionName }} · {{ audit.remark || audit.clubName }}</small>
        <div class="button-row">
          <button class="ghost-button" type="button">拒绝</button>
          <button class="primary-button" type="button">通过</button>
        </div>
      </article>
    </section>

    <section v-else-if="activeScene === 'members'" class="surface-card panel">
      <div class="section-title">
        <h3>学员列表</h3>
        <span>{{ members.length }} 位</span>
      </div>

      <article
        v-for="member in members"
        :key="member.memberId"
        class="member-card"
        @click="selectMember(member.memberId, 'member-detail')"
      >
        <img :src="member.avatarUrl || defaultAvatar" alt="member" />
        <div class="member-card__body">
          <div class="member-card__head">
            <strong>{{ member.memberName }}</strong>
            <span class="pill pill--soft">{{ member.membershipName }}</span>
          </div>
          <small>剩余课时：{{ member.remainingSessions }} 节</small>
          <p>最近上课：{{ member.latestReservation || '--' }}</p>
        </div>
      </article>
    </section>

    <section v-else-if="activeScene === 'member-detail'" class="surface-card panel">
      <div class="member-detail">
        <img :src="selectedMember.avatarUrl || defaultAvatar" alt="member" />
        <div>
          <strong>{{ selectedMember.memberName || '学员' }}</strong>
          <span>{{ selectedMember.membershipName || '普通会员' }}</span>
          <small>剩余 {{ selectedMember.remainingSessions || 0 }} 节 · 最近上课 {{ selectedMember.latestReservation || '--' }}</small>
        </div>
      </div>

      <div class="metric-grid">
        <article>
          <strong>{{ selectedMember.remainingSessions || 0 }}</strong>
          <span>剩余课时</span>
        </article>
        <article>
          <strong>{{ memberRecordCount }}</strong>
          <span>训练记录</span>
        </article>
        <article>
          <strong>{{ selectedMember.phoneNumber || '--' }}</strong>
          <span>联系方式</span>
        </article>
      </div>

      <article v-for="item in selectedMemberLessons" :key="item.recordTime + item.title" class="row-card row-card--border">
        <div>
          <strong>{{ item.recordTime }}</strong>
          <p>{{ item.title }}</p>
        </div>
        <small>{{ item.calories }} kcal</small>
      </article>

      <div class="button-row">
        <button class="ghost-button" type="button" @click="setScene('record')">课程记录</button>
        <button class="primary-button" type="button" @click="setScene('messages')">联系学员</button>
      </div>
    </section>

    <section v-else-if="activeScene === 'record'" class="surface-card panel">
      <div class="section-title">
        <h3>课程记录</h3>
        <span>{{ selectedRecord.recordTime || '--' }}</span>
      </div>

      <div class="record-summary">
        <div><span>课程名称</span><strong>{{ selectedRecord.title || '--' }}</strong></div>
        <div><span>训练时长</span><strong>{{ selectedRecord.durationMinutes || 0 }} 分钟</strong></div>
        <div><span>消耗热量</span><strong>{{ selectedRecord.calories || 0 }} kcal</strong></div>
        <div><span>训练地点</span><strong>{{ selectedRecord.locationName || '--' }}</strong></div>
      </div>

      <div class="coach-note">
        <strong>教练总结</strong>
        <p>{{ selectedRecord.summary || '当前记录已同步到学员端训练记录页。' }}</p>
      </div>

      <div class="button-row">
        <button class="ghost-button" type="button">保存记录</button>
        <button class="primary-button" type="button">发送给学员</button>
      </div>
    </section>

    <section v-else-if="activeScene === 'plan'" class="surface-card panel">
      <div class="section-title">
        <h3>训练计划制定</h3>
        <span>{{ trainingPlan.progressText || '未配置' }}</span>
      </div>

      <div class="chip-grid">
        <button class="chip is-active" type="button">{{ trainingPlan.goal || '训练目标' }}</button>
      </div>

      <article v-for="task in trainingPlan.items" :key="task.dayLabel + task.title" class="plan-row">
        <div>
          <strong>{{ task.dayLabel }}</strong>
          <p>{{ task.title }}</p>
        </div>
        <small>{{ task.durationMinutes }}m · {{ task.caloriesTarget }} kcal</small>
      </article>

      <button class="primary-button" type="button">保存计划</button>
    </section>

    <section v-else class="surface-card panel">
      <div class="sub-tabs">
        <button
          v-for="tab in messageTabs"
          :key="tab"
          class="sub-tabs__item"
          :class="{ 'is-active': activeMessageTab === tab }"
          type="button"
          @click="activeMessageTab = tab"
        >
          {{ tab }}
        </button>
      </div>

      <article v-for="message in filteredMessages" :key="message.sentTime + message.content" class="message-row">
        <span class="message-row__icon">{{ message.senderRole === 'coach' ? '教' : '员' }}</span>
        <div class="message-row__body">
          <strong>{{ message.senderName }}</strong>
          <p>{{ message.content }}</p>
        </div>
        <small>{{ message.sentTime }}</small>
      </article>
    </section>
  </div>
</template>

<script setup lang="ts">
import { Grid, SwitchButton } from '@element-plus/icons-vue'
import { ElMessage } from 'element-plus'
import { computed, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/authStore'
import {
  reservationPortalApi,
  type ReservationCoachWorkbenchData,
  type ReservationConversationMessage,
  type ReservationTrainingRecord
} from '@/api/reservationPortal'

type SceneKey = 'dashboard' | 'schedule' | 'audits' | 'members' | 'member-detail' | 'record' | 'plan' | 'messages'

const router = useRouter()
const route = useRoute()
const authStore = useAuthStore()

const scenes: Array<{ key: SceneKey; label: string }> = [
  { key: 'dashboard', label: '工作台' },
  { key: 'schedule', label: '排班' },
  { key: 'audits', label: '审核' },
  { key: 'members', label: '学员' },
  { key: 'member-detail', label: '详情' },
  { key: 'record', label: '记录' },
  { key: 'plan', label: '计划' },
  { key: 'messages', label: '消息' }
]

const sceneFromQuery = computed<SceneKey>(() => {
  const value = String(route.query.scene || 'dashboard')
  return (scenes.find((item) => item.key === value)?.key ?? 'dashboard') as SceneKey
})

const workbench = ref<ReservationCoachWorkbenchData | null>(null)
const activeScene = ref<SceneKey>(sceneFromQuery.value)
const activeMessageTab = ref('全部')
const selectedMemberId = ref<number>(0)
const defaultAvatar = 'https://images.unsplash.com/photo-1566753323558-f4e0952af115?auto=format&fit=crop&w=320&q=80'
const quickEntries: Array<{ scene: SceneKey; label: string; icon: string }> = [
  { scene: 'schedule', label: '排班管理', icon: '排' },
  { scene: 'audits', label: '预约审核', icon: '审' },
  { scene: 'members', label: '学员管理', icon: '员' },
  { scene: 'record', label: '课程记录', icon: '记' }
]
const messageTabs = ['全部', '学员消息', '教练回复']

const dashboard = computed(
  () =>
    workbench.value?.dashboard || {
      coachUserId: 0,
      trainerProfileId: 0,
      coachName: '',
      title: '',
      avatarUrl: '',
      todayReservationCount: 0,
      upcomingReservationCount: 0,
      completedReservationCount: 0,
      boundMemberCount: 0,
      todayReservations: []
    }
)
const schedule = computed(() => workbench.value?.schedule || { scheduleDate: '', items: [] })
const pendingAudits = computed(() => workbench.value?.pendingAudits || [])
const members = computed(() => workbench.value?.members || [])
const records = computed(() => workbench.value?.records || [])
const trainingPlan = computed(() => workbench.value?.trainingPlan || { planName: '', goal: '', startDate: '', endDate: '', progressText: '', items: [] })
const messages = computed(() => workbench.value?.messages || [])

const selectedMember = computed(() => {
  return members.value.find((item) => item.memberId === selectedMemberId.value) || members.value[0] || {
    memberId: 0,
    memberName: '',
    phoneNumber: '',
    avatarUrl: '',
    membershipName: '',
    remainingSessions: 0,
    latestReservation: ''
  }
})

const selectedMemberLessons = computed<ReservationTrainingRecord[]>(() => {
  if (!selectedMember.value.memberName) return records.value
  return records.value.filter((item) => item.title.includes(selectedMember.value.memberName))
})

const selectedRecord = computed<ReservationTrainingRecord>(() => selectedMemberLessons.value[0] || records.value[0] || {
  recordTime: '',
  title: '',
  coachName: '',
  durationMinutes: 0,
  calories: 0,
  locationName: '',
  statusCode: '',
  summary: ''
})

const memberRecordCount = computed(() => selectedMemberLessons.value.length)

const filteredMessages = computed<ReservationConversationMessage[]>(() => {
  if (activeMessageTab.value === '全部') return messages.value
  if (activeMessageTab.value === '学员消息') return messages.value.filter((item) => item.senderRole !== 'coach')
  return messages.value.filter((item) => item.senderRole === 'coach')
})

watch(
  () => route.query.scene,
  () => {
    activeScene.value = sceneFromQuery.value
  }
)

onMounted(async () => {
  const coachUserId = authStore.user?.userId ?? 0
  if (!coachUserId) {
    ElMessage.warning('请先登录教练账号')
    await router.push({ name: 'login' })
    return
  }

  try {
    const response = await reservationPortalApi.getCoachWorkbench(coachUserId)
    workbench.value = response.data
    selectedMemberId.value = response.data.members?.[0]?.memberId || 0
  } catch {
    ElMessage.error('教练工作台数据加载失败')
  }
})

const setScene = (scene: SceneKey): void => {
  activeScene.value = scene
  void router.replace({ query: { scene } })
}

const selectMember = (memberId: number, scene: SceneKey): void => {
  selectedMemberId.value = memberId
  setScene(scene)
}

const logout = async (): Promise<void> => {
  authStore.logout()
  await router.push({ name: 'login' })
}
</script>

<style scoped>
.coach-page {
  background:
    radial-gradient(circle at top left, rgba(59, 130, 246, 0.12), transparent 26%),
    linear-gradient(180deg, #fbfdff 0%, #f3f7ff 100%);
}

.page-header__title--left {
  align-items: flex-start;
}

.coach-header__actions {
  display: flex;
  gap: 8px;
}

.scene-tabs,
.sub-tabs,
.chip-grid {
  display: flex;
  gap: 8px;
  overflow-x: auto;
  scrollbar-width: none;
}

.scene-tabs::-webkit-scrollbar,
.sub-tabs::-webkit-scrollbar,
.chip-grid::-webkit-scrollbar {
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

.hero-head,
.button-row,
.member-detail,
.record-summary div,
.message-row,
.schedule-row,
.row-card,
.member-card__head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
}

.hero-head img,
.audit-card__head img,
.member-card img,
.member-detail img {
  width: 56px;
  height: 56px;
  border-radius: 18px;
  object-fit: cover;
}

.hero-head strong,
.hero-head span,
.member-detail strong,
.member-detail span,
.member-detail small {
  display: block;
}

.hero-head span,
.member-detail span,
.member-detail small,
.row-card p,
.schedule-row p,
.message-row p,
.message-row small {
  color: var(--muted);
}

.hero-metrics,
.quick-grid,
.metric-grid,
.button-row {
  display: grid;
  gap: 10px;
}

.hero-metrics {
  grid-template-columns: repeat(3, minmax(0, 1fr));
  margin: 16px 0;
}

.hero-metrics article,
.quick-card,
.metric-grid article {
  padding: 14px 10px;
  border-radius: 18px;
  background: #fff;
  text-align: center;
}

.hero-metrics strong,
.hero-metrics span,
.quick-card strong,
.quick-card__icon,
.metric-grid strong,
.metric-grid span {
  display: block;
}

.quick-grid {
  grid-template-columns: repeat(4, minmax(0, 1fr));
  margin-bottom: 16px;
}

.quick-card {
  border: 0;
  box-shadow: inset 0 0 0 1px rgba(15, 23, 42, 0.06);
}

.quick-card__icon {
  margin-bottom: 8px;
  color: #2563eb;
  font-size: 18px;
}

.section-title {
  margin: 14px 0 12px;
}

.section-title h3,
.section-title span {
  margin: 0;
}

.section-title span {
  color: var(--muted);
}

.row-card,
.schedule-row,
.member-card,
.audit-card,
.record-summary,
.coach-note,
.message-row,
.plan-row {
  margin-top: 12px;
  padding: 14px;
  border-radius: 18px;
  background: #fff;
  box-shadow: inset 0 0 0 1px rgba(15, 23, 42, 0.06);
}

.row-card strong,
.row-card p,
.schedule-row strong,
.schedule-row p,
.audit-card strong,
.audit-card p,
.audit-card small,
.message-row strong,
.message-row p,
.message-row small,
.plan-row strong,
.plan-row p,
.coach-note strong,
.coach-note p {
  display: block;
  margin: 0;
}

.row-card small,
.schedule-row small {
  color: #94a3b8;
}

.row-card small.reserved {
  color: #2563eb;
  font-weight: 700;
}

.pill {
  padding: 4px 8px;
  border-radius: 999px;
  background: #f1f5f9;
  color: #64748b;
  font-size: 10px;
  font-weight: 700;
}

.pill--success {
  background: #ebf8ef;
  color: #15803d;
}

.pill--soft {
  background: rgba(37, 99, 235, 0.08);
  color: #2563eb;
}

.audit-card__head,
.member-card {
  display: grid;
  grid-template-columns: 56px minmax(0, 1fr);
  gap: 12px;
}

.member-card__body {
  display: grid;
  gap: 8px;
}

.metric-grid {
  grid-template-columns: repeat(3, minmax(0, 1fr));
  margin: 16px 0;
}

.record-summary {
  display: grid;
  gap: 10px;
}

.record-summary span {
  color: var(--muted);
  font-size: 12px;
}

.coach-note {
  margin-top: 16px;
  background: #f8fbff;
}

.coach-note p {
  margin-top: 6px;
  line-height: 1.7;
}

.plan-row,
.message-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.message-row__icon {
  width: 34px;
  height: 34px;
  border-radius: 12px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  background: rgba(37, 99, 235, 0.08);
  color: #2563eb;
  font-weight: 700;
}

.message-row__body {
  flex: 1 1 auto;
}
</style>
