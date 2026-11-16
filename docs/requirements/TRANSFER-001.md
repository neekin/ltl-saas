# TRANSFER-001：网点调拨全流程 — 设计方案

- 状态：已确认（差异处理第一版：验收一次性写入实收/破损，不可覆盖；超收差异随 P6 异常件细化）
- 日期：2026-11-17
- 关联：ROADMAP P5（调拨单创建/提交/发出/接收、货物清单和数量核验、差异破损短少记录）；AGENTS.md §6.3（调拨是租户内直营网点交接；不产生对外销售收入；短少/破损/拒收保留历史）

## 1. 业务目标

直营网点间货物调拨闭环：**调出清单 → 提交 → 发出确认 → 调入验收（数量核验 + 差异留痕）**。调拨不产生对外销售收入（无财务联动）。

## 2. 领域规则

- 状态机：`Draft → Submitted（提交）→ Dispatched（发出确认）→ Received（调入验收）`；`Draft/Submitted → Cancelled`。
- 起点网点 ≠ 终点网点；调拨号 SEQ-001（`DB + 调出网点码 + 期间 + 序号`）。
- **组合授权**：创建/提交/发出 → 调出网点范围；验收 → **目的网点范围**（AGENTS §5.2，实时校验）。
- **验收一次性写入**：仅 Dispatched 可验收；逐项写入实收数量/破损数量（破损计入实收）；`received + broken ≤ declared` 且 `received ≥ broken`；验收后不可再改（原始交接记录不可覆盖，AGENTS §6.3）；差异 = declared − received（短少）自动可算。
- **历史保留**：差异不修改清单——验收值写在本行且行不可二次验收；短少/破损以字段+备注留痕。
- 实际库存登记（目的网点入库）随仓储库存任务交付（本任务为单据流转与差异核验）。

## 3. 表结构

```sql
CREATE TABLE branch_transfers (
    id uuid PRIMARY KEY, tenant_id uuid NOT NULL,
    transfer_no varchar(64) NOT NULL,       -- ux (tenant_id, transfer_no)
    from_branch_id uuid NOT NULL, to_branch_id uuid NOT NULL,  -- 复合外键 → branches
    status smallint NOT NULL,               -- 1=Draft 2=Submitted 3=Dispatched 4=Received 5=Cancelled  ck
    note varchar(500) NULL,
    submitted_at/by NULL, dispatched_at/by NULL, received_at/by NULL,
    created_at/by, updated_at/by, xmin
);
CREATE TABLE branch_transfer_items (
    id uuid PRIMARY KEY, tenant_id uuid NOT NULL,
    transfer_id uuid NOT NULL,              -- 复合外键 → branch_transfers(tenant_id, id)
    seq int NOT NULL CHECK (>= 1),          -- ux (tenant_id, transfer_id, seq)
    cargo_name varchar(200) NOT NULL,
    declared_quantity int NOT NULL CHECK (>= 1),
    received_quantity int NULL CHECK (received_quantity IS NULL OR received_quantity >= 0),
    broken_quantity int NULL CHECK (broken_quantity IS NULL OR broken_quantity >= 0),
    remark varchar(500) NULL,               -- 验收备注（破损/短少说明）
    created_at timestamptz NOT NULL
);
-- 权限点 seed：transfer.read/manage（0501/0502）
```

## 4. API 契约（认证 + 组合授权）

| 端点 | 权限/范围 |
|------|-----------|
| `POST /api/warehouse/transfers` | manage + 调出网点范围 |
| `GET /api/warehouse/transfers/{id}` | read |
| `POST /{id}/submit` · `/dispatch` | manage + 调出网点范围 |
| `POST /{id}/receive`（逐项实收/破损） | manage + **目的网点范围** |
| `POST /{id}/cancel` | manage + 调出网点范围 |

## 5. 测试计划

1. 全流程：创建（DB+清单）→ 提交 → 发出 → 验收（短少/破损）→ Received；各步骤审计。
2. 状态守卫：未提交发出 409；未发出验收 409；重复验收 409；取消后任何流转 409。
3. 授权：无调出范围创建 → 403；无目的范围验收 → 403。
4. 验收数量校验：received + broken > declared → 400；received < broken → 400。
5. 跨租户 → 404。

## 6. 验收标准

- [ ] 调出/提交/发出/验收全过程可追踪（状态机 + 审计实测）。
- [ ] 验收一次性写入不可覆盖（实测 409）。
- [ ] 短少/破损数量核验并留痕（实测）。
- [ ] 调拨不产生对外销售收入（无财务联动，代码层面无应收写入）。
