<script setup lang="ts">
// 收款方式管理：维护考核完成时可选择的收款渠道。
import { CreditCard, Plus, Refresh, Search } from '@element-plus/icons-vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { onMounted, reactive, ref } from 'vue'
import { paymentMethodApi } from '@/api/services'
import type { PaymentMethod, SavePaymentMethod } from '@/api/types'
import SettingsWorkspace from '@/components/SettingsWorkspace.vue'
import { formatDateTime } from '@/utils/format'

const methods = ref<PaymentMethod[]>([])
const loading = ref(false)
const saving = ref(false)
const filters = reactive({ keyword: '', status: '' })
const dialogVisible = ref(false)
const isEditing = ref(false)
const form = reactive({ id: 0, name: '', sortOrder: 1, isEnabled: true, description: '' })

function statusValue() {
  return filters.status === '' ? undefined : filters.status === 'enabled'
}
async function loadMethods() {
  loading.value = true
  try {
    methods.value = await paymentMethodApi.list({
      keyword: filters.keyword.trim() || undefined,
      isEnabled: statusValue(),
    })
  } finally {
    loading.value = false
  }
}
function query() { void loadMethods() }
function reset() {
  Object.assign(filters, { keyword: '', status: '' })
  void loadMethods()
}
function openCreate() {
  isEditing.value = false
  const nextSort = Math.max(0, ...methods.value.map((item) => item.sortOrder)) + 1
  Object.assign(form, { id: 0, name: '', sortOrder: nextSort, isEnabled: true, description: '' })
  dialogVisible.value = true
}
function openEdit(row: PaymentMethod) {
  isEditing.value = true
  Object.assign(form, {
    id: row.id,
    name: row.name,
    sortOrder: row.sortOrder,
    isEnabled: row.isEnabled,
    description: row.description ?? '',
  })
  dialogVisible.value = true
}
async function save() {
  if (!form.name.trim()) {
    ElMessage.warning('请填写收款方式名称')
    return
  }
  const data: SavePaymentMethod = {
    name: form.name.trim(),
    sortOrder: form.sortOrder,
    isEnabled: form.isEnabled,
    description: form.description.trim() || null,
  }
  saving.value = true
  try {
    if (isEditing.value) await paymentMethodApi.update(form.id, data)
    else await paymentMethodApi.create(data)
    dialogVisible.value = false
    ElMessage.success(isEditing.value ? '收款方式已更新' : '收款方式已新增')
    await loadMethods()
  } finally {
    saving.value = false
  }
}
async function remove(row: PaymentMethod) {
  try {
    await ElMessageBox.confirm(`确定删除收款方式“${row.name}”吗？`, '删除确认', { type: 'warning' })
    await paymentMethodApi.remove(row.id)
    ElMessage.success('删除成功')
    await loadMethods()
  } catch (error) {
    if (error !== 'cancel' && error !== 'close') throw error
  }
}

onMounted(loadMethods)
</script>

<template>
  <SettingsWorkspace active="payment-methods">
    <div class="management-header">
      <h2 class="management-title"><el-icon><CreditCard /></el-icon>收款方式管理</h2>
      <div class="management-actions"><el-button :icon="Refresh" :loading="loading" @click="loadMethods">刷新</el-button></div>
    </div>
    <div class="management-filter">
      <div class="management-filter__field"><label>收款方式名称</label><el-input v-model="filters.keyword" placeholder="请输入收款方式名称" clearable @keyup.enter="query" /></div>
      <div class="management-filter__field"><label>状态</label><el-select v-model="filters.status" placeholder="全部" clearable><el-option label="启用" value="enabled" /><el-option label="停用" value="disabled" /></el-select></div>
      <div class="management-filter__buttons"><el-button type="primary" :icon="Search" @click="query">查询</el-button><el-button :icon="Refresh" @click="reset">重置</el-button><el-button type="primary" :icon="Plus" @click="openCreate">新增方式</el-button></div>
    </div>
    <div class="management-table-wrap">
      <el-table v-loading="loading" class="management-table" :data="methods" stripe border>
        <el-table-column type="selection" width="44" /><el-table-column prop="name" label="收款方式" min-width="130" /><el-table-column prop="sortOrder" label="排序" sortable min-width="95" /><el-table-column label="状态" sortable min-width="105"><template #default="{ row }"><el-tag class="status-tag" :type="row.isEnabled ? 'success' : 'info'" round>{{ row.isEnabled ? '启用' : '停用' }}</el-tag></template></el-table-column><el-table-column prop="description" label="说明" min-width="210" /><el-table-column label="更新时间" sortable min-width="170"><template #default="{ row }">{{ formatDateTime(row.updatedAt) }}</template></el-table-column><el-table-column label="操作" fixed="right" width="140"><template #default="{ row }"><el-button link type="primary" @click="openEdit(row)">编辑</el-button><el-button link type="danger" @click="remove(row)">删除</el-button></template></el-table-column>
      </el-table>
    </div>
    <div class="management-footer"><span>共 {{ methods.length }} 条记录</span></div>
    <el-dialog v-model="dialogVisible" :title="isEditing ? '编辑收款方式' : '新增收款方式'" width="500px">
      <el-form class="editor-form" label-width="110px"><el-form-item label="收款方式名称" required><el-input v-model="form.name" placeholder="请输入收款方式名称" /></el-form-item><el-form-item label="排序"><el-input-number v-model="form.sortOrder" :min="0" /></el-form-item><el-form-item label="状态"><el-radio-group v-model="form.isEnabled"><el-radio :value="true">启用</el-radio><el-radio :value="false">停用</el-radio></el-radio-group></el-form-item><el-form-item label="说明"><el-input v-model="form.description" type="textarea" :rows="3" placeholder="请输入说明" /></el-form-item></el-form>
      <template #footer><el-button @click="dialogVisible = false">取消</el-button><el-button type="primary" :loading="saving" @click="save">确定</el-button></template>
    </el-dialog>
  </SettingsWorkspace>
</template>
