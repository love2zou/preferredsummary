<template>
  <main class="home-page">
    <header class="site-header">
      <div class="header-inner">
        <button class="brand" type="button" @click="selectCategory('')">
          <span class="brand-mark">P</span>
          <span class="brand-text">
            <strong>优选导航</strong>
            <small>Preferred Summary</small>
          </span>
        </button>

        <nav class="header-nav" aria-label="首页导航">
          <a href="#resources">资源目录</a>
        </nav>

        <button class="calendar-button" type="button" @click="openCalendarPage">
          万年日历
        </button>

        <el-popover
          placement="bottom-end"
          trigger="click"
          :width="360"
          popper-class="home-notification-popover"
          @show="handleNotificationPopoverShow"
        >
          <template #reference>
            <button class="notification-button" type="button" title="告警消息">
              <el-badge :value="notificationBadgeValue" :hidden="unreadNotificationCount === 0">
                <span class="notification-trigger-icon">
                  <el-icon><Bell /></el-icon>
                </span>
              </el-badge>
            </button>
          </template>

          <div class="notification-panel">
            <div class="notification-panel-header">
              <div class="notification-panel-heading">
                <span class="notification-panel-emblem">
                  <el-icon><Bell /></el-icon>
                </span>
                <strong>告警消息</strong>
                <p>系统监控与通知提醒</p>
              </div>
              <span class="notification-count">{{ unreadNotificationCount }} 条</span>
            </div>

            <div v-if="notificationLoading" class="notification-state">
              正在加载告警消息...
            </div>
            <div v-else-if="notificationError" class="notification-state error">
              {{ notificationError }}
            </div>
            <div v-else-if="notificationItems.length === 0" class="notification-state">
              暂无告警消息
            </div>
            <ul v-else class="notification-list">
              <li
                v-for="item in notificationItems"
                :key="item.id"
                class="notification-item"
              >
                <span class="notification-level-dot"></span>
                <div class="notification-copy">
                  <strong>{{ item.name }}</strong>
                  <p>{{ item.content }}</p>
                  <small>{{ formatNotificationTime(item.sendTime) }}</small>
                </div>
              </li>
            </ul>

            <button class="notification-more" type="button" @click="openNotificationManagement">
              查看更多
            </button>
          </div>
        </el-popover>

        <button class="admin-button" type="button" title="后台管理" @click="openAdminLogin">
          <el-icon><User /></el-icon>
        </button>
      </div>
    </header>

    <section class="hero-section" aria-label="资源搜索">
      <div class="hero-copy">
        <div class="hero-search">
          <el-icon class="hero-search-icon"><Search /></el-icon>
          <input
            v-model="searchKeyword"
            type="search"
            placeholder="搜索站点、描述或标签"
            @input="handleSearch"
          />
          <button
            v-if="searchKeyword.trim()"
            class="clear-search"
            type="button"
            @click="clearSearch"
          >
            清除筛选
          </button>
        </div>
      </div>
    </section>

    <section id="resources" class="section-block resource-section">
      <div class="category-bar">
        <button
          v-for="category in sortedCategories"
          :key="category.categoryCode"
          class="category-chip"
          :class="{ active: activeTab === category.categoryCode }"
          type="button"
          @click="selectCategory(category.categoryCode)"
        >
          <el-icon v-if="isCategoryElementIcon(category.categoryIcon)">
            <component :is="getCategoryIconComponent(category.categoryIcon)" />
          </el-icon>
          <i v-else :class="getCategoryIconClass(category.categoryIcon)"></i>
          {{ category.categoryName }}
          <span>{{ getCategoryCount(category.categoryCode) }}</span>
        </button>

        <span class="result-count category-result-count">{{ filteredWebsites.length }} 个结果</span>
      </div>

      <div class="resource-grid" v-loading="loading">
        <article
          v-for="website in filteredWebsites"
          :key="website.id"
          class="resource-card"
          :class="{ unavailable: !isWebsiteAvailable(website) }"
        >
          <button class="resource-open" type="button" @click="handleWebsiteOpen(website)">
            <span class="resource-image">
              <img
                v-if="!website.imageError && website.pictureUrl"
                :src="getImageUrl(website.pictureUrl)"
                :alt="website.name"
                @error="() => handleImageError(website)"
              />
              <span v-else class="image-fallback">
                <el-icon><Link /></el-icon>
              </span>
            </span>

            <span class="resource-content">
              <span class="resource-topline">
                <span class="resource-category">{{ website.categoryName || getCategoryName(website.categoryCode) }}</span>
                <span v-if="isMarked(website)" class="mark-badge">推荐</span>
              </span>
              <strong>{{ website.name }}</strong>
              <span class="resource-description">{{ website.description || '暂无描述' }}</span>
            </span>

            <el-icon class="open-icon"><ArrowRight /></el-icon>
          </button>

          <div class="resource-footer">
            <div class="tag-list">
              <el-tag
                v-for="tag in getAllTags(website).slice(0, 3)"
                :key="tag.codeType"
                :style="getTagStyle(tag)"
                size="small"
                effect="plain"
              >
                {{ tag.name }}
              </el-tag>
            </div>
            <span class="view-count" :title="`访问 ${getViewCount(website)} 次`">
              <el-icon><View /></el-icon>
              {{ getViewCount(website) }}
            </span>
          </div>
        </article>
      </div>

      <el-empty
        v-if="!loading && filteredWebsites.length === 0"
        class="empty-state"
        description="没有找到匹配的资源"
      />
    </section>
  </main>
