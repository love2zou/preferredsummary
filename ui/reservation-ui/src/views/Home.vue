<template>
  <div class="home-page">
    <div class="layout-stack">
      <!-- 合并：顶部横幅 + 功能入口，同一张卡更连贯 -->
      <section class="mini-card hero-card">
        <!-- 顶栏：融入 hero 卡片顶部，减少割裂感 -->
        <div class="hero-header">
          <div class="app-title">首页</div>
          <el-dropdown placement="bottom-end" trigger="click">
            <span class="header-action" role="button" aria-label="更多">
              <el-icon><MoreFilled /></el-icon>
            </span>
            <template #dropdown>
              <el-dropdown-menu>
                <el-dropdown-item @click="$router.push('/profile')">个人信息</el-dropdown-item>
                <el-dropdown-item divided @click="confirmLogout">退出登录</el-dropdown-item>
              </el-dropdown-menu>
            </template>
          </el-dropdown>
        </div>

        <div class="hero-banner">
          <div class="hero-text">
            <h2>健身，让生活更有力量</h2>
            <p>科学训练 · 合理饮食 · 持续打卡</p>
          </div>
        </div>

        <div class="section-divider"></div>
        <div class="feature-grid">
          <div class="feature-item" @click="$router.push('/booking/create')">
            <div class="feature-icon">🏋️</div>
            <div class="feature-text">健身约课</div>
          </div>
          <div class="feature-item" @click="$router.push('/booking')">
            <div class="feature-icon">📆</div>
            <div class="feature-text">我的约课</div>
          </div>
          <!-- 新增：我的会员（仅教练可见） -->
          <!-- 模块入口：将 v-if 改为使用 isTrainerUI -->
          <div class="feature-item" v-if="isTrainerUI" @click="$router.push('/members')">
            <div class="feature-icon">👥</div>
            <div class="feature-text">我的会员</div>
          </div>
          <div class="feature-item" @click="$router.push('/profile')">
            <div class="feature-icon">💬</div>
            <div class="feature-text">我的消息</div>
          </div>
          <div class="feature-item" @click="$router.push('/fitness')">
            <div class="feature-icon">💡</div>
            <div class="feature-text">关于健身</div>
          </div>
        </div>
      </section>

      <!-- 贴士保持独立卡片但风格一致 -->
      <section class="mini-card tips-card">
        <h3 class="tips-title">健身小贴士</h3>
        <ul class="tips-list">
          <li>运动前做好热身，降低受伤风险</li>
          <li>训练过程中及时补水，保持状态</li>
          <li>每次训练控制在45-60分钟，避免过度</li>
        </ul>
      </section>
    </div>
  </div>
</template>

<script setup lang="ts">
import { useBookingStore, type Booking } from '@/stores/booking'
import { useUserStore } from '@/stores/user'
import { ElMessageBox } from 'element-plus'
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'

const router = useRouter()
const userStore = useUserStore()
const bookingStore = useBookingStore()
const recentBookings = ref<Booking[]>([])

const onLogout = async () => {
  try {
    if (typeof userStore.logout === 'function') {
      await userStore.logout()
    }
  } finally {
    router.replace('/login')
  }
}
const getStatusText = (status: string) => {
  const statusMap = {
    pending: '待确认',
    confirmed: '已确认',
    cancelled: '已取消',
    completed: '已完成'
  }
  return statusMap[status as keyof typeof statusMap] || status
}

const confirmLogout = async () => {
  try {
    await ElMessageBox.confirm('确定要退出登录吗？', '提示', {
      type: 'warning',
      confirmButtonText: '退出',
      cancelButtonText: '取消'
    })
    await onLogout()
  } catch {
    // 用户取消
  }
}
onMounted(async () => {
  if (userStore.user) {
    await bookingStore.fetchUserBookings(userStore.user.id)
    recentBookings.value = bookingStore.bookings.slice(0, 3)
  }
})
// 新增：基于 userTypeCode 的教练判断回退
const isTrainerUI = computed(() => {
  if (userStore.isTrainer) return true
  const code = String(localStorage.getItem('userTypeCode') || '').toLowerCase()
  return code === 'jiaolian'
})
</script>

<style scoped>
.home-page {
  /* 新增：PC 居中显示，移动端满宽 */
  max-width: var(--app-max-width);
  margin: 0 auto;
  padding: 16px;
}

