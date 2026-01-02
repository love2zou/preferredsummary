<template>
  <div class="home-container">
    <!-- 顶部导航栏 -->
    <div class="top-header">
      <div class="header-content">
        <div class="search-container">
          <el-input
            v-model="searchKeyword"
            placeholder="搜索网站..."
            prefix-icon="Search"
            clearable
            @input="handleSearch"
            class="search-input"
          />
        </div>
      </div>
      <!-- 将按钮替换为纯图标，固定右上角 -->
      <el-icon class="admin-login-icon" @click="openAdminLogin">
        <User />
      </el-icon>
    </div>

    <!-- 主要内容区域 -->
    <div class="main-content">
      <el-tabs v-model="activeTab" @tab-change="handleTabChange" class="category-tabs">
        <el-tab-pane 
          v-for="category in sortedCategories" 
          :key="category.categoryCode"
          :name="category.categoryCode"
          class="tab-pane"
        >
        <!-- 在组件中使用 -->
        <template #label>
          <span class="tab-label">
          <el-icon v-if="isElementIcon(category.categoryIcon)">
            <component :is="getIconComponent(category.categoryIcon)" />
          </el-icon>
          <i v-else :class="getIconClass(category.categoryIcon)"></i>
           {{ category.categoryName }}
           </span>
        </template>

          <!-- 网站卡片网格 -->
          <div class="websites-grid" v-loading="loading">
            <div 
              v-for="website in filteredWebsites" 
              :key="website.id"
              class="website-card"
            >
              <div class="card-layout">
                <!-- 左侧图片区域 -->
                <div class="card-image">
                  <img 
                    v-if="!website.imageError && (website.pictureUrl)"
                    :src="getImageUrl(website.pictureUrl)"
                    :alt="website.name"
                    class="website-image"
                    @error="() => handleImageError(website)"
                  />
                  <div v-else class="default-image">
                    <el-icon class="default-icon"><Link /></el-icon>
                  </div>
                </div>
                
                <!-- 右侧内容区域 -->
                <div class="card-content">
                  <!-- 网站标题和URL -->
                  <div class="title-section">
                    <h3 
                      class="website-title clickable-title" 
                      :title="website.name"
                      @click="handleTitleClick(website)"
                    >
                      {{ website.name }}
                    </h3>
                  </div>
                  
                  <!-- 网站描述 -->
                  <p class="website-description" :title="website.description">
                    {{ website.description || '暂无描述' }}
                  </p>
                  
                  <!-- 底部标签和推荐标识 -->
                  <div class="card-footer">
                    <div class="card-tags">
                      <el-tag 
                        v-for="tag in getAllTags(website)"
                        :key="tag.codeType"
                        :style="{
                          color: tag.hexColor || '#000',
                          backgroundColor: tag.rgbColor || 'rgba(156, 136, 255, 0.1)',
                          border: `1px solid ${tag.hexColor || '#9c88ff'}`
                        }"
                        size="small"
                        class="tag-type"
                        effect="plain"
                      >
                        {{ tag.name }}
                      </el-tag>
                    </div>
                    
                    <div class="card-badges">
                      <el-tag v-if="website.isMark" type="success" size="small" class="recommend-badge">
                        推荐
                      </el-tag>
                    </div>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </el-tab-pane>
      </el-tabs>
    </div>
  </div>
</template>

<script setup lang="ts">
import { getIconClass, getIconComponent, isElementIcon } from '@/data/iconLibrary'
import { API_CONFIG } from '@/services/api'
import AutoLoginService from '@/services/autoLoginService'
import { categoryService, type Category } from '@/services/categoryService'
import { networkUrlService, type NetworkUrlListDto, type TagInfo } from '@/services/networkUrlService'
import { tagService, type Tag } from '@/services/tagService'
import { Link, User } from '@element-plus/icons-vue'
import { ElMessage } from 'element-plus'
import { computed, onMounted, ref, watch } from 'vue'

// 移除临时定义的 TagInfo 接口
// interface TagInfo {
//   codeType: string
//   name: string
//   hexColor: string
//   rgbColor: string
// }

// 响应式数据
const loading = ref<boolean>(false)
const searchKeyword = ref<string>('')
const activeTab = ref<string>('')
const categories = ref<Category[]>([])
const websites = ref<NetworkUrlListDto[]>([])
const tags = ref<Tag[]>([])
const searchTimeout = ref<number | null>(null)