</template>

<script setup lang="ts">
import { getIconClass, getIconComponent, isElementIcon } from '@/data/iconLibrary'
import api, { API_CONFIG } from '@/services/api'
import AutoLoginService from '@/services/autoLoginService'
import { categoryService, type Category } from '@/services/categoryService'
import { networkUrlService, type NetworkUrlListDto, type TagInfo } from '@/services/networkUrlService'
import { tagService, type Tag } from '@/services/tagService'
import { ArrowRight, Bell, Collection, Link, Search, User, View } from '@element-plus/icons-vue'
import { ElMessage } from 'element-plus'
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'

interface NotificationListItem {
  id: number
  isRead: number
  name: string
  content: string
  notifyType: string
  notifyStatus: number
  sendStatus: number
  sendTime: string
  sendUser: string
  receiver: string
  remark?: string
}

interface NotificationPagedResponse<T> {
  data: T[]
  total: number
  page: number
  pageSize: number
  totalPages: number
}

const NOTIFICATION_RECEIVER = 'admin'

const loading = ref(false)
const searchKeyword = ref('')
const activeTab = ref('')
const categories = ref<Category[]>([])
const websites = ref<NetworkUrlListDto[]>([])
const tags = ref<Tag[]>([])
const searchTimeout = ref<number | null>(null)
const tagMap = ref<Map<string, Tag>>(new Map())
const notificationLoading = ref(false)
const notificationError = ref('')
const notificationItems = ref<NotificationListItem[]>([])
const unreadNotificationCount = ref(0)
const notificationRefreshTimer = ref<number | null>(null)

const notificationBadgeValue = computed(() => {
  if (unreadNotificationCount.value <= 0) return ''
  return unreadNotificationCount.value > 9 ? '9+' : unreadNotificationCount.value
})

const normalizeNotificationType = (type?: string): 'notice' | 'remind' | 'alert' => {
  const value = (type || '').trim().toLowerCase()
  if (value === 'system' || value === 'user' || value === '通知') return 'notice'
  if (value === 'warning' || value === 'error' || value === 'alert' || value === '告警') return 'alert'
  if (value === 'remind' || value === '提醒') return 'remind'
  if (value === 'notice') return 'notice'
  return 'notice'
}

const sortedCategories = computed<Category[]>(() => {
  return [...categories.value].sort((a, b) => a.seqNo - b.seqNo)
})

