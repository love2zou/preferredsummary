import { createRouter, createWebHistory } from 'vue-router'
import { useAuthStore } from '@/stores/authStore'

const router = createRouter({
  history: createWebHistory(),
  routes: [
    {
      path: '/',
      redirect: '/login'
    },
    {
      path: '/login',
      name: 'login',
      component: () => import('@/views/Login.vue'),
      meta: { showTabBar: false }
    },
    {
      path: '/coach',
      name: 'coach-dashboard',
      component: () => import('@/views/CoachDashboard.vue'),
      meta: { showTabBar: false, requiresAuth: true, role: 'coach' }
    },
    {
      path: '/home',
      name: 'home',
      component: () => import('@/views/Home.vue'),
      meta: { showTabBar: true, requiresAuth: true, role: 'member' }
    },
    {
      path: '/trainers',
      name: 'trainers',
      component: () => import('@/views/TrainerList.vue'),
      meta: { showTabBar: true, requiresAuth: true, role: 'member' }
    },
    {
      path: '/trainer/:id',
      name: 'trainer-detail',
      component: () => import('@/views/TrainerDetail.vue'),
      props: true,
      meta: { showTabBar: false, requiresAuth: true, role: 'member' }
    },
    {
      path: '/booking/:id',
      name: 'booking',
      component: () => import('@/views/BookingCoach.vue'),
      props: true,
      meta: { showTabBar: false, requiresAuth: true, role: 'member' }
    },
    {
      path: '/reservations',
      name: 'reservations',
      component: () => import('@/views/MyReservations.vue'),
      meta: { showTabBar: true, requiresAuth: true, role: 'member' }
    },
    {
      path: '/training',
      name: 'training',
      component: () => import('@/views/TrainingHub.vue'),
      meta: { showTabBar: true, requiresAuth: true, role: 'member' }
    },
    {
      path: '/reservation-flow',
      name: 'reservation-flow',
      component: () => import('@/views/ReservationFlow.vue'),
      meta: { showTabBar: false, requiresAuth: true, role: 'member' }
    },
    {
      path: '/commerce',
      name: 'commerce-center',
      component: () => import('@/views/CommerceCenter.vue'),
      meta: { showTabBar: true, requiresAuth: true, role: 'member' }
    },
    {
      path: '/member-center',
      name: 'member-center',
      component: () => import('@/views/MemberCenter.vue'),
      meta: { showTabBar: true, requiresAuth: true, role: 'member' }
    },
    {
      path: '/experience-map',
      name: 'experience-map',
      component: () => import('@/views/ExperienceMap.vue'),
      meta: { showTabBar: false, requiresAuth: true, role: 'member' }
    },
    {
      path: '/profile',
      name: 'profile',
      component: () => import('@/views/Profile.vue'),
      meta: { showTabBar: true, requiresAuth: true, role: 'member' }
    }
  ]
})

router.beforeEach((to) => {
  const authStore = useAuthStore()

  if (to.name === 'login' && authStore.isLoggedIn) {
    return authStore.isCoach ? { name: 'coach-dashboard' } : { name: 'home' }
  }

  if (to.meta.requiresAuth && !authStore.isLoggedIn) {
    return { name: 'login' }
  }

  if (to.meta.role === 'coach' && !authStore.isCoach) {
    return { name: 'home' }
  }

  if (to.meta.role === 'member' && !authStore.isMember) {
    return { name: 'coach-dashboard' }
  }

  return true
})

export default router
