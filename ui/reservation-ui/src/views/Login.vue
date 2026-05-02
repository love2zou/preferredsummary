<template>
  <div class="login-page">
    <section class="login-card surface-card">
      <div class="login-card__hero">
        <span class="login-card__eyebrow">Reservation App</span>
        <h1>真实数据联调入口</h1>
        <p>支持会员和教练两种测试角色，登录后会进入各自的真实接口页面。</p>
      </div>

      <div class="demo-accounts">
        <button
          v-for="account in demoAccounts"
          :key="account.userName"
          class="demo-account"
          type="button"
          @click="fillDemo(account.userName, account.password)"
        >
          <strong>{{ account.label }}</strong>
          <span>{{ account.userName }} / {{ account.password }}</span>
        </button>
      </div>

      <form class="login-form" @submit.prevent="submit">
        <label>
          <span>账号</span>
          <input v-model.trim="form.userName" type="text" placeholder="请输入账号" />
        </label>
        <label>
          <span>密码</span>
          <input v-model.trim="form.password" type="password" placeholder="请输入密码" />
        </label>
        <button class="primary-button" type="submit" :disabled="submitting">
          {{ submitting ? '登录中...' : '进入应用' }}
        </button>
      </form>
    </section>
  </div>
</template>

<script setup lang="ts">
import { reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { useAuthStore } from '@/stores/authStore'
import { useReservationStore } from '@/stores/reservationStore'
import { useCoachStore } from '@/stores/coachStore'

const router = useRouter()
const authStore = useAuthStore()
const reservationStore = useReservationStore()
const coachStore = useCoachStore()

const submitting = ref(false)
const form = reactive({
  userName: '',
  password: ''
})

const demoAccounts = [
  { label: '会员测试 1', userName: 'app_member_01', password: 'Test123456' },
  { label: '会员测试 2', userName: 'app_member_02', password: 'Test123456' },
  { label: '教练测试 1', userName: 'app_coach_01', password: 'Test123456' },
  { label: '教练测试 2', userName: 'app_coach_02', password: 'Test123456' }
]

const fillDemo = (userName: string, password: string): void => {
  form.userName = userName
  form.password = password
}

const submit = async (): Promise<void> => {
  if (!form.userName || !form.password) {
    ElMessage.warning('请先输入账号和密码')
    return
  }

  submitting.value = true
  try {
    const user = await authStore.login(form.userName, form.password)
    reservationStore.reset()
    coachStore.reset()
    ElMessage.success('登录成功')
    await router.push(user.userTypeCode.toLowerCase().includes('coach') ? { name: 'coach-dashboard' } : { name: 'home' })
  } catch (error) {
    ElMessage.error(error instanceof Error ? error.message : '登录失败')
  } finally {
    submitting.value = false
  }
}
</script>

<style scoped>
.login-page {
  min-height: 100%;
  padding: 24px 18px;
  display: flex;
  align-items: center;
  justify-content: center;
  background:
    radial-gradient(circle at top left, rgba(22, 163, 74, 0.12), transparent 26%),
    radial-gradient(circle at bottom right, rgba(255, 122, 33, 0.12), transparent 24%),
    linear-gradient(180deg, #f7faf8 0%, #ffffff 100%);
}

.login-card {
  width: min(100%, 360px);
  padding: 22px;
}

.login-card__hero h1,
.login-card__hero p {
  margin: 0;
}

.login-card__eyebrow {
  display: inline-flex;
  padding: 5px 10px;
  border-radius: 999px;
  color: var(--brand-deep);
  background: var(--brand-soft);
  font-size: 11px;
  font-weight: 700;
}

.login-card__hero h1 {
  margin-top: 14px;
  font-size: 28px;
  line-height: 1.1;
}

.login-card__hero p {
  margin-top: 10px;
  color: var(--muted);
  font-size: 13px;
  line-height: 1.6;
}

.demo-accounts {
  display: grid;
  gap: 10px;
  margin-top: 18px;
}

.demo-account {
  border: 1px solid rgba(15, 23, 42, 0.06);
  border-radius: 16px;
  padding: 12px 14px;
  background: #f8faf9;
  text-align: left;
}

.demo-account strong,
.demo-account span {
  display: block;
}

.demo-account span {
  margin-top: 4px;
  color: var(--muted);
  font-size: 12px;
}

.login-form {
  display: grid;
  gap: 12px;
  margin-top: 20px;
}

.login-form label {
  display: grid;
  gap: 8px;
}

.login-form span {
  font-size: 12px;
  font-weight: 700;
}

.login-form input {
  width: 100%;
  height: 44px;
  border: 1px solid rgba(15, 23, 42, 0.08);
  border-radius: 14px;
  padding: 0 14px;
  background: #fff;
  font-size: 14px;
}

.login-form .primary-button {
  margin-top: 6px;
  width: 100%;
}
</style>