// 性能优化：标签映射表
const tagMap = ref<Map<string, Tag>>(new Map())

// 计算属性
const sortedCategories = computed<Category[]>(() => {
  return [...categories.value].sort((a, b) => a.seqNo - b.seqNo)
})

// 修改 filteredWebsites 计算属性中的排序逻辑
const filteredWebsites = computed<NetworkUrlListDto[]>(() => {
  let result = websites.value.filter(website => {
    // 按分类筛选 - 使用分割后的数组进行精确匹配
    if (activeTab.value) {
      const categoryCodes = (website.categoryCode || '').split(',').map(code => code.trim()).filter(code => code)
      if (!categoryCodes.includes(activeTab.value)) {
        return false
      }
    }
    
    // 按搜索关键词筛选
    if (searchKeyword.value) {
      const keyword = searchKeyword.value.toLowerCase()
      const allTags = getAllTags(website)
      const tagNames = allTags.map(tag => tag.name).join(' ')
      
      return (
        website.name.toLowerCase().includes(keyword) ||
        (website.description && website.description.toLowerCase().includes(keyword)) ||
        tagNames.toLowerCase().includes(keyword)
      )
    }
    
    return true
  })
  
  // 修改排序逻辑以适配 boolean 类型
  return result.sort((a, b) => {
    // 推荐的在前 (boolean 比较)
    if (a.isMark !== b.isMark) return (b.isMark ? 1 : 0) - (a.isMark ? 1 : 0)
    // 可用的在前 (boolean 比较)
    if (a.isAvailable !== b.isAvailable) return (b.isAvailable ? 1 : 0) - (a.isAvailable ? 1 : 0)
    // 按序号排序
    return a.seqNo - b.seqNo
  })
})

// 修改点击处理逻辑
const handleTitleClick = (website: NetworkUrlListDto): void => {
  if (!website.isAvailable) {  // boolean 判断
    ElMessage.warning('该网站当前不可用')
    return
  }
  
  // 打开网站
  window.open(website.url, '_blank')
}

const loadCategories = async (): Promise<void> => {
  try {
    await AutoLoginService.ensureLoggedIn()
    
    console.log('开始加载分类数据...')
    const response = await categoryService.getCategoryList({ page: 1, pageSize: 100 })
    console.log('分类API响应:', response)
    categories.value = response.data
    console.log('分类数据:', categories.value)
    
    // 设置默认选中第一个分类
    if (categories.value.length > 0 && !activeTab.value) {
      const firstCategory = sortedCategories.value[0]
      activeTab.value = firstCategory.categoryCode
      console.log('设置默认分类:', firstCategory)
    }
  } catch (error) {
    console.error('加载分类失败:', error)
    ElMessage.error('加载分类失败')
  }
}

// 在 handleTabChange 方法中添加调试信息
const handleTabChange = (tabName: string): void => {
  console.log('切换到选项卡:', tabName)
  activeTab.value = tabName
  loadWebsites()
}

// 在 loadWebsites 方法中添加调试信息
const loadWebsites = async (): Promise<void> => {
  try {
    await AutoLoginService.ensureLoggedIn()
    
    loading.value = true
    console.log('正在加载网站数据，分类代码:', activeTab.value)
    
    const response = await networkUrlService.getNetworkUrlList({ 
      page: 1, 
      pageSize: 1000,
      categoryCode: activeTab.value
    })
    
    console.log('API响应数据:', response)
    console.log('网站数据数量:', response.data?.length || 0)
    
    // 使用优化后的接口数据，包含标签信息
    websites.value = response.data
    
    // 添加过滤后的数据调试
    console.log('过滤后的网站数据:', filteredWebsites.value)
    console.log('过滤后的数据数量:', filteredWebsites.value.length)
    
  } catch (error) {
    console.error('加载网站列表失败:', error)
    ElMessage.error('加载网站列表失败')
  } finally {
    loading.value = false
  }
}

