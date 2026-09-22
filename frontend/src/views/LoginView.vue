<script setup lang="ts">
import { Lock, Refresh, User, Warning } from '@element-plus/icons-vue'
import { ElMessage } from 'element-plus'
import { reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import AppLogo from '@/components/AppLogo.vue'
import { useUserStore } from '@/stores/user'

const router = useRouter()
const route = useRoute()
const userStore = useUserStore()
const loading = ref(false)
const form = reactive({ account: 'admin', password: 'Admin123!', captcha: '', remember: true })

async function submit() {
  if (!form.account.trim() || !form.password) {
    ElMessage.warning('请输入账号和密码')
    return
  }
  loading.value = true
  try {
    await userStore.login(form.account, form.password, form.remember)
    const redirect = typeof route.query.redirect === 'string' ? route.query.redirect : '/dashboard'
    await router.push(redirect)
  } catch {
    // 错误消息由 HTTP 拦截器统一显示
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <main class="login-page">
    <div class="login-page__orb orb-one" /><div class="login-page__orb orb-two" />
    <header><AppLogo /><span>🎧　帮助支持　　|　　简体中文⌄</span></header>
    <section class="login-content">
      <div class="intro">
        <h1>让每一笔记录，都清晰可查</h1>
        <p>高效的考核管理工具，助力企业规范管理、降本增效</p>
        <div class="features">
          <div><i>▤</i><span><strong>考核记录管理</strong><small>全流程规范记录</small></span></div>
          <div><i>▥</i><span><strong>首页数据概览</strong><small>关键数据清晰呈现</small></span></div>
          <div><i>Ⅹ</i><span><strong>Excel 导入导出</strong><small>高效便捷处理</small></span></div>
        </div>
        <div class="illustration"><div class="coin">¥</div><div class="paper"><b /><b /><b /></div><div class="chart">▥</div></div>
      </div>
      <el-card class="login-card" shadow="never">
        <h2>欢迎登录</h2><p>请输入账号信息进入系统</p>
        <el-form :model="form" size="large" @keyup.enter="submit">
          <el-form-item><el-input v-model="form.account" :prefix-icon="User" placeholder="请输入账号 / 手机号" /></el-form-item>
          <el-form-item><el-input v-model="form.password" type="password" show-password :prefix-icon="Lock" placeholder="请输入密码" /></el-form-item>
          <el-form-item><div class="captcha-row"><el-input v-model="form.captcha" :prefix-icon="Warning" placeholder="请输入验证码" /><div class="captcha">7 8 K Q</div><el-button :icon="Refresh" /></div></el-form-item>
          <div class="login-options"><el-checkbox v-model="form.remember">记住我</el-checkbox><el-link type="primary" underline="never">忘记密码？</el-link></div>
          <el-button class="login-button" type="primary" :loading="loading" @click="submit">登录</el-button>
          <el-button class="contact-button" plain>♣　联系管理员</el-button>
        </el-form>
        <div class="login-card__footer"><span />♢　仅限内部授权人员使用<span /></div>
      </el-card>
    </section>
    <footer>© 2026 考核管理系统（财务简版）All Rights Reserved.</footer>
  </main>
</template>

<style scoped>
.login-page { position: relative; min-height: 100vh; overflow: hidden; padding: 28px max(5vw, 28px); color: var(--text-primary); background: radial-gradient(circle at 15% 75%, rgba(77,226,169,.35), transparent 35%), linear-gradient(120deg, #edfffa, #f9ffff 58%, #eefaf7); }.login-page__orb { position: absolute; border-radius: 50%; border: 1px solid rgba(15,170,111,.1); background: rgba(160,243,213,.12); }.orb-one { top: -210px; left: 34%; width: 520px; height: 520px; }.orb-two { left: -260px; bottom: -280px; width: 720px; height: 720px; }
header { position: relative; z-index: 2; display: flex; align-items: center; justify-content: space-between; }header > span { color: var(--text-secondary); font-size: 13px; }.login-content { position: relative; z-index: 2; display: grid; grid-template-columns: 1.25fr minmax(380px, 500px); align-items: center; gap: 7vw; width: min(1380px, 100%); min-height: calc(100vh - 150px); margin: auto; }.intro h1 { margin: 0 0 12px; color: #063c35; font-size: clamp(32px, 3vw, 48px); }.intro > p { color: #4c7775; font-size: 18px; }.features { display: flex; gap: 42px; margin-top: 38px; }.features > div { display: flex; align-items: center; gap: 12px; }.features i { display: grid; place-items: center; width: 54px; height: 54px; border-radius: 50%; color: #00995f; font-style: normal; font-size: 24px; background: rgba(255,255,255,.75); }.features span { display: grid; gap: 5px; }.features small { color: #668482; }
.illustration { position: relative; width: 390px; height: 250px; margin: 48px auto 0; border-radius: 50%; background: linear-gradient(180deg, rgba(255,255,255,.5), rgba(26,190,127,.22)); box-shadow: 0 28px 0 rgba(0,160,100,.08); }.paper { position: absolute; left: 125px; top: 28px; width: 150px; height: 190px; padding: 55px 30px; border-radius: 18px; background: rgba(255,255,255,.88); transform: rotate(-5deg); box-shadow: 0 18px 30px rgba(7,129,94,.13); }.paper b { display: block; height: 12px; margin-bottom: 15px; border-radius: 8px; background: #8de1c2; }.coin { position: absolute; z-index: 2; left: 72px; bottom: 16px; display: grid; place-items: center; width: 92px; height: 108px; border-radius: 14px; color: white; font-size: 56px; font-weight: 700; background: linear-gradient(145deg,#19bc76,#007b50); box-shadow: 0 14px 26px rgba(0,130,79,.22); }.chart { position: absolute; right: 35px; top: 58px; color: #09a66a; font-size: 48px; }
.login-card { padding: 28px; border: 0; border-radius: 18px; box-shadow: 0 22px 60px rgba(37,94,84,.12); }.login-card h2 { margin: 0; text-align: center; font-size: 30px; }.login-card > :deep(.el-card__body) > p { margin: 10px 0 30px; color: var(--text-muted); text-align: center; }.login-card :deep(.el-input__wrapper) { min-height: 50px; }.captcha-row { display: grid; grid-template-columns: 1fr 116px 50px; gap: 8px; width: 100%; }.captcha { display: grid; place-items: center; border: 1px solid var(--border-color); border-radius: 6px; color: #37534c; font-weight: 700; font-size: 20px; letter-spacing: 6px; background: #f7fbfa; }.login-options { display: flex; justify-content: space-between; margin: 2px 0 24px; }.login-button, .contact-button { width: 100%; height: 52px; margin: 0 0 12px; font-size: 16px; }.contact-button { margin-left: 0; color: var(--primary); border-color: #8bd4bb; }.login-card__footer { display: flex; align-items: center; gap: 18px; margin-top: 24px; color: var(--text-muted); font-size: 12px; }.login-card__footer span { flex: 1; height: 1px; background: var(--border-color); }footer { position: absolute; z-index: 2; bottom: 24px; left: 0; right: 0; color: #6f8a89; text-align: center; font-size: 12px; }
@media (max-width: 1050px) { .login-content { grid-template-columns: 1fr minmax(360px, 470px); gap: 35px; }.features { gap: 16px; flex-wrap: wrap; }.illustration { transform: scale(.8); transform-origin: top center; } }
@media (max-width: 800px) { .login-page { padding: 20px; }.login-content { display: block; padding: 50px 0 80px; }.intro { display: none; }.login-card { max-width: 480px; margin: auto; }header .app-logo { transform: scale(.85); transform-origin: left; }header > span { display: none; } }
</style>
