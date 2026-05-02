<template>
  <div v-if="profileUser" class="app-page profile-page">
    <header class="page-header profile-header">
      <div></div>
      <div class="profile-actions">
        <button class="icon-button" type="button" @click="showHint('设置页入口已保留，后续可以补账号与隐私能力')">
          <el-icon><Setting /></el-icon>
        </button>
        <button class="icon-button profile-actions__message" type="button" @click="showHint('消息中心稍后接入，这里先保留入口')">
          <el-icon><Message /></el-icon>
        </button>
      </div>
    </header>

    <section class="surface-card profile-card">
      <div class="profile-card__identity">
        <TrainerArtwork
          :name="profileUser.name"
          tone="#eef3f7"
          accent="#16a34a"
          :photo-url="profileUser.avatarUrl"
          variant="thumb"
        />
        <div class="profile-card__copy">
          <div class="profile-card__name">
            <strong>{{ profileUser.name }}</strong>
            <span class="tag tag--orange">{{ profileUser.membership }}</span>
          </div>
          <small>当前门店：{{ profileUser.homeClub }}</small>
        </div>
      </div>

      <div class="profile-balance">
        <div>
          <span>私教课剩余</span>
          <strong>{{ profileUser.remainingSessions }} <small>节</small></strong>
        </div>
        <div class="profile-balance__right">
          <span>会员有效期至</span>
          <strong>{{ profileUser.expireAt }}</strong>
        </div>
      </div>
    </section>

    <section class="surface-card profile-menu">
      <button
        v-for="item in profileMenus"
        :key="item.key"
        class="profile-menu__item"
        type="button"
        @click="navigate(item.routeName, item.label)"
      >
        <span class="profile-menu__left">
          <el-icon>
            <component :is="resolveIcon(item.icon as keyof typeof iconMap)" />
          </el-icon>
          {{ item.label }}
        </span>
        <el-icon class="profile-menu__arrow"><ArrowRight /></el-icon>
      </button>

      <button class="profile-menu__item" type="button" @click="logout">
        <span class="profile-menu__left">
          <el-icon><SwitchButton /></el-icon>
          退出登录
        </span>
        <el-icon class="profile-menu__arrow"><ArrowRight /></el-icon>
      </button>
    </section>
  </div>
</template>

<script setup lang="ts">
import TrainerArtwork from '@/components/TrainerArtwork.vue'
import { profileMenus } from '@/data/appConstants'
import { useAuthStore } from '@/stores/authStore'
import { useCoachStore } from '@/stores/coachStore'
import { useReservationStore } from '@/stores/reservationStore'
import {
  ArrowRight,
  Calendar,
  DataAnalysis,
  Histogram,
  Message,
  Notebook,
  Phone,
  Setting,
  SwitchButton,
  Tickets
} from '@element-plus/icons-vue'
import { ElMessage } from 'element-plus'
import { storeToRefs } from 'pinia'
import { computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'

const router = useRouter()
const authStore = useAuthStore()
const reservationStore = useReservationStore()
const coachStore = useCoachStore()
const { profile } = storeToRefs(reservationStore)

const profileUser = computed(() => profile.value?.user ?? null)

const iconMap = {
  Calendar,
  Notebook,
  Tickets,
  DataAnalysis,
  Histogram,
  Phone,
  Setting
}

onMounted(async () => {
  try {
    await reservationStore.loadProfile()
  } catch {
    ElMessage.error('个人信息加载失败')
  }
})

const resolveIcon = (iconName: keyof typeof iconMap) => iconMap[iconName] ?? Setting

const showHint = (message: string): void => {
  ElMessage.success(message)
}

const navigate = (routeName: string | undefined, label: string): void => {
  if (!routeName) {
    ElMessage.success(`${label}功能入口已保留，下一步可以继续补完整页面`)
    return
  }

  if (routeName === 'commerce-center') {
    const scene = label === '我的订单' ? 'orders' : 'market'
    router.push({ name: routeName, query: { scene } })
    return
  }

  if (routeName === 'member-center') {
    const sceneMap: Record<string, string> = {
      身体数据: 'body',
      训练记录: 'records',
      资料设置: 'edit'
    }
    router.push({ name: routeName, query: { scene: sceneMap[label] ?? 'profile' } })
    return
  }

  if (routeName === 'reservation-flow') {
    router.push({ name: routeName, query: { scene: 'chat' } })
    return
  }

  router.push({ name: routeName })
}

const logout = async (): Promise<void> => {
  authStore.logout()
  reservationStore.reset()
  coachStore.reset()
  await router.push({ name: 'login' })
}
</script>

<style scoped>
.profile-page {
  padding-top: 2px;
  background: linear-gradient(180deg, #ffffff 0%, #f8faf9 100%);
}

.profile-header {
  margin-bottom: 10px;
}

.profile-actions {
  display: flex;
  gap: 8px;
}

.profile-actions__message {
  position: relative;
}

.profile-actions__message::after {
  content: '';
  position: absolute;
  top: 7px;
  right: 6px;
  width: 6px;
  height: 6px;
  border-radius: 50%;
  background: #ef4444;
}

.profile-card {
  padding: 12px;
}

.profile-card__identity {
  display: grid;
  grid-template-columns: 62px minmax(0, 1fr);
  gap: 10px;
  align-items: center;
}

.profile-card__copy {
  min-width: 0;
}

.profile-card__name {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
}

.profile-card__name strong {
  font-size: 18px;
}

.profile-card__copy small {
  display: block;
  margin-top: 4px;
  color: var(--muted);
  font-size: 11px;
}

.profile-balance {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 12px;
  margin-top: 14px;
  padding: 16px 14px;
  border-radius: 16px;
  background:
    radial-gradient(circle at 84% 24%, rgba(255, 255, 255, 0.7), transparent 18%),
    linear-gradient(135deg, #eefbf1 0%, #f7fbf8 58%, #edf8f1 100%);
}

.profile-balance span,
.profile-balance strong {
  display: block;
}

.profile-balance span {
  color: #6b7280;
  font-size: 11px;
}

.profile-balance strong {
  margin-top: 6px;
  font-size: 18px;
}

.profile-balance strong small {
  font-size: 11px;
}

.profile-balance__right {
  text-align: right;
}

.profile-menu {
  margin-top: 12px;
  padding: 0 14px;
}

.profile-menu__item {
  width: 100%;
  border: 0;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  padding: 16px 0;
  background: transparent;
  border-bottom: 1px solid rgba(15, 23, 42, 0.06);
}

.profile-menu__item:last-child {
  border-bottom: 0;
}

.profile-menu__left {
  display: inline-flex;
  align-items: center;
  gap: 10px;
  font-size: 13px;
  font-weight: 600;
}

.profile-menu__arrow {
  color: #b1b8c2;
}
</style>
