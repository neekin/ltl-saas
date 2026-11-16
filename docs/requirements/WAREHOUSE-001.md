# WAREHOUSE-001：分拣任务（仓库作业）— 设计方案

- 状态：已确认
- 日期：2026-11-17
- 关联：ROADMAP P4（仓库收货记录和分拣任务）；RECEIVE-001（收货记录已交付）；AGENTS.md §5.2/§6.1

## 1. 业务目标

收货后对运单创建**分拣任务**并完成分拣：货物/仓储状态 从 已收货 → 已分拣（经聚合守卫），任务全生命周期留痕。收货记录（cargo_receipts）已在 RECEIVE-001 交付，本任务补齐仓储作业的任务侧。

## 2. 领域规则

- 任务仅可对**货物状态 = 已收货**的运单创建（分拣在收货之后）；草稿/未收货/已分拣 → 409。
- 每运单同时最多**一个未完成任务**（部分唯一索引 `WHERE status = 1`）；完成后可因返工再次创建。
- 完成任务：经 `Waybill.MarkCargoSorted`（聚合守卫）流转 + `waybill_events`（Sorted）+ 审计，同环境事务。
- 组合授权：`AuthorizeAsync(user, branchId, warehouse.cargo.sort)`（权限点 + 网点范围实时校验）。

## 3. 表结构

```sql
CREATE TABLE cargo_handling_tasks (
    id uuid PRIMARY KEY,
    tenant_id uuid NOT NULL,        -- FK → tenants(id)
    waybill_id uuid NOT NULL,       -- 复合外键 → waybills(tenant_id, id)
    branch_id uuid NOT NULL,        -- 复合外键 → branches(tenant_id, id)
    task_type smallint NOT NULL,    -- 1=分拣  ck
    status smallint NOT NULL,       -- 1=未完成 2=已完成  ck
    operator_user_id uuid NULL,     -- 审计值
    completed_at timestamptz NULL, completed_by uuid NULL,
    note varchar(500) NULL,
    created_at timestamptz NOT NULL,
    -- 部分唯一索引：每运单同时最多一个未完成任务
    CREATE UNIQUE INDEX ux_cargo_handling_tasks_active ON (tenant_id, waybill_id) WHERE status = 1
);
```

## 4. API 契约

| 端点 | 权限 | 说明 |
|------|------|------|
| `POST /api/warehouse/sorting-tasks` | sort | 创建 `{waybillId, branchId, note?}` |
| `POST /api/warehouse/sorting-tasks/{id}/complete` | sort | 完成（任务所属网点授权校验） |
| `GET /api/warehouse/sorting-tasks?waybillId=` | cargo.read | 任务列表 |

## 5. 测试计划

1. 未收货运单创建任务 → 409；收货后创建 → 200（Pending）。
2. 完成 → 货物状态 Sorted + 事件 Sorted + 审计；再次完成 → 409。
3. 重复创建未完成任务 → 409（唯一索引）。
4. 组合授权：无网点范围 → 403；分批分拣返工：完成后再次创建 → 200。
5. 跨租户 → 404。

## 6. 验收标准

- [ ] 分拣任务全生命周期（创建/完成）与货物状态流转一致（实测）。
- [ ] 每运单同时最多一个未完成任务（数据库强制，实测）。
- [ ] 状态/事件/审计同事务（实测回滚无残留）。