// 删除第150-177行的重复定义
// const filteredWebsites = computed<NetworkUrlListDto[]>(() => {
//   let result = websites.value.filter(website => {
//     // 按分类筛选
//     if (activeTab.value && website.categoryCode !== activeTab.value) {
//       return false
//     }
//     
//     // 按搜索关键词筛选
//     if (searchKeyword.value) {
//       const keyword = searchKeyword.value.toLowerCase()
//       const allTags = getAllTags(website)
//       const tagNames = allTags.map(tag => tag.name).join(' ')
//       
//       return (
//         website.name.toLowerCase().includes(keyword) ||
//         (website.description && website.description.toLowerCase().includes(keyword)) ||
//         tagNames.toLowerCase().includes(keyword)
//       )
//     }
//     
//     return true
//   })
//   
//   // 修改排序逻辑以适配 boolean 类型
//   return result.sort((a, b) => {
//     // 推荐的在前 (boolean 比较)
//     if (a.isMark !== b.isMark) return (b.isMark ? 1 : 0) - (a.isMark ? 1 : 0)
//     // 可用的在前 (boolean 比较)
//     if (a.isAvailable !== b.isAvailable) return (b.isAvailable ? 1 : 0) - (a.isAvailable ? 1 : 0)
//     // 按序号排序
//     return a.seqNo - b.seqNo
//   })
// })

// 删除第218-244行的重复定义，完全移除以下代码：
// const filteredWebsites = computed<NetworkUrlListDto[]>(() => {
//   console.log('开始过滤网站数据')
//   console.log('当前选中分类:', activeTab.value)
//   console.log('原始网站数据:', websites.value)
//   
//   let result = websites.value.filter(website => {
//     // 按分类筛选
//     if (activeTab.value && website.categoryCode !== activeTab.value) {
//       console.log(`网站 ${website.name} 被过滤，分类不匹配: ${website.categoryCode} !== ${activeTab.value}`)
//       return false
//     }
//     
//     // 按搜索关键词筛选
//     if (searchKeyword.value) {
//       const keyword = searchKeyword.value.toLowerCase()
//       const allTags = getAllTags(website)
//       const tagNames = allTags.map(tag => tag.name).join(' ')
//       
//       return (
//         website.name.toLowerCase().includes(keyword) ||
//         (website.description && website.description.toLowerCase().includes(keyword)) ||
//         tagNames.toLowerCase().includes(keyword)
//       )
//     }
//     
//     console.log(`网站 ${website.name} 通过过滤，分类: ${website.categoryCode}`)
//     return true
//   })
//   
//   console.log('过滤结果:', result)
//   
//   // 修改排序逻辑以适配 boolean 类型
//   return result.sort((a, b) => {
//     // 推荐的在前 (boolean 比较)
//     if (a.isMark !== b.isMark) return (b.isMark ? 1 : 0) - (a.isMark ? 1 : 0)
//     // 可用的在前 (boolean 比较)
//     if (a.isAvailable !== b.isAvailable) return (b.isAvailable ? 1 : 0) - (a.isAvailable ? 1 : 0)
//     // 按序号排序
//     return a.seqNo - b.seqNo
//   })
// })

const loadTags = async (): Promise<void> => {
  try {
    await AutoLoginService.ensureLoggedIn()
    
    const response = await tagService.getTagList({ page: 1, pageSize: 1000 })
    tags.value = response.data
    
    // 创建标签映射表，提升查找性能
    tagMap.value.clear()
    tags.value.forEach(tag => {
      tagMap.value.set(tag.codeType, tag)
    })
  } catch (error) {
    console.error('加载标签失败:', error)
  }
}

const handleSearch = (): void => {
  // 防抖搜索
  if (searchTimeout.value) {
    clearTimeout(searchTimeout.value)
  }
  searchTimeout.value = window.setTimeout(() => {
    // 搜索逻辑在计算属性中处理
  }, 300)
}

const getImageUrl = (url: string) => {
  if (!url) return '/default-image.png'
  if (url.startsWith('http')) return url
  return `${API_CONFIG.BASE_URL}${url}`
}

const handleImageError = (website: NetworkUrlListDto): void => {
  // 标记该网站的图片加载失败，这样会显示默认图标
  website.imageError = true
}

// 多标签处理方法
const getAllTags = (website: NetworkUrlListDto): TagInfo[] => {
  if (website.tags && website.tags.length > 0) {
    return website.tags
  }
  
  // 兼容旧数据：如果没有tags数组，使用tagCodeType
  const mainTag = tagMap.value.get(website.tagCodeType)
  if (mainTag) {
    return [{
      codeType: mainTag.codeType,
      name: mainTag.name,
      hexColor: mainTag.hexColor,
      rgbColor: mainTag.rgbColor
    }]
  }
  return []
}

