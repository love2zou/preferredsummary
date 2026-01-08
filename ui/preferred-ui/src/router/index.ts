import Home from '@/views/Home.vue'
import { createRouter, createWebHistory } from 'vue-router'

const router = createRouter({
  history: createWebHistory(),
  routes: [
    {
      path: '/',
      name: 'Home',
      component: Home
    },
    {
      path: '/zwav-analysis',
      name: 'ZwavAnalysis',
      component: () => import('@/views/zwavpages/ZwavAnalysis.vue')
    },
    {
      path: '/zwav-analysis/viewer/:guid',
      name: 'ZwavOnlineViewer',
      component: () => import('@/views/zwavpages/ZwavOnlineViewer.vue'),
    }
    // 如果需要保留登录功能但不作为主要入口，可以保留这些路由
    // {
    //   path: '/login',
    //   name: 'Login',
    //   component: () => import('@/views/Login.vue')
    // },
    // {
    //   path: '/register',
    //   name: 'Register',
    //   component: () => import('@/views/Register.vue')
    // }
  ]
})

export default router
