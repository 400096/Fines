<script setup lang="ts">
// 部门管理：分页查询部门，并维护名称、编码、负责人、排序和启停状态。
import { OfficeBuilding, Plus, Refresh, Search } from '@element-plus/icons-vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { onMounted, reactive, ref } from 'vue'
import { departmentApi } from '@/api/services'
import type { Department, SaveDepartment } from '@/api/types'
import SettingsWorkspace from '@/components/SettingsWorkspace.vue'
import { formatDateTime } from '@/utils/format'

const departments = ref<Department[]>([])
const loading = ref(false)
const saving = ref(false)
const total = ref(0)
const page = ref(1)
const pageSize = ref(10)
const filters = reactive({ keyword: '', status: '' })
const dialogVisible = ref(false)
const isEditing = ref(false)
const form = reactive({ id: 0, name: '', code: '', sortOrder: 1, isEnabled: true, manager: '', remark: '' })

function statusValue() {
  return filters.status === '' ? undefined : filters.status === 'enabled'
}

async function loadDepartments() {
  loading.value = true
  try {
    const result = await departmentApi.listPaged({
      keyword: filters.keyword.trim() || undefined,
      isEnabled: statusValue(),
      page: page.value,
      pageSize: pageSize.value,
    })
    departments.value = result.items
    total.value = result.total
  } finally {
    loading.value = false
  }
}

function query() { page.value = 1; void loadDepartments() }
function reset() {
  Object.assign(filters, { keyword: '', status: '' })
  page.value = 1
  void loadDepartments()
}
function changePageSize(size: number) {
  pageSize.value = size
  page.value = 1
  void loadDepartments()
}
function openCreate() {
  isEditing.value = false
  const nextSort = Math.max(0, ...departments.value.map((item) => item.sortOrder)) + 1
  Object.assign(form, { id: 0, name: '', code: '', sortOrder: nextSort, isEnabled: true, manager: '', remark: '' })
  dialogVisible.value = true
}
function openEdit(row: Department) {
  isEditing.value = true
  Object.assign(form, {
    id: row.id,
    name: row.name,
    code: row.code,
    sortOrder: row.sortOrder,
    isEnabled: row.isEnabled,
    manager: row.manager ?? '',
    remark: row.remark ?? '',
  })
  dialogVisible.value = true
}
async function save() {
  if (!form.name.trim() || !form.code.trim()) {
    ElMessage.warning('请填写部门名称和编码')
    return
  }
  const data: SaveDepartment = {
    name: form.name.trim(),
    code: form.code.trim(),
    sortOrder: form.sortOrder,
    isEnabled: form.isEnabled,
    manager: form.manager.trim() || null,
    remark: form.remark.trim() || null,
  }
  saving.value = true
  try {
    if (isEditing.value) await departmentApi.update(form.id, data)
    else await departmentApi.create(data)
    dialogVisible.value = false
    ElMessage.success(isEditing.value ? '部门已更新' : '部门已新增')
    await loadDepartments()
  } finally {
    saving.value = false
  }
}
async function remove(row: Department) {
  try {
    await ElMessageBox.confirm(`确定删除部门“${row.name}”吗？`, '删除确认', { type: 'warning' })
    await departmentApi.remove(row.id)
    ElMessage.success('删除成功')
    if (departments.value.length === 1 && page.value > 1) page.value--
    await loadDepartments()
  } catch (error) {
    if (error !== 'cancel' && error !== 'close') throw error
  }
}

onMounted(loadDepartments)
</script>

<template>
  <SettingsWorkspace active="departments">
    <div class="management-header">
      <h2 class="management-title"><el-icon><OfficeBuilding /></el-icon>部门管理</h2>
      <div class="management-actions"><el-button :icon="Refresh" :loading="loading" @click="loadDepartments">刷新</el-button></div>
    </div>
    <div class="management-filter">
      <div class="management-filter__field"><label>部门名称/编码</label><el-input v-model="filters.keyword" placeholder="请输入部门名称或编码" clearable @keyup.enter="query" /></div>
      <div class="management-filter__field"><label>状态</label><el-select v-model="filters.status" placeholder="全部" clearable><el-option label="启用" value="enabled" /><el-option label="停用" value="disabled" /></el-select></div>
      <div class="management-filter__buttons"><el-button type="primary" :icon="Search" @click="query">查询</el-button><el-button :icon="Refresh" @click="reset">重置</el-button><el-button type="primary" :icon="Plus" @click="openCreate">新增部门</el-button></div>
    </div>
    <div class="management-table-wrap">
      <el-table v-loading="loading" class="management-table" :data="departments" stripe border>
        <el-table-column type="selection" width="44" /><el-table-column prop="name" label="部门名称" sortable min-width="120" /><el-table-column prop="code" label="部门编码" sortable width="210" /><el-table-column prop="sortOrder" label="排序" sortable width="86" /><el-table-column label="状态" sortable min-width="100"><template #default="{ row }"><el-tag class="status-tag" :type="row.isEnabled ? 'success' : 'info'" round>{{ row.isEnabled ? '启用' : '停用' }}</el-tag></template></el-table-column><el-table-column prop="manager" label="负责人" sortable min-width="110" /><el-table-column label="更新时间" sortable min-width="165"><template #default="{ row }">{{ formatDateTime(row.updatedAt) }}</template></el-table-column><el-table-column prop="remark" label="备注" min-width="145" /><el-table-column label="操作" fixed="right" width="130"><template #default="{ row }"><el-button link type="primary" @click="openEdit(row)">编辑</el-button><el-button link type="danger" @click="remove(row)">删除</el-button></template></el-table-column>
      </el-table>
    </div>
    <div class="management-footer"><span>共 {{ total }} 条记录</span><el-pagination v-model:current-page="page" v-model:page-size="pageSize" background layout="prev, pager, next, sizes" :total="total" @current-change="loadDepartments" @size-change="changePageSize" /></div>
    <el-dialog v-model="dialogVisible" :title="isEditing ? '编辑部门' : '新增部门'" width="520px">
      <el-form class="editor-form" label-width="90px"><el-form-item label="部门名称" required><el-input v-model="form.name" placeholder="请输入部门名称" /></el-form-item><el-form-item label="部门编码" required><el-input v-model="form.code" placeholder="请输入部门编码" /></el-form-item><el-form-item label="负责人"><el-input v-model="form.manager" placeholder="请输入负责人" /></el-form-item><el-form-item label="排序"><el-input-number v-model="form.sortOrder" :min="0" /></el-form-item><el-form-item label="状态"><el-radio-group v-model="form.isEnabled"><el-radio :value="true">启用</el-radio><el-radio :value="false">停用</el-radio></el-radio-group></el-form-item><el-form-item label="备注"><el-input v-model="form.remark" type="textarea" :rows="3" /></el-form-item></el-form>
      <template #footer><el-button @click="dialogVisible = false">取消</el-button><el-button type="primary" :loading="saving" @click="save">确定</el-button></template>
    </el-dialog>
  </SettingsWorkspace>
</template>