const getAdditionalTags = (website: NetworkUrlListDto): TagInfo[] => {
  if (!website.tags) return []
  
  // 过滤掉主标签，返回其他标签，最多显示2个额外标签
  return website.tags
    .filter(tag => tag.codeType !== website.tagCodeType)
    .slice(0, 2)
}

const hasMoreTags = (website: NetworkUrlListDto): boolean => {
  if (!website.tags) return false
  const additionalCount = website.tags.filter(tag => tag.codeType !== website.tagCodeType).length
  return additionalCount > 2
}

// 兼容性方法：通过标签代码获取样式
const getTagStyleByCode = (tagCodeType: string) => {
  const tag = tagMap.value.get(tagCodeType)
  if (tag && tag.hexColor && tag.rgbColor) {
    return {
      color: tag.hexColor,
      backgroundColor: tag.rgbColor,
      borderColor: tag.hexColor
    }
  }
  return {}
}

// 兼容性方法：通过标签代码获取名称
const getTagNameByCode = (tagCodeType: string): string => {
  const tag = tagMap.value.get(tagCodeType)
  return tag ? tag.name : tagCodeType
}

// 生命周期 - 先自动登录，再加载数据
onMounted(async () => {
  try {
    await AutoLoginService.autoLogin()
    console.log('自动登录成功，开始加载数据')
    
    try {
      await loadCategories()
      console.log('分类数据加载完成')
      
      await loadTags()
      console.log('标签数据加载完成')
      
      await loadWebsites()
      console.log('网站数据加载完成')
    } catch (dataError) {
      console.error('数据加载失败:', dataError)
      ElMessage.error('数据加载失败，请刷新页面重试')
    }
  } catch (loginError) {
    console.error('自动登录失败:', loginError)
    ElMessage.error('登录失败，无法加载数据')
  }
})

// 监听分类变化
watch(activeTab, () => {
  loadWebsites()
})
const ADMIN_LOGIN_URL = (import.meta.env.VITE_ADMIN_BASE_URL
  ? `${import.meta.env.VITE_ADMIN_BASE_URL}/login`
  : `${window.location.protocol}//${window.location.hostname}:8081/login`)

const openAdminLogin = (): void => {
  window.open(ADMIN_LOGIN_URL, '_blank')
}
// // 图标组件处理
// const isElementIcon = (iconName: string): boolean => {
//   const elementIcons = ['Folder', 'Setting', 'House', 'Star', 'Grid', 'Document', 'Menu', 'More', 'Management', 'Files', 'DataBoard']
//   return elementIcons.includes(iconName)
// }

// const getIconComponent = (iconName: string) => {
//   if (!iconName) return Folder
  
//   const iconMap: Record<string, any> = {
//     'Folder': Folder, 'Setting': Setting, 'House': House, 'Star': Star,
//     'Grid': Grid, 'Document': Document, 'Menu': Menu, 'More': More,
//     'Management': Management, 'Files': Files, 'DataBoard': DataBoard,
//     'Search': Search, 'Link': Link
//   }
  
//   return iconMap[iconName] || iconMap[iconName.toLowerCase()] || Folder
// }
</script>

<style scoped>
.home-container {
  min-height: 100vh;
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  padding: 0;
}

.top-header {
  background: rgba(255, 255, 255, 0.98);
  backdrop-filter: blur(10px);
  border-bottom: 1px solid rgba(0, 0, 0, 0.1);
  position: sticky;
  top: 0;
  z-index: 1000;
  padding: 16px 24px;
}

.header-content {
  max-width: 1200px;
  margin: 0 auto;
  display: flex;
  justify-content: center; /* 改为居中对齐 */
  align-items: center;
}

.nav-links {
  display: flex;
  gap: 24px;
}

.nav-link {
  color: #606266;
  text-decoration: none;
  font-weight: 500;
  padding: 8px 16px;
  border-radius: 8px;
  transition: all 0.3s ease;
}

.nav-link:hover {
  color: var(--el-color-primary);
  background: rgba(64, 158, 255, 0.1);
}

.nav-link.router-link-active {
  color: var(--el-color-primary);
  background: rgba(64, 158, 255, 0.15);
}

.search-container {
  width: 400px;
}

.search-input {
  border-radius: 20px;
}

