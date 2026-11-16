# VEHICLE-001：车辆和司机档案 — 设计方案

- 状态：已确认
- 日期：2026-11-17
- 关联：ROADMAP P5（车辆和司机档案）；ORG-003（管理 API 与权限样板）；AGENTS.md §5.1/§7

## 1. 业务目标

建立运输基础档案：车辆（车牌、载重/容积上限）与司机（姓名/电话/证号），供运输批次配载（TRIP-001）引用。

## 2. 领域规则

- 车牌号 `plate_no` 租户内唯一；司机证号 `license_no` 租户内唯一（部分唯一：仅启用状态强制，重复启用历史号冲突 → 409）。
  简化：`ux` 全量唯一（租户内），停用档案不删除、编码不回收。
- 启停用幂等；车辆载重/容积上限用于 TRIP-001 配载校验。
- 权限：`transport.vehicle.read` / `transport.vehicle.manage`（0401/0402，模块迁移 seed）。

## 3. 表结构

```sql
CREATE TABLE vehicles (
    id uuid PRIMARY KEY, tenant_id uuid NOT NULL REFERENCES tenants(id),
    plate_no varchar(20) NOT NULL, vehicle_type varchar(20) NOT NULL,   -- van/truck 等（字符串白名单由服务校验）
    max_load_kg numeric(12,3) NOT NULL CHECK (> 0),
    max_volume_m3 numeric(12,4) NOT NULL CHECK (> 0),
    status smallint NOT NULL CHECK (IN (0,1)), note varchar(500) NULL,
    created_at/by, updated_at/by, xmin,
    UNIQUE (tenant_id, plate_no)
);
CREATE TABLE drivers (
    id uuid PRIMARY KEY, tenant_id uuid NOT NULL REFERENCES tenants(id),
    name varchar(100) NOT NULL, phone varchar(50) NOT NULL,
    license_no varchar(64) NOT NULL, status smallint NOT NULL CHECK (IN (0,1)),
    note varchar(500) NULL, created_at/by, updated_at/by, xmin,
    UNIQUE (tenant_id, license_no)
);
```

## 4. API 契约（认证 + 权限，ORG-003 样板）

| 端点 | 权限 |
|------|------|
| `GET/POST /api/transport/vehicles`、`GET/PUT /api/transport/vehicles/{id}`、`POST .../disable` `/enable` | vehicle.read / manage |
| `GET/POST /api/transport/drivers`、`GET/PUT /api/transport/drivers/{id}`、`POST .../disable` `/enable` | vehicle.read / manage |

## 5. 测试计划

1. 车辆/司机创建、重复车牌/证号 409、启停用、分页。
2. 无权限 403；跨租户 404。
3. 载重/容积 ≤ 0 → 400。

## 6. 验收标准

- [ ] 档案 CRUD 全流程实测；租户内唯一性实测。
- [ ] TRIP-001 可引用（上限字段就绪）。
