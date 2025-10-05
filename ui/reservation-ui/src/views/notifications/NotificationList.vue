<template>
  <div class="page">
    <div class="hero-header">
      <div class="left-actions" @click="goBack">
        <el-icon><ArrowLeft /></el-icon>
      </div>
      <div class="app-title">我的消息</div>
      <div class="right-actions"></div>
    </div>

    <div class="content">
      <!-- 旧的 toolbar 移除，改为数据项 chips 过滤 -->
      <div class="filters">
        <div class="chip" :class="{ active: !typeFilter }" @click="setType('')">类型: 全部</div>
        <div class="chip" v-for="t in typeOptions" :key="t" :class="{ active: typeFilter === t }" @click="setType(t)">{{ t }}</div>
        <div class="divider"></div>
        <div class="chip" :class="{ active: statusFilter === undefined }" @click="setStatus(undefined)">状态: 全部</div>
        <div class="chip" :class="{ active: statusFilter === 0 }" @click="setStatus(0)">未读</div>
        <div class="chip" :class="{ active: statusFilter === 1 }" @click="setStatus(1)">已读</div>
      </div>
  
      <!-- 滚动区域，滚动到底部自动加载更多 -->
      <div
        class="scroll-area"
        v-infinite-scroll="loadMore"
        :infinite-scroll-disabled="loading || loadingMore || !hasMore"
        :infinite-scroll-distance="80"
      >
        <div v-if="loading" class="hint">正在加载...</div>
        <div v-else-if="items.length === 0" class="hint">暂无消息</div>
        <div v-else class="list">
          <div class="card" v-for="it in items" :key="it.id" :class="[getTypeClass(it), getStatusClass(it)]">
            <div class="line1">
              <div class="type-icon">{{ getTypeIcon(it) }}</div>
              <span class="title">{{ it.name || it.title || '无标题' }}</span>
              <el-tag size="small" :type="getTypeTag(it)">{{ getTypeText(it) }}</el-tag>
            </div>
            <div class="content-text">{{ it.content || it.message || '' }}</div>
            <div class="line2">
              <span class="meta">{{ formatTime(it) }}</span>
              <el-tag v-if="isRead(it)" type="success" size="small">已读</el-tag>
              <el-button v-else size="small" type="primary" @click="markAsRead(it)">标记已读</el-button>
            </div>
          </div>
        </div>
        <div class="load-hint" v-if="loadingMore">加载中...</div>
        <div class="load-hint" v-else-if="!hasMore && items.length > 0">没有更多了</div>
      </div>
    </div>

    <!-- 移除分页组件 -->
    <!--
    <div class="pager">
      <el-pagination
        background
        layout="prev, pager, next"
        :page-size="pageSize"
        :current-page="page"
        :total="total"
        @current-change="changePage"
      />
    </div>
    -->
  </div>
</template>

<script setup lang="ts">
import { notificationService, type NotificationItem } from '@/services/notificationService'
import { useUserStore } from '@/stores/user'
import { ArrowLeft } from '@element-plus/icons-vue'
import { ElMessage } from 'element-plus'
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'

const router = useRouter()
const userStore = useUserStore()

const items = ref<NotificationItem[]>([])
const loading = ref(false)
const page = ref(1)
const pageSize = ref(10)
const total = ref(0)

const keyword = ref('')
const filterStatus = ref<number | undefined>(undefined)
const filterType = ref<string>('')

const goBack = () => router.back()

function normalizePaged(resp: any) {
  const dataArr = Array.isArray(resp?.data) ? resp.data : (Array.isArray(resp?.Data) ? resp.Data : [])
  const t = resp?.total ?? resp?.Total ?? 0
  const p = resp?.page ?? resp?.Page ?? 1
  const ps = resp?.pageSize ?? resp?.PageSize ?? pageSize.value
  return { dataArr, t, p, ps }
}

// 移除旧的筛选输入变量，改为 chips 选择
const typeFilter = ref<string>('')
const statusFilter = ref<number | undefined>(undefined)

// 前端过滤后的展示数据
const displayItems = computed(() => {
  return items.value.filter((it) => {
    const typeText = getTypeText(it)
    const typeOk = !typeFilter.value || typeFilter.value === typeText
    const statusOk = statusFilter.value === undefined
      ? true
      : (statusFilter.value === 1 ? isRead(it) : !isRead(it))
    return typeOk && statusOk
  })
})

// 从当前数据里提取“类型”数据项供点击
const typeOptions = computed(() => {
  const set = new Set<string>()
  items.value.forEach((it) => {
    const text = String(getTypeText(it) || '').trim()
    if (text) set.add(text)
  })
  return Array.from(set)
})

