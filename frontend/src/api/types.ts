export type AssessmentStatus = 'Pending' | 'Completed'
export type AssessmentDateType = 'Fixed' | 'Range'

export interface LoginResponse {
  token: string
  expiresAt: string
  userId: number
  username: string
  name: string
  role: string
}

export interface PagedResult<T> {
  items: T[]
  total: number
  page: number
  pageSize: number
}

export interface AssessmentRecord {
  id: number
  assessmentDateType: AssessmentDateType
  assessmentDate: string
  assessmentEndDate: string | null
  detailDate: string
  departmentId: number
  departmentName: string
  personName: string
  amount: number
  amountDisplay: string
  reason: string
  remark: string | null
  status: AssessmentStatus
  paymentMethodId: number | null
  paymentMethodName: string | null
  createdAt: string
  updatedAt: string
}

export interface AssessmentRecordQuery {
  dateFrom?: string
  dateTo?: string
  detailDateFrom?: string
  detailDateTo?: string
  departmentId?: number
  departmentIds?: number[]
  name?: string
  status?: AssessmentStatus
  sortBy?: 'assessmentDate' | 'detailDate' | 'departmentName' | 'personName' | 'amount'
  sortOrder?: 'ascending' | 'descending'
  page?: number
  pageSize?: number
}

export interface SaveAssessmentRecord {
  assessmentDateType: AssessmentDateType
  assessmentDate: string
  assessmentEndDate?: string | null
  detailDate: string
  departmentId: number
  personName: string
  amount: number
  reason: string
  remark?: string | null
}

export interface UpdateAssessmentRecord extends SaveAssessmentRecord {
  paymentMethodId?: number | null
}

export interface SaveStagedAssessmentRecord {
  assessmentDateType: AssessmentDateType
  assessmentDate: string
  assessmentEndDate?: string | null
  detailDate: string
  departmentId?: number | null
  departmentName: string
  personName: string
  amount: number
  reason: string
  remark?: string | null
  paymentMethodId?: number | null
}

export interface ExcelImportResult {
  batchId: number
  staged: number
  failed: number
  errors: string[]
  pendingDepartments?: string[]
  stagedRows?: ExcelStagedRow[]
  failedRows?: ExcelImportFailedRow[]
}

export interface ExcelStagedRow {
  stagingId: number
  rowNumber: number
  assessmentDateType: AssessmentDateType
  assessmentDate: string
  assessmentEndDate: string | null
  detailDate: string
  departmentId: number
  departmentName: string
  personName: string
  amount: number
  amountDisplay: string
  reason: string
  remark: string | null
  paymentMethodId: number | null
  paymentMethodName: string | null
}

export interface ExcelImportConfirmResult {
  imported: number
  createdDepartments: string[]
}

export interface ExcelImportFailedRow {
  rowNumber: number
  message: string
  assessmentDate: string | null
  detailDate: string | null
  departmentName: string | null
  personName: string | null
  amountText: string | null
  reason: string | null
  remark: string | null
  paymentMethodName: string | null
}

export interface Department {
  id: number
  name: string
  code: string
  sortOrder: number
  isEnabled: boolean
  manager: string | null
  remark: string | null
  updatedAt: string
}

export interface SaveDepartment {
  name: string
  code: string
  sortOrder: number
  isEnabled: boolean
  manager?: string | null
  remark?: string | null
}

export interface DepartmentQuery {
  keyword?: string
  isEnabled?: boolean
  page?: number
  pageSize?: number
}

export interface PaymentMethod {
  id: number
  name: string
  sortOrder: number
  isEnabled: boolean
  description: string | null
  updatedAt: string
}

export interface SavePaymentMethod {
  name: string
  sortOrder: number
  isEnabled: boolean
  description?: string | null
}

export interface SystemUser {
  id: number
  username: string
  name: string
  phone: string | null
  role: string
  isEnabled: boolean
  lastLoginAt: string | null
  createdAt: string
}

export interface UserQuery {
  keyword?: string
  role?: string
  isEnabled?: boolean
  page?: number
  pageSize?: number
}

export interface CreateUser {
  username: string
  password: string
  name: string
  phone?: string | null
  role: string
  isEnabled: boolean
}

export interface UpdateUser {
  password?: string | null
  name: string
  phone?: string | null
  role: string
  isEnabled: boolean
}

export interface DashboardResponse {
  summary: {
    todayCount: number
    monthAmount: number
    monthCount: number
    pendingCount: number
    personCount: number
    departmentCount: number
    monthGrowthPercent: number
  }
  trend: Array<{ date: string; amount: number }>
  distribution: Array<{ name: string; amount: number; count: number }>
  recentRecords: Array<{
    id: number
    assessmentDateType: AssessmentDateType
    assessmentDate: string
    assessmentEndDate: string | null
    detailDate: string
    departmentName: string
    personName: string
    amount: number
    amountDisplay: string
    reason: string
    status: AssessmentStatus
    paymentMethodName: string | null
  }>
}
