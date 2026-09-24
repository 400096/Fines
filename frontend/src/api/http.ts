import axios, { type AxiosRequestConfig } from 'axios'
import { ElMessage } from 'element-plus'

export const TOKEN_KEY = 'fine-admin-token'
export const USER_KEY = 'fine-admin-user'

/** 从“记住登录”和当前会话两种存储位置读取访问令牌。 */
export function getStoredToken() {
  return localStorage.getItem(TOKEN_KEY) ?? sessionStorage.getItem(TOKEN_KEY) ?? ''
}

/** 同时清除 localStorage 与 sessionStorage，避免残留会话互相覆盖。 */
export function clearStoredSession() {
  localStorage.removeItem(TOKEN_KEY)
  localStorage.removeItem(USER_KEY)
  sessionStorage.removeItem(TOKEN_KEY)
  sessionStorage.removeItem(USER_KEY)
}

// 所有业务接口都复用此实例；部署时可通过 VITE_API_BASE_URL 指向后端地址。
const http = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL ?? '/api',
  timeout: 15_000,
})

// 请求发出前自动携带 JWT，页面代码无需重复拼接 Authorization 头。
http.interceptors.request.use((config) => {
  const token = getStoredToken()
  if (token) config.headers.Authorization = `Bearer ${token}`
  return config
})

// 将后端校验错误转换为统一提示；401 时清理失效会话并跳回登录页。
http.interceptors.response.use(
  (response) => response,
  (error: unknown) => {
    let message = '请求失败，请稍后重试'
    if (axios.isAxiosError(error)) {
      const data = error.response?.data as { message?: string; title?: string; errors?: Record<string, string[]> } | undefined
      const validationMessage = data?.errors ? Object.values(data.errors).flat()[0] : undefined
      message = data?.message ?? validationMessage ?? data?.title ?? message

      if (error.response?.status === 401 && !error.config?.url?.includes('/auth/login')) {
        clearStoredSession()
        const currentRoute = window.location.hash.slice(1) || '/dashboard'
        if (!window.location.hash.startsWith('#/login')) {
          window.location.assign(`${import.meta.env.BASE_URL}#/login?redirect=${encodeURIComponent(currentRoute)}`)
        }
        message = '登录已失效，请重新登录'
      }
    }
    ElMessage.error(message)
    return Promise.reject(error)
  },
)

export async function request<T>(config: AxiosRequestConfig): Promise<T> {
  const response = await http.request<T>(config)
  return response.data
}

export default http
