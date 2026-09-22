<script setup lang="ts">
import { CreditCard, OfficeBuilding, Setting, UserFilled } from '@element-plus/icons-vue'
import { computed } from 'vue'
import { useRouter } from 'vue-router'
import PageTitle from '@/components/PageTitle.vue'
import { useUserStore } from '@/stores/user'

defineProps<{ active: 'departments' | 'payment-methods' | 'users' }>()
const router = useRouter()
const userStore = useUserStore()

const items = computed(() => [
  { key: 'departments', label: '部门管理', icon: OfficeBuilding, path: '/departments' },
  { key: 'payment-methods', label: '收款方式', icon: CreditCard, path: '/payment-methods' },
  { key: 'users', label: '用户管理', icon: UserFilled, path: '/users' },
].filter((item) => item.key !== 'users' || userStore.userInfo.role === '超级管理员'))
</script>

<template>
  <div class="page-container settings-page">
    <div class="breadcrumb">⌂ 首页　/　系统设置</div>
    <PageTitle title="系统设置" subtitle="系统参数与业务基础配置" :icon="Setting" />
    <div class="settings-workspace">
      <aside class="card settings-nav">
        <button
          v-for="item in items"
          :key="item.key"
          type="button"
          :class="{ active: item.key === active }"
          @click="item.path && router.push(item.path)"
        >
          <el-icon><component :is="item.icon" /></el-icon><span>{{ item.label }}</span>
        </button>
      </aside>
      <section class="card settings-content"><slot /></section>
    </div>
  </div>
</template>

<style scoped>
.breadcrumb { margin: -5px 0 22px; color: #72829a; font-size: 13px; }
.settings-workspace { display: grid; grid-template-columns: 220px minmax(0, 1fr); gap: 14px; min-height: 650px; }
.settings-nav { padding: 15px 10px; }
.settings-nav button { position: relative; display: flex; align-items: center; gap: 13px; width: 100%; height: 54px; padding: 0 18px; border: 0; border-radius: 7px; color: #31425d; font-size: 15px; text-align: left; background: transparent; cursor: pointer; }
.settings-nav button .el-icon { font-size: 20px; }
.settings-nav button:hover { color: var(--primary); background: #effaf6; }
.settings-nav button.active { color: var(--primary); font-weight: 600; background: linear-gradient(90deg, #ebfaf4, #dff8ee); }
.settings-nav button.active::before { content: ''; position: absolute; left: 0; width: 3px; height: 36px; border-radius: 0 3px 3px 0; background: var(--primary); }
.settings-content { min-width: 0; padding: 20px; }
@media (max-width: 900px) { .settings-workspace { grid-template-columns: 1fr; }.settings-nav { display: flex; gap: 8px; overflow-x: auto; }.settings-nav button { flex: 0 0 auto; width: auto; padding: 0 15px; }.settings-nav button.active::before { inset: auto 12px 0; width: auto; height: 3px; } }
@media (max-width: 640px) { .settings-content { padding: 14px; } }
</style>
