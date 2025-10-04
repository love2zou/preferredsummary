<template>
  <div class="page-container">
    <div class="hero-header">
      <div class="header-left" @click="goBack" role="button" aria-label="返回">
        <el-icon class="back-btn"><ArrowLeft /></el-icon>
      </div>
      <div class="app-title">个人信息</div>
      <div class="header-right"></div>
    </div>
    <!-- 加入骨架屏和空状态 -->
    <div v-if="loading" class="mini-card">
      <el-skeleton animated :rows="6" />
    </div>
    <template v-else>
      <template v-if="profile">
        <!-- 资料卡片 -->
        <section class="mini-card profile-card">
          <div class="profile-header">
            <div class="avatar" v-if="profile?.profilePictureUrl">
              <img :src="profile?.profilePictureUrl" alt="avatar" />
            </div>
            <div class="avatar" v-else>
              <el-icon><User /></el-icon>
            </div>
            <!-- template：username 右侧显示用户类型标签，并使用返回的颜色 -->
            <div class="user-info">
              <!-- template：username 右侧显示用户类型标签，并使用返回的颜色 -->
              <div class="name-row">
                <div class="username">{{ displayName }}</div>
                <el-tag class="user-type-tag" size="small" :style="userTypeTagStyle">{{ userTypeText }}</el-tag>
              </div>
              <div class="user-sub">{{ profile?.email || '-' }}</div>
            </div>
            <el-button class="edit-btn" type="primary" size="small" @click="openEdit">编辑资料</el-button>
          </div>

          <!-- template: 资料卡片 - 更新统计栏 -->
          <div class="profile-stats">
            <div class="stat-item">
              <div class="label">注册日期</div>
              <div class="value">{{ formatDateOnly(profile?.crtTime) }}</div>
            </div>
            <div class="stat-item">
              <div class="label">最近登录</div>
              <div class="value">{{ formatDateTime(profile?.lastLoginTime) }}</div>
            </div>
          </div>
        </section>

        <!-- 详细信息卡片 -->
        <section class="mini-card detail-card">
          <div class="detail-row">
            <div class="detail-label">
              <el-icon><Phone /></el-icon>
              <span>电话</span>
            </div>
            <div class="detail-value">{{ profile?.phoneNumber || '-' }}</div>
          </div>
          <div class="detail-row">
            <div class="detail-label">
              <el-icon><Message /></el-icon>
              <span>邮箱</span>
            </div>
            <div class="detail-value">{{ profile?.email || '-' }}</div>
          </div>
          <div class="detail-row">
            <div class="detail-label">
              <el-icon><EditPen /></el-icon>
              <span>个人简介</span>
            </div>
            <div class="detail-value bio">{{ profile?.bio || '暂无简介' }}</div>
          </div>
        </section>

        <!-- 编辑抽屉 -->
        <el-drawer v-model="editVisible" :with-header="false" size="80%" direction="rtl">
          <div class="drawer-container">
            <h3 class="drawer-title">编辑资料</h3>
            <el-form ref="formRef" :model="editForm" label-width="80px">
              <el-form-item label="邮箱" prop="email">
                <el-input v-model="editForm.email" placeholder="请输入邮箱" />
              </el-form-item>
              <el-form-item label="电话" prop="phoneNumber">
                <el-input v-model="editForm.phoneNumber" placeholder="请输入电话" />
              </el-form-item>
              <el-form-item label="简介" prop="bio">
                <el-input v-model="editForm.bio" type="textarea" :rows="3" placeholder="请输入个人简介" />
              </el-form-item>
            </el-form>
            <div class="drawer-actions">
              <el-button @click="editVisible = false">取消</el-button>
              <el-button type="primary" :loading="saving" @click="saveEdit">保存</el-button>
            </div>
          </div>
        </el-drawer>
      </template>
      <template v-else>
        <section class="mini-card empty-card">
          <div class="empty-tip">暂无个人信息数据</div>
          <el-button type="primary" size="small" @click="fetchProfile">刷新</el-button>
        </section>
      </template>
    </template>
  </div>
</template>

<script setup lang="ts">
import { userService, type AdminUser } from '@/services/userService'
import { useUserStore } from '@/stores/user'
import { ArrowLeft, EditPen, Message, Phone, User } from '@element-plus/icons-vue'
import { ElForm, ElMessage } from 'element-plus'
import { computed, onMounted, reactive, ref } from 'vue'
import { useRouter } from 'vue-router'

const router = useRouter()
const userStore = useUserStore()

const loading = ref(false)
const saving = ref(false)
const editVisible = ref(false)
const formRef = ref<InstanceType<typeof ElForm>>()

const profile = ref<AdminUser | null>(null)
const editForm = reactive({ email: '', phoneNumber: '', bio: '' })

const displayName = computed(() => profile.value?.userName || userStore.user?.username || '-')

const userId = computed(() => userStore.user?.id ?? 0)

