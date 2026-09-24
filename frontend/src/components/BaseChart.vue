<script setup lang="ts">
// ECharts 通用封装：监听容器尺寸和配置变化，统一处理初始化与销毁。
import type { EChartsOption } from 'echarts'
import { BarChart, LineChart, PieChart } from 'echarts/charts'
import { GridComponent, LegendComponent, TooltipComponent } from 'echarts/components'
import { init, use, type EChartsType } from 'echarts/core'
import { CanvasRenderer } from 'echarts/renderers'
import { onBeforeUnmount, onMounted, ref, watch } from 'vue'

use([BarChart, LineChart, PieChart, GridComponent, LegendComponent, TooltipComponent, CanvasRenderer])

const props = defineProps<{ option: EChartsOption }>()
const chartEl = ref<HTMLElement>()
let chart: EChartsType | undefined
let observer: ResizeObserver | undefined
let resizeFrame = 0
let lastWidth = 0
let lastHeight = 0

onMounted(() => {
  if (!chartEl.value) return
  chart = init(chartEl.value)
  chart.setOption(props.option)
  observer = new ResizeObserver(([entry]) => {
    if (!entry) return
    const { width, height } = entry.contentRect
    if (width === lastWidth && height === lastHeight) return
    lastWidth = width
    lastHeight = height
    cancelAnimationFrame(resizeFrame)
    resizeFrame = requestAnimationFrame(() => chart?.resize({ width, height }))
  })
  observer.observe(chartEl.value)
})

watch(() => props.option, (option) => chart?.setOption(option, true), { deep: true })
onBeforeUnmount(() => { observer?.disconnect(); cancelAnimationFrame(resizeFrame); chart?.dispose() })
</script>

<template><div ref="chartEl" class="base-chart" /></template>
<style scoped>
.base-chart { width: 100%; height: 270px; }
@media (max-width: 640px) { .base-chart { height: 240px; } }
</style>