const filteredWebsites = computed<NetworkUrlListDto[]>(() => {
  const keyword = searchKeyword.value.trim().toLowerCase()

  return websites.value
    .filter((website) => {
      if (activeTab.value && !getWebsiteCategoryCodes(website).includes(activeTab.value)) {
        return false
      }

      if (!keyword) return true

      const tagNames = getAllTags(website).map((tag) => tag.name).join(' ')
      return (
        website.name.toLowerCase().includes(keyword) ||
        (website.description || '').toLowerCase().includes(keyword) ||
        tagNames.toLowerCase().includes(keyword) ||
        (website.categoryName || '').toLowerCase().includes(keyword)
      )
    })
    .sort((a, b) => {
      if (isMarked(a) !== isMarked(b)) return Number(isMarked(b)) - Number(isMarked(a))
      if (isWebsiteAvailable(a) !== isWebsiteAvailable(b)) {
        return Number(isWebsiteAvailable(b)) - Number(isWebsiteAvailable(a))
      }
      const viewDiff = getViewCount(b) - getViewCount(a)
      if (viewDiff !== 0) return viewDiff
      return a.name.localeCompare(b.name, 'zh-CN')
    })
})

const selectCategory = (categoryCode: string): void => {
  const currentScrollY = window.scrollY
  activeTab.value = categoryCode
  requestAnimationFrame(() => {
    window.scrollTo({ top: currentScrollY, left: 0, behavior: 'auto' })
  })
}

const handleSearch = (): void => {
  if (searchTimeout.value) {
    clearTimeout(searchTimeout.value)
  }

  searchTimeout.value = window.setTimeout(() => {
    searchTimeout.value = null
  }, 240)
}

const clearSearch = (): void => {
  searchKeyword.value = ''
  if (searchTimeout.value) {
    clearTimeout(searchTimeout.value)
    searchTimeout.value = null
  }
}

const handleWebsiteOpen = (website: NetworkUrlListDto): void => {
  if (!isWebsiteAvailable(website)) {
    ElMessage.warning('该网站当前不可用')
    return
  }

  window.open(website.url, '_blank')
  void incrementClickCount(website)
}

const isMarked = (website: NetworkUrlListDto): boolean => {
  return website.isMark === true || website.isMark === 1
}

const isWebsiteAvailable = (website: NetworkUrlListDto): boolean => {
  return website.isAvailable === true || website.isAvailable === 1
}

const getViewCount = (website: NetworkUrlListDto): number => {
  const seqNo = Number(website.seqNo)
  return Number.isFinite(seqNo) ? seqNo : 0
}

const setWebsiteViewCount = (id: number, seqNo: number, keepHigher = false): void => {
  const target = websites.value.find((item) => item.id === id)
  if (target) {
    target.seqNo = keepHigher ? Math.max(getViewCount(target), seqNo) : seqNo
  }
}

const incrementClickCount = async (website: NetworkUrlListDto): Promise<void> => {
  const optimisticCount = getViewCount(website) + 1
  setWebsiteViewCount(website.id, optimisticCount)

  try {
    const response = await networkUrlService.incrementClickCount(website.id)
    setWebsiteViewCount(website.id, Number(response.data?.seqNo ?? optimisticCount), true)
  } catch (error) {
    setWebsiteViewCount(website.id, Math.max(getViewCount(website) - 1, 0))
    console.error('更新点击次数失败:', error)
  }
}

const loadCategories = async (): Promise<void> => {
  try {
    await AutoLoginService.ensureLoggedIn()
    const response = await categoryService.getCategoryList({ page: 1, pageSize: 100 })
    categories.value = response.data || []
  } catch (error) {
    console.error('加载分类失败:', error)
    ElMessage.error('加载分类失败')
  }
}

const loadTags = async (): Promise<void> => {
  try {
    await AutoLoginService.ensureLoggedIn()
    const response = await tagService.getTagList({ page: 1, pageSize: 1000 })
    tags.value = response.data || []
    tagMap.value.clear()
    tags.value.forEach((tag) => {
      tagMap.value.set(tag.codeType, tag)
    })
  } catch (error) {
    console.error('加载标签失败:', error)
  }
}

