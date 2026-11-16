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
