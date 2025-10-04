<template>
  <div class="page">
    <div class="hero-header">
      <div class="left-actions" @click="goBack"><el-icon><ArrowLeft /></el-icon></div>
      <div class="app-title">我的会员</div>
      <div class="right-actions"></div>
    </div>

    <div class="content">
      <div class="toolbar">
        <div class="toolbar-left">
          <el-input v-model="keyword" placeholder="搜索会员姓名/手机" clearable @input="onSearch" />
        </div>
        <el-button type="primary" @click="$router.push('/members/add')">添加会员</el-button>
      </div>

      <!-- 搜索栏下方的操作栏（与 AddMembers 保持一致） -->
      <div class="action-bar" v-if="mode==='bound'">
        <div class="action-left">
          <div class="selected-hint" v-if="selectedBoundIds.size>0">已选 {{ selectedBoundIds.size }} 人</div>
        </div>
        <div class="action-right">
          <el-button type="danger" :disabled="selectedBoundIds.size===0" :loading="unbinding" @click="unbindSelected">取消绑定所选</el-button>
        </div>
      </div>

      <div v-if="mode==='bound'" v-loading="loading" class="list cards-grid">
        <div v-if="boundMembers.length===0" class="empty">暂无绑定会员</div>
        <div
          v-else
          v-for="m in boundMembers"
          :key="m.id"
          :class="['item', { selected: selectedBoundIds.has(m.id) }]"
          @click="toggleBound(m.id)"
        >
          <el-checkbox
            :model-value="selectedBoundIds.has(m.id)"
            @click.stop
            @change="toggleBound(m.id)"
          ></el-checkbox>
          <div class="meta">
            <div class="name">{{ m.userName || '未命名' }}</div>
            <div class="sub">{{ m.phoneNumber || '' }}</div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { bookingService, type BoundMember } from '@/services/bookingService'