const loadWebsites = async (): Promise<void> => {
  try {
    await AutoLoginService.ensureLoggedIn()
    loading.value = true
    const response = await networkUrlService.getNetworkUrlList({
      page: 1,
      pageSize: 1000
    })
    websites.value = response.data || []
  } catch (error) {
    console.error('加载网站列表失败:', error)
    ElMessage.error('加载网站列表失败')
  } finally {
    loading.value = false
  }
}

const loadAlertNotifications = async (): Promise<void> => {
  try {
    await AutoLoginService.ensureLoggedIn()
    notificationLoading.value = true
    notificationError.value = ''

    const response = await api.get('/api/Notification/list', {
      params: {
        page: 1,
        size: 20,
        receiver: NOTIFICATION_RECEIVER,
        isRead: 0
      }
    }) as NotificationPagedResponse<NotificationListItem>

    const sourceItems = Array.isArray(response.data) ? response.data : []
    const filteredItems = sourceItems.filter((item) => {
      const type = normalizeNotificationType(item.notifyType)
      return type === 'alert' || type === 'remind'
    })

    notificationItems.value = filteredItems.slice(0, 6)
    unreadNotificationCount.value = filteredItems.length
  } catch (error) {
    notificationError.value = '告警消息加载失败'
    console.error('加载告警消息失败:', error)
  } finally {
    notificationLoading.value = false
  }
}

const handleNotificationPopoverShow = (): void => {
  void loadAlertNotifications()
}

const startNotificationPolling = (): void => {
  stopNotificationPolling()
  notificationRefreshTimer.value = window.setInterval(() => {
    void loadAlertNotifications()
  }, 60000)
}

const stopNotificationPolling = (): void => {
  if (notificationRefreshTimer.value) {
    clearInterval(notificationRefreshTimer.value)
    notificationRefreshTimer.value = null
  }
}

const formatNotificationTime = (value?: string): string => {
  if (!value) return ''

  const parsed = new Date(value)
  if (Number.isNaN(parsed.getTime())) {
    return value
  }

  return parsed.toLocaleString('zh-CN', {
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit'
  })
}

const getImageUrl = (url: string): string => {
  if (!url) return '/default-image.png'
  if (url.startsWith('http')) return url
  return `${API_CONFIG.BASE_URL}${url}`
}

const handleImageError = (website: NetworkUrlListDto): void => {
  website.imageError = true
}

const getWebsiteCategoryCodes = (website: NetworkUrlListDto): string[] => {
  return (website.categoryCode || '')
    .split(',')
    .map((code) => code.trim())
    .filter(Boolean)
}

const getCategoryCount = (categoryCode: string): number => {
  return websites.value.filter((website) => getWebsiteCategoryCodes(website).includes(categoryCode)).length
}

const getCategoryName = (categoryCode?: string): string => {
  const firstCode = (categoryCode || '').split(',')[0]?.trim()
  const category = categories.value.find((item) => item.categoryCode === firstCode)
  return category?.categoryName || '未分类'
}

const getAllTags = (website: NetworkUrlListDto): TagInfo[] => {
  if (website.tags && website.tags.length > 0) {
    return website.tags
  }

  const mainTag = tagMap.value.get(website.tagCodeType)
  if (!mainTag) return []

  return [{
    codeType: mainTag.codeType,
    name: mainTag.name,
    hexColor: mainTag.hexColor,
    rgbColor: mainTag.rgbColor
  }]
}

const getTagStyle = (tag: TagInfo) => {
  const color = tag.hexColor || '#245b55'
  const backgroundColor = tag.rgbColor || 'rgba(36, 91, 85, 0.08)'

  return {
    color,
    backgroundColor,
    borderColor: color
  }
}

const isCategoryElementIcon = (iconName?: string): boolean => {
  if (!iconName) return true
  return isElementIcon(iconName) || isElementIcon(iconName.toLowerCase())
}

const getCategoryIconComponent = (iconName?: string) => {
  if (!iconName) return Collection
  return getIconComponent(iconName) || getIconComponent(iconName.toLowerCase()) || Collection
}

