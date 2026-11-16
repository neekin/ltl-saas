# AUTH-001：登录、JWT 认证与刷新令牌 — 设计方案

- 状态：已确认（密码策略阈值为**第一版建议值**，集中封装可调）
- 日期：2026-11-17
- 关联：ROADMAP P1（登录、刷新令牌、退出和密码安全策略）；AGENTS.md §5.1/§8/§11；ADR-0005（JWT 契约）；ORG-001/002（用户/密码哈希/授权服务）；TEN-001（租户解析中间件）

## 1. 业务目标

交付员工身份认证闭环：登录签发 JWT（Access + Refresh）、刷新轮换、退出撤销、密码策略；全部业务 API 转为默认受保护（未认证 401）。本任务是所有 HTTP 业务端点（P1 CRUD、P4 收货等）的前置解锁点。

## 2. 登录租户定位（与 AGENTS §5.1 的关系论证）

`users.username` 是**租户内唯一**（ORG-001 `(tenant_id, username)`），登录请求必须携带 `tenantCode`（tenants.code 全局唯一）定位待验证身份。

- **不违反 §5.1**：`tenant_id 不得从请求读取` 针对的是**已认证后的业务授权依据**；登录发生在身份建立之前，`tenantCode` 仅用于**查找**候选身份，不产生任何数据访问授权。
- 验证通过后 `tenant_id` 进入 JWT claim（ADR-0005），此后所有请求的租户上下文**只**来自已验证 token——后续请求不再接受任何客户端提供的租户标识。
- 失败响应统一"登录名或密码不正确"（401），不区分租户不存在/用户不存在/密码错误/用户停用（防枚举）。

## 3. API 契约（首批受保护/匿名端点划分）

| 端点 | 认证 | 请求 | 响应/错误 |
|------|------|------|-----------|
| `POST /api/auth/login` | 匿名 | `{tenantCode, username, password}` | 200 `{access_token, expires_in, refresh_token, user{id, username, displayName, tenantId, permissions[]}}`；401 统一信息 |
| `POST /api/auth/refresh` | 匿名 | `{refresh_token}` | 200 新 access + **轮换**新 refresh；401（无效/过期/已撤销；重放已轮换旧令牌 401） |
| `POST /api/auth/logout` | 匿名（幂等） | `{refresh_token}` | 200；撤销该刷新令牌，不存在也 200 |
| `GET /api/auth/me` | **认证** | — | 当前用户 + 实时权限点列表 |
| 其余 `/api/*` | **默认要求认证**（FallbackPolicy） | — | 401 |

- 权限点列表仅用于前端菜单展示（登录时点快照）；**后端授权始终实时查库**（ORG-002 语义，禁用即时生效）。
- 租户/用户停用后刷新令牌立即失效（刷新时重验 User.Active + Tenant.Active）。

## 4. Token 设计（遵守 ADR-0005）

- **Access Token**：HS256 对称签名；claims：`sub`=userId、`tenant_id`（`TenantClaimTypes.TenantId`）、`username`；有效期 15 分钟（可配）。**不含权限点**（授权实时查库）。
- **Refresh Token**：256 位随机数，仅存 **SHA-256 哈希**（明文不落库，AGENTS §11）；有效期 7 天（可配）；一次性轮换（refresh 时撤销旧令牌、签发新令牌）。
- 配置：`Jwt{Secret, Issuer, Audience, AccessTokenMinutes, RefreshTokenDays}`；**Secret 仅来自环境变量/配置管理，开发占位密钥不含真实凭据**（AGENTS §11）。

## 5. 表结构（Organization 模块）

```sql
CREATE TABLE refresh_tokens (
    id uuid PRIMARY KEY,
    tenant_id uuid NOT NULL,           -- FK → tenants(id)
    user_id uuid NOT NULL,             -- 复合外键 → users(tenant_id, id)
    token_hash varchar(64) NOT NULL,   -- SHA-256 hex；ux_refresh_tokens_hash 全局唯一
    expires_at timestamptz NOT NULL CHECK (expires_at > created_at),
    revoked_at timestamptz NULL,
    created_at timestamptz NOT NULL
);
```

- 过期令牌清理：P9 运维任务（Hangfire 后台），本期不引入后台任务。

## 6. 密码策略（第一版建议值，`PasswordPolicy` 纯函数集中封装）

- 长度 8..64；至少包含字母与数字；拒绝空白字符密码。
- 应用于后续用户创建/改密 API（ORG-003）；登录侧仅校验（哈希验证已由 `PasswordHasher` 承担）。
- 账户失败锁定/防爆破：**本期不交付**（如实记录）；失败行为仅结构化日志（Serilog，不含密码）。

## 7. 组合根接线（ADR-0005 顺序）

```text
AddAuthentication(JwtBearer) → AddAuthorization(FallbackPolicy = RequireAuthenticatedUser)
UseAuthentication → UseMiddleware<TenantResolutionMiddleware> → UseAuthorization → endpoints
```

- 匿名端点显式 `.AllowAnonymous()`：`/`、`/health`、`/api/auth/login|refresh|logout`、`/openapi`、`/dev/throw`（开发）。
- `TenantResolutionMiddleware` 已从认证身份读 claim（TEN-001 实现），顺序满足即无需改动。

## 8. 测试计划

### 单元测试

- `PasswordPolicy`：长度边界（7/8/64/65）、字符类（仅字母/仅数字拒绝、字母+数字通过）、空白拒绝。
- `RefreshToken` 实体：签发/撤销/过期/有效判断、重复撤销幂等。

### 集成测试（WebApplicationFactory + Testcontainers）

1. 登录成功 → token 可用：携带 access token 访问 `/api/tenancy/current` → 200 且 `tenantId` 来自 claim（端到端租户解析）。
2. 登录失败矩阵（错密码/未知用户/未知租户/停用用户）→ 全部 401 且响应一致（防枚举）。
3. 刷新轮换：旧 refresh 换新 → 旧令牌重放 401；新令牌可继续刷新。
4. 退出：logout 后 refresh → 401。
5. 禁用用户后 refresh → 401；access token 过期前的存量请求行为不变。
6. 未认证访问业务端点 → 401；篡改签名 token → 401。
7. `/api/auth/me` 认证后返回用户与实时权限点（seed 角色权限）。
8. `/health`、`/` 匿名 200（FallbackPolicy 不误伤）。

## 9. 验收标准

- [ ] 租户 claim 只来自已验证 JWT；登录后所有请求租户上下文来自 token（实测）。
- [ ] 刷新令牌轮换、撤销、过期行为正确（实测）。
- [ ] 业务 API 默认 401；匿名白名单不误伤（实测）。
- [ ] 密码明文与刷新令牌明文不落库；密钥不提交仓库（AGENTS §11）。
