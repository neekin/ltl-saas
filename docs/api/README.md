# API 文档

> 完整规范见 AGENTS.md §8。各任务新增 API 时在此目录补充契约文档。

## 现有端点（BASE-002）

| 方法 | 路径 | 说明 | 认证 |
| --- | --- | --- | --- |
| GET | `/health` | 健康检查，返回纯文本 `Healthy` | 无（基础设施端点） |
| GET | `/openapi/v1.json` | OpenAPI 3.0 文档（仅开发环境） | 无（基础设施端点） |
| GET | `/api/tenancy/current` | 返回服务端解析的租户 `{ tenantId }`；无租户上下文 → 401 ProblemDetails | 要求租户上下文（TEN-001） |
| GET | `/dev/throw` | 演示异常端点，仅开发环境，用于验证统一错误格式 | 无（仅本地调试） |
| GET | `/` | API 元信息 `{ name, status }` | 无（基础设施端点） |

## P10 新增端点（2026-11-18）

| 方法 | 路径 | 说明 | 认证/权限 |
| --- | --- | --- | --- |
| POST | `/api/waybills` | 开单（补暴露 IWaybillService；含计价快照/事件/查询码），返回 `queryCode` 明文 | waybill.manage + JWT |
| POST | `/api/waybills/{id}/query-code` | 重置客户查询码（换码作废旧码），返回 `{ queryCode }` | waybill.read + JWT + 网点范围 |
| POST | `/api/public/waybill-query` | 客户匿名查单票：请求 `{ tenantCode, waybillNo, queryCode }`，返回脱敏视图（手机号掩码、无内部 ID/网点/客户） | 匿名；租户编码定位租户 + 运单号+查询码双因子；错误统一 401 防枚举 |
| GET | `/api/reports/branches?month=yyyyMM` | 网点维度运营报表 | finance.read + JWT |
| GET | `/api/reports/routes?month=yyyyMM` | 线路维度运营报表 | finance.read + JWT |
| GET | `/api/reports/customers?month=yyyyMM` | 客户维度运营报表 | finance.read + JWT |
| GET | `/api/reports/usage` | 租户用量（运单数/用户数/文件数/存储字节），仅统计展示 | finance.read + JWT |

约定：匿名客户查询**不依赖 EF 全局查询过滤器**（无 JWT 时上下文为空），显式 `IgnoreQueryFilters()` + SQL 参数化携带 `tenant_id`（AGENTS.md §5.1）；查询码库内仅存 SHA256 哈希，明文仅开单/重置时返回；报表 SQL 别名一律 snake_case（与 `UseSnakeCaseNamingConvention` 一致）。前端独立路由：`/m`（移动作业）、`/track`（客户查询）、`/waybills/:id/label`（A5 面单）。

## 统一错误格式（BASE-002 已落地）

- 全局异常处理：未处理异常一律返回 `500` + `application/problem+json`（RFC 9457 ProblemDetails）。
- 响应体不包含堆栈、SQL、异常类型等内部信息；仅携带 `traceId` 供与 Serilog 日志关联定位。
- 结构化日志：Serilog 控制台输出，请求日志经 `UseSerilogRequestLogging`。
- 客户端（前端 axios 拦截器）统一解析 ProblemDetails 的 `title`/`detail` 为用户可读错误。

## 契约约定

- REST 风格与明确的资源命名。
- 请求/响应一律 DTO，不得直接暴露 EF Core 实体。
- 业务错误同样使用 ProblemDetails；分页、排序、筛选建立一致约定。
- 关键命令（创建运单、收款、发车、交接、签收、COD 结算）具备幂等策略。
- 并发更新使用乐观并发控制。
- 所有业务 API 必须做认证、租户校验和权限校验；`tenant_id` 从服务端身份解析，不信任客户端提交。
- 金额、状态、时间、枚举值服务端验证。
