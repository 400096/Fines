import http, { request } from './http'
import type {
  AssessmentRecord,
  AssessmentRecordQuery,
  CreateUser,
  DashboardResponse,
  Department,
  DepartmentQuery,
  ExcelImportResult,
  ExcelImportConfirmResult,
  ExcelStagedRow,
  LoginResponse,
  PagedResult,
  PaymentMethod,
  SaveAssessmentRecord,
  SaveDepartment,
  SavePaymentMethod,
  SaveStagedAssessmentRecord,
  SystemUser,
  UpdateAssessmentRecord,
  UpdateUser,
  UserQuery,
} from './types'

// 登录接口。
export const authApi = {
  login: (username: string, password: string) => request<LoginResponse>({
    method: 'POST', url: '/auth/login', data: { username, password },
  }),
}

// 考核记录接口，包含正式记录、Excel 暂存确认以及导出功能。
export const assessmentApi = {
  list: (params: AssessmentRecordQuery) => request<PagedResult<AssessmentRecord>>({
    method: 'GET', url: '/assessment-records', params, paramsSerializer: { indexes: null },
  }),
  personNameSuggestions: (keyword: string, limit = 10) => request<string[]>({
    method: 'GET', url: '/assessment-records/person-name-suggestions', params: { keyword, limit },
  }),
  create: (data: SaveAssessmentRecord) => request<AssessmentRecord>({
    method: 'POST', url: '/assessment-records', data,
  }),
  update: (id: number, data: UpdateAssessmentRecord) => request<AssessmentRecord>({
    method: 'PUT', url: `/assessment-records/${id}`, data,
  }),
  remove: (id: number) => request<void>({ method: 'DELETE', url: `/assessment-records/${id}` }),
  importExcel: (file: File) => {
    const data = new FormData()
    data.append('file', file)
    return request<ExcelImportResult>({ method: 'POST', url: '/assessment-records/import', data })
  },
  addStagedRow: (batchId: number, data: SaveStagedAssessmentRecord) => request<ExcelStagedRow>({
    method: 'POST', url: `/assessment-records/import/${batchId}/rows`, data,
  }),
  updateStagedRow: (batchId: number, stagingId: number, data: SaveStagedAssessmentRecord) => request<ExcelStagedRow>({
    method: 'PUT', url: `/assessment-records/import/${batchId}/rows/${stagingId}`, data,
  }),
  removeStagedRow: (batchId: number, stagingId: number) => request<void>({
    method: 'DELETE', url: `/assessment-records/import/${batchId}/rows/${stagingId}`,
  }),
  confirmImport: (batchId: number) => request<ExcelImportConfirmResult>({
    method: 'POST', url: `/assessment-records/import/${batchId}/confirm`,
  }),
  cancelImport: (batchId: number) => request<void>({
    method: 'DELETE', url: `/assessment-records/import/${batchId}`,
  }),
  exportExcel: async (params: AssessmentRecordQuery) => {
    const response = await http.get<Blob>('/assessment-records/export', {
      params,
      paramsSerializer: { indexes: null },
      responseType: 'blob',
    })
    const disposition = String(response.headers['content-disposition'] ?? '')
    const utf8Name = disposition.match(/filename\*=UTF-8''([^;]+)/i)?.[1]
    return {
      blob: response.data,
      filename: utf8Name ? decodeURIComponent(utf8Name) : `考核记录_${new Date().toISOString().slice(0, 10)}.xlsx`,
    }
  },
}

// 部门管理接口，列表接口同时供下拉选择和分页管理页面使用。
export const departmentApi = {
  list: (params?: { keyword?: string; isEnabled?: boolean }) => request<Department[]>({
    method: 'GET', url: '/departments', params,
  }),
  listPaged: (params: DepartmentQuery) => request<PagedResult<Department>>({
    method: 'GET', url: '/departments/paged', params,
  }),
  create: (data: SaveDepartment) => request<Department>({ method: 'POST', url: '/departments', data }),
  update: (id: number, data: SaveDepartment) => request<Department>({ method: 'PUT', url: `/departments/${id}`, data }),
  remove: (id: number) => request<void>({ method: 'DELETE', url: `/departments/${id}` }),
}

// 收款方式管理接口。
export const paymentMethodApi = {
  list: (params?: { keyword?: string; isEnabled?: boolean }) => request<PaymentMethod[]>({
    method: 'GET', url: '/payment-methods', params,
  }),
  create: (data: SavePaymentMethod) => request<PaymentMethod>({ method: 'POST', url: '/payment-methods', data }),
  update: (id: number, data: SavePaymentMethod) => request<PaymentMethod>({ method: 'PUT', url: `/payment-methods/${id}`, data }),
  remove: (id: number) => request<void>({ method: 'DELETE', url: `/payment-methods/${id}` }),
}

// 系统用户管理接口。
export const userApi = {
  list: (params: UserQuery) => request<PagedResult<SystemUser>>({ method: 'GET', url: '/users', params }),
  create: (data: CreateUser) => request<SystemUser>({ method: 'POST', url: '/users', data }),
  update: (id: number, data: UpdateUser) => request<SystemUser>({ method: 'PUT', url: `/users/${id}`, data }),
  remove: (id: number) => request<void>({ method: 'DELETE', url: `/users/${id}` }),
}

// 首页汇总接口。
export const dashboardApi = {
  get: () => request<DashboardResponse>({ method: 'GET', url: '/dashboard' }),
}
