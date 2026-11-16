# RECEIVE-001：收货登记、称重、体积复核 — 设计方案

- 状态：已确认（差异处理策略为第一版：登记差异不阻断收货，异常处理随 P6 到货任务细化）
- 日期：2026-11-17
- 关联：ROADMAP P4（收货登记、称重、体积复核）；AGENTS.md §5.2（网点授权）、§6.1（状态分离/事件）；WB-001/002（运单六类状态）；ORG-002/003（组合授权与权限样板）

## 1. 业务目标

始发网点对**已确认**运单执行收货登记：记录实收件数/称重/体积复核值，与运单申报值自动比对差异；货物/仓储状态从待收货 → 已收货（六类状态分离中"货物/仓储状态"的首次真实流转），并落运单事件。

## 2. 领域规则

- **前置状态**：运单必须 `Confirmed` 且 `cargo_status = PendingReceive`；草稿/取消/已收货的运单拒绝登记（409，`WaybillDomainException`）。
- **网点授权（AGENTS §5.2）**：操作者须同时满足 ① 权限点 `warehouse.cargo.receive`（角色）② 对**收货网点**的数据范围（`AuthorizeAsync` 组合校验，实时查库）。
- **状态流转守卫**：货物状态变化经 `Waybill` 聚合领域方法（`MarkCargoReceived`），禁止在服务里直改状态字段（AGENTS §6.1）。
- **事件**：收货成功追加 `waybill_events`（类型 Received，detail 携带实收摘要），与收货记录同事务（AGENTS §6.1）。
- **分批收货**：允许一个运单多次登记（分批到货），首登状态转已收货；后续登记仅追加记录（实收累计供后续环节核对）。收货记录追加式，不更新不删除。

## 3. 表结构（Warehouse 模块首迁移）

```sql
CREATE TABLE cargo_receipts (
    id uuid PRIMARY KEY,
    tenant_id uuid NOT NULL,            -- FK → tenants(id)
    waybill_id uuid NOT NULL,           -- 复合外键 → waybills(tenant_id, id)
    branch_id uuid NOT NULL,            -- 复合外键 → branches(tenant_id, id)（收货网点）
    received_pieces int NOT NULL CHECK (>= 1),
    received_weight_kg numeric(12,3) NOT NULL CHECK (>= 0),
    received_volume_m3 numeric(12,4) NOT NULL CHECK (>= 0),
    operator_user_id uuid NULL,         -- 审计值拷贝（不建外键）
    note varchar(500) NULL,
    occurred_at timestamptz NOT NULL,
    created_at timestamptz NOT NULL
);
CREATE INDEX ix_cargo_receipts_waybill ON cargo_receipts (tenant_id, waybill_id, occurred_at);
```

- 权限点 seed（模块首个迁移）：`warehouse.cargo.receive`（收货登记）、`warehouse.cargo.read`（查询）、`warehouse.cargo.sort`（分拣，WAREHOUSE-001 使用），固定 Guid 0000…0201/0202/0203（ORG-001 约定）。
- 申报值来自运单聚合（Σ货物件），差异（件数/重量/体积）在查询响应中计算返回，不在记录上固化（避免申报口径变化的双写不一致）。

## 4. 跨模块协作

- Warehouse → Waybills：经公开接口 `IWaybillCargoService`（Waybills.Application 新增）：
  - `MarkCargoReceivedAsync(waybillId, branchId, operatorId, 实收摘要)`：状态守卫 + 流转 + 运单事件（WaybillsDbContext 保存）；
  - 返回运单申报口径快照（Σ件数/重量/体积 + 运单号），供收货记录与差异比对。
- 事务：收货记录（WarehouseDbContext）+ 状态/事件（WaybillsDbContext）+ 审计（PlatformDbContext）经 `TransactionScope` 同库环境事务（ORG-003 样板）。

## 5. API 契约（认证 + 组合授权）

| 端点 | 权限 | 说明 |
|------|------|------|
| `POST /api/warehouse/receipts` | `AuthorizeAsync(user, branchId, warehouse.cargo.receive)` | 登记收货 `{waybillId, branchId, receivedPieces, receivedWeightKg, receivedVolumeM3, note?}`；返回收货 DTO + 与申报的差异 |
| `GET /api/warehouse/receipts?waybillId=` | `warehouse.cargo.read` | 该运单收货记录列表（时间正序） |

- 组合授权失败 → 403；状态机违规 → 409；跨租户运单 → 404。

## 6. 测试计划（集成，HTTP 级）

1. 正常收货：已确认运单 → 登记 → 货物状态 Received、运单事件 Received、记录与审计落库；响应含申报 vs 实收差异。
2. 组合授权：有权限点但无该网点范围 → 403；无权限点 → 403。
3. 状态守卫：草稿运单 / 取消运单 / 已收货后再登记（允许分批 → 200；先验证 409 规则集）——按 §2：草稿 409、取消 409、已收货可继续分批登记（不再改状态）。
4. 跨租户运单 → 404。
5. 回滚：状态流转成功但收货记录违规（负数件数）→ 整体回滚（无状态变化、无事件）。

## 7. 验收标准

- [ ] 收货登记产生称重/体积复核记录并与申报值比对（实测）。
- [ ] 货物/仓储状态首次真实流转且经领域守卫（实测草稿/取消拒绝）。
- [ ] 网点范围组合授权实测（AGENTS §5.2）。
- [ ] 事件与记录同事务（实测）。
