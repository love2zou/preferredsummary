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
          <a href="#featured">精选推荐</a>
          <a href="#resources">资源目录</a>
        </nav>

        <button class="admin-button" type="button" title="后台管理" @click="openAdminLogin">
          <el-icon><User /></el-icon>
        </button>
      </div>
    </header>

    <section id="featured" class="section-block" v-if="featuredWebsites.length > 0">
      <div class="section-heading">
        <div>
          <p class="eyebrow">Featured</p>
          <h2>精选推荐</h2>
        </div>
        <button class="text-button" type="button" @click="selectCategory('')">
          查看全部
          <el-icon><ArrowRight /></el-icon>
        </button>
      </div>

      <div class="featured-grid">
        <article
          v-for="website in featuredWebsites"
          :key="website.id"
          class="featured-card"
          @click="handleWebsiteOpen(website)"
        >
          <div class="featured-image">
            <img
              v-if="!website.imageError && website.pictureUrl"
              :src="getImageUrl(website.pictureUrl)"
              :alt="website.name"
              @error="() => handleImageError(website)"
            />
            <div v-else class="image-fallback">
              <el-icon><Link /></el-icon>
            </div>
          </div>
          <div class="featured-content">
            <span class="pill">
              <el-icon><Star /></el-icon>
              推荐
            </span>
            <h3>{{ website.name }}</h3>
            <p>{{ website.description || '这个资源暂时还没有描述。' }}</p>
          </div>
        </article>
      </div>
    </section>

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
        </div>

        <div class="hero-stats" aria-label="资源统计">
          <div>
            <strong>{{ totalWebsiteCount }}</strong>
            <span>收录资源</span>
          </div>
          <div>
            <strong>{{ categories.length }}</strong>
            <span>内容分类</span>
          </div>
          <div>
            <strong>{{ markedWebsiteCount }}</strong>
            <span>精选推荐</span>
          </div>
        </div>
      </div>
    </section>

    <section id="resources" class="section-block resource-section">
      <div class="section-heading">
        <div>
          <p class="eyebrow">Directory</p>
          <h2>资源目录</h2>
        </div>
        <span class="result-count">{{ filteredWebsites.length }} 个结果</span>
      </div>

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
import { API_CONFIG } from '@/services/api'
import AutoLoginService from '@/services/autoLoginService'
import { categoryService, type Category } from '@/services/categoryService'
import { networkUrlService, type NetworkUrlListDto, type TagInfo } from '@/services/networkUrlService'
import { tagService, type Tag } from '@/services/tagService'
import { ArrowRight, Collection, Link, Search, Star, User, View } from '@element-plus/icons-vue'
import { ElMessage } from 'element-plus'
import { computed, onMounted, ref } from 'vue'

const loading = ref(false)
const searchKeyword = ref('')
const activeTab = ref('')
const categories = ref<Category[]>([])
const websites = ref<NetworkUrlListDto[]>([])
const tags = ref<Tag[]>([])
const searchTimeout = ref<number | null>(null)
const tagMap = ref<Map<string, Tag>>(new Map())

const sortedCategories = computed<Category[]>(() => {
  return [...categories.value].sort((a, b) => a.seqNo - b.seqNo)
})

const totalWebsiteCount = computed(() => websites.value.length)
const markedWebsiteCount = computed(() => websites.value.filter(isMarked).length)