.top-header {
  position: relative; /* 使右上角定位生效 */
}
.admin-login-icon {
  position: absolute;
  top: 10px;
  right: 16px;
  font-size: 20px; /* 图标大小 */
  color: var(--el-color-primary);
  cursor: pointer;
}
.admin-login-icon:hover {
  filter: brightness(1.1);
}
.main-content {
  max-width: 1200px;
  margin: 0 auto;
  padding: 24px;
}

.category-tabs {
  background: rgba(255, 255, 255, 0.95);
  border-radius: 12px;
  padding: 16px;
  box-shadow: 0 4px 20px rgba(0, 0, 0, 0.1);
}

.tab-label {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 16px;
}

.tab-icon {
  font-size: 16px;
}

.websites-grid {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 16px;
  margin-top: 10px;
  padding: 0 8px;
}

.website-card {
  background: white;
  border-radius: 12px;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.08);
  transition: all 0.3s ease;
  border: 1px solid #f0f0f0;
  position: relative;
  min-height: 100px;
  overflow: hidden;
  padding: 0;
}

.website-card:hover {
  transform: translateY(-2px);
  box-shadow: 0 8px 24px rgba(0, 0, 0, 0.12);
  border-color: #e6f7ff;
}

.card-layout {
  display: flex;
  height: 134px;
  position: relative;
}

.card-image {
  flex: 0 0 100px;
  position: relative;
  display: flex;
  align-items: center;
  justify-content: center;
  height: 100%;
}

.website-image {
  width: 130px;
  height: 100%;
  object-fit: cover;
  border-radius: 12px 0 0 12px;
  border: none;
  box-shadow: none;
}

.default-image {
  width: 100px;
  height: 100%;
  background: linear-gradient(135deg, #f5f7fa 0%, #c3cfe2 100%);
  border-radius: 12px 0 0 12px;
  display: flex;
  align-items: center;
  justify-content: center;
  border: none;
}

.default-icon {
  font-size: 32px;
  color: #999;
}

.status-indicator {
  position: absolute;
  top: 8px;
  right: 8px;
  z-index: 2;
}

.status-dot {
  width: 12px;
  height: 12px;
  border-radius: 50%;
  border: 2px solid white;
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.2);
}

.status-dot.available {
  background-color: #52c41a;
}

.status-dot.unavailable {
  background-color: #ff4d4f;
}

.card-content {
  flex: 1;
  display: flex;
  flex-direction: column;
  justify-content: space-between;
  padding: 10px;
  min-height: 100px;
}

/* 基础卡片样式优化 - 减少间距 */
.title-section {
  margin-bottom: 4px; /* 从8px减少到4px */
}

.website-title {
  margin: 0 0 2px 0; /* 从4px减少到2px */
  font-size: 18px;
  font-weight: 600;
  color: #1a1a1a;
  line-height: 1.3;
  cursor: pointer;
  transition: color 0.2s ease;
}

.website-description {
  margin: 0 0 6px 0; /* 从12px减少到6px */
  font-size: 14px;
  color: #666;
  line-height: 1.4;
  overflow: hidden;
  text-overflow: ellipsis;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  flex: 1;
}

.website-title:hover {
  color: #1890ff;
}

.website-description {
  margin: 0 0 12px 0;
  font-size: 13px;
  color: #666;
  line-height: 1.4;
  overflow: hidden;
  text-overflow: ellipsis;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  flex: 1;
}

.card-footer {
  display: flex;
  justify-content: space-between;
  align-items: flex-end;
  margin-top: 4px; /* 减少自动边距 */
  gap: 8px;
}

.card-tags {
  display: flex;
  gap: 4px;
  flex-wrap: wrap;
  flex: 1;
}

.tag-type {
  font-size: 11px;
  border-radius: 4px;
  padding: 2px 6px;
  font-weight: 500;
}

.card-badges {
  flex-shrink: 0;
}

.recommend-badge {
  font-size: 10px;
  padding: 2px 6px;
  border-radius: 4px;
  font-weight: 500;
}

.unavailable-badge {
  font-size: 9px;
  padding: 1px 4px;
  border-radius: 3px;
}

