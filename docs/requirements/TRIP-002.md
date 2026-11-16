# TRIP-002：发车、到达、运输事件与运输费用 — 设计方案

- 状态：已确认
- 日期：2026-11-17
- 关联：ROADMAP P5（发车、到达和运输事件；运输费用记录；运输批次与运单的关联历史）；TRIP-001；WB-001；AGENTS.md §6.1/§4.3

## 1. 业务目标

批次生命周期闭环：发车（运单运输状态 → 已发运，解锁"已发运不可取消"守卫）、到达（配载行转非活跃，多段运输可继续装车）、批次运输事件与运输费用记录（追加式）。

## 2. 领域规则

- **发车**：`Planned → Departed`，且批次至少有 1 条 active 配载（空车不发）；同事务把全部 active 配载运单的运输状态 → Shipped（经 Waybills 聚合守卫）+ 追加运单事件（Departed）。发车后不可配载/撤销配载（TRIP-001 守卫已就位）。
- **到达**：`Departed → Arrived`；全部 active 配载行 `MarkInactive`（多段运输场景 B：运单可在下一段批次再次装车）+ 追加运单事件（Arrived）。**到达不改交付状态**（AGENTS §6.1：到货≠交付，P6 处理）。
- **运输费用**：`transport_trip_fees` 追加式（类型白名单：fuel/toll/driver/other），金额 decimal ≥0。
- **运输事件**：`transport_trip_events` 追加式（批次维度，departed/arrived）。
- 组合授权：trip.manage（发车/到达/费用）+ trip.read（查询）。

## 3. 表结构与枚举扩展

```sql
CREATE TABLE transport_trip_fees (
    id uuid PK, tenant_id uuid NOT NULL, trip_id uuid NOT NULL,   -- 复合外键
    fee_type varchar(20) NOT NULL,     -- fuel/toll/driver/other
    amount numeric(18,2) NOT NULL CHECK (>= 0),
    note varchar(500) NULL, operator_user_id uuid NULL,
    occurred_at timestamptz NOT NULL, created_at timestamptz NOT NULL
);
CREATE TABLE transport_trip_events (
    id uuid PK, tenant_id uuid NOT NULL, trip_id uuid NOT NULL,
    event_type varchar(30) NOT NULL,   -- departed/arrived
    detail jsonb NULL, operator_user_id uuid NULL,
    occurred_at timestamptz NOT NULL, created_at timestamptz NOT NULL
);
-- 枚举扩展迁移：
-- transport_trips.status ck → IN (1,2,3)
-- waybills.transport_status ck → IN (1,2)（新增 Shipped=2）
-- waybill_events.event_type ck → IN (1..7)（Departed=6, Arrived=7）
```

- 运单运输状态流转经 `Waybill.MarkTransportShipped`（聚合守卫：仅 NotShipped → Shipped）。

## 4. API 契约

| 端点 | 权限 |
|------|------|
| `POST /api/transport/trips/{id}/depart` | trip.manage |
| `POST /api/transport/trips/{id}/arrive` | trip.manage |
| `POST /api/transport/trips/{id}/fees` · `GET .../fees` | trip.manage / trip.read |

## 5. 测试计划

1. 发车：有配载 → 200；运输状态 Shipped + 事件；空批次发车 → 409。
2. 发车后：配载/撤销配载 → 409；运单取消 → 409（"已发运不可取消"守卫真实生效）。
3. 到达：配载行 inactive → 运单可装下一批次（场景 B 多段）；再次到达 → 409。
4. 费用：登记/查询；负金额 → 400。
5. 状态守卫：未发车到达 → 409；重复发车 → 409。

## 6. 验收标准

- [ ] 发车/到达状态机与运单运输状态联动（实测）。
- [ ] 多段运输：到达后运单可再装车（场景 B 基础，实测）。
- [ ] 运输费用/事件追加式（实测）。
