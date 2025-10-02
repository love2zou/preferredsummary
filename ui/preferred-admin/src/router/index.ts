import { createRouter, createWebHistory } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import Login from '@/views/Login.vue'
import Register from '@/views/Register.vue'
import MainLayout from '@/layouts/MainLayout.vue'
import Home from '@/views/Home.vue'
import UserManagement from '@/views/UserManagement.vue'
import TagManagement from '@/views/TagManagement.vue'
import ImageDataManagement from '@/views/ImageDataManagement.vue'
import FileManagement from '@/views/FileManagement.vue'
import AccessManagement from '@/views/AccessManagement.vue'
import NotificationManagement from '@/views/NotificationManagement.vue'
import CategoryManagement from '@/views/CategoryManagement.vue'
import ScheduledTaskManagement from '@/views/ScheduledTaskManagement.vue'

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    {
      path: '/',
      redirect: '/login'
    },
    {
      path: '/login',
      name: 'Login',
      component: Login,
      meta: { requiresGuest: true }
    },
    {
      path: '/register',
      name: 'Register',
      component: Register,
      meta: { requiresGuest: true }
    },
    {
      path: '/admin',
      component: MainLayout,
      meta: { requiresAuth: true },
      children: [
        {
          path: '',
          redirect: '/dashboard'
        },
        {
          path: '/dashboard',
          name: 'Dashboard',
          component: Home,
          meta: { requiresAuth: true }
        },
        {
          path: '/user-management',
          name: 'UserManagement',
          component: UserManagement,
          meta: { requiresAuth: true }
        },
        {
          path: '/tag-management',
          name: 'TagManagement',
          component: TagManagement,
          meta: { requiresAuth: true }
        },
        {
          path: '/category-management',
          name: 'CategoryManagement',
          component: CategoryManagement,
          meta: { requiresAuth: true }
        },
        {
          path: '/image-data-management',
          name: 'ImageDataManagement',
          component: ImageDataManagement,
          meta: { requiresAuth: true }
        },
        {
          path: '/file-management',
          name: 'FileManagement',
          component: FileManagement,
          meta: { requiresAuth: true }
        },
        {
          path: '/access-management',
          name: 'AccessManagement',
          component: AccessManagement,
          meta: { requiresAuth: true }
        },
        {
          path: '/notification-management',
          name: 'NotificationManagement',
          component: NotificationManagement,
          meta: { requiresAuth: true }
        },
        {
          path: '/scheduled-task-management',
          name: 'ScheduledTaskManagement',
          component: ScheduledTaskManagement,
          meta: { requiresAuth: true }
        }
      ]
    }
  ]
})

// 路由守卫
router.beforeEach((to, from, next) => {
  const authStore = useAuthStore()
  
  if (to.meta.requiresAuth) {
    // 检查登录状态和token过期
    if (!authStore.isLoggedIn || authStore.checkTokenExpiration()) {
      next('/login')
    } else {
      next()
    }
  } else if (to.meta.requiresGuest && authStore.isLoggedIn && !authStore.isTokenExpired()) {
    next('/dashboard')
  } else {
    next()
  }
})

export default router
