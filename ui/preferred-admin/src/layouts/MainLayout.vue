<template>
  <div class="main-layout">
    <!-- 顶部导航栏 -->
    <el-header class="header">
      <div class="header-left">
        <h3>后台管理系统</h3>
      </div>
      <div class="header-right">
        <el-dropdown @command="handleCommand">
          <span class="user-info">
            <el-icon><User /></el-icon>
            {{ authStore.user?.username || '用户' }}
            <el-icon class="el-icon--right"><arrow-down /></el-icon>
          </span>
          <template #dropdown>
            <el-dropdown-menu>
              <el-dropdown-item command="logout">
                <el-icon><SwitchButton /></el-icon>
                退出登录
              </el-dropdown-item>
            </el-dropdown-menu>
          </template>
        </el-dropdown>
      </div>
    </el-header>

    <el-container class="main-container">
      <!-- 左侧菜单栏 -->
      <el-aside class="sidebar" width="250px">
        <el-menu
          :default-active="activeMenu"
          class="sidebar-menu"
          router
          background-color="#6366f1"
          text-color="#e0e7ff"
          active-text-color="#ffffff"
        >
          <!-- 首页菜单 -->
          <el-menu-item index="/dashboard">
            <el-icon><House /></el-icon>
            <span>首页</span>
          </el-menu-item>

          <!-- 运行监控菜单 -->
          <el-sub-menu index="monitor">
            <template #title>
              <el-icon><Monitor /></el-icon>
              <span>运行监控</span>
            </template>
            <el-menu-item index="/notification-management">
              <el-icon><Bell /></el-icon>
              <span>通知管理</span>
            </el-menu-item>
           <el-menu-item index="/access-management">
              <el-icon><Link /></el-icon>
              <span>访问地址管理</span>
            </el-menu-item>
             <el-menu-item index="/scheduled-task-management">
              <el-icon><Timer /></el-icon>
              <span>定时任务管理</span>
            </el-menu-item>
          </el-sub-menu>

          <!-- 设置数据源菜单 -->
          <el-sub-menu index="datasource">
            <template #title>
              <el-icon><Setting /></el-icon>
              <span>设置数据源</span>
            </template>
            <el-menu-item index="/tag-management">
              <el-icon><Collection /></el-icon>
              <span>标签管理</span>
            </el-menu-item>
            <el-menu-item index="/image-data-management">
              <el-icon><Picture /></el-icon>
              <span>图片管理</span>
            </el-menu-item>
            <!-- 修改这里：移除 /admin 前缀，因为已经在 /admin 路由下 -->
            <el-menu-item index="/file-management">
              <el-icon><Folder /></el-icon>
              <span>文件管理</span>
            </el-menu-item>
            
            <!-- 分类管理菜单（第80-84行）-->
            <el-menu-item index="/category-management">
              <el-icon><Collection /></el-icon>
              <span>分类管理</span>
            </el-menu-item>
          </el-sub-menu>

          <!-- 用户管理菜单 -->
          <el-menu-item index="/user-management">
            <el-icon><UserFilled /></el-icon>
            <span>用户管理</span>
          </el-menu-item>
        </el-menu>
      </el-aside>

      <!-- 主内容区域 -->
      <el-main class="main-content">
        <router-view />
      </el-main>
    </el-container>
  </div>
</template>

<script setup lang="ts">
import { useAuthStore } from '@/stores/auth'
import {
  ArrowDown,
  Bell,
  Collection,
  Folder,
  House,
  Link,
  Monitor,
  Picture,
  Setting,
  SwitchButton,
  Timer // 新增定时任务图标
  ,
  User,
  UserFilled
} from '@element-plus/icons-vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'

const route = useRoute()
const router = useRouter()
const authStore = useAuthStore()

// 当前激活的菜单项
const activeMenu = computed(() => {
  return route.path
})

// 处理下拉菜单命令
const handleCommand = async (command: string) => {
  if (command === 'logout') {
    try {
      await ElMessageBox.confirm(
        '确定要退出登录吗？',
        '退出确认',
        {
          confirmButtonText: '确定',
          cancelButtonText: '取消',
          type: 'warning'
        }
      )
      
      authStore.logout()
      ElMessage.success('已退出登录')
      router.push('/login')
    } catch {
      // 用户取消退出
    }
  }
}

onMounted(() => {
  console.log('MainLayout mounted')
})
</script>

