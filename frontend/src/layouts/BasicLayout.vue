<script setup lang="ts">
import { Bell, Document, Fold, HomeFilled, Search, Setting } from '@element-plus/icons-vue'
import { computed, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import AppLogo from '@/components/AppLogo.vue'
import { useUserStore } from '@/stores/user'

const route = useRoute()
const router = useRouter()
const userStore = useUserStore()
const collapsed = ref(false)
const activePath = computed(() => ['/departments', '/payment-methods', '/users'].includes(route.path) ? '/departments' : route.path)
const menus = [
  { path: '/dashboard', label: '首页', icon: HomeFilled },
  { path: '/fine-records', label: '考核记录', icon: Document },
  { path: '/departments', label: '系统设置', icon: Setting },
]
function logout() { userStore.logout(); router.push('/login') }
</script>

<template>
  <el-container class="shell">
    <el-aside class="sidebar" :width="collapsed ? '76px' : '200px'">
      <div class="sidebar__brand"><AppLogo compact /></div>
      <el-menu :default-active="activePath" router :collapse="collapsed" class="sidebar__menu">
        <el-menu-item v-for="item in menus" :key="item.path" :index="item.path"><el-icon><component :is="item.icon" /></el-icon><template #title>{{ item.label }}</template></el-menu-item>
      </el-menu>
      <div v-if="!collapsed" class="sidebar__slogan"><Document /><span>让每一笔记录<br />都有据可查</span></div>
      <button class="sidebar__toggle" type="button" aria-label="切换侧栏" @click="collapsed = !collapsed"><Fold /></button>
    </el-aside>
    <el-container class="workbench" :class="{ 'is-collapsed': collapsed }">
      <el-header class="topbar">
        <AppLogo class="topbar__brand" />
        <el-input class="topbar__search" placeholder="搜索姓名、部门或事由..." :prefix-icon="Search" clearable />
        <div class="topbar__actions">
          <el-badge is-dot><el-button circle text :icon="Bell" aria-label="通知" /></el-badge>
          <el-dropdown trigger="click">
            <div class="user-panel"><el-avatar :size="42">{{ userStore.userInfo.name.slice(0, 1) }}</el-avatar><div><strong>{{ userStore.userInfo.role }}</strong><span>您好，{{ userStore.userInfo.name }}</span></div></div>
            <template #dropdown><el-dropdown-menu><el-dropdown-item @click="logout">退出登录</el-dropdown-item></el-dropdown-menu></template>
          </el-dropdown>
        </div>
      </el-header>
      <el-main class="main-content"><RouterView /></el-main>
    </el-container>
  </el-container>
</template>

<style scoped>
.shell { min-height: 100vh; }.sidebar { position: fixed; inset: 0 auto 0 0; z-index: 20; display: flex; flex-direction: column; overflow: hidden; color: white; background: linear-gradient(180deg, #08715b, #004b43); transition: width .25s; }.sidebar__brand { display: flex; align-items: center; justify-content: center; height: 80px; background: rgba(12, 178, 121, .18); }.sidebar__menu { flex: 1; padding: 12px 8px; border: 0; background: transparent; }.sidebar__menu :deep(.el-menu-item) { height: 54px; margin: 4px 0; border-radius: 9px; color: rgba(255,255,255,.85); font-size: 15px; }.sidebar__menu :deep(.el-menu-item .el-icon) { font-size: 20px; }.sidebar__menu :deep(.el-menu-item:hover), .sidebar__menu :deep(.el-menu-item.is-active) { color: #fff; background: linear-gradient(90deg, rgba(30,193,133,.88), rgba(39,174,128,.72)); }.sidebar__menu :deep(.el-menu-item.is-disabled) { color: rgba(255,255,255,.78); opacity: 1; cursor: default; }.sidebar__slogan { display: grid; justify-items: center; gap: 14px; padding: 24px 0 38px; color: rgba(255,255,255,.75); text-align: center; font-size: 13px; line-height: 1.8; }.sidebar__slogan svg { width: 56px; opacity: .16; }.sidebar__toggle { position: absolute; right: 10px; bottom: 12px; width: 28px; border: 0; color: rgba(255,255,255,.6); background: transparent; cursor: pointer; }
.workbench { min-width: 0; margin-left: 200px; transition: margin-left .25s; }.workbench.is-collapsed { margin-left: 76px; }.topbar { position: sticky; top: 0; z-index: 10; display: flex; align-items: center; gap: 26px; height: 80px; padding: 0 28px; border-bottom: 1px solid var(--border-color); background: rgba(255,255,255,.94); backdrop-filter: blur(12px); }.topbar__brand { flex: 0 0 auto; }.topbar__search { width: min(420px, 34vw); margin: 0 auto; }.topbar__actions, .user-panel { display: flex; align-items: center; gap: 18px; }.user-panel { gap: 10px; cursor: pointer; }.user-panel > div { display: grid; gap: 2px; white-space: nowrap; }.user-panel strong { color: var(--text-primary); font-size: 13px; }.user-panel span { color: var(--text-muted); font-size: 12px; }.main-content { min-width: 0; padding: 24px; overflow: visible; background: var(--page-bg); }
@media (max-width: 900px) { .topbar__brand { display: none; }.topbar__search { margin-left: 0; width: 100%; }.user-panel > div { display: none; } }
@media (max-width: 640px) { .sidebar { width: 68px !important; }.sidebar__slogan { display: none; }.workbench, .workbench.is-collapsed { margin-left: 68px; }.topbar { padding: 0 14px; }.main-content { padding: 14px; } }
</style>
