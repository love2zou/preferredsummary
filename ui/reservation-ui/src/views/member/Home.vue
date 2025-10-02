<template>
  <div class="page-container">
    <!-- 顶部用户信息 -->
    <div class="user-header">
      <div class="user-info">
        <div class="avatar">
          <el-icon><User /></el-icon>
        </div>
        <div class="user-details">
          <div class="username">{{ userStore.user?.name }}</div>
          <div class="user-level">普通会员</div>
        </div>
      </div>
      <div class="header-actions">
        <el-icon class="action-icon" @click="handleSettings"><Setting /></el-icon>
      </div>
    </div>

    <!-- 功能菜单 -->
    <div class="menu-grid">
      <div class="menu-item" @click="goToProfile">
        <div class="menu-icon">
          <el-icon><User /></el-icon>
        </div>
        <div class="menu-text">会员信息</div>
      </div>
      
      <div class="menu-item" @click="goToFitness">
        <div class="menu-icon">
          <el-icon><Trophy /></el-icon>
        </div>
        <div class="menu-text">关于健身</div>
      </div>
      
      <div class="menu-item" @click="goToBooking">
        <div class="menu-icon">
          <el-icon><Calendar /></el-icon>
        </div>
        <div class="menu-text">约课</div>
      </div>
      
      <div class="menu-item" @click="goToHistory">
        <div class="menu-icon">
          <el-icon><Clock /></el-icon>
        </div>
        <div class="menu-text">历史记录</div>
      </div>
    </div>

    <!-- 今日课程 -->
    <div class="mini-card">
      <div class="card-header">
        <div class="card-title">今日课程</div>
        <router-link to="/member/booking" class="more-link">更多</router-link>
      </div>
      <div class="today-classes">
        <div v-if="todayClasses.length === 0" class="empty-state">
          <el-icon class="empty-icon"><Calendar /></el-icon>
          <div class="empty-text">今天暂无课程</div>
          <router-link to="/member/booking" class="book-now-btn">立即约课</router-link>
        </div>
        <div v-else class="class-list">
          <div v-for="classItem in todayClasses" :key="classItem.id" class="class-item">
            <div class="class-time">{{ classItem.time }}</div>
            <div class="class-info">
              <div class="class-name">{{ classItem.name }}</div>
              <div class="class-trainer">教练：{{ classItem.trainer }}</div>
            </div>
            <div class="class-status" :class="classItem.status">
              {{ getStatusText(classItem.status) }}
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- 推荐课程 -->
    <div class="mini-card">
      <div class="card-header">
        <div class="card-title">推荐课程</div>
      </div>
      <div class="recommended-classes">
        <div v-for="course in recommendedCourses" :key="course.id" class="course-card">
          <div class="course-image">
            <el-icon class="course-icon"><Trophy /></el-icon>
          </div>
          <div class="course-info">
            <div class="course-name">{{ course.name }}</div>
            <div class="course-description">{{ course.description }}</div>
            <div class="course-meta">
              <span class="course-duration">{{ course.duration }}分钟</span>
              <span class="course-difficulty">{{ course.difficulty }}</span>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- 底部导航 -->
    <div class="mini-tabbar">
      <div class="mini-tabbar-item active">
        <el-icon class="mini-tabbar-icon"><House /></el-icon>
        <span>首页</span>
      </div>
      <div class="mini-tabbar-item" @click="goToBooking">
        <el-icon class="mini-tabbar-icon"><Calendar /></el-icon>
        <span>约课</span>
      </div>
      <div class="mini-tabbar-item" @click="goToProfile">
        <el-icon class="mini-tabbar-icon"><User /></el-icon>
        <span>我的</span>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { User, Setting, Trophy, Calendar, Clock, House } from '@element-plus/icons-vue'
import { useUserStore } from '@/stores/user'

const router = useRouter()
const userStore = useUserStore()

const todayClasses = ref([
  {
    id: 1,
    time: '09:00-10:00',
    name: '力量训练',
    trainer: '李教练',
    status: 'upcoming'
  },
  {
    id: 2,
    time: '14:00-15:00',
    name: '瑜伽课程',
    trainer: '王教练',
    status: 'booked'
  }
])

const recommendedCourses = ref([
  {
    id: 1,
    name: '力量训练基础',
    description: '适合初学者的力量训练课程',
    duration: 60,
    difficulty: '初级'
  },
  {
    id: 2,
    name: '有氧燃脂',
    description: '高效燃脂，塑造完美身材',
    duration: 45,
    difficulty: '中级'
  },
  {
    id: 3,
    name: '瑜伽冥想',
    description: '放松身心，提升柔韧性',
    duration: 75,
    difficulty: '初级'
  }
])

const getStatusText = (status: string) => {
  const statusMap: Record<string, string> = {
    upcoming: '即将开始',
    booked: '已预约',
    completed: '已完成',
    cancelled: '已取消'
  }
  return statusMap[status] || status
}

