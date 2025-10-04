<template>
  <div class="page">
    <div class="hero-header">
      <div class="left-actions" @click="goBack"><el-icon><ArrowLeft /></el-icon></div>
      <div class="app-title">添加会员</div>
      <div class="right-actions"></div>
    </div>

    <div class="content">
      <div class="toolbar">
        <div class="toolbar-left">
          <el-input v-model="keyword" placeholder="搜索会员姓名/手机" clearable @input="onSearch" />
        </div>
      </div>

      <!-- 将原“底部操作栏”移到搜索栏下面 -->
      <div class="action-bar">
        <div class="action-left">
          <div class="selected-hint" v-if="selectedIds.size>0">已选 {{ selectedIds.size }} 人</div>
        </div>
        <div class="action-right">
          <el-button type="primary" :disabled="selectedIds.size===0" :loading="binding" @click="bindSelected">绑定所选会员</el-button>
        </div>
      </div>

      <div class="list cards-grid" v-loading="loading">
        <div v-if="candidates.length===0" class="empty">暂无会员候选</div>
        <div
          v-for="m in candidates"
          :key="m.id"
          :class="['item', { selected: selectedIds.has(m.id), disabled: boundToMeMap[m.id] }]"
          @click="toggle(m.id)"
        >
          <el-checkbox
            :model-value="selectedIds.has(m.id)"
            :disabled="boundToMeMap[m.id]"
            @click.stop
            @change="toggle(m.id)"
          ></el-checkbox>
          <div class="meta">
            <div class="name">{{ m.userName || '未命名' }}</div>
            <div class="sub">{{ m.phoneNumber || '' }}</div>
            <div class="bound-info" v-if="(boundCoachNamesMap[m.id] || []).length > 0">
              已被
              <span class="coach-name" v-for="(n,i) in boundCoachNamesMap[m.id]" :key="i">
                {{ n }}<span v-if="i < boundCoachNamesMap[m.id].length - 1">、</span>
              </span>
              教练绑定
            </div>
          </div>
        </div>
      </div>

      <!-- 移除页面底部的操作栏 -->
      <!-- 原 .bottom-bar 已删除 -->
    </div>
  </div>
</template>

<script setup lang="ts">
import { bookingService } from '@/services/bookingService'
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
const keyword = ref('')
const candidates = ref<AdminUser[]>([])
const selectedIds = ref<Set<number>>(new Set())
const boundCoachNamesMap = ref<Record<number, string[]>>({})
const boundToMeMap = ref<Record<number, boolean>>({})

function goBack() { router.back() }

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
    const q = keyword.value.trim().toLowerCase()
    candidates.value = all
      .filter(u => (String(u.userTypeCode || '').toLowerCase() === 'huiyuan') || (String((u as any).role || '').toLowerCase() === 'member'))
      .filter(x => {
        const name = x?.userName ?? (x as any)?.username ?? ''
        const phone = x?.phoneNumber ?? (x as any)?.phone ?? ''
        const email = x?.email ?? ''
        return !q || name.toLowerCase().includes(q) || phone.includes(q) || email.toLowerCase().includes(q)
      })
    await loadBoundCoachesForCandidates()
  } catch (e) {
    console.error('loadMembers error', e)
    ElMessage.error('加载会员列表失败')
  } finally {
    loading.value = false
  }
}

async function loadBoundCoachesForCandidates() {
  const myCoachId = Number(userStore.user?.id ?? -1)
  const tasks = candidates.value.map(async (m) => {
    try {
      const resp: any = await bookingService.getBoundCoaches(m.id)
      const payload = resp?.data ?? resp
      const list: any[] = Array.isArray(payload?.data) ? payload.data : Array.isArray(payload) ? payload : []
      const names: string[] = list
        .map(c => c?.coachName ?? c?.userName ?? c?.username ?? c?.name ?? '')
        .filter(Boolean)
      boundCoachNamesMap.value[m.id] = names
      // 新增：当前教练是否已绑定该会员
      boundToMeMap.value[m.id] = list.some(c => Number(c?.coachId ?? -2) === myCoachId)
    } catch {
      boundCoachNamesMap.value[m.id] = []
      boundToMeMap.value[m.id] = false
    }
  })
  await Promise.allSettled(tasks)
}

function toggle(id: number) {
  // 禁用：已被当前教练绑定的会员不可选
  if (boundToMeMap.value[id]) return
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
        const payload = resp?.data ?? resp
        const success = payload?.success ?? payload?.data?.success ?? false
        if (success) ok++; else fail++
      } catch (e) {
        console.error('bind error', e)
        fail++
      }
    }
    if (fail === 0) ElMessage.success(`绑定成功：${ok} 位会员`)
    else if (ok === 0) ElMessage.error('绑定全部失败，请稍后重试')
    else ElMessage.warning(`部分成功：成功 ${ok}，失败 ${fail}`)

    selectedIds.value.clear()
    router.push('/members')
  } finally {
    binding.value = false
  }
}

onMounted(async () => {
  await loadMembers()
})
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

/* 新增：搜索栏下方的操作栏 */
.action-bar { display: flex; justify-content: space-between; align-items: center; gap: 12px; margin-bottom: 12px; }
.action-left { display: flex; align-items: center; }
.action-right { display: flex; align-items: center; }

/* 两列网格 */
.cards-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 12px; }

/* 卡片样式与选中高亮 */
.item { display: flex; align-items: center; gap: 12px; border: 1px solid var(--el-border-color); border-radius: 10px; padding: 12px; background: var(--el-bg-color); cursor: pointer; user-select: none; }
.item.selected { border-color: var(--el-color-primary); background: var(--el-color-primary-light-9); }
.item.disabled { opacity: 0.6; border-style: dashed; cursor: not-allowed; }

.meta { display: flex; flex-direction: column; }
.name { font-weight: 700; font-size: 16px; line-height: 22px; }
.sub { color: var(--el-text-color-secondary); font-size: 14px; line-height: 20px; }
.bound-info { margin-top: 4px; color: var(--el-color-warning); font-size: 13px; }
.coach-name { color: var(--el-color-primary); font-weight: 600; }

/* 删除底部操作栏样式 */
/* .bottom-bar, .bottom-left, .bottom-right 移除 */

/* 轻量标签样式 */
.selected-hint { color: var(--el-color-primary); background: var(--el-color-primary-light-9); border: 1px solid var(--el-color-primary); border-radius: 12px; padding: 4px 10px; font-size: 13px; }

.empty { color: var(--el-text-color-secondary); text-align: center; padding: 48px 0; }
</style>