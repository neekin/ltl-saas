# ARRIVAL-001：到货登记与异常记录 — 设计方案

- 状态：已确认
- 日期：2026-11-17
- 关联：ROADMAP P6（到货登记/核对/异常/入库/待提货）；AGENTS.md §6.1/§6.3；TRIP-001/002（批次与配载）；WB-001（六类状态）

## 1. 业务目标

目的网点对运输批次到货进行登记核对（逐运单实收件数 vs 装车件数），差异/破损留痕；运单交付状态 → 待交付（可提/可配），货物状态 → 已到达。到货必须关联实际运输批次。

## 2. 领域规则

- 到货登记：`{tripId, branchId(到达网点=trip.DestBranchId), items: [{waybillId, receivedPieces, brokenPieces, remark}]}`；仅 `Departed` 批次可到货登记（`Planned` 未发运/已 `Arrived` 均拒绝）。
- 登记后：批次 → Arrived（聚合守卫）；全部 active 配载行 → MarkInactive；运单 `DeliveryStatus = WaitingPickup`（新增枚举值）；运单事件 Arrived。
- 异常留痕：实收 < 装车 → 差异（短少/破损）在 arrival_items 上记录，后续处理流程（P6 异常件）不覆盖原始记录。
- 逐运单**仅一次到货登记**（ux: tenant_id + waybill_id WHERE active 到货行）——重复登记拒绝。
- 组合授权：`arrival.record` 权限 + 到货网点范围（AGENTS §5.2）。

## 3. 表结构

```sql
CREATE TABLE arrivals (
    id uuid PRIMARY KEY, tenant_id uuid NOT NULL,
    trip_id uuid NOT NULL,              -- 复合外键 → transport_trips(tenant_id, id)
    branch_id uuid NOT NULL,            -- 复合外键 → branches(tenant_id, id)（到达网点）
    operator_user_id uuid NULL, note varchar(500) NULL,
    arrived_at timestamptz NOT NULL, created_at timestamptz NOT NULL, xmin
);
CREATE TABLE arrival_items (
    id uuid PRIMARY KEY, tenant_id uuid NOT NULL,
    arrival_id uuid NOT NULL,           -- 复合外键 → arrivals(tenant_id, id)
    waybill_id uuid NOT NULL,           -- 复合外键 → waybills(tenant_id, id)
    loaded_pieces int NOT NULL CHECK (>= 1),      -- 装车件数（发车时快照）
    received_pieces int NOT NULL CHECK (>= 0),    -- 实收件数
    broken_pieces int NOT NULL CHECK (>= 0) DEFAULT 0,
    remark varchar(500) NULL
);
-- 部分唯一：每运单仅一次有效到货
CREATE UNIQUE INDEX ux_arrival_items_active_waybill ON arrival_items (tenant_id, waybill_id);
-- 简化：arrival_items 无 active 列（追加式，运单仅到货一次）→ ux (tenant_id, waybill_id) 全量唯一
```

## 4. 权限

- `arrival.record`（0801）/ `arrival.read`（0802）—— Arrival 模块 seed。

## 5. API

| 端点 | 权限 |
|------|------|
| `POST /api/arrivals` | arrival.record + 到货网点范围 |
| `GET /api/arrivals?tripId=` | arrival.read |
| `GET /api/waybills/{id}/arrival` | arrival.read |

## 6. 测试计划

1. 发车→到达全流程：到货登记 → 批次 Arrived + 运单 WaitingPickup + 事件 + 审计。
2. 异常：短少/破损记录留痕。
3. 守卫：Planned 批次拒绝；已到货运单重复登记拒绝（唯一索引）；运单非 Shipped 拒绝。
4. 授权：无网点范围 → 403；跨租户 → 404。

## 7. 验收标准

- [ ] 到货关联实际运输批次（tripId 必填，实测）。
- [ ] 短少/破损留痕（实测）。
- [ ] 运单交付状态 → WaitingPickup（实测）。
