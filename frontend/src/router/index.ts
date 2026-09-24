import { createRouter, createWebHashHistory } from 'vue-router'
import { getStoredToken } from '@/api/http'

// 使用 Hash 路由，部署到 IIS 静态站点时刷新子页面不依赖 URL Rewrite。
const router = createRouter({
  history: createWebHashHistory(import.meta.env.BASE_URL),
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

// 全局登录守卫：未登录页面统一跳转，并保留原目标地址供登录后返回。
router.beforeEach((to) => {
  const token = getStoredToken()
  if (to.path !== '/login' && !token) {
    return { path: '/login', query: { redirect: to.fullPath } }
  }
  if (to.path === '/login' && token) return '/dashboard'
})

// 根据当前路由同步浏览器标题。
router.afterEach((to) => {
  document.title = `${String(to.meta.title ?? '首页')} - 考核管理系统`
})

export default router
