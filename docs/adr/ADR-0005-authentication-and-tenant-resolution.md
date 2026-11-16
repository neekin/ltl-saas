# ADR-0005：认证机制（JWT）与租户上下文解析

- 状态：已接受
- 日期：2026-11-17
- 关联：AGENTS.md §5、§8；ROADMAP P1；ADR-0001；docs/requirements/TEN-001-tenant-context.md

## 背景

多租户 SaaS 需要一个跨模块一致的认证与租户解析机制：`tenant_id` 必须从已验证身份解析（AGENTS §5.1），登录能力在 P1 交付，但租户上下文基础设施（TEN-001）必须先行，且两者契约必须一致。

## 决策

1. **认证机制采用 JWT**（用户已确认）：登录签发 Access Token + Refresh Token（具体签发与刷新由 P1 登录任务实现）。
2. 租户上下文契约：JWT claim `tenant_id`（`TenantClaimTypes.TenantId`），由 `TenantResolutionMiddleware` 从已认证身份解析进 `ITenantContext`。
3. `tenant_id` 永不从 Body/Query/Header 读取；claim 非法或缺失时业务端点拒绝（401/403），不降级。
4. 后台任务不依赖请求隐式状态，必须显式 `ITenantContext.SetTenant()` 建立租户上下文。
5. 租户生命周期管理（租户 CRUD、启停用、平台管理角色）与登录一起在 P1 交付；TEN-001 不暴露无认证的租户管理 API。

## 后果

- P1 登录任务必须遵守本 claim 契约；认证中间件与租户解析中间件的顺序为：`UseAuthentication` → `TenantResolutionMiddleware` → 业务端点。
- 所有业务端点统一通过 `RequireTenant()` 获取租户，禁止各自直接读 claim。
- EF Core 10.x 稳定版 + Npgsql Provider 10.0.3 作为过渡版本组合（Npgsql EF 11 稳定版发布后升级），详见 TEN-001 设计文档第 5 节。

## 依据

用户 2026-11-17 会话指示（认证机制 JWT）；AGENTS.md §5.1（tenant_id 解析来源）；AGENTS.md §4.3（后台任务显式租户上下文）。
