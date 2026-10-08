<script setup lang="ts">
// 只读考核记录详情：打开时加载，之后定时读取最新的字段变更日志。
import { onUnmounted, ref, watch } from 'vue'
import { ElMessage } from 'element-plus'
import { assessmentApi } from '@/api/services'
import type { AssessmentRecordChange, AssessmentRecordDetail } from '@/api/types'
import { formatDateTime } from '@/utils/format'

const props = defineProps<{ modelValue: boolean; recordId: number | null }>()
const emit = defineEmits<{ 'update:modelValue': [value: boolean] }>()
const detail = ref<AssessmentRecordDetail | null>(null)
const loading = ref(false)
const refreshing = ref(false)
const errorMessage = ref('')
let timer: ReturnType<typeof setInterval> | undefined
let requestVersion = 0

const actionText: Record<AssessmentRecordChange['action'], string> = {
  Created: '新增记录',
  Updated: '编辑记录',
  Imported: 'Excel 导入',
  Deleted: '删除记录',
}

function closeTimer() {
  if (timer) clearInterval(timer)
  timer = undefined
}

async function refresh(silent = false) {
  if (!props.modelValue || props.recordId === null || refreshing.value) return
  const version = ++requestVersion
  const id = props.recordId
  refreshing.value = true
  if (!silent) loading.value = true
  try {
    const result = await assessmentApi.details(id)
    if (version === requestVersion && props.modelValue && props.recordId === id) {
      detail.value = result
      errorMessage.value = ''
    }
  } catch {
    if (version === requestVersion && props.modelValue) {
      errorMessage.value = '详情暂时无法更新，请稍后重试'
      if (!silent) ElMessage.error(errorMessage.value)
    }
  } finally {
    if (version === requestVersion) {
      loading.value = false
      refreshing.value = false
    }
  }
}

watch(() => [props.modelValue, props.recordId] as const, ([visible, id]) => {
  closeTimer()
  requestVersion += 1
  refreshing.value = false
  if (!visible || id === null) {
    detail.value = null
    errorMessage.value = ''
    loading.value = false
    return
  }
  detail.value = null
  void refresh()
  timer = setInterval(() => { void refresh(true) }, 10_000)
}, { immediate: true })

onUnmounted(closeTimer)

function formatDate(record: AssessmentRecordDetail['record']) {
  return record.assessmentEndDate
    ? `${record.assessmentDate} 至 ${record.assessmentEndDate}`
    : record.assessmentDate
}

function display(value: string | null | undefined) {
  return value?.trim() || '—'
}
</script>

<template>
  <el-dialog :model-value="modelValue" title="考核记录详情" width="min(820px, 96vw)" destroy-on-close @update:model-value="emit('update:modelValue', $event)">
    <div v-loading="loading" class="record-detail">
      <template v-if="detail">
        <div class="record-detail__head">
          <el-tag v-if="detail.isDeleted" type="danger">已删除</el-tag>
          <span>记录编号 #{{ detail.record.id }}</span>
          <el-button size="small" :loading="refreshing" @click="refresh()">刷新变动</el-button>
        </div>
        <el-descriptions :column="2" border size="small" class="record-detail__fields">
          <el-descriptions-item label="单据日期">{{ formatDate(detail.record) }}</el-descriptions-item>
          <el-descriptions-item label="录入日期">{{ detail.record.detailDate }}</el-descriptions-item>
          <el-descriptions-item label="部门">{{ detail.record.departmentName }}</el-descriptions-item>
          <el-descriptions-item label="姓名">{{ detail.record.personName }}</el-descriptions-item>
          <el-descriptions-item label="金额（元）">{{ detail.record.amountDisplay }}</el-descriptions-item>
          <el-descriptions-item label="状态">{{ detail.record.status === 'Completed' ? '已完成' : '待处理' }}</el-descriptions-item>
          <el-descriptions-item label="收款方式">{{ display(detail.record.paymentMethodName) }}</el-descriptions-item>
          <el-descriptions-item label="创建时间">{{ formatDateTime(detail.record.createdAt) }}</el-descriptions-item>
          <el-descriptions-item label="事由" :span="2">{{ detail.record.reason }}</el-descriptions-item>
          <el-descriptions-item label="备注" :span="2">{{ display(detail.record.remark) }}</el-descriptions-item>
        </el-descriptions>
        <div class="record-detail__history-title"><h3>数据变动时间线</h3><span>每 10 秒自动更新</span></div>
        <el-alert v-if="detail.hasEarlierUntrackedChanges" title="这条记录创建于变更日志启用前，早期修改无法追溯。" type="info" :closable="false" class="record-detail__notice" />
        <el-alert v-if="errorMessage" :title="errorMessage" type="warning" :closable="false" class="record-detail__notice" />
        <el-timeline v-if="detail.timeline.length" class="record-detail__timeline">
          <el-timeline-item v-for="entry in detail.timeline" :key="entry.id" :timestamp="formatDateTime(entry.occurredAt)" placement="top" :type="entry.action === 'Deleted' ? 'danger' : entry.action === 'Updated' ? 'primary' : 'success'">
            <div class="history-entry">
              <strong>{{ actionText[entry.action] }}</strong><span>操作人：{{ entry.actorName }}</span>
              <div v-for="change in entry.changes" :key="change.field" class="history-change">
                <b>{{ change.field }}</b>
                <template v-if="entry.action === 'Updated'"><span>{{ display(change.before) }}</span><span aria-hidden="true">→</span></template>
                <span>{{ display(change.after) }}</span>
              </div>
              <p v-if="entry.action === 'Deleted'">记录已被逻辑删除</p>
            </div>
          </el-timeline-item>
        </el-timeline>
        <el-empty v-else description="暂无可追溯的变更记录" :image-size="72" />
      </template>
      <el-empty v-else-if="!loading" description="详情暂时不可用" :image-size="72" />
    </div>
    <template #footer><el-button @click="emit('update:modelValue', false)">关闭</el-button></template>
  </el-dialog>
</template>

<style scoped>
.record-detail { min-height: 180px; max-height: 70vh; overflow-y: auto; padding-right: 4px; }
.record-detail__head { display: flex; align-items: center; gap: 10px; margin-bottom: 12px; color: var(--text-muted); font-size: 13px; }
.record-detail__head .el-button { margin-left: auto; }
.record-detail__fields :deep(.el-descriptions__content) { overflow-wrap: anywhere; }
.record-detail__history-title { display: flex; align-items: center; justify-content: space-between; margin: 20px 0 14px; }
.record-detail__history-title h3 { margin: 0; font-size: 16px; color: var(--text-primary); }
.record-detail__history-title span { color: var(--text-muted); font-size: 12px; }
.record-detail__notice { margin-bottom: 14px; }
.record-detail__timeline { padding-left: 8px; }
.history-entry { padding: 2px 0 12px; }
.history-entry > strong { margin-right: 12px; color: var(--text-primary); }
.history-entry > span, .history-entry > p { color: var(--text-muted); font-size: 13px; }
.history-change { display: flex; flex-wrap: wrap; gap: 8px; margin-top: 7px; font-size: 13px; overflow-wrap: anywhere; }
.history-change b { color: var(--text-secondary); }
.history-change span:last-child { color: var(--primary); }
@media (max-width: 650px) { .record-detail__fields :deep(.el-descriptions__body) { overflow-x: auto; } }
</style>
