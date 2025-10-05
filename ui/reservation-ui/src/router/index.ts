// 路由配置（新增 BindCoach 路由）
import { useUserStore } from '@/stores/user'
import { createRouter, createWebHistory } from 'vue-router'

const router = createRouter({
  history: createWebHistory(),
  routes: [
    {
      path: '/',
      redirect: '/home'
    },
    {
      path: '/login',
      name: 'Login',
      component: () => import('@/views/Login.vue'),
      meta: { requiresAuth: false }
    },
    {
      path: '/register',
      name: 'Register',
      component: () => import('@/views/Register.vue'),
      meta: { requiresAuth: false }
    },
    {
      path: '/home',
      name: 'Home',
      component: () => import('@/views/Home.vue'),
      meta: { requiresAuth: true }
    },
    // 新增：个人信息
    {
      path: '/profile',
      name: 'Profile',
      component: () => import('@/views/member/Profile.vue'),
      meta: { requiresAuth: true }
    },
    // 新增：关于健身
    {
      path: '/fitness',
      name: 'Fitness',
      component: () => import('@/views/Fitness.vue'),
      meta: { requiresAuth: true }
    },
    // 新增：健身预约 - 创建
    {
      path: '/booking/create',
      name: 'CreateBooking',
      component: () => import('@/views/booking/CreateBooking.vue'),
      meta: { requiresAuth: true }
    },
    {
      path: '/booking',
      name: 'BookingList',
      component: () => import('@/views/booking/BookingList.vue'),
      meta: { requiresAuth: true }
    },
    // 移除：会员端绑定教练页面
    // 原：{
    //   path: '/bind-coach',
    //   name: 'BindCoach',
    //   component: () => import('@/views/booking/BindCoach.vue'),
    //   meta: { requiresAuth: true }
    // }
    {
      path: '/members',
      name: 'MyMembers',
      component: () => import('@/views/trainer/MyMembers.vue'),
      meta: { requiresAuth: true }
    },
    // 新增：添加会员页面
    {
      path: '/members/add',
      name: 'AddMembers',
      component: () => import('@/views/trainer/AddMembers.vue'),
      meta: { requiresAuth: true }
    },
    {
      path: '/notifications',
      name: 'Notifications',
      component: () => import('@/views/notifications/NotificationList.vue'),
      meta: { requiresAuth: true }
    }
  ]
})

// 全局守卫：未登录禁止进入需要认证的页面
router.beforeEach((to, from, next) => {
  const userStore = useUserStore()

  if (to.meta.requiresAuth && !userStore.isLoggedIn) {
    next('/login')
    return
  }
  if (!to.meta.requiresAuth && userStore.isLoggedIn && to.path === '/login') {
    next('/home')
    return
  }
  next()
})

export default router