/* 顶部栏（小程序/APP样式） */
.app-header {
  height: 48px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0 12px;
  background: #fff;
  border-radius: var(--radius);
  box-shadow: var(--shadow);
  border: 1px solid var(--border-color);
  margin-bottom: 12px;
  position: sticky;
  top: 0;
  z-index: 10;
}
.app-title {
  font-size: 16px;
  font-weight: 600;
  color: var(--text-color);
}
.header-action {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 32px;
  height: 32px;
  border-radius: 8px;
  color: var(--text-color);
  cursor: pointer;
  transition: background-color .2s ease, color .2s ease;
}
.header-action:hover {
  background: rgba(0,0,0,0.06);
  color: var(--primary-color);
}
.logout-btn { display: none; }
.logout-btn {
  background: var(--primary-color);
  color: #fff;
  border: 0;
  padding: 6px 12px;
  border-radius: var(--radius);
  font-size: 13px;
  cursor: pointer;
}
.logout-btn:hover {
  background: var(--primary-color-600);
}

/* 顶部宣传区域：扁平化渐变，不依赖图片 */
.promo-section {
  height: 33vh;
  min-height: 220px;
  position: relative;
  border-radius: var(--radius);
  overflow: hidden;
  box-shadow: var(--shadow);
  margin-bottom: 16px;
  background: linear-gradient(135deg, var(--primary-color) 0%, var(--primary-color-600) 100%);
}
.promo-text {
  position: absolute;
  left: 16px;
  bottom: 16px;
  color: #fff;
  text-shadow: 0 2px 8px rgba(0,0,0,0.2);
}
.promo-text h2 { margin: 0; font-size: 22px; font-weight: 700; }
.promo-text p { margin: 6px 0 0; font-size: 13px; opacity: .95; }

/* 功能项：小程序风格（圆角、轻阴影、扁平） */
.feature-section { padding: 16px; }
.feature-item {
  background: #fff;
  border-radius: var(--radius);
  box-shadow: var(--shadow);
  border: 1px solid var(--border-color);
  padding: 14px 8px;
  text-align: center;
  cursor: pointer;
  transition: transform .15s ease;
}
.feature-item:hover { transform: translateY(-2px); }
.feature-icon { font-size: 22px; margin-bottom: 8px; }
.feature-text { font-size: 13px; color: var(--text-color); }

/* 小贴士 */
.tips-section { margin-top: 16px; padding: 16px; }
.tips-section h3 { margin: 0 0 8px; font-size: 16px; font-weight: 600; color: var(--text-color); }
.tips-list { margin: 0; padding-left: 18px; color: var(--text-secondary); font-size: 13px; }

@media (max-width: 768px) {
  .promo-text h2 { font-size: 18px; }
  .promo-text p { font-size: 12px; }
}

/* 顶栏融入 hero 卡片：玻璃质感，解除割裂 */
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
.header-action {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 32px;
  height: 32px;
  border-radius: 8px;
  color: var(--text-color);
  cursor: pointer;
  transition: background-color .2s ease, color .2s ease;
}
.header-action:hover { background: rgba(0,0,0,0.06); color: var(--primary-color); }

/* 移除旧隐藏规则影响（保持选择器但覆盖） */
.app-header, .logout-btn { /* no-op: 旧选择器保留以避免冲突 */ }

/* hero 卡样式保留 */
.hero-card { padding: 0; overflow: hidden; }
.hero-banner {
  height: 240px;
  background: linear-gradient(135deg, var(--primary-color), var(--primary-color-600));
  position: relative;
}
.hero-text { position: absolute; left: 16px; bottom: 16px; color: #fff; }
.hero-text h2 { margin: 0; font-size: 22px; font-weight: 700; }
.hero-text p { margin: 6px 0 0; font-size: 13px; opacity: .95; }
.section-divider { height: 1px; background: var(--border-color); margin: 0 16px; opacity: .6; }
.feature-grid { display: grid; grid-template-columns: repeat(2, 1fr); gap: 12px; padding: 16px; }
.feature-item { background: var(--bg-page); border: 1px solid var(--border-color); border-radius: var(--radius); padding: 14px 10px; text-align: center; box-shadow: none; cursor: pointer; transition: transform .15s ease; }
.feature-item:hover { transform: translateY(-2px); }
.feature-icon { font-size: 22px; margin-bottom: 8px; }
.feature-text { font-size: 13px; color: var(--text-color); }
.tips-card { padding: 16px; }
.tips-title { margin: 0 0 8px; font-size: 16px; font-weight: 600; color: var(--text-color); }
.tips-list { margin: 0; padding-left: 18px; color: var(--text-secondary); font-size: 13px; }

/* 兼容：老的分散版块样式不再使用 */
.feature-section, .tips-section, .promo-section { display: none; }
</style>