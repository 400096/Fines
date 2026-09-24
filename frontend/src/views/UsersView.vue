<script setup lang="ts">
// 用户管理：维护登录账号、角色和启停状态，并禁止当前用户删除自己。
import { Plus, Refresh, Search, UserFilled } from '@element-plus/icons-vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { onMounted, reactive, ref } from 'vue'
import { userApi } from '@/api/services'
import type { CreateUser, SystemUser, UpdateUser } from '@/api/types'
import SettingsWorkspace from '@/components/SettingsWorkspace.vue'
import { useUserStore } from '@/stores/user'
import { formatDateTime } from '@/utils/format'

const userStore = useUserStore()
const users = ref<SystemUser[]>([])
const loading = ref(false)
const saving = ref(false)
const total = ref(0)
const page = ref(1)
const pageSize = ref(10)
const roles = ['超级管理员', '财务专员', '财务录入员', '财务审核员', '查看人员']
const filters = reactive({ keyword: '', role: '', status: '' })
const dialogVisible = ref(false)
const isEditing = ref(false)
const form = reactive({ id: 0, username: '', name: '', phone: '', role: '查看人员', isEnabled: true, password: '' })

function enabledFilter() {
  return filters.status === '' ? undefined : filters.status === 'enabled'
}
async function loadUsers() {
  loading.value = true
  try {
    const result = await userApi.list({
      keyword: filters.keyword.trim() || undefined,
      role: filters.role || undefined,
      isEnabled: enabledFilter(),
      page: page.value,
      pageSize: pageSize.value,
    })
    users.value = result.items
    total.value = result.total
  } finally {
    loading.value = false
  }
}
function query() { page.value = 1; void loadUsers() }
function reset() {
  Object.assign(filters, { keyword: '', role: '', status: '' })
  page.value = 1
  void loadUsers()
}
function changePageSize(size: number) {
  pageSize.value = size
  page.value = 1
  void loadUsers()
}
function openCreate() {
  isEditing.value = false
  Object.assign(form, { id: 0, username: '', name: '', phone: '', role: '查看人员', isEnabled: true, password: '' })
  dialogVisible.value = true
}
function openEdit(row: SystemUser) {
  isEditing.value = true
  Object.assign(form, {
    id: row.id,
    username: row.username,
    name: row.name,
    phone: row.phone ?? '',
    role: row.role,
    isEnabled: row.isEnabled,
    password: '',
  })
  dialogVisible.value = true
}
function formatPhone(phone: string | null) {
  // 列表中对标准手机号做脱敏显示，编辑弹窗仍使用原始值。
  if (!phone) return '—'
  return /^1\d{10}$/.test(phone) ? `${phone.slice(0, 3)}****${phone.slice(-4)}` : phone
}
async function save() {
  if (!form.username.trim() || !form.name.trim()) {
    ElMessage.warning('请填写用户名和姓名')
    return
  }
  if (!isEditing.value && form.password.length < 8) {
    ElMessage.warning('初始密码至少需要 8 位')
    return
  }
  if (isEditing.value && form.password && form.password.length < 8) {
    ElMessage.warning('新密码至少需要 8 位')
    return
  }
  saving.value = true
  try {
    if (isEditing.value) {
      const data: UpdateUser = {
        name: form.name.trim(),
        phone: form.phone.trim() || null,
        role: form.role,
        isEnabled: form.isEnabled,
        password: form.password || null,
      }
      await userApi.update(form.id, data)
    } else {
      const data: CreateUser = {
        username: form.username.trim(),
        password: form.password,
        name: form.name.trim(),
        phone: form.phone.trim() || null,
        role: form.role,
        isEnabled: form.isEnabled,
      }
      await userApi.create(data)
    }
    dialogVisible.value = false
    ElMessage.success(isEditing.value ? '用户信息已更新' : '用户已新增')
    await loadUsers()
  } finally {
    saving.value = false
  }
}
async function remove(row: SystemUser) {
  try {
    await ElMessageBox.confirm(`确定删除用户“${row.name}”吗？`, '删除确认', { type: 'warning' })
    await userApi.remove(row.id)
    ElMessage.success('删除成功')
    if (users.value.length === 1 && page.value > 1) page.value--
    await loadUsers()
  } catch (error) {
    if (error !== 'cancel' && error !== 'close') throw error
  }
}

onMounted(loadUsers)
</script>

