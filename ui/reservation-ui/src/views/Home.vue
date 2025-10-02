<template>
  <div class="home-page">
    <!-- 顶部栏：APP样式标题 + 退出 -->
    <header class="app-header">
      <div class="app-title">首页</div>
      <button class="logout-btn" @click="onLogout">退出登录</button>
    </header>
    <!-- 顶部：宣传区域（上1/3，渐变背景） -->
    <div class="promo-section">
      <div class="promo-text">
        <h2>健身，让生活更有力量</h2>
        <p>科学训练 · 合理饮食 · 持续打卡</p>
      </div>
    </div>

    <!-- 功能区：四个入口（小程序风格栅格） -->
    <div class="feature-section mini-card">
      <div class="mini-grid">
        <div class="feature-item" @click="$router.push('/booking/create')">
          <div class="feature-icon">🏋️</div>
          <div class="feature-text">健身约课</div>
        </div>
        <div class="feature-item" @click="$router.push('/booking')">
          <div class="feature-icon">📆</div>
          <div class="feature-text">我的约课</div>
        </div>
        <div class="feature-item" @click="$router.push('/profile')">
          <div class="feature-icon">👤</div>
          <div class="feature-text">个人信息</div>
        </div>
        <div class="feature-item" @click="$router.push('/fitness')">
          <div class="feature-icon">💡</div>
          <div class="feature-text">关于健身</div>
        </div>
      </div>
    </div>

    <!-- 底部：健身小贴士（扁平化卡片） -->
    <div class="tips-section mini-card">
      <h3>健身小贴士</h3>
      <ul class="tips-list">
        <li>运动前做好热身，降低受伤风险</li>
        <li>训练过程中及时补水，保持状态</li>
        <li>每次训练控制在45-60分钟，避免过度</li>
      </ul>
    </div>
  </div>
</template>

<script setup lang="ts">
import { useBookingStore, type Booking } from '@/stores/booking'
import { useUserStore } from '@/stores/user'
import { onMounted, ref } from 'vue'
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

onMounted(async () => {
  if (userStore.user) {
    await bookingStore.fetchUserBookings(userStore.user.id)
    recentBookings.value = bookingStore.bookings.slice(0, 3)
  }
})
</script>

<style scoped>
.home-page { padding: 16px; }

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
</style>