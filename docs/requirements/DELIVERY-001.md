# DELIVERY-001：自提/配送/签收/异常 — 设计方案

- 状态：已确认
- 日期：2026-11-17
- 关联：ROADMAP P6（自提交付单、配送任务、签收凭证、异常流程）；AGENTS.md §6.1（签收 ≠ 财务结清）；ARRIVAL-001（待提货状态）；WB-001（六类状态）

## 1. 业务目标

交付闭环：对已到货运单创建交付单（自提/配送），签收后生成凭证；拒收走异常流程。**同一运单不能被重复完成交付**；签收与财务状态独立。

## 2. 领域规则

- **创建交付单**：运单 `DeliveryStatus = WaitingPickup`；类型 `SelfPickup`（自提）/ `Delivery`（配送）；配送须指定司机（可选，暂不强制）。每运单仅一张有效交付单（部分唯一索引）。
- **签收**：仅 `Pending` 交付单可签收；签收须提供签收人姓名；签收后运单 `DeliveryStatus = Delivered`（聚合守卫），POD 落库；**不改运费财务状态**（AGENTS §6.1）。
- **拒收**：仅 `Pending` 交付单可拒收；运单 `DeliveryStatus = Rejected`；交付单标记 Rejected。
- **凭证（POD）**：签收时创建，含签收人/时间/照片（文件引用，可选）。
- **交付事件**：签收/拒收追加 `delivery_events`。
- 组合授权：`delivery.manage` + 交付网点范围。

## 3. 表结构

```sql
CREATE TABLE delivery_orders (
    id uuid PK, tenant_id uuid NOT NULL,
    waybill_id uuid NOT NULL,       -- ux (tenant_id, waybill_id) 有效交付单
    branch_id uuid NOT NULL,        -- 交付网点
    delivery_type smallint NOT NULL, -- 1=自提 2=配送
    status smallint NOT NULL,       -- 1=Pending 2=Completed 3=Rejected
    driver_id uuid NULL,            -- 配送司机（配送类型）
    receiver_name varchar(100) NULL, -- 签收人（签收后写入）
    operator_user_id uuid NULL,
    completed_at timestamptz NULL, completed_by uuid NULL,
    created_at timestamptz NOT NULL, xmin
);
CREATE TABLE proof_of_deliveries (
    id uuid PK, tenant_id uuid NOT NULL,
    delivery_order_id uuid NOT NULL,  -- ux（每交付单一张凭证）
    receiver_name varchar(100) NOT NULL,
    photo_file_id uuid NULL,          -- FK → file_objects
    signed_at timestamptz NOT NULL, created_at timestamptz NOT NULL
);
CREATE TABLE delivery_events (
    id uuid PK, tenant_id uuid NOT NULL, delivery_order_id uuid NOT NULL,
    event_type varchar(30) NOT NULL,  -- created/completed/rejected
    detail jsonb NULL, operator_user_id uuid NULL, occurred_at timestamptz NOT NULL
);
```

## 4. 权限

- `delivery.read`（0901）/ `delivery.manage`（0902）—— Delivery 模块 seed。

## 5. 测试计划

1. 自提流程：创建 → 签收（含签收人）→ 运单 Delivered + POD + 事件。
2. 配送流程：创建（含司机）→ 签收。
3. 拒收：创建 → 拒收 → 运单 Rejected。
4. 守卫：非 WaitingPickup 运单创建交付单 → 409；重复签收 → 409；重复交付单 → 409。
5. 跨租户 → 404。

## 6. 验收标准

- [ ] 自提和配送均有交付凭证（POD 实测）。
- [ ] 同一运单不能被重复完成交付（唯一索引 + 聚合守卫实测）。
- [ ] 运单签收不自动等于运费结清（签收后 freight_status 不变）。
