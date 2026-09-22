<script setup lang="ts">
import type { Component } from 'vue'
defineProps<{ title: string; subtitle: string; icon: Component }>()
</script>

<template>
  <div class="page-title">
    <div class="page-title__icon"><component :is="icon" /></div>
    <div><h1>{{ title }}</h1><p>{{ subtitle }}</p></div>
  </div>
</template>

<style scoped>
.page-title { display: flex; align-items: center; gap: 14px; margin-bottom: 20px; }
.page-title__icon { display: grid; place-items: center; width: 48px; height: 48px; border-radius: 13px; color: white; font-size: 24px; background: linear-gradient(145deg, #17b676, #008f59); box-shadow: 0 8px 20px rgba(0,145,91,.18); }
h1 { margin: 0 0 4px; color: var(--text-primary); font-size: 25px; line-height: 1.2; }
p { margin: 0; color: var(--text-muted); font-size: 14px; }
</style>
