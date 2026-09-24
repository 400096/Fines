<script setup lang="ts">
// 系统品牌标识；compact 模式仅展示图标。
withDefaults(defineProps<{ compact?: boolean; light?: boolean }>(), {
  compact: false,
  light: false,
})
</script>

<template>
  <div class="app-logo" :class="{ compact, light }">
    <div class="app-logo__icon"><span>¥</span></div>
    <div v-if="!compact" class="app-logo__text">
      <strong>考核管理系统（财务简版）</strong>
      <span>规范管理 · 清晰记录 · 高效对账</span>
    </div>
  </div>
</template>

<style scoped>
.app-logo { display: flex; align-items: center; gap: 14px; min-width: 0; }
.app-logo__icon { position: relative; display: grid; place-items: center; flex: 0 0 auto; width: 46px; height: 46px; border-radius: 12px; color: #fff; font-size: 25px; font-weight: 800; background: linear-gradient(145deg, #18b876, #008b58); box-shadow: 0 8px 18px rgba(0, 153, 96, .2); }
.app-logo__icon::before { content: ''; position: absolute; inset: 8px 10px; border-radius: 3px; background: rgba(255,255,255,.92); }
.app-logo__icon span { position: relative; color: #07945d; }
.app-logo__text { display: grid; gap: 3px; min-width: 0; white-space: nowrap; }
.app-logo__text strong { color: #111c37; font-size: 20px; line-height: 1.2; }
.app-logo__text span { color: #8190a5; font-size: 12px; }
.app-logo.light .app-logo__text strong, .app-logo.light .app-logo__text span { color: #fff; }
.app-logo.compact .app-logo__icon { width: 42px; height: 42px; }
</style>
