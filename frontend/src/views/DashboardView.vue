<script setup lang="ts">
import type { EChartsOption } from 'echarts'
import { Coin, Document, List, Timer } from '@element-plus/icons-vue'
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { dashboardApi } from '@/api/services'
import type { DashboardResponse } from '@/api/types'
import BaseChart from '@/components/BaseChart.vue'
import StatCard from '@/components/StatCard.vue'
import { formatMoney, todayString } from '@/utils/format'
import { setPendingImportFile } from '@/utils/importTransfer'

const router = useRouter()
const importFileInput = ref<HTMLInputElement>()
const loading = ref(false)
const dashboard = ref<DashboardResponse>({
  summary: { todayCount: 0, monthAmount: 0, monthCount: 0, pendingCount: 0, personCount: 0, departmentCount: 0, monthGrowthPercent: 0 },
  trend: [], distribution: [], recentRecords: [],
})

const lineOption = computed<EChartsOption>(() => ({
  grid: { left: 42, right: 20, top: 24, bottom: 30 },
  tooltip: { trigger: 'axis' },
  xAxis: {
    type: 'category', boundaryGap: false,
    data: dashboard.value.trend.map((item) => item.date.slice(5).replace('-', '/')),
    axisLine: { lineStyle: { color: '#dce8e5' } },
  },
  yAxis: { type: 'value', splitLine: { lineStyle: { color: '#edf3f2' } } },
  series: [{
    type: 'line', smooth: true,
    data: dashboard.value.trend.map((item) => item.amount),
    symbolSize: 8,
    itemStyle: { color: '#079960' },
    lineStyle: { width: 3, color: '#079960' },
    areaStyle: { color: { type: 'linear', x: 0, y: 0, x2: 0, y2: 1, colorStops: [{ offset: 0, color: 'rgba(24,184,118,.34)' }, { offset: 1, color: 'rgba(24,184,118,.02)' }] } },
  }],
}))
const pieOption = computed<EChartsOption>(() => ({
  tooltip: { trigger: 'item' },
  legend: { orient: 'vertical', right: 5, top: 'center', itemWidth: 9, itemHeight: 9, textStyle: { color: '#56667d' } },
  series: [{
    type: 'pie', radius: ['45%', '70%'], center: ['35%', '52%'], label: { show: false },
    data: dashboard.value.distribution.map((item) => ({ value: item.amount, name: `${item.name}（${item.count}笔）` })),
    color: ['#23ad6c', '#398bf2', '#42b9dc', '#f5ae37', '#a9b8c4'],
  }],
}))
const growthText = computed(() => {
  const value = dashboard.value.summary.monthGrowthPercent
  return `${value > 0 ? '+' : ''}${value}%`
})

async function loadDashboard() {
  loading.value = true
  try { dashboard.value = await dashboardApi.get() }
  finally { loading.value = false }
}

function openTodayRecords() {
  void router.push({ path: '/fine-records', query: { date: todayString() } })
}

function openPendingRecords() {
  void router.push({ path: '/fine-records', query: { status: 'Pending' } })
}

function openCreateRecord() {
  void router.push({ path: '/fine-records', query: { action: 'create' } })
}

function chooseImportFile() {
  importFileInput.value?.click()
}

function importFromDashboard(event: Event) {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  input.value = ''
  if (!file) return
  setPendingImportFile(file)
  void router.push({ path: '/fine-records', query: { action: 'import' } })
}

onMounted(loadDashboard)
</script>

