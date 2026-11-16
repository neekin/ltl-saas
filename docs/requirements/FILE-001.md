# FILE-001：文件上传元数据与对象存储适配接口 — 设计方案

- 状态：已确认（本地文件系统适配器为开发实现；生产 S3 适配器随 P9 部署任务接入）
- 日期：2026-11-17
- 关联：ROADMAP P2（基础文件上传元数据和对象存储适配接口）；AGENTS.md §2（S3 兼容对象存储，文件服务通过抽象接口接入）、§5.1、§8；AUTH-001/ORG-003（认证与权限样板）

## 1. 业务目标

建立租户隔离的文件上传/下载基础设施：`file_objects` 元数据 + **对象存储抽象接口**（AGENTS §2：文件服务通过抽象接口接入）。后续运单附件、签收凭证（P6）等复用。

## 2. 架构

```text
Application: IFileStorage（存储抽象：SaveAsync/GetAsync 流读写，key 由服务生成）
             IFileObjectService（元数据 + 上传/下载编排）
Infrastructure: LocalFileStorage（开发实现：本地目录，key = {tenantId}/{yyyyMM}/{guid}{ext}）
               （生产：S3CompatibleFileStorage，P9 接入，实现同一接口）
```

- 抽象接口在 Application，实现在 Infrastructure —— AGENTS §2"抽象接口接入"，生产更换存储不改调用方。
- 文件内容**不经数据库**；数据库仅存元数据（file_objects）。

## 3. 表结构

```sql
CREATE TABLE file_objects (
    id uuid PRIMARY KEY,
    tenant_id uuid NOT NULL REFERENCES tenants(id),
    file_name varchar(200) NOT NULL,     -- 原始文件名
    content_type varchar(100) NOT NULL,
    size_bytes bigint NOT NULL CHECK (size_bytes > 0),
    storage_provider varchar(20) NOT NULL,  -- "local" / "s3"
    storage_key varchar(200) NOT NULL,      -- ux 全局唯一
    uploaded_by uuid NULL,                  -- 审计值（JWT sub）
    created_at timestamptz NOT NULL
);
```

- 租户过滤 + tenants 复合外键（标准约定）；下载按 Id 查元数据（过滤器隔离，跨租户 → 404）。

## 4. API 契约（认证；第一版权限 = 已认证即可，附件权限随业务实体任务细化）

| 端点 | 说明 |
|------|------|
| `POST /api/files`（multipart/form-data，字段 file） | 上传：单文件 ≤20MB（可配），流式写入存储适配器 → 落元数据；返回 `{id, fileName, contentType, sizeBytes}` |
| `GET /api/files/{id}` | 元数据 |
| `GET /api/files/{id}/content` | 下载流（Content-Type/文件名回显） |

- 大小超限 → 400；空文件 → 400；存储键含租户前缀，物理层无跨租户覆盖。
- 病毒/类型白名单：本期不做（如实边界），文件名仅存储不用于路径拼接（防路径穿越：扩展名白名单化提取，key 服务端生成）。

## 5. 测试计划（集成，Testcontainers + 临时目录）

1. 上传 → 元数据落库、存储文件存在、下载内容字节一致（round-trip）。
2. 文件名/Content-Type 回显正确；超过大小限制 → 400。
3. 跨租户下载 → 404（过滤器隔离）。
4. 未认证 → 401（FallbackPolicy）。
5. 存储键全局唯一（ux）。

## 6. 验收标准

- [ ] 元数据与存储内容一致可回取（实测 round-trip）。
- [ ] 租户隔离（跨租户 404、存储键租户前缀）。
- [ ] 更换存储实现无需改动调用方（接口抽象）。