const goToProfile = () => {
  router.push('/member/profile')
}

const goToFitness = () => {
  router.push('/member/fitness')
}

const goToBooking = () => {
  router.push('/member/booking')
}

const goToHistory = () => {
  router.push('/member/history')
}

const handleSettings = () => {
  ElMessage.info('设置功能开发中...')
}

onMounted(() => {
  // 页面加载时的初始化逻辑
})
</script>

<style scoped>
.user-header {
  background: linear-gradient(135deg, var(--primary-color), #06ad56);
  color: white;
  padding: 20px 16px;
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.user-info {
  display: flex;
  align-items: center;
}

.avatar {
  width: 50px;
  height: 50px;
  border-radius: 50%;
  background: rgba(255, 255, 255, 0.2);
  display: flex;
  align-items: center;
  justify-content: center;
  margin-right: 12px;
  font-size: 24px;
}

.username {
  font-size: 18px;
  font-weight: 600;
  margin-bottom: 4px;
}

.user-level {
  font-size: 14px;
  opacity: 0.8;
}

.action-icon {
  font-size: 20px;
  cursor: pointer;
  padding: 8px;
  border-radius: 50%;
  transition: background-color 0.3s;
}

.action-icon:hover {
  background: rgba(255, 255, 255, 0.1);
}

.menu-grid {
  display: grid;
  grid-template-columns: repeat(2, 1fr);
  gap: 12px;
  padding: 16px;
}

.menu-item {
  background: var(--card-bg);
  border-radius: 12px;
  padding: 20px;
  text-align: center;
  box-shadow: var(--shadow);
  cursor: pointer;
  transition: transform 0.3s;
}

.menu-item:hover {
  transform: translateY(-2px);
}

.menu-icon {
  font-size: 32px;
  color: var(--primary-color);
  margin-bottom: 8px;
}

.menu-text {
  font-size: 14px;
  font-weight: 500;
  color: var(--text-color);
}

.card-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 16px 16px 0;
}

.card-title {
  font-size: 16px;
  font-weight: 600;
  color: var(--text-color);
}

.more-link {
  font-size: 14px;
  color: var(--primary-color);
  text-decoration: none;
}

.today-classes {
  padding: 16px;
}

.empty-state {
  text-align: center;
  padding: 40px 20px;
}

.empty-icon {
  font-size: 48px;
  color: var(--text-secondary);
  margin-bottom: 16px;
}

.empty-text {
  font-size: 16px;
  color: var(--text-secondary);
  margin-bottom: 20px;
}

.book-now-btn {
  display: inline-block;
  padding: 8px 16px;
  background: var(--primary-color);
  color: white;
  border-radius: 6px;
  text-decoration: none;
  font-size: 14px;
}

.class-list {
  space-y: 12px;
}

.class-item {
  display: flex;
  align-items: center;
  padding: 12px;
  background: #f8f9fa;
  border-radius: 8px;
  margin-bottom: 8px;
}

.class-time {
  font-size: 14px;
  font-weight: 600;
  color: var(--primary-color);
  min-width: 100px;
}

.class-info {
  flex: 1;
  margin-left: 12px;
}

.class-name {
  font-size: 16px;
  font-weight: 500;
  color: var(--text-color);
  margin-bottom: 4px;
}

.class-trainer {
  font-size: 14px;
  color: var(--text-secondary);
}

.class-status {
  font-size: 12px;
  padding: 4px 8px;
  border-radius: 4px;
  font-weight: 500;
}

.class-status.upcoming {
  background: #fff3cd;
  color: #856404;
}

.class-status.booked {
  background: #d1ecf1;
  color: #0c5460;
}

.recommended-classes {
  padding: 16px;
}

.course-card {
  display: flex;
  align-items: center;
  padding: 12px;
  border-bottom: 1px solid var(--border-color);
  cursor: pointer;
  transition: background-color 0.3s;
}

.course-card:hover {
  background: #f8f9fa;
}

.course-card:last-child {
  border-bottom: none;
}

.course-image {
  width: 50px;
  height: 50px;
  border-radius: 8px;
  background: linear-gradient(135deg, var(--primary-color), #06ad56);
  display: flex;
  align-items: center;
  justify-content: center;
  margin-right: 12px;
}

.course-icon {
  font-size: 24px;
  color: white;
}

.course-info {
  flex: 1;
}

.course-name {
  font-size: 16px;
  font-weight: 500;
  color: var(--text-color);
  margin-bottom: 4px;
}

.course-description {
  font-size: 14px;
  color: var(--text-secondary);
  margin-bottom: 8px;
}

.course-meta {
  display: flex;
  gap: 12px;
}

.course-duration,
.course-difficulty {
  font-size: 12px;
  padding: 2px 6px;
  border-radius: 4px;
  background: #f0f0f0;
  color: var(--text-secondary);
}
</style>