<style scoped>
.main-layout {
  height: 100vh;
  display: flex;
  flex-direction: column;
  overflow: hidden; /* 添加这行 */
}

.main-container {
  flex: 1;
  overflow: hidden;
  display: flex; /* 添加这行 */
}

.main-content {
  background-color: #fefefe;
  overflow: hidden; /* 将 overflow-y: auto 改为 overflow: hidden */
  padding: 0; /* 确保没有内边距导致的高度问题 */
  height: 100%; /* 确保高度为100% */
}

.header {
  background-color: #fff;
  border-bottom: 1px solid #e4e7ed;
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 0 20px;
  box-shadow: 0 2px 8px rgba(0,21,41,.08);
  z-index: 1000;
}

.header-left h3 {
  margin: 0;
  color: #303133;
  font-weight: 600;
}

.header-right {
  display: flex;
  align-items: center;
}

.user-info {
  display: flex;
  align-items: center;
  cursor: pointer;
  padding: 8px 12px;
  border-radius: 6px;
  transition: all 0.3s ease;
  color: #606266;
}

.user-info:hover {
  background-color: #f5f7fa;
  color: #409EFF;
}

.main-container {
  flex: 1;
  overflow: hidden;
}

.sidebar {
  background: linear-gradient(180deg, #6366f1 0%, #5b21b6 100%);
  border-right: 1px solid #4c1d95;
  overflow-y: auto;
  box-shadow: 2px 0 8px rgba(99, 102, 241, 0.15);
}

.sidebar-menu {
  border-right: none;
  height: 100%;
  padding: 8px 0;
}

.main-content {
  background-color: #fefefe;
  overflow-y: hidden !important;
}

:deep(.el-menu-item),
:deep(.el-sub-menu__title) {
  height: 50px;
  line-height: 50px;
  transition: all 0.3s ease;
  margin: 3px 12px;
  border-radius: 8px;
  font-weight: 500;
}

:deep(.el-menu-item:hover),
:deep(.el-sub-menu__title:hover) {
  background-color: rgba(255, 255, 255, 0.1) !important;
  color: #ffffff !important;
  transform: translateX(2px);
}

:deep(.el-menu-item.is-active) {
  background: rgba(255, 255, 255, 0.2) !important;
  color: #ffffff !important;
  box-shadow: inset 4px 0 0 #ffffff;
  border-radius: 0 8px 8px 0;
  font-weight: 600;
}

:deep(.el-sub-menu .el-menu-item) {
  background-color: transparent;
  margin: 2px 16px 2px 28px;
  height: 44px;
  line-height: 44px;
  font-size: 14px;
}

:deep(.el-sub-menu .el-menu-item:hover) {
  background-color: rgba(255, 255, 255, 0.08) !important;
  color: #ffffff !important;
}

:deep(.el-sub-menu .el-menu-item.is-active) {
  background: rgba(255, 255, 255, 0.15) !important;
  color: #ffffff !important;
  box-shadow: inset 3px 0 0 rgba(255, 255, 255, 0.8);
  border-radius: 0 6px 6px 0;
  font-weight: 600;
}

/* 子菜单展开样式 */
:deep(.el-sub-menu .el-menu) {
  background-color: rgba(91, 33, 182, 0.2);
}

/* 图标样式优化 */
:deep(.el-menu-item .el-icon),
:deep(.el-sub-menu__title .el-icon) {
  margin-right: 12px;
  font-size: 16px;
  transition: all 0.3s ease;
}

:deep(.el-menu-item:hover .el-icon),
:deep(.el-sub-menu__title:hover .el-icon) {
  transform: scale(1.05);
}

/* 滚动条样式 */
.sidebar::-webkit-scrollbar {
  width: 6px;
}

.sidebar::-webkit-scrollbar-track {
  background: rgba(255, 255, 255, 0.1);
  border-radius: 3px;
}

.sidebar::-webkit-scrollbar-thumb {
  background: rgba(255, 255, 255, 0.3);
  border-radius: 3px;
}

.sidebar::-webkit-scrollbar-thumb:hover {
  background: rgba(255, 255, 255, 0.5);
}

/* 响应式设计 */
@media (max-width: 768px) {
  .sidebar {
    width: 200px !important;
  }
  
  :deep(.el-menu-item),
  :deep(.el-sub-menu__title) {
    margin: 2px 8px;
    height: 44px;
    line-height: 44px;
  }
}
</style>