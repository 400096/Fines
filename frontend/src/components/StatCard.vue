<script setup lang="ts">
// 首页统计卡片，统一展示指标、单位及辅助趋势信息。
import type { Component } from 'vue'
const props = withDefaults(defineProps<{ label: string; value: string; unit: string; icon: Component; tone?: 'green' | 'blue' | 'orange'; trend?: string; clickable?: boolean }>(), { tone: 'green', trend: '+12%', clickable: false })
const emit = defineEmits<{ click: [] }>()
function activate() { if (props.clickable) emit('click') }
</script>

<template>
  <article class="stat-card card" :class="{ 'is-clickable': clickable }" :role="clickable ? 'button' : undefined" :tabindex="clickable ? 0 : undefined" @click="activate" @keydown.enter="activate" @keydown.space.prevent="activate">
    <div class="stat-card__icon" :class="`is-${tone}`"><component :is="icon" /></div>
    <div class="stat-card__body"><span>{{ label }}</span><div><strong>{{ value }}</strong><small>{{ unit }}</small></div><p>较上月 <em>{{ trend }}</em> ↗</p></div>
    <div class="stat-card__bars"><i /><i /><i /><i /></div>
  </article>
</template>

<style scoped>
.stat-card { position: relative; display: flex; align-items: center; gap: 16px; min-height: 124px; padding: 20px; overflow: hidden; }
.stat-card.is-clickable { cursor: pointer; transition: transform .18s, box-shadow .18s; }.stat-card.is-clickable:hover { transform: translateY(-2px); box-shadow: 0 8px 24px rgba(20, 91, 75, .12); }
.stat-card__icon { display: grid; place-items: center; width: 58px; height: 58px; border-radius: 16px; font-size: 29px; }
.is-green { color: #00975e; background: #def8ed; }.is-blue { color: #2d7ff0; background: #e4efff; }.is-orange { color: #f68c22; background: #fff0df; }
.stat-card__body { position: relative; z-index: 1; display: grid; gap: 5px; }.stat-card__body > span { color: var(--text-secondary); font-size: 14px; }
.stat-card strong { margin-right: 8px; color: var(--text-primary); font-size: 28px; line-height: 1; }.stat-card small { color: var(--text-secondary); }.stat-card p { margin: 0; color: var(--text-muted); font-size: 13px; }.stat-card em { margin-left: 4px; color: #f04444; font-style: normal; }
.stat-card__bars { position: absolute; right: 20px; bottom: 23px; display: flex; align-items: end; gap: 5px; opacity: .18; }.stat-card__bars i { width: 8px; height: 20px; border-radius: 2px 2px 0 0; background: #09a96b; }.stat-card__bars i:nth-child(2) { height: 31px; }.stat-card__bars i:nth-child(3) { height: 43px; }.stat-card__bars i:nth-child(4) { height: 35px; }
@media (max-width: 720px) { .stat-card__bars { display: none; } }
</style>
