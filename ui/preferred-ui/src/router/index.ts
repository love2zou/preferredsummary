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

    , {
      path: '/algorithm-planning',
      name: 'AlgorithmPlanning',
      component: () => import('@/views/AlgorithmPlanning.vue')
    }
    , {
      path: '/fire-analysis',
      name: 'FireAnalysis',
      component: () => import('@/views/FireAnalysis.vue')
    }
    , {
      path: '/wavefile-analysis',
      name: 'WaveFileAnalysis',
      component: () => import('@/views/WaveFileAnalysis.vue')
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