/* 响应式设计 - 高分辨率大屏幕优化 */
@media (min-width: 2560px) {
  .top-header {
    padding: 24px 32px;
  }
  
  .header-content {
    max-width: 1800px;
  }
  
  .search-container {
    width: 600px;
  }
  
  .search-input {
    font-size: 18px;
  }
  
  .main-content {
    max-width: 1800px;
    padding: 32px;
  }
  
  .category-tabs {
    padding: 24px;
    border-radius: 16px;
  }
  
  .tab-label {
    font-size: 18px;
    gap: 12px;
  }
  
  .tab-icon {
    font-size: 20px;
  }
  
  .websites-grid {
    grid-template-columns: repeat(3, 1fr); /* 改为3列 */
    gap: 24px;
    margin-top: 10px;
    padding: 0 12px;
  }

  .card-layout {
    height: 180px;
  }
  
  .card-image {
    flex: 0 0 140px;
  }
  
  .website-image {
    width: 180px;
  }
  
  .default-image {
    width: 140px;
  }
  
  .default-icon {
    font-size: 48px;
  }
  
  .card-content {
    padding: 20px;
  }
    
  .website-title {
    font-size: 20px;
    margin-bottom: 4px; /* 减少间距 */
  }
  
  .website-description {
    font-size: 16px;
    margin-bottom: 8px; /* 减少间距 */
  }
    
  .tag-type {
    font-size: 14px;
    padding: 4px 8px;
  }
  
  .recommend-badge {
    font-size: 12px;
    padding: 4px 8px;
  }
  
  .status-dot {
    width: 16px;
    height: 16px;
  }
}

/* 1440p 和 1600p 分辨率优化 */
@media (min-width: 1920px) and (max-width: 2559px) {
  .top-header {
    padding: 20px 28px;
  }
  
  .header-content {
    max-width: 1600px;
  }
  
  .search-container {
    width: 500px;
  }
  
  .search-input {
    font-size: 16px;
  }
  
  .main-content {
    max-width: 1600px;
    padding: 28px;
  }
  
  .category-tabs {
    padding: 20px;
  }
  
  .tab-label {
    font-size: 16px;
    gap: 10px;
  }
  
  .tab-icon {
    font-size: 18px;
  }

  .websites-grid {
    grid-template-columns: repeat(3, 1fr); /* 改为3列 */
    gap: 20px;
    margin-top: 10px;
  }
  
  .card-layout {
    height: 150px;
  }
  
  .card-image {
    flex: 0 0 120px;
  }
  
  .website-image {
    width: 150px;
  }
  
  .default-image {
    width: 120px;
  }
  
  .default-icon {
    font-size: 40px;
  }
  
  .website-title {
    font-size: 18px;
    margin-bottom: 3px; /* 减少间距 */
  }
  
  .website-description {
    font-size: 15px;
    margin-bottom: 8px; /* 减少间距 */
  }
  
  .tag-type {
    font-size: 13px;
    padding: 3px 7px;
  }
  
  .recommend-badge {
    font-size: 11px;
    padding: 3px 7px;
  }
  
  .status-dot {
    width: 14px;
    height: 14px;
  }
}

/* 标准桌面分辨率 */
@media (min-width: 1200px) and (max-width: 1919px) {
  .websites-grid {
    grid-template-columns: repeat(3, 1fr);
  }
  .website-title {
    margin-bottom: 2px; /* 减少间距 */
  }
  
  .website-description {
    margin-bottom: 6px; /* 减少间距 */
  }
}

/* 中等屏幕 */
@media (max-width: 1200px) {
  .websites-grid {
    grid-template-columns: repeat(2, 1fr);
  }
  
  .main-content {
    padding: 20px;
  }
  
  .search-container {
    width: 350px;
  }
  .website-title {
    margin-bottom: 2px;
  }
  
  .website-description {
    margin-bottom: 6px;
  }
}

/* 小屏幕 */
@media (max-width: 768px) {
  .websites-grid {
    grid-template-columns: 1fr;
  }
  
  .main-content {
    padding: 16px;
  }
  
  .search-container {
    width: 300px;
  }
  
  .top-header {
    padding: 12px 16px;
  }
  
  .card-layout {
    height: 120px;
  }
  
  .card-image {
    flex: 0 0 80px;
  }
  
  .website-image {
    width: 120px;
  }
  
  .default-image {
    width: 80px;
  }
  
  .default-icon {
    font-size: 24px;
  }
  
  .website-title {
    font-size: 14px;
  }
  
  .website-description {
    font-size: 12px;
  }
  
  .tag-type {
    font-size: 10px;
    padding: 2px 4px;
  }
  
  .recommend-badge {
    font-size: 9px;
    padding: 2px 4px;
  }
}
</style>