// 数据加载：失败时回填本地登录信息
const fetchProfile = async () => {
  if (!userId.value) return
  const hasToken = !!localStorage.getItem('token')
  if (!hasToken) return
  loading.value = true
  try {
    const resp = await userService.getDetail(userId.value)
    const data = (resp as any)?.data ?? resp
    if (!data) throw new Error('无有效用户数据')
    profile.value = data
    editForm.email = data?.email ?? ''
    editForm.phoneNumber = data?.phoneNumber ?? ''
    editForm.bio = data?.bio ?? ''
  } catch (e: any) {
    if (e?.response?.status !== 401) {
      ElMessage.error('加载用户信息失败')
    }
  }
  // 回填本地信息（仅在接口没有拿到数据时）
  if (!profile.value && userStore.user) {
    const u = userStore.user
    profile.value = {
      id: u.id,
      userName: u.username,
      email: u.email ?? '',
      phoneNumber: u.phone ?? '',
      profilePictureUrl: u.avatar ?? '',
      bio: '',
      userTypeCode: null,
      userToSystemCode: null,
      isActive: true,
      isEmailVerified: undefined,
      crtTime: u.createdAt,
      updTime: undefined,
      lastLoginTime: undefined
    }
    if (!editForm.email) editForm.email = u.email ?? ''
    if (!editForm.phoneNumber) editForm.phoneNumber = u.phone ?? ''
    if (!editForm.bio) editForm.bio = ''
  }
  loading.value = false
}

function openEdit() {
  editVisible.value = true
}

async function saveEdit() {
  if (!userId.value) return
  saving.value = true
  try {
    await userService.update(userId.value, {
      email: editForm.email,
      phoneNumber: editForm.phoneNumber,
      bio: editForm.bio
    })
    ElMessage.success('保存成功')
    editVisible.value = false
    await fetchProfile()
  } catch (e) {
    ElMessage.error('保存失败')
  } finally {
    saving.value = false
  }
}

function goBack() {
  router.back()
}

function formatDateOnly(str?: string | null) {
  if (!str) return '-'
  try { return new Date(str).toLocaleDateString('zh-CN') } catch { return '-' }
}
function formatDateTime(str?: string | null) {
  if (!str) return '-'
  try { return new Date(str).toLocaleString('zh-CN') } catch { return '-' }
}
const userTypeText = computed(() => {
  const n = profile.value?.userTypeName
  if (n && n.trim()) return n
  const code = profile.value?.userTypeCode
  if (!code) return '-'
  switch (code) {
    case 'trainer': return '教练'
    case 'member': return '会员'
    default: return code
  }
})

const userTypeTagStyle = computed(() => {
  const hex = profile.value?.userTypeHexColor
  const rgb = profile.value?.userTypeRgbColor
  return (hex || rgb) ? { color: hex || '#fff', backgroundColor: rgb || 'rgba(0,0,0,0.06)' } : {}
})
const userTypeTagType = computed(() => {
  const key = String(profile.value?.userTypeCode || '').toLowerCase()
  return key === 'trainer' ? 'warning' : key === 'admin' ? 'danger' : 'success'
})
onMounted(fetchProfile)
</script>

<style scoped>
.page-container { padding: 16px; }
/* 顶部栏样式：与 Home.vue 的 hero-header 保持一致 */
.hero-header {
  height: 48px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0 12px;
  background: rgba(255,255,255,0.85);
  backdrop-filter: saturate(180%) blur(8px);
  border-bottom: 1px solid rgba(0,0,0,0.06);
}
.app-title { font-size: 16px; font-weight: 600; color: var(--text-color); }
.back-btn { font-size: 18px; cursor: pointer; padding: 6px; border-radius: 50%; }
.page-title { font-weight: 600; }
.header-placeholder { width: 36px; }

/* 空状态样式 */
.empty-card { padding: 16px; text-align: center; }
.empty-tip { color: var(--text-secondary); margin-bottom: 8px; }

.mini-card { background: var(--card-bg); border: 1px solid var(--border-color); border-radius: var(--radius); box-shadow: var(--shadow); margin-top: 12px; }
.profile-card { padding: 12px; }
.profile-header { display: flex; align-items: center; gap: 12px; }
.avatar { width: 56px; height: 56px; border-radius: 50%; background: #f1f5f9; display: grid; place-items: center; overflow: hidden; }
.avatar img { width: 100%; height: 100%; object-fit: cover; }
.user-info { flex: 1; }
.username { font-size: 16px; font-weight: 600; color: var(--text-color); }
.user-sub { font-size: 13px; color: var(--text-secondary); }
.edit-btn { align-self: center; }

.profile-stats { display: grid; grid-template-columns: repeat(2, 1fr); gap: 12px; margin-top: 12px; }
.stat-item { padding: 8px; border: 1px dashed var(--border-color); border-radius: 8px; }
.label { font-size: 12px; color: var(--text-secondary); }
.value { font-size: 14px; font-weight: 500; color: var(--text-color); }
.value.inactive { color: var(--danger-color); }

.detail-card { padding: 8px 12px; }
.detail-row { display: flex; align-items: center; justify-content: space-between; padding: 10px 0; border-bottom: 1px solid var(--border-color); }
.detail-row:last-child { border-bottom: none; }
.detail-label { display: flex; align-items: center; gap: 8px; color: var(--text-secondary); }
.detail-value { color: var(--text-color); }
.detail-value.bio { white-space: pre-wrap; }

.action-card { padding: 12px; display: flex; justify-content: center; }

.drawer-container { padding: 16px; }
.drawer-title { margin: 0 0 12px; font-size: 16px; font-weight: 600; }
.drawer-actions { display: flex; justify-content: flex-end; gap: 12px; padding-top: 8px; }

.name-row { display: flex; align-items: center; gap: 8px; }
.user-type-tag { transform: translateY(-1px); }
</style>