const getCategoryIconClass = (iconName?: string): string => {
  if (!iconName) return 'fas fa-folder'
  return getIconClass(iconName) || getIconClass(iconName.toLowerCase())
}

const ADMIN_LOGIN_URL = (import.meta.env.VITE_ADMIN_BASE_URL
  ? `${import.meta.env.VITE_ADMIN_BASE_URL}/login`
  : `${window.location.protocol}//${window.location.hostname}:8081/login`)

const ADMIN_NOTIFICATION_URL = (import.meta.env.VITE_ADMIN_BASE_URL
  ? `${import.meta.env.VITE_ADMIN_BASE_URL}/notification-management`
  : `${window.location.protocol}//${window.location.hostname}:8081/notification-management`)

const openAdminLogin = (): void => {
  window.open(ADMIN_LOGIN_URL, '_blank', 'noopener')
}

const openNotificationManagement = (): void => {
  window.open(ADMIN_NOTIFICATION_URL, '_blank', 'noopener')
}

const openCalendarPage = (): void => {
  const calendarUrl = new URL('calendar', window.location.origin + import.meta.env.BASE_URL)
  window.open(calendarUrl.toString(), '_blank', 'noopener')
}

onMounted(async () => {
  try {
    await AutoLoginService.autoLogin()
    await Promise.all([loadCategories(), loadTags(), loadWebsites(), loadAlertNotifications()])
    startNotificationPolling()
  } catch (error) {
    console.error('首页初始化失败:', error)
    ElMessage.error('登录失败，无法加载数据')
  }
})

onBeforeUnmount(() => {
  stopNotificationPolling()
})
</script>

<style scoped>
.home-page {
  min-height: 100vh;
  color: #152421;
  background:
    linear-gradient(180deg, rgba(246, 250, 248, 0.94), rgba(255, 255, 255, 0.98)),
    url('https://images.unsplash.com/photo-1497366754035-f200968a6e72?auto=format&fit=crop&w=1800&q=80') center top / cover fixed;
}

.site-header {
  position: sticky;
  top: 0;
  z-index: 20;
  border-bottom: 1px solid rgba(21, 36, 33, 0.1);
  background: rgba(255, 255, 255, 0.88);
  backdrop-filter: blur(18px);
}

.header-inner {
  max-width: 1180px;
  margin: 0 auto;
  padding: 14px 24px;
  display: flex;
  align-items: center;
  gap: 24px;
}

.brand,
.calendar-button,
.notification-button,
.admin-button,
.category-chip,
.resource-open {
  border: 0;
  font: inherit;
}

.brand {
  display: inline-flex;
  align-items: center;
  gap: 10px;
  padding: 0;
  background: transparent;
  color: inherit;
  cursor: pointer;
}

.brand-mark {
  width: 38px;
  height: 38px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  border-radius: 8px;
  color: #fff;
  background: #245b55;
  font-weight: 800;
  box-shadow: 0 10px 24px rgba(36, 91, 85, 0.22);
}

.brand-text {
  display: flex;
  flex-direction: column;
  line-height: 1.1;
  text-align: left;
}

.brand-text strong {
  font-size: 16px;
}

.brand-text small {
  color: #6e7d78;
  font-size: 12px;
}

.header-nav {
  display: flex;
  gap: 18px;
  margin-left: auto;
}

.header-nav a {
  color: #51615d;
  text-decoration: none;
  font-size: 14px;
}

.header-nav a:hover {
  color: #245b55;
}

.calendar-button {
  height: 38px;
  padding: 0 16px;
  border-radius: 999px;
  color: #245b55;
  background: rgba(232, 242, 239, 0.92);
  box-shadow: inset 0 0 0 1px rgba(36, 91, 85, 0.12);
  cursor: pointer;
  transition: transform 0.18s ease, box-shadow 0.18s ease, background 0.18s ease;
}

.calendar-button:hover {
  transform: translateY(-1px);
  background: #eef7f4;
  box-shadow:
    inset 0 0 0 1px rgba(36, 91, 85, 0.18),
    0 10px 24px rgba(36, 91, 85, 0.12);
}

