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
    // 新增：我的约课
    {
      path: '/booking',
      name: 'Booking',
      component: () => import('@/views/member/Booking.vue'),
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
    // 在路由表中新增“新建约课”页面的路由
    {
      path: '/booking/create',
      name: 'BookingCreate',
      component: () => import('@/views/BookingCreate.vue'),
      meta: { requiresAuth: true, title: '新建约课' }
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