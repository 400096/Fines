import { createRouter, createWebHistory } from 'vue-router'
import { getStoredToken } from '@/api/http'

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    { path: '/', redirect: '/dashboard' },
    {
      path: '/login',
      name: 'login',
      component: () => import('@/views/LoginView.vue'),
      meta: { title: '登录' },
    },
    {
      path: '/',
      component: () => import('@/layouts/BasicLayout.vue'),
      children: [
        {
          path: 'dashboard',
          name: 'dashboard',
          component: () => import('@/views/DashboardView.vue'),
          meta: { title: '首页' },
        },
        {
          path: 'fine-records',
          name: 'fine-records',
          component: () => import('@/views/FineRecordsView.vue'),
          meta: { title: '考核记录' },
        },
        {
          path: 'departments',
          name: 'departments',
          component: () => import('@/views/DepartmentsView.vue'),
          meta: { title: '部门管理' },
        },
        {
          path: 'payment-methods',
          name: 'payment-methods',
          component: () => import('@/views/PaymentMethodsView.vue'),
          meta: { title: '收款方式' },
        },
        {
          path: 'users',
          name: 'users',
          component: () => import('@/views/UsersView.vue'),
          meta: { title: '用户管理' },
        },
      ],
    },
    { path: '/:pathMatch(.*)*', redirect: '/dashboard' },
  ],
})

router.beforeEach((to) => {
  const token = getStoredToken()
  if (to.path !== '/login' && !token) {
    return { path: '/login', query: { redirect: to.fullPath } }
  }
  if (to.path === '/login' && token) return '/dashboard'
})

router.afterEach((to) => {
  document.title = `${String(to.meta.title ?? '首页')} - 考核管理系统`
})

export default router
