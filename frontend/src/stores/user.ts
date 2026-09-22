import { defineStore } from 'pinia'
import { authApi } from '@/api/services'
import { clearStoredSession, getStoredToken, TOKEN_KEY, USER_KEY } from '@/api/http'

interface UserInfo {
  userId: number
  username: string
  name: string
  role: string
  expiresAt: string
}

function loadUserInfo(): UserInfo {
  const raw = localStorage.getItem(USER_KEY) ?? sessionStorage.getItem(USER_KEY)
  if (raw) {
    try { return JSON.parse(raw) as UserInfo } catch { clearStoredSession() }
  }
  return { userId: 0, username: '', name: '访客', role: '', expiresAt: '' }
}

export const useUserStore = defineStore('user', {
  state: () => ({
    token: getStoredToken(),
    userInfo: loadUserInfo(),
  }),
  actions: {
    async login(username: string, password: string, remember: boolean) {
      const result = await authApi.login(username.trim(), password)
      clearStoredSession()
      this.token = result.token
      this.userInfo = {
        userId: result.userId,
        username: result.username,
        name: result.name,
        role: result.role,
        expiresAt: result.expiresAt,
      }
      const storage = remember ? localStorage : sessionStorage
      storage.setItem(TOKEN_KEY, result.token)
      storage.setItem(USER_KEY, JSON.stringify(this.userInfo))
    },
    logout() {
      this.token = ''
      this.userInfo = { userId: 0, username: '', name: '访客', role: '', expiresAt: '' }
      clearStoredSession()
    },
  },
})
