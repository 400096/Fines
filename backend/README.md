# 考核管理系统后端

ASP.NET Core 8 Web API，使用 EF Core + MySQL、JWT Bearer、ClosedXML 和 Swagger。

## 环境要求

- .NET 8 SDK
- MySQL 8.0 或更高版本

## 初始化与启动

1. 在 MySQL 中创建应用账号（也可以在本地开发时暂时使用 `root`），然后修改 `appsettings.json` 中的数据库连接字符串和 `Jwt:Key`。也可以通过环境变量 `ConnectionStrings__DefaultConnection` 覆盖连接字符串。
2. 可任选一种初始化方式：
   - 执行 `mysql -u root -p < database/init.sql`；
   - 保持 `Database:InitializeOnStartup=true`，应用首次启动时自动建表并写入基础数据。

`init.sql` 已包含所有表和字段注释。已有数据库可额外执行 `mysql -u root -p fine_management < database/add_comments.sql` 补充注释，该脚本不会清空业务数据。

系统不会预置部门。部门可以在部门管理中手动新增，也会在导入考核记录时按 Excel 中的部门名称自动创建。
3. 执行：

```bash
dotnet restore
dotnet run
```

4. 打开 `http://localhost:8080/swagger`。

## 日志

- 后端会自动在程序运行目录的 `logs` 文件夹中写入日志。
- 日志按自然日滚动，每天一个文件，文件名格式为 `backend-YYYYMMDD.log`。
- 默认保留最近 30 个日志文件，自动清理更早的文件。
- 日志包含接口请求方法、地址、响应状态码、耗时，以及程序异常信息。

默认管理员：`admin` / `Admin123!`。首次登录后请立即修改密码。

## 主要接口

| 方法 | 地址 | 说明 |
|---|---|---|
| POST | `/api/auth/login` | 登录并获取 JWT |
| GET | `/api/dashboard` | 首页数据汇总、趋势、事由占比与最近记录 |
| GET/POST | `/api/assessment-records` | 查询、新增考核记录 |
| GET/PUT/DELETE | `/api/assessment-records/{id}` | 详情、编辑、删除考核记录 |
| POST | `/api/assessment-records/import` | 导入 Excel |
| GET | `/api/assessment-records/export` | 导出 Excel |
| GET/POST | `/api/departments` | 部门查询、新增 |
| GET/PUT/DELETE | `/api/departments/{id}` | 部门详情、编辑、删除 |
| GET/POST | `/api/payment-methods` | 收款方式查询、新增 |
| GET/PUT/DELETE | `/api/payment-methods/{id}` | 收款方式详情、编辑、删除 |
| GET/POST | `/api/users` | 用户查询、新增 |
| GET/PUT/DELETE | `/api/users/{id}` | 用户详情、编辑、删除 |

除登录外，接口均需要 `Authorization: Bearer <token>`。用户管理接口仅允许“超级管理员”访问。

当前后端 CORS 策略允许任意来源、请求头和 HTTP 方法；接口身份认证和角色权限不受影响。

## Excel 导入导出格式

以《九月一日考核.xls》的 6 列格式为准，导出文件也使用同样的表头：

1. 日期
2. 部门
3. 姓名
4. 金额/元
5. 事由
6. 备注

支持 `.xls`、`.xlsx` 和 `.xlsm`，单文件最大 20 MB。导入器会自动查找表头行，支持部门留空时向下沿用，也能从“微信已收”“现金已收”等备注中识别收款方式。姓名可以包含多人，例如“王敏、付超”；“10（每人5元）”会以总额 10 元入库，列表和导出仍显示均分信息。导入时如果部门不存在，系统会自动创建启用状态的部门，后续可在部门管理中编辑。

## 数据规则

- 新增考核记录默认状态为 `Pending`（待处理）。
- 编辑记录并选择收款方式后，状态自动变为 `Completed`（已完成）。
- 已被考核记录引用的部门和收款方式不能直接删除，可改为停用。
- 系统始终保留至少一个启用的超级管理员。
- 考核记录、部门、收款方式和用户的删除操作均为逻辑删除；数据会标记 `IsDeleted=true` 并记录 `DeletedAt`，正常查询不会返回已删除数据。