function setType(t: string) { typeFilter.value = t }
function setStatus(s: number | undefined) { statusFilter.value = s }

// 统一发送时间为 yyyy-MM-dd HH:mm:ss
function pad(n: number) { return String(n).padStart(2, '0') }
function formatDate(d: Date) {
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}:${pad(d.getSeconds())}`
}
function formatTime(it: NotificationItem) {
  const raw: any = it.createdAt || it.crtTime || ''
  if (!raw) return ''
  // 兼容后端返回的字符串/ISO/时间戳
  let d: Date
  if (typeof raw === 'number') {
    d = new Date(raw)
  } else {
    const s = String(raw).replace('T', ' ').replace('Z', '').replace(/\.\d+$/, '')
    d = new Date(s)
  }
  if (isNaN(d.getTime())) return String(raw)
  return formatDate(d)
}

// 仅保留基础查询请求，无复杂筛选参数（前端 chips 过滤）
async function loadList() {
  const receiver = String(
    userStore.user?.username || localStorage.getItem('username') || userStore.user?.phone || ''
  )
  if (!receiver) return
  loading.value = true
  try {
    const resp = await notificationService.list({
      page: page.value,
      size: pageSize.value,
      receiver
    })
    const { dataArr, t, p, ps } = normalizePaged(resp)
    items.value = (dataArr || []).map((x: any) => ({
      id: x.id ?? x.Id,
      name: x.name ?? x.Name,
      content: x.content ?? x.Content,
      notifyType: x.notifyType ?? x.NotifyType,
      notifyStatus: x.notifyStatus ?? x.NotifyStatus,
      isRead: x.isRead ?? x.IsRead,
      sendUser: x.sendUser ?? x.SendUser,
      receiver: x.receiver ?? x.Receiver,
      createdAt: x.createdAt ?? x.CrtTime ?? x.crtTime,
      crtTime: x.crtTime ?? x.CrtTime ?? x.createdAt
    }))
    items.value.sort((a, b) => getTimestamp(b.createdAt || b.crtTime) - getTimestamp(a.createdAt || a.crtTime))
    total.value = t
    page.value = p
    pageSize.value = ps
  } catch (e) {
    ElMessage.error('加载消息失败')
  } finally {
    loading.value = false
  }
}

async function markAsRead(it: NotificationItem) {
  if (!it?.id) return
  try {
    await notificationService.markRead(it.id)
    it.isRead = 1
  } catch {
    ElMessage.error('标记已读失败')
  }
}

function isRead(it: NotificationItem) {
  const v = it?.isRead
  return v === true || v === 1
}

function getTypeClass(it: NotificationItem) {
  const t = String((it.notifyType)).toLowerCase()
  if (t.includes('alert') || t.includes('告警')) return 'type-alert'
  if (t.includes('remind') || t.includes('提醒')) return 'type-remind'
  return 'type-notice'
}

function getTypeIcon(it: NotificationItem) {
  const t = String((it.notifyType)).toLowerCase()
  if (t.includes('alert') || t.includes('告警')) return '🚨'
  if (t.includes('remind') || t.includes('提醒')) return '🔔'
  return '📢'
}

function getTypeTag(it: NotificationItem) {
  const t = String((it.notifyType)).toLowerCase()
  if (t.includes('alert') || t.includes('告警')) return 'danger'
  if (t.includes('remind') || t.includes('提醒')) return 'warning'
  return 'info'
}
function getTypeText(it: NotificationItem): string {
  const t = String((it.notifyType)).toLowerCase()
  if (t.includes('alert') || t.includes('告警')) return '告警'
  if (t.includes('remind') || t.includes('提醒')) return '提醒'
  if (t.includes('notice') || t.includes('通知')) return '通知'
  return String(it.notifyType || '')
}

function getStatusClass(it: NotificationItem) {
  return isRead(it) ? 'status-read' : 'status-unread'
}

function getTimestamp(raw: any): number {
  if (!raw) return 0
  if (typeof raw === 'number') return raw
  const s = String(raw).replace('T', ' ').replace('Z', '').replace(/\.\d+$/, '')
  const d = new Date(s)
  return isNaN(d.getTime()) ? 0 : d.getTime()
}

// 加载更多：追加后统一按发送时间倒序
async function loadMore() {
  if (!receiver) return
  loading.value = true
  try {
    const resp = await notificationService.list({
      page: page.value,
      size: pageSize.value,
      receiver
    })
    const { dataArr, t, p, ps } = normalizePaged(resp)
    const more = (dataArr || []).map((x: any) => ({
      id: x.id ?? x.Id,
      name: x.name ?? x.Name,
      content: x.content ?? x.Content,
      notifyType: x.notifyType ?? x.NotifyType,
      notifyStatus: x.notifyStatus ?? x.NotifyStatus,
      isRead: x.isRead ?? x.IsRead,
      sendUser: x.sendUser ?? x.SendUser,
      receiver: x.receiver ?? x.Receiver,
      createdAt: x.createdAt ?? x.CrtTime ?? x.crtTime,
      crtTime: x.crtTime ?? x.CrtTime ?? x.createdAt
    }))
    items.value = items.value.concat(more)
    items.value.sort((a, b) => getTimestamp(b.createdAt || b.crtTime) - getTimestamp(a.createdAt || a.crtTime))
    total.value = t
    page.value = p
    pageSize.value = ps
  } catch (e) {
    ElMessage.error('加载消息失败')
  } finally {
    loading.value = false
  }
}

function reload() {
  page.value = 1
  loadList()
}

function changePage(p: number) {
  page.value = p
  loadList()
}

onMounted(loadList)
</script>

<style scoped>
/* 移动端 APP 风格：吸顶、毛玻璃头部，卡片更柔和 */
.page { display: flex; flex-direction: column; min-height: 100dvh; background: var(--bg-page); }
.hero-header { position: sticky; top: 0; z-index: 10; display: flex; align-items: center; height: 48px; padding: 0 12px; background: rgba(255,255,255,0.9); backdrop-filter: saturate(180%) blur(8px); border-bottom: 1px solid rgba(0,0,0,0.06); }
.left-actions { width: 48px; display: flex; align-items: center; cursor: pointer; }
.app-title { flex: 1; text-align: center; font-weight: 600; }
.right-actions { width: 48px; }
.content { flex: 1; padding: 12px; }
.filters { display: flex; flex-wrap: wrap; gap: 8px; align-items: center; margin-bottom: 12px; }
.divider { width: 1px; height: 22px; background: var(--el-border-color); margin: 0 4px; }
.chip { padding: 4px 10px; border-radius: 999px; border: 1px solid var(--el-border-color); background: #fff; cursor: pointer; font-size: 12px; }
.chip.active { background: var(--el-color-primary-light-9); border-color: var(--el-color-primary); color: var(--el-color-primary); }

/* 列表卡片上下间距 */
.list { display: flex; flex-direction: column; gap: 18px; }

/* 淡化边框 + 柔和阴影 + 更舒适留白 */
.card { border: 1px solid var(--el-border-color-light); border-radius: 14px; padding: 14px; box-shadow: 0 8px 20px rgba(0,0,0,0.06); background: #fff; transition: transform .2s ease, box-shadow .2s ease; }
.card:hover { transform: translateY(-2px); box-shadow: 0 14px 28px rgba(0,0,0,0.09); }
.card .line1 { display: flex; align-items: center; gap: 10px; margin-bottom: 8px; }
.card .title { font-weight: 600; flex: 1; }
.type-icon { font-size: 18px; line-height: 1; }
.content-text { color: var(--el-text-color-secondary); font-size: 14px; }
.line2 { display: flex; justify-content: space-between; align-items: center; margin-top: 10px; }

/* 类型左侧细条（保留），但由状态样式覆盖具体颜色 */
.card.type-notice { box-shadow: inset 2px 0 0 var(--el-color-info), 0 8px 20px rgba(0,0,0,0.06); }
.card.type-remind { box-shadow: inset 2px 0 0 var(--el-color-warning), 0 8px 20px rgba(0,0,0,0.06); }
.card.type-alert { box-shadow: inset 2px 0 0 var(--el-color-danger), 0 8px 20px rgba(0,0,0,0.06); }

/* 根据已读/未读覆盖左侧色条颜色（定义在类型样式之后以覆盖） */
.card.status-unread { box-shadow: inset 2px 0 0 #ea8c37, 0 8px 20px rgba(0, 0, 0, 0.06); }
.card.status-read { box-shadow: inset 2px 0 0 #d7d7d7, 0 8px 20px rgba(0, 0, 0, 0.06); }

/* “没有更多了” 提示弱化、居中且与卡片有间距 */
.load-hint { text-align: center; color: var(--el-text-color-disabled); font-size: 12px; margin-top: 12px; padding: 8px 0; }
.hint { color: var(--el-text-color-secondary); font-size: 13px; }
</style>