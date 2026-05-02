import { defineStore } from 'pinia'
import { computed, ref } from 'vue'
import { authApi, type LoginResponseData } from '@/api/auth'

const TOKEN_KEY = 'reservation-ui-token'
const USER_KEY = 'reservation-ui-user'

const parseUser = (): LoginResponseData | null => {
  const raw = localStorage.getItem(USER_KEY)
  if (!raw) return null

  try {
    return JSON.parse(raw) as LoginResponseData
  } catch {
    return null
  }
}

const normalizeRole = (userTypeCode?: string): 'coach' | 'member' => {
  const raw = (userTypeCode || '').toLowerCase()
  return raw.includes('coach') || raw.includes('trainer') ? 'coach' : 'member'
}

export const useAuthStore = defineStore('reservation-auth', () => {
  const token = ref(localStorage.getItem(TOKEN_KEY) || '')
  const user = ref<LoginResponseData | null>(parseUser())

  const isLoggedIn = computed(() => Boolean(token.value && user.value))
  const role = computed(() => normalizeRole(user.value?.userTypeCode))
  const isCoach = computed(() => role.value === 'coach')
  const isMember = computed(() => role.value === 'member')

  const login = async (userName: string, password: string): Promise<LoginResponseData> => {
    const response = await authApi.login(userName, password)
    if (!response.success) {
      throw new Error(response.message || '登录失败')
    }

    token.value = response.data.token
    user.value = response.data
    localStorage.setItem(TOKEN_KEY, response.data.token)
    localStorage.setItem(USER_KEY, JSON.stringify(response.data))
    return response.data
  }

  const logout = (): void => {
    token.value = ''
    user.value = null
    localStorage.removeItem(TOKEN_KEY)
    localStorage.removeItem(USER_KEY)
  }

  return {
    token,
    user,
    isLoggedIn,
    role,
    isCoach,
    isMember,
    login,
    logout
  }
})