.notification-button,
.admin-button {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
}

.notification-button {
  position: relative;
  width: 42px;
  height: 42px;
  padding: 0;
  border-radius: 14px;
  color: #245b55;
  background:
    linear-gradient(180deg, rgba(255, 255, 255, 0.95), rgba(240, 248, 245, 0.96));
  box-shadow:
    inset 0 0 0 1px rgba(36, 91, 85, 0.12),
    0 10px 24px rgba(36, 91, 85, 0.1);
  transition: transform 0.18s ease, box-shadow 0.18s ease, background 0.18s ease;
}

.notification-button:hover {
  transform: translateY(-1px);
  background:
    linear-gradient(180deg, rgba(255, 255, 255, 1), rgba(233, 246, 241, 1));
  box-shadow:
    inset 0 0 0 1px rgba(36, 91, 85, 0.16),
    0 16px 30px rgba(36, 91, 85, 0.14);
}

.notification-trigger-icon {
  width: 30px;
  height: 30px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  border-radius: 999px;
  color: #245b55;
  background: rgba(36, 91, 85, 0.1);
  box-shadow: inset 0 0 0 1px rgba(36, 91, 85, 0.08);
}

.admin-button {
  width: 36px;
  height: 36px;
  border-radius: 8px;
  color: #245b55;
  background: #e8f2ef;
}

