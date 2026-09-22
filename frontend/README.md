# 考核管理系统前端

基于 Vue 3、TypeScript、Vite、Element Plus、Pinia、Vue Router、Axios 与 ECharts 的管理端项目，页面数据已接入 ASP.NET Core 后端 API。

## 启动

```bash
npm install
npm run dev
```

默认地址：`http://localhost:5173`。开发环境下 `/api` 会代理至 `http://localhost:8080`。

请先启动 `backend`。首次初始化后的默认账号为 `admin`，密码为 `Admin123!`。

## 页面

- `/login`：登录页
- `/dashboard`：首页仪表盘
- `/fine-records`：考核记录
- `/departments`：部门管理
- `/payment-methods`：收款方式管理
- `/users`：用户管理

登录状态、请求错误和 JWT Token 由 `src/api/http.ts` 与 `src/stores/user.ts` 统一管理。需要连接其他后端地址时，可设置 `VITE_API_BASE_URL`。
