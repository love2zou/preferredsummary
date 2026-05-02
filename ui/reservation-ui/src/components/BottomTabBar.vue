<template>
  <nav class="bottom-tabbar" aria-label="底部导航">
    <button
      v-for="item in items"
      :key="item.routeName"
      class="bottom-tabbar__item"
      :class="{ 'is-active': currentName === item.routeName, 'is-center': item.center }"
      type="button"
      @click="router.push({ name: item.routeName })"
    >
      <span class="bottom-tabbar__icon">
        <el-icon>
          <component :is="item.icon" />
        </el-icon>
      </span>
      <span class="bottom-tabbar__label">{{ item.label }}</span>
    </button>
  </nav>
</template>

<script setup lang="ts">
import { Calendar, DataAnalysis, HomeFilled, Management, User } from '@element-plus/icons-vue'
import { computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'

const route = useRoute()
const router = useRouter()

const currentName = computed(() => String(route.name ?? ''))

const items = [
  { label: '首页', routeName: 'home', icon: HomeFilled, center: false },
  { label: '私教', routeName: 'trainers', icon: User, center: false },
  { label: '预约', routeName: 'reservations', icon: Calendar, center: true },
  { label: '训练', routeName: 'training', icon: DataAnalysis, center: false },
  { label: '我的', routeName: 'profile', icon: Management, center: false }
]
</script>

<style scoped>
.bottom-tabbar {
  display: grid;
  grid-template-columns: repeat(5, minmax(0, 1fr));
  align-items: end;
  gap: 2px;
  padding: 8px 10px calc(8px + env(safe-area-inset-bottom));
  border-top: 1px solid rgba(15, 23, 42, 0.05);
  background:
    linear-gradient(180deg, rgba(255, 255, 255, 0.94), rgba(255, 255, 255, 0.99)),
    rgba(255, 255, 255, 0.98);
  box-shadow: 0 -10px 24px rgba(15, 23, 42, 0.03);
}

.bottom-tabbar__item {
  border: 0;
  background: transparent;
  color: #a2a8b1;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 3px;
  padding: 0;
  font: inherit;
}

.bottom-tabbar__item.is-active {
  color: var(--brand);
}

.bottom-tabbar__item.is-center {
  transform: translateY(-11px);
}

.bottom-tabbar__icon {
  width: 22px;
  height: 22px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  font-size: 18px;
}

.bottom-tabbar__item.is-center .bottom-tabbar__icon {
  width: 44px;
  height: 44px;
  border-radius: 18px;
  color: #fff;
  background: linear-gradient(135deg, var(--brand), var(--brand-deep));
  box-shadow: 0 10px 18px rgba(15, 138, 67, 0.26);
}

.bottom-tabbar__label {
  font-size: 10px;
  font-weight: 600;
}
</style>