import { userService, type AdminUser } from '@/services/userService'
import { useUserStore } from '@/stores/user'
import { ArrowLeft } from '@element-plus/icons-vue'
import { ElMessage } from 'element-plus'
import { onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'

const router = useRouter()
const userStore = useUserStore()

const loading = ref(false)
const binding = ref(false)
const mode = ref<'bound' | 'select'>('bound')
const boundMembers = ref<BoundMember[]>([])
const keyword = ref('')
const candidates = ref<AdminUser[]>([])
const selectedIds = ref<Set<number>>(new Set())
// 新增：已绑定会员选择与批量取消绑定状态
const selectedBoundIds = ref<Set<number>>(new Set())
const unbinding = ref(false)

function goBack() {
  router.push('/home')
}

function normalizePayload(resp: any) {
  const envelope = (resp && typeof resp === 'object' && 'success' in resp && 'data' in resp)
    ? resp
    : (resp?.data ?? resp)
  const data = Array.isArray(envelope?.data) ? envelope.data : (Array.isArray(envelope) ? envelope : [])
  return {
    success: envelope?.success ?? false,
    data,
    message: envelope?.message ?? envelope?.Message ?? ''
  }
}

// 新增：加载教练已绑定会员
async function loadBoundMembers() {
  loading.value = true
  try {
    const coachId = userStore.user?.id ?? 0
    const resp = await bookingService.getBoundMembers(coachId)
    const p = normalizePayload(resp)
    const list: BoundMember[] = Array.isArray(p?.data) ? p.data : []
    boundMembers.value = list
  } catch (e) {
    console.error('loadBoundMembers error', e)
    boundMembers.value = []
  } finally {
    loading.value = false
  }
}

async function loadMembers() {
  loading.value = true
  try {
    let page = 1
    const size = 100
    const all: AdminUser[] = []
    while (true) {
      const resp: any = await userService.list(page, size, { isActive: true })
      const list: AdminUser[] = Array.isArray(resp?.data) ? resp.data : []
      all.push(...list)
      const totalPages: number = Number(resp?.totalPages ?? 1)
      if (page >= totalPages || list.length === 0) break
      page++
    }
    // 仅保留会员（可能字段为 userTypeCode/huiyuan，或 role/member）
    const q = keyword.value.trim().toLowerCase()
    candidates.value = all
      .filter(u => (String(u.userTypeCode || '').toLowerCase() === 'huiyuan') || (String((u as any).role || '').toLowerCase() === 'member'))
      .filter(x => {
        const name = x?.userName ?? (x as any)?.username ?? ''
        const phone = x?.phoneNumber ?? (x as any)?.phone ?? ''
        const email = x?.email ?? ''
        return !q || name.toLowerCase().includes(q) || phone.includes(q) || email.toLowerCase().includes(q)
      })
  } catch (e) {
    console.error('loadMembers error', e)
    ElMessage.error('加载会员列表失败')
  } finally {
    loading.value = false
  }
}

function toggle(id: number) {
  const set = selectedIds.value
  if (set.has(id)) set.delete(id); else set.add(id)
}

function onSearch() { loadMembers() }

async function bindSelected() {
  if (selectedIds.value.size === 0) return
  binding.value = true
  let ok = 0, fail = 0
  const coachId = userStore.user?.id ?? 0
  try {
    for (const memberId of selectedIds.value) {
      try {
        const resp = await bookingService.bindCoach(memberId, coachId)
        const p = normalizePayload(resp)
        if (p.success) ok++; else fail++
      } catch (e) {
        console.error('bind error', e)
        fail++
      }
    }
    if (fail === 0) ElMessage.success(`绑定成功：${ok} 位会员`)
    else if (ok === 0) ElMessage.error('绑定全部失败，请稍后重试')
    else ElMessage.warning(`部分成功：成功 ${ok}，失败 ${fail}`)

    selectedIds.value.clear()
    await loadMembers()
    // 新增：绑定完成后回到已绑定视图并刷新卡片
    await loadBoundMembers()
    mode.value = 'bound'
  } finally {
    binding.value = false
  }
}

function goSelectMode() {
  mode.value = 'select'
  loadMembers()
}
function goBoundMode() { mode.value = 'bound'; loadBoundMembers() }

onMounted(async () => {
  await loadBoundMembers()
  // 不再自动切换到选择模式，改为通过独立页面添加会员
})


async function unbindSelected() {
  if (selectedBoundIds.value.size === 0) return
  unbinding.value = true
  let ok = 0, fail = 0
  const coachId = userStore.user?.id ?? 0
  try {
    for (const memberId of selectedBoundIds.value) {
      try {
        const resp = await bookingService.unbindCoach(memberId, coachId)
        const p = normalizePayload(resp)
        if (p.success) ok++; else fail++
      } catch (e) {
        console.error('unbind error', e)
        fail++
      }
    }
    if (fail === 0) ElMessage.success(`已取消绑定：${ok} 位会员`)
    else if (ok === 0) ElMessage.error('取消绑定全部失败，请稍后重试')
    else ElMessage.warning(`部分成功：成功 ${ok}，失败 ${fail}`)
    selectedBoundIds.value.clear()
    await loadBoundMembers()
  } finally {
    unbinding.value = false
  }
}
function toggleBound(id: number) {
  const set = selectedBoundIds.value
  if (set.has(id)) set.delete(id); else set.add(id)
}
</script>

<style scoped>
.page { display: flex; flex-direction: column; min-height: 100vh; }
.hero-header { display: flex; align-items: center; height: 56px; padding: 0 12px; border-bottom: 1px solid var(--el-border-color); }
.left-actions { width: 48px; display: flex; align-items: center; cursor: pointer; }
.app-title { flex: 1; text-align: center; font-weight: 600; }
.right-actions { width: 48px; }
.content { flex: 1; padding: 12px; }
.toolbar { display: flex; justify-content: space-between; align-items: center; gap: 12px; margin-bottom: 12px; }
.toolbar-left { flex: 1; }

/* 搜索栏下方的操作栏（保持与 AddMembers.vue 一致） */
.action-bar { display: flex; justify-content: space-between; align-items: center; gap: 12px; margin-bottom: 12px; }
.action-left { display: flex; align-items: center; }
.action-right { display: flex; align-items: center; }

/* 两列网格（与 AddMembers.vue 一致） */
.cards-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 12px; }

/* 卡片样式与选中高亮（复用 AddMembers.vue 的 .item） */
.item { display: flex; align-items: center; gap: 12px; border: 1px solid var(--el-border-color); border-radius: 10px; padding: 12px; background: var(--el-bg-color); cursor: pointer; user-select: none; }
.item.selected { border-color: var(--el-color-primary); background: var(--el-color-primary-light-9); }
.meta { display: flex; flex-direction: column; }
.name { font-weight: 700; font-size: 16px; line-height: 22px; }
.sub { color: var(--el-text-color-secondary); font-size: 14px; line-height: 20px; }

/* 提示与空态 */
.selected-hint { color: var(--el-color-primary); background: var(--el-color-primary-light-9); border: 1px solid var(--el-color-primary); border-radius: 12px; padding: 4px 10px; font-size: 13px; }
.empty { color: var(--el-text-color-secondary); text-align: center; padding: 48px 0; }
.list { display: flex; flex-direction: column; gap: 8px; }
.list.cards-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 12px; }
.item { display: flex; align-items: center; gap: 8px; border: 1px solid var(--el-border-color); border-radius: 8px; padding: 8px; background: var(--el-bg-color); }
.meta { display: flex; flex-direction: column; }
.name { font-weight: 600; }
.sub { color: var(--el-text-color-secondary); font-size: 12px; }
.empty { color: var(--el-text-color-secondary); text-align: center; padding: 48px 0; }
</style>