const featuredWebsites = computed<NetworkUrlListDto[]>(() => {
  return websites.value
    .filter((website) => isMarked(website) && isWebsiteAvailable(website))
    .sort((a, b) => getViewCount(b) - getViewCount(a))
    .slice(0, 3)
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

const openAdminLogin = (): void => {
  window.open(ADMIN_LOGIN_URL, '_blank')
}

onMounted(async () => {
  try {
    await AutoLoginService.autoLogin()
    await Promise.all([loadCategories(), loadTags(), loadWebsites()])
  } catch (error) {
    console.error('首页初始化失败:', error)
    ElMessage.error('登录失败，无法加载数据')
  }
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
.admin-button,
.text-button,
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

.admin-button {
  width: 36px;
  height: 36px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  border-radius: 8px;
  color: #245b55;
  background: #e8f2ef;
  cursor: pointer;
}

.hero-section,
.section-block {
  max-width: 1180px;
  margin: 0 auto;
  padding-left: 24px;
  padding-right: 24px;
}

.hero-section {
  display: flex;
  justify-content: center;
  min-height: auto;
  padding-top: 12px;
  padding-bottom: 28px;
}

.hero-copy {
  width: min(760px, 100%);
}

.eyebrow {
  margin: 0 0 10px;
  color: #b85b3d;
  font-size: 13px;
  font-weight: 700;
  letter-spacing: 0;
  text-transform: uppercase;
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

.hero-stats {
  display: flex;
  justify-content: center;
  gap: 12px;
  margin-top: 18px;
}

.hero-stats div {
  flex: 1;
  min-width: 0;
  padding: 14px 16px;
  box-sizing: border-box;
  border: 1px solid rgba(36, 91, 85, 0.12);
  border-radius: 8px;
  background: rgba(255, 255, 255, 0.72);
}

.hero-stats strong,
.hero-stats span {
  display: block;
}

.hero-stats strong {
  font-size: 24px;
}

.hero-stats span {
  margin-top: 4px;
  color: #6a7a75;
  font-size: 13px;
}

.section-block {
  padding-top: 38px;
  padding-bottom: 34px;
}

.section-heading {
  display: flex;
  align-items: flex-end;
  justify-content: space-between;
  gap: 18px;
  margin-bottom: 18px;
}

.section-heading h2 {
  margin: 0;
  font-size: 30px;
  line-height: 1.2;
}

.text-button {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  color: #245b55;
  background: transparent;
  cursor: pointer;
}

.result-count {
  color: #6e7d78;
  font-size: 14px;
}

.featured-grid,
.resource-grid {
  display: grid;
  gap: 16px;
}

.featured-grid {
  grid-template-columns: repeat(3, minmax(0, 1fr));
}

.featured-card {
  overflow: hidden;
  border: 1px solid rgba(36, 91, 85, 0.11);
  border-radius: 8px;
  background: #fff;
  box-shadow: 0 14px 34px rgba(21, 36, 33, 0.08);
  cursor: pointer;
  transition: transform 0.18s ease, box-shadow 0.18s ease;
}

.featured-card:hover,
.resource-card:hover {
  transform: translateY(-3px);
  box-shadow: 0 18px 38px rgba(21, 36, 33, 0.13);
}

.featured-image {
  height: 164px;
  background: #e8f2ef;
}

.featured-image img,
.image-fallback {
  width: 100%;
  height: 100%;
  object-fit: cover;
}

.image-fallback {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  color: #245b55;
  background: #e8f2ef;
  font-size: 28px;
}

.featured-content {
  padding: 16px;
}

.pill {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  padding: 4px 8px;
  border-radius: 999px;
  color: #8f3f28;
  background: #f9ece6;
  font-size: 12px;
  font-weight: 700;
}

.featured-content h3 {
  margin: 12px 0 8px;
  font-size: 20px;
}

.featured-content p {
  min-height: 44px;
  margin: 0;
  color: #5e6d68;
  line-height: 1.55;
  display: -webkit-box;
  overflow: hidden;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
}

.resource-section {
  scroll-margin-top: 90px;
  padding-top: 22px;
}

.category-bar {
  display: flex;
  flex-wrap: wrap;
  gap: 10px;
  overflow: visible;
  padding: 4px 0 18px;
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

.resource-grid {
  grid-template-columns: repeat(3, minmax(0, 1fr));
}

.resource-card {
  border: 1px solid rgba(36, 91, 85, 0.11);
  border-radius: 8px;
  background: rgba(255, 255, 255, 0.94);
  box-shadow: 0 10px 26px rgba(21, 36, 33, 0.07);
  transition: transform 0.18s ease, box-shadow 0.18s ease;
}

.resource-card.unavailable {
  opacity: 0.62;
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

@media (max-width: 980px) {
  .header-nav {
    display: none;
  }

  .hero-section {
    padding-top: 28px;
  }

  .featured-grid,
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

  .featured-grid,
  .resource-grid {
    grid-template-columns: 1fr;
  }

  .hero-stats {
    flex-direction: column;
  }

  .section-heading {
    align-items: flex-start;
    flex-direction: column;
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
}
</style>
