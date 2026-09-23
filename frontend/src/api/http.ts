import axios, { type AxiosRequestConfig } from 'axios'
import { ElMessage } from 'element-plus'

export const TOKEN_KEY = 'fine-admin-token'
export const USER_KEY = 'fine-admin-user'

export function getStoredToken() {
  return localStorage.getItem(TOKEN_KEY) ?? sessionStorage.getItem(TOKEN_KEY) ?? ''
}

export function clearStoredSession() {
  localStorage.removeItem(TOKEN_KEY)
  localStorage.removeItem(USER_KEY)
  sessionStorage.removeItem(TOKEN_KEY)
  sessionStorage.removeItem(USER_KEY)
}

const http = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL ?? '/api',
  timeout: 15_000,
})

http.interceptors.request.use((config) => {
  const token = getStoredToken()
  if (token) config.headers.Authorization = `Bearer ${token}`
  return config
})

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