.notification-button :deep(.el-badge__content) {
  border: 0;
  min-width: 18px;
  height: 18px;
  padding: 0 5px;
  background: linear-gradient(180deg, #e36c57, #cf4d38);
  box-shadow: 0 8px 16px rgba(210, 85, 63, 0.32);
  font-size: 11px;
  font-weight: 700;
}

.hero-section,
.section-block {
  max-width: 1200px;
  margin: 0 auto;
  padding-left: 24px;
  padding-right: 24px;
}

.hero-section {
  display: flex;
  justify-content: center;
  min-height: auto;
  padding-top: 12px;
  padding-bottom: 12px;
}

.hero-copy {
  width: 100%;
}

.hero-search {
  width: 100%;
  height: 58px;
  margin-top: 0;
  padding: 0 18px;
  box-sizing: border-box;
  display: flex;
  align-items: center;
  gap: 12px;
  border: 1px solid rgba(36, 91, 85, 0.18);
  border-radius: 8px;
  background: rgba(255, 255, 255, 0.96);
  box-shadow: 0 18px 50px rgba(36, 91, 85, 0.12);
}

.hero-search-icon {
  color: #245b55;
  font-size: 20px;
}

.hero-search input {
  width: 100%;
  min-width: 0;
  border: 0;
  outline: 0;
  color: #152421;
  background: transparent;
  font-size: 16px;
}

.clear-search {
  flex: 0 0 auto;
  padding: 0;
  border: 0;
  color: #245b55;
  background: transparent;
  font-size: 13px;
  font-weight: 600;
  white-space: nowrap;
  cursor: pointer;
}

.clear-search:hover {
  color: #1a4742;
}

.section-block {
  padding-top: 38px;
  padding-bottom: 34px;
}

.resource-grid {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 16px;
}

.resource-card {
  border: 1px solid rgba(36, 91, 85, 0.11);
  border-radius: 8px;
  background: rgba(255, 255, 255, 0.94);
  box-shadow: 0 10px 26px rgba(21, 36, 33, 0.07);
  transition: transform 0.18s ease, box-shadow 0.18s ease;
}

.resource-card:hover {
  transform: translateY(-3px);
  box-shadow: 0 18px 38px rgba(21, 36, 33, 0.13);
}

.resource-card.unavailable {
  opacity: 0.62;
}

.resource-section {
  scroll-margin-top: 90px;
  padding-top: 0;
}

.category-bar {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 10px;
  overflow: visible;
  padding: 4px 0 14px;
}

.result-count {
  color: #6e7d78;
  font-size: 14px;
}

.category-result-count {
  margin-left: auto;
  color: #556661;
  font-weight: 600;
  white-space: nowrap;
}

.category-chip {
  min-height: 40px;
  display: inline-flex;
  align-items: center;
  gap: 7px;
  flex: 0 0 auto;
  padding: 8px 12px;
  border: 1px solid rgba(36, 91, 85, 0.14);
  border-radius: 8px;
  color: #40514c;
  background: rgba(255, 255, 255, 0.82);
  cursor: pointer;
}

.category-chip.active {
  color: #fff;
  border-color: #245b55;
  background: #245b55;
}

.category-chip span {
  min-width: 24px;
  padding: 2px 7px;
  border-radius: 999px;
  background: rgba(36, 91, 85, 0.1);
  font-size: 12px;
}

.category-chip.active span {
  background: rgba(255, 255, 255, 0.18);
}

.resource-open {
  width: 100%;
  display: grid;
  grid-template-columns: 82px minmax(0, 1fr) 22px;
  gap: 14px;
  align-items: center;
  padding: 14px;
  text-align: left;
  color: inherit;
  background: transparent;
  cursor: pointer;
}

.resource-image {
  width: 82px;
  height: 70px;
  overflow: hidden;
  border-radius: 8px;
  background: #e8f2ef;
}

.resource-image img {
  width: 100%;
  height: 100%;
  object-fit: cover;
}

.image-fallback {
  width: 100%;
  height: 100%;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  color: #245b55;
  background: #e8f2ef;
  font-size: 28px;
}

.resource-content {
  min-width: 0;
}

.resource-topline {
  display: flex;
  align-items: center;
  gap: 8px;
  min-height: 20px;
  margin-bottom: 4px;
}

.resource-category {
  overflow: hidden;
  color: #75817d;
  font-size: 12px;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.mark-badge {
  flex: 0 0 auto;
  padding: 2px 6px;
  border-radius: 999px;
  color: #8f3f28;
  background: #f9ece6;
  font-size: 11px;
  font-weight: 700;
}

.resource-content strong {
  display: block;
  overflow: hidden;
  margin-bottom: 5px;
  font-size: 17px;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.resource-description {
  color: #5f6c68;
  font-size: 13px;
  line-height: 1.45;
  display: -webkit-box;
  overflow: hidden;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
}

.open-icon {
  color: #9aa8a4;
}

.resource-footer {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 10px;
  padding: 0 14px 14px;
}

.tag-list {
  min-width: 0;
  display: flex;
  flex-wrap: wrap;
  gap: 5px;
}

.tag-list .el-tag {
  max-width: 112px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.view-count {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  flex: 0 0 auto;
  color: #7d8985;
  font-size: 12px;
}

.empty-state {
  margin: 28px 0;
  padding: 44px 0;
  border: 1px solid rgba(36, 91, 85, 0.1);
  border-radius: 8px;
  background: rgba(255, 255, 255, 0.86);
}

:deep(.home-notification-popover) {
  padding: 0;
  overflow: hidden;
  border-radius: 22px;
  border: 1px solid rgba(36, 91, 85, 0.1);
  background:
    radial-gradient(circle at top right, rgba(224, 239, 233, 0.72), transparent 36%),
    linear-gradient(180deg, rgba(250, 252, 251, 0.98), rgba(255, 255, 255, 1));
  box-shadow: 0 26px 60px rgba(21, 36, 33, 0.16);
}

.notification-panel {
  padding: 18px;
  background: transparent;
}

.notification-panel-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
  margin-bottom: 16px;
  padding-bottom: 14px;
  border-bottom: 1px solid rgba(36, 91, 85, 0.08);
}

.notification-panel-heading {
  display: grid;
  grid-template-columns: 40px minmax(0, 1fr);
  align-items: center;
  column-gap: 12px;
}

.notification-panel-emblem {
  width: 40px;
  height: 40px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  grid-row: span 2;
  border-radius: 14px;
  color: #d2553f;
  background: linear-gradient(180deg, rgba(254, 238, 234, 0.96), rgba(255, 247, 244, 0.96));
  box-shadow:
    inset 0 0 0 1px rgba(210, 85, 63, 0.12),
    0 10px 22px rgba(210, 85, 63, 0.12);
}

.notification-panel-header strong {
  display: block;
  color: #173733;
  font-size: 16px;
  font-weight: 800;
}

.notification-panel-header p {
  margin: 2px 0 0;
  color: #73827d;
  font-size: 12px;
}

.notification-count {
  flex: 0 0 auto;
  padding: 6px 12px;
  border-radius: 999px;
  color: #245b55;
  background: rgba(232, 242, 239, 0.96);
  box-shadow: inset 0 0 0 1px rgba(36, 91, 85, 0.08);
  font-size: 12px;
  font-weight: 700;
}

.notification-state {
  padding: 28px 16px;
  color: #73827d;
  text-align: center;
  font-size: 13px;
}

.notification-state.error {
  color: #c0523f;
}

.notification-list {
  display: grid;
  gap: 10px;
  margin: 0;
  padding: 0;
  list-style: none;
}

.notification-item {
  display: grid;
  grid-template-columns: 12px minmax(0, 1fr);
  gap: 12px;
  padding: 11px 12px 10px;
  border-radius: 14px;
  background:
    linear-gradient(180deg, rgba(255, 248, 245, 0.96), rgba(255, 255, 255, 0.98));
  border: 1px solid rgba(210, 85, 63, 0.1);
  box-shadow: 0 12px 26px rgba(210, 85, 63, 0.08);
}

.notification-level-dot {
  width: 12px;
  height: 12px;
  margin-top: 5px;
  border-radius: 999px;
  background: linear-gradient(180deg, #ea7a62, #d2553f);
  box-shadow: 0 0 0 6px rgba(210, 85, 63, 0.08);
}

.notification-copy {
  min-width: 0;
}

.notification-copy strong {
  display: block;
  margin-bottom: 3px;
  color: #1f3632;
  font-size: 13px;
  font-weight: 700;
  line-height: 1.35;
}

.notification-copy p {
  margin: 0;
  color: #5b6b66;
  font-size: 12px;
  line-height: 1.5;
  display: -webkit-box;
  overflow: hidden;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
}

.notification-copy small {
  display: block;
  margin-top: 6px;
  color: #8b9793;
  font-size: 11px;
}

.notification-more {
  width: 100%;
  height: 42px;
  margin-top: 16px;
  border: 0;
  border-radius: 14px;
  color: #245b55;
  background:
    linear-gradient(180deg, rgba(236, 246, 243, 0.96), rgba(227, 241, 236, 0.96));
  box-shadow:
    inset 0 0 0 1px rgba(36, 91, 85, 0.1),
    0 10px 20px rgba(36, 91, 85, 0.08);
  font: inherit;
  font-weight: 700;
  letter-spacing: 0.02em;
  cursor: pointer;
  transition: transform 0.18s ease, box-shadow 0.18s ease, background 0.18s ease;
}

.notification-more:hover {
  transform: translateY(-1px);
  background:
    linear-gradient(180deg, rgba(241, 249, 247, 1), rgba(232, 244, 240, 1));
  box-shadow:
    inset 0 0 0 1px rgba(36, 91, 85, 0.14),
    0 14px 24px rgba(36, 91, 85, 0.12);
}

@media (max-width: 980px) {
  .header-nav {
    display: none;
  }

  .hero-section {
    padding-top: 28px;
    padding-bottom: 10px;
  }

  .resource-grid {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}

@media (max-width: 680px) {
  .header-inner,
  .hero-section,
  .section-block {
    padding-left: 16px;
    padding-right: 16px;
  }

  .brand-text small {
    display: none;
  }

  .hero-search {
    height: 52px;
  }

  .resource-grid {
    grid-template-columns: 1fr;
  }

  .resource-open {
    grid-template-columns: 72px minmax(0, 1fr);
  }

  .resource-image {
    width: 72px;
    height: 64px;
  }

  .open-icon {
    display: none;
  }

  .category-result-count {
    width: 100%;
    margin-left: 0;
  }
}
</style>
