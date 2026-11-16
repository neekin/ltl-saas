# TEN-001：Tenant 上下文和租户隔离 — 设计方案

- 状态：已确认（JWT 基线经用户指示采纳；租户创建入口按保守边界处理）
- 日期：2026-11-17
- 关联：ROADMAP P1/TEN-001；AGENTS.md §4.2、§5、§8；ADR-0001、ADR-0005

## 1. 业务目标

建立租户上下文解析与数据隔离的基础设施，使后续所有业务模块（网点、客户、运单、财务…）的每一条数据访问都能绑定到服务端解析出的租户，禁止信任客户端提交的 `tenant_id`。

## 2. 范围

**实现：**

1. `Tenant` 领域实体与 `TenantStatus`（启用/停用）。
2. `ITenantContext` 抽象与实现：请求内由中间件从**已认证身份的 claim** 解析租户；后台任务可显式 `SetTenant` 建立上下文（AGENTS §4.3）。
3. `TenantResolutionMiddleware`：从 `tenant_id` claim 解析租户上下文；非法 claim 返回 401。
4. EF Core（PostgreSQL/Npgsql）映射：`TenancyDbContext` + `tenants` 表初始迁移。
5. 租户范围实体基础设施：`ITenantScopedEntity` 标记（SharedKernel），为后续业务表统一附加全局查询过滤器与复合外键提供约定。
6. 诊断端点 `GET /api/tenancy/current`（要求租户上下文，无上下文 → 401 ProblemDetails）。
7. 单元测试 + Testcontainers 集成测试。

**不实现（明确排除）：**

- 登录、JWT 签发与认证中间件（P1 登录任务交付；本任务仅约定 claim 契约）。
- 租户 CRUD / 启停用 API 与平台管理角色（随 P1 登录与角色任务交付；避免出现无认证的业务 API，AGENTS §8）。
- 任何业务表（网点、用户等，属 ORG-001）。

## 3. 数据库设计（tenants 表）

```sql
CREATE TABLE tenants (
    id          uuid        PRIMARY KEY,
    code        text        NOT NULL,                 -- 租户编码，全局唯一
    name        text        NOT NULL,                 -- 企业名称
    status      smallint    NOT NULL,                 -- 0=启用 1=停用
    created_at  timestamptz NOT NULL DEFAULT now(),
    created_by  uuid        NULL,
    updated_at  timestamptz NULL,
    updated_by  uuid        NULL,
    row_version             NOT NULL                  -- PostgreSQL xmin 系统列，乐观并发令牌
);
CREATE UNIQUE INDEX ux_tenants_code ON tenants (code);
ALTER TABLE tenants ADD CONSTRAINT ck_tenants_status CHECK (status IN (0, 1));
```

- 主键 UUID（AGENTS §7）；`tenants` 是**全局表**，不带 `tenant_id`（自身即租户）。
- `row_version` 采用 Npgsql 的 `xmin` 并发令牌实现乐观并发控制（AGENTS §8）。
- 时间 `TIMESTAMPTZ`，应用层 UTC。

### 复合外键约定（供后续业务表执行）

- 租户内业务表实现 `ITenantScopedEntity`（含 `TenantId`），由模块 DbContext 统一配置：
  1. EF Core 全局查询过滤器（纵深防御，非唯一边界，AGENTS §5.1）；
  2. 与租户内实体的引用使用复合外键 `(tenant_id, entity_id)`，防止租户 A 数据引用租户 B 数据；
  3. 租户范围唯一约束必须包含 `tenant_id`。
- `tenants(id)` 本身作为单列 FK 目标仅用于校验“租户存在”。

## 4. 租户上下文解析

```text
HTTP 请求：JWT 认证（P1）→ HttpContext.User 含 claim "tenant_id"
          → TenantResolutionMiddleware 解析并 SetTenant
          → 后续业务端点通过 ITenantContext.RequireTenant() 取得租户
后台任务：显式 SetTenant(tenantId) 建立上下文（AGENTS §4.3，不依赖线程隐式状态）
```

- claim 契约：`Ltl.Modules.Tenancy.Application.TenantClaimTypes.TenantId = "tenant_id"`，由 P1 登录签发时遵守。
- 解析失败（claim 非法/空）→ 401；已认证但无租户 claim → 租户上下文为空，业务端点 `RequireTenant()` 抛 `TenantContextMissingException` → 统一异常处理映射 401 ProblemDetails。
- `tenant_id` 永不信任 Body/Query/Header（AGENTS §5.1）。

## 5. EF Core 与版本决策

- **EF Core 10.0.15 + Npgsql.EntityFrameworkCore.PostgreSQL 10.0.3（稳定版），运行于 .NET 11**。
- 原因：Npgsql EF Provider 适配 EF Core 11 的稳定版尚未发布（仅 rc 预览），不采用预览包作为数据层底座；provider 10.0.3 依赖约束 `EF [10.0.4, 11.0.0)`。Npgsql EF 11 稳定发布后升级（迁移文件极少，成本低）。
- 迁移使用本地工具清单（`dotnet-ef` 10.x），`dotnet ef migrations add` 生成的迁移随仓库提交，可在空库执行。
- 连接串经配置注入（开发环境指向 compose 的 `localhost:5433`），禁止提交真实生产连接串。

## 6. 测试计划

### 单元测试（Ltl.UnitTests）

- `TenantContext`：`SetTenant` 后 `TenantId` 生效；未设置时 `RequireTenant()` 抛 `TenantContextMissingException`。
- `TenantResolutionMiddleware`：
  - 已认证 + 合法 `tenant_id` claim → 上下文设置、管道继续；
  - 已认证 + 非法/空 claim → 401，管道终止；
  - 未认证 → 上下文为空、管道继续。

### 集成测试（Ltl.IntegrationTests，Testcontainers PostgreSQL 17）

- 迁移可在空库执行（`Database.Migrate()`）。
- `tenants.code` 唯一约束：重复编码插入失败。
- `ck_tenants_status` 检查约束：非法状态值插入失败。
- `xmin` 乐观并发：两个上下文更新同一租户 → 第二次保存抛 `DbUpdateConcurrencyException`。
- 后台任务租户上下文：新 scope 内 `SetTenant` 生效（AGENTS §5.3）。

### 暂缓（如实说明）

- 跨租户读/写越权与全局查询过滤器测试：待首个租户范围业务表（ORG-001 网点/用户）落地后补齐，TEN-001 无租户范围表可测。
- JWT 端到端解析测试：随 P1 登录任务。

## 7. API 变更

| 方法 | 路径 | 说明 | 认证 |
| --- | --- | --- | --- |
| GET | `/api/tenancy/current` | 返回当前解析租户 `{ tenantId }`；无租户上下文 → 401 ProblemDetails | 要求租户上下文（P1 起由 JWT 认证保障） |

## 8. 验收标准

- [ ] 迁移可在空数据库执行。
- [ ] `tenant_id` 只能来自服务端解析的身份 claim，客户端提交无效。
- [ ] 后台任务可显式建立租户上下文。
- [ ] 上述测试全部通过。