<template>
  <SettingsWorkspace active="users">
    <div class="management-header">
      <div>
        <h2 class="management-title"><el-icon><UserFilled /></el-icon>用户管理</h2>
        <p class="user-subtitle">管理系统用户账号、角色与使用状态</p>
      </div>
      <div class="management-actions"><el-button :icon="Refresh" :loading="loading" @click="loadUsers">刷新</el-button></div>
    </div>
    <div class="management-filter user-filter">
      <div class="management-filter__field"><label>账号/姓名</label><el-input v-model="filters.keyword" placeholder="请输入账号或姓名" clearable @keyup.enter="query" /></div>
      <div class="management-filter__field"><label>角色</label><el-select v-model="filters.role" placeholder="全部" clearable><el-option v-for="role in roles" :key="role" :label="role" :value="role" /></el-select></div>
      <div class="management-filter__field"><label>状态</label><el-select v-model="filters.status" placeholder="全部" clearable><el-option label="启用" value="enabled" /><el-option label="停用" value="disabled" /></el-select></div>
      <div class="management-filter__buttons"><el-button type="primary" :icon="Search" @click="query">查询</el-button><el-button :icon="Refresh" @click="reset">重置</el-button><el-button type="primary" :icon="Plus" @click="openCreate">新增用户</el-button></div>
    </div>
    <div class="management-table-wrap">
      <el-table v-loading="loading" class="management-table user-table" :data="users" stripe border>
        <el-table-column type="selection" width="44" /><el-table-column prop="username" label="用户名" sortable min-width="110" /><el-table-column prop="name" label="姓名" sortable min-width="90" /><el-table-column label="手机号" min-width="130"><template #default="{ row }">{{ formatPhone(row.phone) }}</template></el-table-column><el-table-column prop="role" label="所属角色" sortable min-width="125" /><el-table-column label="状态" sortable min-width="90"><template #default="{ row }"><el-tag class="status-tag" :type="row.isEnabled ? 'success' : 'info'" round>{{ row.isEnabled ? '启用' : '停用' }}</el-tag></template></el-table-column><el-table-column label="最后登录时间" sortable min-width="165"><template #default="{ row }">{{ formatDateTime(row.lastLoginAt) }}</template></el-table-column><el-table-column label="创建时间" sortable min-width="165"><template #default="{ row }">{{ formatDateTime(row.createdAt) }}</template></el-table-column><el-table-column label="操作" fixed="right" width="130"><template #default="{ row }"><el-button link type="primary" @click="openEdit(row)">编辑</el-button><el-button link type="danger" :disabled="row.id === userStore.userInfo.userId" @click="remove(row)">删除</el-button></template></el-table-column>
      </el-table>
    </div>
    <div class="management-footer"><span>共 {{ total }} 条记录</span><el-pagination v-model:current-page="page" v-model:page-size="pageSize" background layout="prev, pager, next, sizes" :total="total" @current-change="loadUsers" @size-change="changePageSize" /></div>
    <el-dialog v-model="dialogVisible" :title="isEditing ? '编辑用户' : '新增用户'" width="540px">
      <el-form class="editor-form" label-width="90px">
        <el-form-item label="用户名" required><el-input v-model="form.username" :disabled="isEditing" placeholder="请输入登录用户名" /></el-form-item>
        <el-form-item label="姓名" required><el-input v-model="form.name" placeholder="请输入姓名" /></el-form-item>
        <el-form-item label="手机号"><el-input v-model="form.phone" placeholder="请输入手机号" maxlength="30" /></el-form-item>
        <el-form-item label="角色"><el-select v-model="form.role"><el-option v-for="role in roles" :key="role" :label="role" :value="role" /></el-select></el-form-item>
        <el-form-item label="初始密码" :required="!isEditing"><el-input v-model="form.password" type="password" show-password :placeholder="isEditing ? '留空表示不修改' : '至少 8 位'" /></el-form-item>
        <el-form-item label="状态"><el-radio-group v-model="form.isEnabled"><el-radio :value="true">启用</el-radio><el-radio :value="false">停用</el-radio></el-radio-group></el-form-item>
      </el-form>
      <template #footer><el-button @click="dialogVisible = false">取消</el-button><el-button type="primary" :loading="saving" @click="save">确定</el-button></template>
    </el-dialog>
  </SettingsWorkspace>
</template>

<style scoped>
.user-subtitle { margin: 7px 0 0 38px; color: var(--text-muted); font-size: 13px; }
.user-filter { grid-template-columns: 1fr .95fr .95fr; }
.user-filter .management-filter__buttons { grid-column: 1 / -1; }
.user-table { min-width: 1100px; }
@media (max-width: 1050px) { .user-filter { grid-template-columns: 1fr 1fr; }.user-filter .management-filter__buttons { grid-column: 1 / -1; } }
@media (max-width: 640px) { .user-filter { grid-template-columns: 1fr; }.user-filter .management-filter__buttons { grid-column: auto; } }
</style>