<template>
  <div v-loading="loading" class="dashboard page-container">
    <section class="stats-grid">
      <StatCard label="今日记录" :value="String(dashboard.summary.todayCount)" unit="笔" :icon="Document" trend="今日" clickable @click="openTodayRecords" /><StatCard label="本月金额" :value="formatMoney(dashboard.summary.monthAmount)" unit="元" :icon="Coin" :trend="growthText" /><StatCard label="本月笔数" :value="String(dashboard.summary.monthCount)" unit="笔" :icon="List" tone="blue" trend="本月" /><StatCard label="待整理" :value="String(dashboard.summary.pendingCount)" unit="笔" :icon="Timer" tone="orange" trend="请及时处理" clickable @click="openPendingRecords" />
    </section>
    <section class="charts-grid">
      <article class="card panel"><div class="panel__title"><strong>▥　近7天金额趋势</strong><el-tag type="success" effect="plain">近7天</el-tag></div><BaseChart :option="lineOption" /></article>
      <article class="card panel"><div class="panel__title"><strong>◕　考核事由占比</strong><el-link type="info" underline="never" @click="router.push('/fine-records')">查看详情 ›</el-link></div><BaseChart :option="pieOption" /></article>
    </section>
    <section class="lower-grid">
      <article class="card summary"><div><span>本月考核总额（元）</span><strong>{{ formatMoney(dashboard.summary.monthAmount) }}</strong></div><div><span>考核笔数</span><strong>{{ dashboard.summary.monthCount }}</strong></div><div><span>涉及人员（人）</span><strong>{{ dashboard.summary.personCount }}</strong></div><div><span>涉及部门（个）</span><strong>{{ dashboard.summary.departmentCount }}</strong></div><em>{{ growthText }}<small>较上月金额变化</small></em></article>
      <article class="card quick"><div class="panel__title"><strong>⚡　快捷操作</strong></div><div><el-button type="primary" @click="openCreateRecord">＋ 新增记录</el-button><el-button plain @click="chooseImportFile">⇧ 导入 Excel</el-button><el-button plain @click="router.push('/fine-records')">⇩ 导出记录</el-button><input ref="importFileInput" class="file-input" type="file" accept=".xls,.xlsx,.xlsm" @change="importFromDashboard" /></div></article>
    </section>
    <article class="card records"><div class="panel__title"><strong>▤　最近记录</strong><el-link type="info" underline="never" @click="router.push('/fine-records')">查看更多 ›</el-link></div><el-table :data="dashboard.recentRecords" stripe><el-table-column prop="assessmentDate" label="日期" /><el-table-column prop="departmentName" label="部门" /><el-table-column prop="personName" label="姓名" /><el-table-column prop="amount" label="金额（元）" min-width="145"><template #default="{ row }">{{ row.amountDisplay }}</template></el-table-column><el-table-column prop="reason" label="事由" min-width="210" /><el-table-column label="状态" min-width="110"><template #default="{ row }"><el-tag :type="row.status === 'Completed' ? 'success' : 'warning'" round>{{ row.status === 'Completed' ? (row.paymentMethodName || '收款方式未设置') : '待处理' }}</el-tag></template></el-table-column><el-table-column label="操作"><template #default><el-link type="primary" underline="never" @click="router.push('/fine-records')">查看</el-link></template></el-table-column></el-table></article>
  </div>
</template>

<style scoped>
.stats-grid { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: 14px; }.charts-grid { display: grid; grid-template-columns: 1.55fr 1fr; gap: 14px; margin-top: 14px; }.panel { min-height: 348px; padding: 18px; }.panel__title { display: flex; align-items: center; justify-content: space-between; min-height: 32px; color: var(--text-primary); font-size: 16px; }.lower-grid { display: grid; grid-template-columns: 1.35fr 1fr; gap: 14px; margin-top: 14px; }.summary { display: flex; align-items: center; justify-content: space-around; min-height: 130px; padding: 20px; }.summary > div { display: grid; gap: 10px; padding: 0 22px; border-right: 1px solid var(--border-color); }.summary span { color: var(--text-muted); font-size: 13px; }.summary strong { font-size: 24px; }.summary em { display: grid; gap: 8px; color: var(--primary); font-size: 22px; font-style: normal; }.summary small { color: var(--text-muted); font-size: 12px; font-weight: 400; }.quick { padding: 18px; }.quick > div:last-child { display: flex; margin-top: 12px; }.quick .el-button { flex: 1; height: 48px; }.file-input { display: none; }.records { margin-top: 14px; padding: 16px; }.records .panel__title { margin-bottom: 10px; }
@media (max-width: 1200px) { .stats-grid { grid-template-columns: repeat(2, 1fr); }.charts-grid, .lower-grid { grid-template-columns: 1fr; } }
@media (max-width: 680px) { .stats-grid { grid-template-columns: 1fr; }.summary { align-items: stretch; flex-direction: column; gap: 16px; }.summary > div { border: 0; }.quick > div:last-child { flex-direction: column; gap: 8px; }.quick .el-button { margin: 0; } }
</style>
