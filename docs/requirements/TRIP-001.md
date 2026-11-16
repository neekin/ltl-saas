# TRIP-001：运输批次、运输区段与配载 — 设计方案

- 状态：已确认（配载为一步式"装车确认"；发车/到达/运输事件/运输费用属 TRIP-002）
- 日期：2026-11-17
- 关联：ROADMAP P5（运输批次和运输区段、运单配载、装车确认、防重复装车）；AGENTS.md §6.2（批次≠线路定义）、§7（单号 SEQ-001）；VEHICLE-001；WB-001/002；ROUTE-001

## 1. 业务目标

创建**运输批次**（一次实际运输任务，AGENTS §6.2：TransportTrip ≠ Route），把已收货/已分拣的运单**装车**（配载 + 装车确认），并以数据库约束保证**同一运单不能同时被分配到两个有效（未到达）运输批次**（P5 验收）。

## 2. 领域规则

- **批次创建**：起点/终点网点（线路可选）、车辆（必填、本租户启用）、司机（可选）；批次号经 SEQ-001（`PC + 始发网点码 + 期间 + 序号`）；自动生成 1 条运输区段（seq=1，起点→终点，多段历史随到达扩展）。
- **配载（装车确认）**：仅 `Planned` 批次可配载；运单须为**已收货或已分拣**（`cargo_status ∈ {Received, Sorted}`，货物在本网点后才可装车——网点归属校验随发车任务细化）；配载即装车确认（loaded_at/by 留痕）。
- **防重复装车**：配载行含 `active` 标记；**部分唯一索引 `(tenant_id, waybill_id) WHERE active = true`** 数据库层强制"同一运单不能同时被分配到两个有效批次"；到达（TRIP-002）将配载行置 inactive 后方可再次装车（多段运输场景 B）。
- **车辆容量**：配载时校验累计申报重量 ≤ 车辆最大载重、体积 ≤ 最大容积（超载 → 409）。
- **撤销配载**：仅 Planned 批次可移除配载行（记录删除 = 撤销装车，历史批次不含该行；发车后不可撤销）。
- 权限：`transport.trip.read` / `transport.trip.manage`（VEHICLE-001 已 seed）。

## 3. 表结构

```sql
CREATE TABLE transport_trips (
    id uuid PRIMARY KEY, tenant_id uuid NOT NULL,
    trip_no varchar(64) NOT NULL,             -- ux (tenant_id, trip_no)，SEQ-001 生成
    route_id uuid NULL,                       -- 复合外键 → routes(tenant_id, id)
    vehicle_id uuid NOT NULL,                 -- 复合外键 → vehicles(tenant_id, id)
    driver_id uuid NULL,                      -- 复合外键 → drivers(tenant_id, id)
    origin_branch_id uuid NOT NULL, dest_branch_id uuid NOT NULL,  -- 复合外键 → branches
    status smallint NOT NULL,                 -- 1=Planned（TRIP-002 扩展 2=Departed 3=Arrived）ck
    note varchar(500) NULL, created_at/by, updated_at/by, xmin
);
CREATE TABLE transport_trip_segments (
    id uuid PRIMARY KEY, tenant_id uuid NOT NULL,
    trip_id uuid NOT NULL, seq int NOT NULL CHECK (>= 1),   -- ux (tenant_id, trip_id, seq)
    from_branch_id uuid NOT NULL, to_branch_id uuid NOT NULL,  -- 复合外键 → branches
    created_at timestamptz NOT NULL
);
CREATE TABLE transport_trip_waybills (
    id uuid PRIMARY KEY, tenant_id uuid NOT NULL,
    trip_id uuid NOT NULL, waybill_id uuid NOT NULL,        -- 复合外键；ux (tenant, trip, waybill)
    active bool NOT NULL DEFAULT true,                      -- 部分唯一 (tenant_id, waybill_id) WHERE active
    loaded_at timestamptz NOT NULL, loaded_by uuid NULL
);
```

## 4. API 契约（认证 + 权限）

| 端点 | 权限 |
|------|------|
| `POST /api/transport/trips`、`GET /api/transport/trips/{id}` | trip.manage / trip.read |
| `POST /api/transport/trips/{id}/waybills`（配载+装车确认） | trip.manage |
| `DELETE /api/transport/trips/{id}/waybills/{assignmentId}`（撤销配载） | trip.manage |
| `GET /api/waybills/{id}/trips`（运单运输批次历史） | trip.read |

## 5. 测试计划

1. 批次创建（单号格式 PC+网点码+期间+序号、自动区段）；车辆停用/跨租户 → 404。
2. 配载：已收货运单 → 200（active=true、loaded_at/by 留痕）；草稿运单 → 409；超载 → 409。
3. 防重复装车：同运单配到第二有效批次 → 409（数据库强制）。
4. 撤销配载后可再次配载；Departed 后（TRIP-002）不可撤销——本任务先验证 Planned 内撤销。
5. 跨租户运单 → 404；trip.read 无权限 → 403。

## 6. 验收标准

- [ ] 同一运单不能同时被分配到两个有效运输批次（数据库约束实测）。
- [ ] 配载校验车辆容量上限（实测超载拒绝）。
- [ ] 批次号事务安全（SEQ-001）与区段记录就绪（多段历史 TRIP-002 扩展）。
