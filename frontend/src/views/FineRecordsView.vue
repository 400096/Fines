<script setup lang="ts">
// 考核记录主页面：负责筛选、分页排序、增删改、Excel 暂存确认和导出。
import { Document, Download, Plus, Refresh, Search, Upload } from '@element-plus/icons-vue'
import type { FormInstance, FormRules, TableInstance } from 'element-plus'
import { ElMessage, ElMessageBox } from 'element-plus'
import { computed, nextTick, onMounted, reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { assessmentApi, departmentApi, paymentMethodApi } from '@/api/services'
import type { AssessmentDateType, AssessmentRecord, AssessmentRecordQuery, AssessmentStatus, Department, ExcelStagedRow, ExcelImportFailedRow, ExcelImportResult, PaymentMethod } from '@/api/types'
import PageTitle from '@/components/PageTitle.vue'
import { formatMoney, todayString } from '@/utils/format'
import { consumePendingImportFile } from '@/utils/importTransfer'

const route = useRoute()
const router = useRouter()
// 筛选条件直接映射后端查询 DTO；部门支持多选，录入日期为单日筛选。
const filters = reactive({ dates: [] as string[], detailDate: '', departmentIds: [] as number[], name: '', reason: '', remark: '', status: '' })
const records = ref<AssessmentRecord[]>([])
const departments = ref<Department[]>([])
const paymentMethods = ref<PaymentMethod[]>([])
const loading = ref(false)
const saving = ref(false)
const importing = ref(false)
const confirming = ref(false)
const exporting = ref(false)
const total = ref(0)
const page = ref(1)
const pageSize = ref(10)
const sortBy = ref<NonNullable<AssessmentRecordQuery['sortBy']>>('assessmentDate')
const sortOrder = ref<NonNullable<AssessmentRecordQuery['sortOrder']>>('descending')
const selectedRecords = ref<AssessmentRecord[]>([])
const selectedAmountTotal = computed(() => selectedRecords.value.reduce((sum, item) => sum + item.amount, 0))
const dialogVisible = ref(false)
const batchAdding = ref(false)
const importResultVisible = ref(false)
const lastImportResult = ref<ExcelImportResult | null>(null)
const manualFailedRow = ref<ExcelImportFailedRow | null>(null)
const addingFromImport = ref(false)
const editingId = ref<number | null>(null)
const editingStagingId = ref<number | null>(null)
const formRef = ref<FormInstance>()
const recordsTableRef = ref<TableInstance>()
const fileInput = ref<HTMLInputElement>()
const departmentKeyword = ref('')
interface PersonNameSuggestion { value: string }
const recordForm = reactive({
  assessmentDateType: 'Fixed' as AssessmentDateType, assessmentDate: '', assessmentEndDate: '', detailDate: '',
  departmentId: 0, departmentName: '', personName: '', amount: undefined as number | undefined,
  reason: '', remark: '', paymentMethodId: undefined as number | undefined,
})
const rules: FormRules<typeof recordForm> = {
  assessmentDate: [{ required: true, message: '请选择单据日期', trigger: 'change' }],
  detailDate: [{ required: true, message: '请选择录入日期', trigger: 'change' }],
  personName: [{ required: true, message: '请输入姓名', trigger: 'blur' }],
  amount: [{ required: true, type: 'number', min: 0.01, message: '请输入大于 0 的考核金额', trigger: 'change' }],
  reason: [{ required: true, message: '请输入考核事由', trigger: 'blur' }],
}
const enabledDepartments = computed(() => departments.value.filter((item) => item.isEnabled))
const enabledPaymentMethods = computed(() => paymentMethods.value.filter((item) => item.isEnabled))
const pendingDepartmentNames = computed(() => Array.from(new Set(
  (lastImportResult.value?.stagedRows ?? [])
    .filter(item => !item.departmentId)
    .map(item => item.departmentName),
)))
// 未识别收款方式的数据排在最前，方便在确认导入前优先人工修正。
const orderedStagedRows = computed(() => [...(lastImportResult.value?.stagedRows ?? [])].sort((left, right) => {
  const leftUnrecognized = !left.paymentMethodName
  const rightUnrecognized = !right.paymentMethodName
  if (leftUnrecognized !== rightUnrecognized) return leftUnrecognized ? -1 : 1
  return left.rowNumber - right.rowNumber
}))
const selectablePendingDepartmentNames = computed(() => Array.from(new Set([
  ...pendingDepartmentNames.value,
  ...(addingFromImport.value && !recordForm.departmentId && recordForm.departmentName
    ? [recordForm.departmentName]
    : []),
])))
const matchesDepartmentKeyword = (name: string) => {
  const keyword = departmentKeyword.value.replace(/\s/g, '').toLocaleLowerCase()
  return !keyword || name.replace(/\s/g, '').toLocaleLowerCase().includes(keyword)
}
const filteredEnabledDepartments = computed(() => enabledDepartments.value.filter(
  item => matchesDepartmentKeyword(item.name) || matchesDepartmentKeyword(item.code),
))
const filteredPendingDepartmentNames = computed(() => selectablePendingDepartmentNames.value.filter(matchesDepartmentKeyword))
const departmentSelection = computed<number | string>({
  get: () => recordForm.departmentId || (recordForm.departmentName ? `pending:${recordForm.departmentName}` : ''),
  set: (value) => {
    if (typeof value === 'number') {
      recordForm.departmentId = value
      recordForm.departmentName = departments.value.find(item => item.id === value)?.name ?? ''
      return
    }
    recordForm.departmentId = 0
    recordForm.departmentName = value.startsWith('pending:') ? value.slice('pending:'.length) : value
  },
})

function filterDepartments(value: string) {
  departmentKeyword.value = value.trim()
}

function handleDepartmentVisibleChange(visible: boolean) {
  if (!visible) departmentKeyword.value = ''
}

function selectUniqueDepartment() {
  if (!departmentKeyword.value) return Boolean(departmentSelection.value)
  const options: Array<number | string> = [
    ...filteredEnabledDepartments.value.map(item => item.id),
    ...(addingFromImport.value
      ? filteredPendingDepartmentNames.value.map(name => `pending:${name}`)
      : []),
  ]
  if (options.length !== 1) return false
  departmentSelection.value = options[0]
  departmentKeyword.value = ''
  return true
}

async function queryPersonNameSuggestions(
  queryString: string,
  callback: (suggestions: PersonNameSuggestion[]) => void,
) {
  try {
    const names = await assessmentApi.personNameSuggestions(queryString.trim())
    callback(names.map(value => ({ value })))
  } catch {
    // 自动补全失败不阻止用户继续手工输入姓名。
    callback([])
  }
}

function focusNextFormItem(event: KeyboardEvent) {
  if (event.isComposing || event.ctrlKey || event.altKey || event.metaKey) return
  if (event.target instanceof HTMLTextAreaElement && event.shiftKey) return

  const target = event.target as HTMLElement
  const form = target.closest<HTMLElement>('.record-form')
  const currentItem = target.closest<HTMLElement>('.el-form-item')
  if (!form || !currentItem) return

  // 部门只在过滤结果唯一时自动选择；仍有多项时保留下拉框供用户继续选择。
  if (currentItem.dataset.field === 'department' && !selectUniqueDepartment()) return

  const items = Array.from(form.querySelectorAll<HTMLElement>('.el-form-item'))
    .filter(item => item.offsetParent !== null)
  const currentIndex = items.indexOf(currentItem)
  if (currentIndex < 0) return

  const findControl = (item: HTMLElement) => Array.from(item.querySelectorAll<HTMLElement>(
    'input:not([type="hidden"]):not([disabled]), textarea:not([disabled]), button:not([disabled])',
  )).find(control => control.offsetParent !== null && control.tabIndex >= 0)

  let nextControl: HTMLElement | undefined
  for (let index = currentIndex + 1; index < items.length && !nextControl; index++) {
    nextControl = findControl(items[index])
  }
  nextControl ??= form.closest('.el-dialog')?.querySelector<HTMLElement>('[data-record-save]') ?? undefined
  if (!nextControl) return

  event.preventDefault()
  event.stopPropagation()
  window.setTimeout(() => nextControl?.focus())
}

function buildQuery(withPaging = true): AssessmentRecordQuery {
  return {
    dateFrom: filters.dates?.[0] || undefined,
    dateTo: filters.dates?.[1] || undefined,
    detailDateFrom: filters.detailDate || undefined,
    detailDateTo: filters.detailDate || undefined,
    departmentIds: filters.departmentIds.length ? filters.departmentIds : undefined,
    name: filters.name.trim() || undefined,
    reason: filters.reason.trim() || undefined,
    remark: filters.remark.trim() || undefined,
    status: (filters.status || undefined) as AssessmentStatus | undefined,
    sortBy: sortBy.value,
    sortOrder: sortOrder.value,
    page: withPaging ? page.value : undefined,
    pageSize: withPaging ? pageSize.value : undefined,
  }
}
async function loadRecords() {
  loading.value = true
  selectedRecords.value = []
  try {
    const result = await assessmentApi.list(buildQuery())
    records.value = result.items
    total.value = result.total
  } finally {
    loading.value = false
  }
}
async function loadOptions() {
  const [departmentList, paymentMethodList] = await Promise.all([
    departmentApi.list(),
    paymentMethodApi.list(),
  ])
  departments.value = departmentList
  paymentMethods.value = paymentMethodList
}
function query() { page.value = 1; void loadRecords() }
function handleFilterEnter(event: KeyboardEvent) {
  if (event.isComposing || event.keyCode === 229) return
  const target = event.target
  if (!(target instanceof HTMLInputElement) || !target.closest('[data-filter-text]')) return
  event.preventDefault()
  query()
}
function reset() {
  Object.assign(filters, { dates: [], detailDate: '', departmentIds: [], name: '', reason: '', remark: '', status: '' })
  page.value = 1
  void loadRecords()
}
function changePageSize(size: number) {
  pageSize.value = size
  page.value = 1
  void loadRecords()
}
function changeSort({ prop, order }: { prop: string; order: 'ascending' | 'descending' | null }) {
  const sortableFields: NonNullable<AssessmentRecordQuery['sortBy']>[] = ['assessmentDate', 'detailDate', 'departmentName', 'personName', 'amount']
  sortBy.value = sortableFields.includes(prop as NonNullable<AssessmentRecordQuery['sortBy']>)
    ? prop as NonNullable<AssessmentRecordQuery['sortBy']>
    : 'assessmentDate'
  sortOrder.value = order ?? 'descending'
  page.value = 1
  void loadRecords()
}
function changeSelection(rows: AssessmentRecord[]) {
  selectedRecords.value = rows
}
function toggleRowSelection(row: AssessmentRecord, _column: unknown, event: Event) {
  const target = event.target as HTMLElement
  if (target.closest('.el-checkbox, button, a')) return
  recordsTableRef.value?.toggleRowSelection(row)
}
function openCreate() {
  departmentKeyword.value = ''
  batchAdding.value = false
  addingFromImport.value = false
  manualFailedRow.value = null
  editingId.value = null
  editingStagingId.value = null
  Object.assign(recordForm, {
    assessmentDateType: 'Fixed', assessmentDate: todayString(), assessmentEndDate: '', detailDate: todayString(),
    departmentId: 0, departmentName: '', personName: '', amount: undefined,
    reason: '', remark: '', paymentMethodId: undefined,
  })
  dialogVisible.value = true
  nextTick(() => formRef.value?.clearValidate())
}
function openEdit(row: AssessmentRecord) {
  departmentKeyword.value = ''
  batchAdding.value = false
  addingFromImport.value = false
  manualFailedRow.value = null
  editingId.value = row.id
  editingStagingId.value = null
  const assessmentDateType = normalizeAssessmentDateType(row.assessmentDateType, row.assessmentEndDate)
  Object.assign(recordForm, {
    assessmentDateType,
    assessmentDate: row.assessmentDate,
    assessmentEndDate: row.assessmentEndDate ?? '',
    detailDate: row.detailDate,
    departmentId: row.departmentId,
    departmentName: row.departmentName,
    personName: row.personName,
    amount: row.amount,
    reason: row.reason,
    remark: row.remark ?? '',
    paymentMethodId: row.paymentMethodId ?? undefined,
  })
  dialogVisible.value = true
  nextTick(() => formRef.value?.clearValidate())
}
function openImportedEdit(row: ExcelStagedRow) {
  departmentKeyword.value = ''
  batchAdding.value = false
  addingFromImport.value = true
  manualFailedRow.value = null
  editingId.value = null
  editingStagingId.value = row.stagingId
  const assessmentDateType = normalizeAssessmentDateType(row.assessmentDateType, row.assessmentEndDate)
  Object.assign(recordForm, {
    assessmentDateType,
    assessmentDate: row.assessmentDate,
    assessmentEndDate: row.assessmentEndDate ?? '',
    detailDate: row.detailDate,
    departmentId: row.departmentId,
    departmentName: row.departmentName,
    personName: row.personName,
    amount: row.amount,
    reason: row.reason,
    remark: row.remark ?? '',
    paymentMethodId: row.paymentMethodId ?? undefined,
  })
  dialogVisible.value = true
  nextTick(() => formRef.value?.clearValidate())
}
function openManualImportAdd() {
  departmentKeyword.value = ''
  batchAdding.value = false
  addingFromImport.value = true
  manualFailedRow.value = null
  editingId.value = null
  editingStagingId.value = null
  Object.assign(recordForm, {
    assessmentDateType: 'Fixed', assessmentDate: todayString(), assessmentEndDate: '', detailDate: todayString(),
    departmentId: 0, departmentName: '', personName: '', amount: undefined,
    reason: '', remark: '', paymentMethodId: undefined,
  })
  dialogVisible.value = true
  nextTick(() => formRef.value?.clearValidate())
}
function normalizedDepartmentName(value: string) {
  return value.replace(/\s/g, '').replace(/部$/, '')
}
function normalizeImportedDate(value: string | null) {
  const match = value?.trim().match(/^(\d{4})[\/-](\d{1,2})[\/-](\d{1,2})/)
  if (!match) return todayString()
  return `${match[1]}-${match[2].padStart(2, '0')}-${match[3].padStart(2, '0')}`
}
function normalizeImportedDetailDate(value: string | null, assessmentDate: string | null) {
  const fullDate = value?.trim().match(/^(\d{4})[\/-](\d{1,2})[\/-](\d{1,2})/)
  if (fullDate) return `${fullDate[1]}-${fullDate[2].padStart(2, '0')}-${fullDate[3].padStart(2, '0')}`
  const monthDay = value?.trim().match(/^(\d{1,2})[./月](\d{1,2})/)
  const year = assessmentDate?.trim().match(/^(\d{4})/)?.[1]
  if (monthDay && year) return `${year}-${monthDay[1].padStart(2, '0')}-${monthDay[2].padStart(2, '0')}`
  return todayString()
}
function parseImportedAssessmentDate(value: string | null) {
  const text = value?.trim() ?? ''
  const range = text.match(/^(\d{4})[\/-](\d{1,2})[\/-](\d{1,2})\s*[-－—]\s*(?:(\d{1,2})[\/-])?(\d{1,2})/)
  if (!range) return { type: 'Fixed' as AssessmentDateType, start: normalizeImportedDate(value), end: '' }
  const endMonth = range[4] || range[2]
  return {
    type: 'Range' as AssessmentDateType,
    start: `${range[1]}-${range[2].padStart(2, '0')}-${range[3].padStart(2, '0')}`,
    end: `${range[1]}-${endMonth.padStart(2, '0')}-${range[5].padStart(2, '0')}`,
  }
}
function formatAssessmentDate(row: { assessmentDate: string; assessmentEndDate?: string | null }) {
  return row.assessmentEndDate ? `${row.assessmentDate} 至 ${row.assessmentEndDate}` : row.assessmentDate
}
function normalizeAssessmentDateType(value: unknown, assessmentEndDate?: string | null): AssessmentDateType {
  if (assessmentEndDate || String(value).toLowerCase() === 'range' || value === 1 || value === '1') return 'Range'
  return 'Fixed'
}
function updateAssessmentRange(value: [string, string] | null) {
  recordForm.assessmentDate = value?.[0] || ''
  recordForm.assessmentEndDate = value?.[1] || ''
}
function parseImportedAmount(value: string | null) {
  const match = value?.replace(',', '.').match(/\d+(?:\.\d+)?/)
  return match ? Number(match[0]) : undefined
}
function openFailedAdd(row: ExcelImportFailedRow) {
  departmentKeyword.value = ''
  batchAdding.value = false
  addingFromImport.value = true
  manualFailedRow.value = row
  editingId.value = null
  editingStagingId.value = null
  const departmentName = row.departmentName ? normalizedDepartmentName(row.departmentName) : ''
  const department = enabledDepartments.value.find(item => normalizedDepartmentName(item.name) === departmentName)
  const paymentMethod = enabledPaymentMethods.value.find(item => row.paymentMethodName?.includes(item.name))
  const importedDate = parseImportedAssessmentDate(row.assessmentDate)
  Object.assign(recordForm, {
    assessmentDateType: importedDate.type,
    assessmentDate: importedDate.start,
    assessmentEndDate: importedDate.end,
    detailDate: normalizeImportedDetailDate(row.detailDate, row.assessmentDate),
    departmentId: department?.id ?? 0,
    departmentName: department?.name ?? row.departmentName?.trim() ?? '',
    personName: row.personName?.trim() ?? '',
    amount: parseImportedAmount(row.amountText),
    reason: row.reason?.trim() ?? '',
    remark: row.remark?.trim() ?? '',
    paymentMethodId: paymentMethod?.id,
  })
  if (!department && row.departmentName) ElMessage.info(`部门“${row.departmentName}”将在确认导入时创建，也可以改选现有部门`)
  dialogVisible.value = true
  nextTick(() => formRef.value?.clearValidate())
}
async function saveRecord() {
  const valid = await formRef.value?.validate().catch(() => false)
  if (!valid) return
  const amount = recordForm.amount
  if (amount === undefined || amount <= 0) {
    ElMessage.warning('请输入大于 0 的考核金额')
    return
  }
  if (!recordForm.departmentId && (!addingFromImport.value || !recordForm.departmentName.trim())) {
    ElMessage.warning('请选择部门')
    return
  }
  if (recordForm.assessmentDateType === 'Range' && !recordForm.assessmentEndDate) {
    ElMessage.warning('请选择单据日期区间的结束日期')
    return
  }
  if (recordForm.assessmentDateType === 'Range' && recordForm.assessmentEndDate < recordForm.assessmentDate) {
    ElMessage.warning('结束日期不能早于开始日期')
    return
  }
  const baseData = {
    assessmentDateType: recordForm.assessmentDateType,
    assessmentDate: recordForm.assessmentDate,
    assessmentEndDate: recordForm.assessmentDateType === 'Range' ? recordForm.assessmentEndDate : null,
    detailDate: recordForm.detailDate,
    departmentId: recordForm.departmentId,
    personName: recordForm.personName.trim(),
    amount,
    reason: recordForm.reason.trim(),
    remark: recordForm.remark.trim() || null,
    paymentMethodId: recordForm.paymentMethodId || null,
  }
  const shouldContinueBatch = !addingFromImport.value && editingId.value === null && batchAdding.value
  saving.value = true
  try {
    if (addingFromImport.value) {
      const importResult = lastImportResult.value
      if (!importResult) {
        ElMessage.error('导入批次已失效，请重新选择文件')
        return
      }
      const stagingData = {
        ...baseData,
        departmentId: recordForm.departmentId || null,
        departmentName: recordForm.departmentName.trim(),
      }
      if (editingStagingId.value === null) {
        const saved = await assessmentApi.addStagedRow(importResult.batchId, stagingData)
        const source = manualFailedRow.value
        importResult.stagedRows = [...(importResult.stagedRows ?? []), saved]
        importResult.staged += 1
        if (source) {
          importResult.failedRows = (importResult.failedRows ?? [])
            .filter(item => item.rowNumber !== source.rowNumber || item.message !== source.message)
          importResult.errors = (importResult.failedRows ?? [])
            .map(item => item.rowNumber > 0 ? `第 ${item.rowNumber} 行：${item.message}` : item.message)
          importResult.failed = Math.max(0, importResult.failed - 1)
        }
        ElMessage.success('已添加到待确认数据')
      } else {
        const saved = await assessmentApi.updateStagedRow(importResult.batchId, editingStagingId.value, stagingData)
        const stagedRow = importResult.stagedRows?.find(item => item.stagingId === saved.stagingId)
        if (stagedRow) Object.assign(stagedRow, saved)
        ElMessage.success('暂存数据已更新')
      }
    } else if (editingId.value === null) {
      await assessmentApi.create(baseData)
      ElMessage.success('考核记录已新增')
    } else {
      await assessmentApi.update(editingId.value, baseData)
      ElMessage.success('考核记录已更新')
    }
    if (shouldContinueBatch) {
      Object.assign(recordForm, {
        personName: '', amount: undefined, reason: '', remark: '',
      })
      await loadRecords()
      await nextTick()
      formRef.value?.clearValidate()
      document.querySelector<HTMLInputElement>('.record-form [data-field="person-name"] input')?.focus()
      return
    }
    const wasStaging = addingFromImport.value
    manualFailedRow.value = null
    addingFromImport.value = false
    editingStagingId.value = null
    dialogVisible.value = false
    if (!wasStaging) await loadRecords()
  } finally {
    saving.value = false
  }
}
async function removeImportedRecord(row: ExcelStagedRow) {
  try {
    await ElMessageBox.confirm(`确定从本次导入中删除“${row.personName}”吗？`, '删除确认', { type: 'warning' })
    const importResult = lastImportResult.value
    if (!importResult) return
    await assessmentApi.removeStagedRow(importResult.batchId, row.stagingId)
    importResult.stagedRows = (importResult.stagedRows ?? []).filter(item => item.stagingId !== row.stagingId)
    importResult.staged = Math.max(0, importResult.staged - 1)
    ElMessage.success('已从待确认数据中删除')
  } catch (error) {
    if (error !== 'cancel' && error !== 'close') throw error
  }
}
async function removeRecord(row: AssessmentRecord) {
  try {
    await ElMessageBox.confirm(`确定删除“${row.personName}”的这条考核记录吗？`, '删除确认', { type: 'warning' })
    await assessmentApi.remove(row.id)
    ElMessage.success('删除成功')
    if (records.value.length === 1 && page.value > 1) page.value--
    await loadRecords()
  } catch (error) {
    if (error !== 'cancel' && error !== 'close') throw error
  }
}
async function exportExcel() {
  exporting.value = true
  try {
    const { blob, filename } = await assessmentApi.exportExcel(buildQuery(false))
    const url = URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = url
    link.download = filename
    link.click()
    URL.revokeObjectURL(url)
    ElMessage.success('导出成功')
  } finally {
    exporting.value = false
  }
}
async function processImportFile(file: File) {
  importing.value = true
  try {
    const result = await assessmentApi.importExcel(file)
    // 检测旧版后端响应，避免缺少日期类型字段时把区间日期误当成固定日期。
    const missingNewDateFields = (result.stagedRows ?? []).some((row) => {
      const dateType = normalizeAssessmentDateType(row.assessmentDateType, row.assessmentEndDate)
      return row.assessmentDateType == null
        || !row.detailDate
        || !/^\d{4}-\d{2}-\d{2}$/.test(row.detailDate)
        || (dateType === 'Range' && !row.assessmentEndDate)
    })
    if (missingNewDateFields) {
      await assessmentApi.cancelImport(result.batchId).catch(() => undefined)
      ElMessage.error('当前运行的后端版本过旧，未返回日期类型、结束日期或录入日期。请更新并重启后端后重新导入')
      return
    }
    const stagedRows = (result.stagedRows ?? []).map(row => ({
      ...row,
      assessmentDateType: normalizeAssessmentDateType(row.assessmentDateType, row.assessmentEndDate),
    }))
    lastImportResult.value = {
      ...result,
      pendingDepartments: result.pendingDepartments ?? [],
      stagedRows,
      failedRows: result.failedRows ?? [],
    }
    importResultVisible.value = true
  } finally {
    importing.value = false
  }
}

async function importExcel(event: Event) {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  input.value = ''
  if (file) await processImportFile(file)
}

async function confirmImport() {
  const importResult = lastImportResult.value
  if (!importResult || importResult.staged === 0) {
    ElMessage.warning('没有可确认导入的数据')
    return
  }
  confirming.value = true
  try {
    const result = await assessmentApi.confirmImport(importResult.batchId)
    lastImportResult.value = null
    importResultVisible.value = false
    page.value = 1
    await Promise.all([loadOptions(), loadRecords()])
    const departmentMessage = result.createdDepartments.length
      ? `，新增或恢复部门：${result.createdDepartments.join('、')}`
      : ''
    ElMessage.success(`成功导入 ${result.imported} 条考核记录${departmentMessage}`)
  } finally {
    confirming.value = false
  }
}

async function discardImport(): Promise<boolean> {
  const importResult = lastImportResult.value
  if (!importResult) return true
  try {
    await ElMessageBox.confirm('取消后，本次暂存的导入数据将被移除，确定继续吗？', '取消导入', { type: 'warning' })
    await assessmentApi.cancelImport(importResult.batchId)
    lastImportResult.value = null
    return true
  } catch (error) {
    if (error === 'cancel' || error === 'close') return false
    throw error
  }
}

async function cancelImport() {
  if (await discardImport()) importResultVisible.value = false
}

function beforeCloseImport(done: () => void) {
  void discardImport().then((discarded) => { if (discarded) done() })
}

onMounted(async () => {
  // 首页跳转可通过查询参数预设日期、状态，或直接触发新增/导入操作。
  const routeDate = typeof route.query.date === 'string' ? route.query.date : ''
  const routeDetailDate = typeof route.query.detailDate === 'string' ? route.query.detailDate : ''
  const routeStatus = typeof route.query.status === 'string' ? route.query.status : ''
  if (/^\d{4}-\d{2}-\d{2}$/.test(routeDate)) filters.dates = [routeDate, routeDate]
  if (/^\d{4}-\d{2}-\d{2}$/.test(routeDetailDate)) filters.detailDate = routeDetailDate
  if (routeStatus === 'Pending' || routeStatus === 'Completed') filters.status = routeStatus

  await Promise.all([loadOptions(), loadRecords()])

  const action = typeof route.query.action === 'string' ? route.query.action : ''
  if (action === 'create') {
    openCreate()
  } else if (action === 'import') {
    const transferredFile = consumePendingImportFile()
    if (transferredFile) await processImportFile(transferredFile)
    else fileInput.value?.click()
  }

  if (action) {
    const nextQuery = { ...route.query }
    delete nextQuery.action
    await router.replace({ path: route.path, query: nextQuery })
  }
})
</script>

<template>
  <div class="page-container">
    <PageTitle title="考核记录" subtitle="查询、整理与导出考核明细" :icon="Document" />
    <section class="card filter-card">
      <el-form label-position="top" :model="filters" @keydown.enter.capture="handleFilterEnter" @submit.prevent><div class="filter-grid">
        <el-form-item label="单据日期"><el-date-picker v-model="filters.dates" type="daterange" value-format="YYYY-MM-DD" start-placeholder="开始日期" end-placeholder="结束日期" /></el-form-item>
        <el-form-item label="录入日期"><el-date-picker v-model="filters.detailDate" type="date" value-format="YYYY-MM-DD" placeholder="请选择录入日期" clearable /></el-form-item>
        <el-form-item label="部门"><el-select v-model="filters.departmentIds" placeholder="输入部门名称搜索" multiple clearable filterable collapse-tags collapse-tags-tooltip :max-collapse-tags="1"><el-option v-for="item in departments" :key="item.id" :label="item.name" :value="item.id" /></el-select></el-form-item>
        <el-form-item label="姓名" data-filter-text><el-input v-model="filters.name" placeholder="请输入姓名" /></el-form-item>
        <el-form-item label="事由" data-filter-text><el-input v-model="filters.reason" placeholder="请输入事由关键字" clearable /></el-form-item>
        <el-form-item label="备注" data-filter-text><el-input v-model="filters.remark" placeholder="请输入备注关键字" clearable /></el-form-item>
        <el-form-item label="状态"><el-select v-model="filters.status" placeholder="全部" clearable><el-option label="已完成" value="Completed" /><el-option label="待处理" value="Pending" /></el-select></el-form-item>
        <div class="filter-actions"><el-button type="primary" :icon="Search" @click="query">查询</el-button><el-button :icon="Refresh" @click="reset">重置</el-button><el-button :icon="Download" :loading="exporting" plain @click="exportExcel">导出 Excel</el-button><el-button type="primary" :icon="Plus" @click="openCreate">新增记录</el-button><el-button :icon="Upload" :loading="importing" plain @click="fileInput?.click()">导入 Excel</el-button><input ref="fileInput" class="file-input" type="file" accept=".xls,.xlsx,.xlsm" @change="importExcel" /></div>
      </div></el-form>
    </section>
    <section class="card table-card">
      <div class="table-card__head"><h2>▤　考核记录 <small>共 {{ total }} 条记录</small></h2><el-button :icon="Refresh" :loading="loading" @click="loadRecords">刷新</el-button></div>
      <el-table ref="recordsTableRef" v-loading="loading" class="records-table" :data="records" stripe border :default-sort="{ prop: sortBy, order: sortOrder }" @sort-change="changeSort" @selection-change="changeSelection" @row-click="toggleRowSelection"><el-table-column type="selection" width="44" /><el-table-column prop="assessmentDate" label="单据日期" sortable="custom" :sort-orders="['ascending', 'descending']" min-width="190"><template #default="{ row }">{{ formatAssessmentDate(row) }}</template></el-table-column><el-table-column prop="detailDate" label="录入日期" sortable="custom" :sort-orders="['ascending', 'descending']" min-width="135" /><el-table-column prop="departmentName" label="部门" sortable="custom" :sort-orders="['ascending', 'descending']" min-width="110" /><el-table-column prop="personName" label="姓名" sortable="custom" :sort-orders="['ascending', 'descending']" min-width="140" /><el-table-column prop="amount" label="金额（元）" sortable="custom" :sort-orders="['ascending', 'descending']" min-width="155"><template #default="{ row }">{{ row.amountDisplay }}</template></el-table-column><el-table-column prop="reason" label="事由" min-width="170" /><el-table-column prop="remark" label="备注" min-width="120" /><el-table-column label="状态" min-width="110"><template #default="{ row }"><el-tag :type="row.status === 'Completed' ? 'success' : 'warning'" round>{{ row.status === 'Completed' ? (row.paymentMethodName || '收款方式未设置') : '待处理' }}</el-tag></template></el-table-column><el-table-column label="操作" width="120"><template #default="{ row }"><el-button link type="primary" @click.stop="openEdit(row)">编辑</el-button><el-button link type="danger" @click.stop="removeRecord(row)">删除</el-button></template></el-table-column></el-table>
      <div class="pagination"><div class="pagination__summary"><span>已选 {{ selectedRecords.length }} 条数据，金额合计：<strong>¥{{ formatMoney(selectedAmountTotal) }}</strong></span></div><el-pagination v-model:current-page="page" v-model:page-size="pageSize" background layout="prev, pager, next, sizes" :total="total" @current-change="loadRecords" @size-change="changePageSize" /></div>
    </section>
    <el-dialog v-model="dialogVisible" :title="addingFromImport ? (editingStagingId === null ? '添加待导入记录' : '编辑待导入记录') : (editingId === null ? '新增考核记录' : '编辑考核记录')" width="min(640px, 92vw)" destroy-on-close>
      <el-form ref="formRef" class="record-form" :model="recordForm" :rules="rules" label-position="top" @keyup.enter.capture="focusNextFormItem">
        <div class="record-form__grid">
          <el-form-item label="单据日期类型"><el-radio-group v-model="recordForm.assessmentDateType"><el-radio-button value="Fixed">固定日期</el-radio-button><el-radio-button value="Range">日期区间</el-radio-button></el-radio-group></el-form-item>
          <el-form-item v-if="recordForm.assessmentDateType === 'Fixed'" label="单据日期" prop="assessmentDate"><el-date-picker v-model="recordForm.assessmentDate" type="date" value-format="YYYY-MM-DD" placeholder="请选择单据日期" /></el-form-item>
          <el-form-item v-else label="单据日期区间" required><el-date-picker :model-value="[recordForm.assessmentDate, recordForm.assessmentEndDate]" type="daterange" value-format="YYYY-MM-DD" start-placeholder="开始日期" end-placeholder="结束日期" @update:model-value="updateAssessmentRange" /></el-form-item>
          <el-form-item label="录入日期" prop="detailDate"><el-date-picker v-model="recordForm.detailDate" type="date" value-format="YYYY-MM-DD" placeholder="请选择录入日期" /></el-form-item>
          <el-form-item label="部门" required data-field="department"><el-select v-model="departmentSelection" placeholder="输入部门名称或编码搜索" filterable :filter-method="filterDepartments" @visible-change="handleDepartmentVisibleChange"><el-option v-for="item in filteredEnabledDepartments" :key="item.id" :label="item.name" :value="item.id" /><template v-if="addingFromImport"><el-option v-for="name in filteredPendingDepartmentNames" :key="`pending:${name}`" :label="`${name}（待创建）`" :value="`pending:${name}`" /></template></el-select></el-form-item>
          <el-form-item label="姓名" prop="personName" data-field="person-name"><el-autocomplete v-model="recordForm.personName" :fetch-suggestions="queryPersonNameSuggestions" placeholder="请输入姓名" maxlength="50" clearable /></el-form-item>
          <el-form-item label="考核金额（元）" prop="amount"><el-input-number v-model="recordForm.amount" :min="0.01" :precision="2" :step="10" controls-position="right" placeholder="请输入考核金额" /></el-form-item>
        </div>
        <el-form-item label="考核事由" prop="reason"><el-input v-model="recordForm.reason" placeholder="请输入考核事由" maxlength="300" show-word-limit /></el-form-item>
        <el-form-item label="备注"><el-input v-model="recordForm.remark" type="textarea" :rows="3" placeholder="请输入备注信息" maxlength="500" show-word-limit /></el-form-item>
        <el-form-item label="收款方式"><el-radio-group v-model="recordForm.paymentMethodId"><el-radio :value="0">暂不选择</el-radio><el-radio v-for="item in enabledPaymentMethods" :key="item.id" :value="item.id">{{ item.name }}</el-radio></el-radio-group></el-form-item>
      </el-form>
      <template #footer><el-checkbox v-if="!addingFromImport && editingId === null" v-model="batchAdding" class="batch-add-checkbox">批量添加</el-checkbox><el-button @click="dialogVisible = false">取消</el-button><el-button data-record-save type="primary" :loading="saving" @click="saveRecord">保存</el-button></template>
    </el-dialog>
    <el-dialog v-model="importResultVisible" title="Excel 导入结果" width="min(1460px, 98vw)" destroy-on-close :close-on-click-modal="false" :before-close="beforeCloseImport">
      <template v-if="lastImportResult">
        <div class="import-summary"><div><el-tag type="success">待确认 {{ lastImportResult.staged }} 条</el-tag><el-tag v-if="lastImportResult.failed" type="danger">识别失败 {{ lastImportResult.failed }} 条</el-tag><span v-if="pendingDepartmentNames.length">确认后自动新增或恢复部门：{{ pendingDepartmentNames.join('、') }}</span></div><el-button type="primary" :icon="Plus" @click="openManualImportAdd">手动添加</el-button></div>
        <h3 class="import-section-title">待确认导入的数据</h3>
        <el-table :data="orderedStagedRows" stripe border max-height="480" empty-text="没有可确认导入的数据">
          <el-table-column prop="rowNumber" label="来源" width="82"><template #default="{ row }">{{ row.rowNumber > 0 ? `第 ${row.rowNumber} 行` : '手动添加' }}</template></el-table-column>
          <el-table-column label="单据日期" width="190"><template #default="{ row }">{{ formatAssessmentDate(row) }}</template></el-table-column>
          <el-table-column prop="detailDate" label="录入日期" width="115" />
          <el-table-column prop="departmentName" label="部门" min-width="120" />
          <el-table-column prop="personName" label="姓名" min-width="145" />
          <el-table-column label="金额（元）" min-width="150"><template #default="{ row }">{{ row.amountDisplay }}</template></el-table-column>
          <el-table-column prop="reason" label="事由" min-width="180" show-overflow-tooltip />
          <el-table-column prop="remark" label="备注" min-width="130" show-overflow-tooltip />
          <el-table-column prop="paymentMethodName" label="收款方式" width="100"><template #default="{ row }">{{ row.paymentMethodName || '未识别' }}</template></el-table-column>
          <el-table-column label="操作" fixed="right" width="120"><template #default="{ row }"><el-button link type="primary" @click="openImportedEdit(row)">编辑</el-button><el-button link type="danger" @click="removeImportedRecord(row)">删除</el-button></template></el-table-column>
        </el-table>
        <template v-if="lastImportResult.failedRows?.length">
          <h3 class="import-section-title import-section-title--error">导入失败的数据</h3>
          <el-table :data="lastImportResult.failedRows" stripe border max-height="300">
            <el-table-column prop="rowNumber" label="Excel 行" width="82" />
            <el-table-column prop="assessmentDate" label="单据日期原文" width="135" />
            <el-table-column prop="detailDate" label="录入日期" width="105" />
            <el-table-column prop="departmentName" label="部门原文" min-width="120" />
            <el-table-column prop="personName" label="姓名原文" min-width="130" />
            <el-table-column prop="amountText" label="金额原文" min-width="135" />
            <el-table-column prop="reason" label="事由原文" min-width="150" show-overflow-tooltip />
            <el-table-column prop="message" label="失败原因" min-width="190" show-overflow-tooltip />
            <el-table-column label="操作" fixed="right" width="80"><template #default="{ row }"><el-button link type="primary" @click="openFailedAdd(row)">添加</el-button></template></el-table-column>
          </el-table>
        </template>
      </template>
      <template #footer><el-button :disabled="confirming" @click="cancelImport">取消导入</el-button><el-button type="primary" :loading="confirming" :disabled="!lastImportResult?.staged" @click="confirmImport">确认导入</el-button></template>
    </el-dialog>
  </div>
</template>

<style scoped>
.filter-card { padding: 12px 18px; }.filter-card h2, .table-card h2 { margin: 0 0 14px; font-size: 17px; }.filter-grid { display: grid; grid-template-columns: repeat(12, minmax(0, 1fr)); column-gap: 16px; row-gap: 2px; }.filter-grid > .el-form-item:nth-child(-n+4) { grid-column: span 3; }.filter-grid > .el-form-item:nth-child(n+5) { grid-column: span 2; }.filter-grid :deep(.el-form-item) { margin-bottom: 6px; }.filter-grid :deep(.el-form-item__label) { height: auto; margin-bottom: 3px; line-height: 18px; }.filter-grid :deep(.el-select), .filter-grid :deep(.el-date-editor) { width: 100%; }.filter-actions { grid-column: span 6; display: flex; align-items: flex-end; justify-content: flex-end; flex-wrap: wrap; gap: 6px; padding-bottom: 6px; }.filter-actions .el-button { min-width: 92px; height: 32px; margin: 0; padding: 0 8px; }.file-input { display: none; }.table-card { margin-top: 14px; padding: 16px; overflow-x: auto; }.table-card__head { display: flex; align-items: center; justify-content: space-between; }.table-card__head h2 { margin: 0; }.table-card small { margin-left: 8px; color: var(--primary); font-size: 13px; font-weight: 400; }.records-table :deep(.el-table__body tr) { cursor: pointer; }.pagination { display: flex; align-items: center; justify-content: space-between; margin-top: 16px; color: var(--text-muted); font-size: 13px; }.pagination__summary { display: flex; align-items: center; gap: 20px; }.pagination__summary strong { color: var(--primary); font-size: 15px; }
.record-form { padding: 2px 4px 0; }.record-form__grid { display: grid; grid-template-columns: 1fr 1fr; column-gap: 18px; }.record-form :deep(.el-select), .record-form :deep(.el-autocomplete), .record-form :deep(.el-date-editor), .record-form :deep(.el-input-number) { width: 100%; }
.batch-add-checkbox { margin-right: 18px; }
.import-summary { display: flex; align-items: center; justify-content: space-between; flex-wrap: wrap; gap: 10px; margin-bottom: 14px; }.import-summary > div { display: flex; align-items: center; flex-wrap: wrap; gap: 10px; }.import-summary span { color: var(--text-muted); font-size: 13px; }.import-section-title { margin: 14px 0 10px; font-size: 15px; }.import-section-title--error { color: var(--el-color-danger); }
@media (max-width: 1100px) { .filter-grid { grid-template-columns: repeat(2, minmax(0, 1fr)); }.filter-grid > .el-form-item:nth-child(n) { grid-column: span 1; }.filter-actions { grid-column: 1 / -1; justify-content: flex-start; } }
@media (max-width: 650px) { .filter-grid, .record-form__grid { grid-template-columns: 1fr; }.pagination { align-items: flex-start; flex-direction: column; gap: 12px; } }
</style>
