# ROUTE-001：线路、站点和运输方案 — 设计方案

- 状态：已确认（概念定义来自 AGENTS.md §6.2 已确认约束）
- 日期：2026-11-17
- 关联：ROADMAP P2/ROUTE-001；AGENTS.md §6.2、§7；ADR-0001、ADR-0002；ORG-001/CUS-001 设计文档

## 1. 业务目标

建立线路主数据与计划运输路径，支持直达和多段（中转）运输方案（P2 验收：一条运输方案可包含多个运输区段），为 P5 运输批次与广州—武汉—长沙多段运输场景奠定模型。

概念不混淆（AGENTS §6.2）：`Route` 是业务线路定义；`RouteStop` 是线路站点；`TransportPlan` 是计划运输路径；`TransportPlanSegment` 是计划路径区段；**实际车辆运输**（TransportTrip）属 P5，不在本任务。

## 2. 范围

**实现：**

1. 领域模型：`Route`、`RouteStop`、`TransportPlan`、`TransportPlanSegment`。
2. 完整性校验器（纯函数）：`RouteSpec`（站点顺序完整性）、`TransportPlanSpec`（区段连续性）。
3. EF Core 映射 + `InitialRouting` 迁移（复合外键）。
4. 单元测试 + 集成测试（越权、隔离、唯一约束、并发）。

**不实现（明确排除）：**

- 线路/方案的 HTTP CRUD 端点（登录任务交付）；
- `TransportTrip`（实际运输，P5）；
- 线路历史引用保护（“已被业务引用的线路不能物理删除”在运单引用线路后生效——本阶段无引用方，规则在引用方任务落地并补测试）；
- 站点/区段的编辑服务（变更以整体重设语义随 CRUD 任务实现，本任务交付校验规则与数据层约束）。

## 3. 表结构（全部为租户范围表）

### routes（业务线路）

```sql
CREATE TABLE routes (
    id uuid PRIMARY KEY,
    tenant_id uuid NOT NULL,
    code varchar(64) NOT NULL,              -- 线路编码，租户内唯一
    name varchar(200) NOT NULL,
    origin_branch_id uuid NOT NULL,         -- 起点网点 → branches (tenant_id, id) 复合外键
    destination_branch_id uuid NOT NULL,    -- 终点网点 → 复合外键
    status smallint NOT NULL CHECK (status IN (0, 1)),  -- 0=启用 1=停用
    created_at timestamptz NOT NULL, ... , xmin rowversion
);
CREATE UNIQUE INDEX ux_routes_tenant_code ON routes (tenant_id, code);
CREATE UNIQUE INDEX ux_routes_tenant_id ON routes (tenant_id, id);
```

领域规则：起点 ≠ 终点（`Route.Create` 校验，空跑线路无意义）。

### route_stops（线路站点）

```sql
CREATE TABLE route_stops (
    id uuid PRIMARY KEY,
    tenant_id uuid NOT NULL,
    route_id uuid NOT NULL,                 -- 复合外键 (tenant_id, route_id) → routes(tenant_id, id)
    branch_id uuid NOT NULL,                -- 站点网点 → branches(tenant_id, id) 复合外键
    sequence int NOT NULL,                  -- 站点顺序：0=起点，连续递增，最后一个=终点
    remark varchar(200) NULL,
    created_at timestamptz NOT NULL, ... , xmin rowversion
);
CREATE UNIQUE INDEX ux_route_stops_tenant_route_sequence ON route_stops (tenant_id, route_id, sequence);
CREATE UNIQUE INDEX ux_route_stops_tenant_route_branch ON route_stops (tenant_id, route_id, branch_id);
```

### transport_plans（运输方案）

```sql
CREATE TABLE transport_plans (
    id uuid PRIMARY KEY,
    tenant_id uuid NOT NULL,
    route_id uuid NOT NULL,                 -- 方案基于线路 → 复合外键
    name varchar(200) NOT NULL,
    status smallint NOT NULL CHECK (status IN (0, 1)),  -- 0=启用 1=停用
    created_at timestamptz NOT NULL, ... , xmin rowversion
);
CREATE UNIQUE INDEX ux_transport_plans_tenant_id ON transport_plans (tenant_id, id);
```

### transport_plan_segments（计划区段）

```sql
CREATE TABLE transport_plan_segments (
    id uuid PRIMARY KEY,
    tenant_id uuid NOT NULL,
    plan_id uuid NOT NULL,                  -- 复合外键 (tenant_id, plan_id) → transport_plans(tenant_id, id)
    sequence int NOT NULL,                  -- 区段顺序：1 开始，连续递增
    from_branch_id uuid NOT NULL,           -- 区段起点网点 → branches 复合外键
    to_branch_id uuid NOT NULL,             -- 区段终点网点 → 复合外键
    created_at timestamptz NOT NULL, ... , xmin rowversion
);
CREATE UNIQUE INDEX ux_transport_plan_segments_tenant_plan_sequence
    ON transport_plan_segments (tenant_id, plan_id, sequence);
```

## 4. 站点顺序与区段完整性校验（纯函数）

**RouteSpec.Validate(originBranchId, destinationBranchId, stops)**（站点集合）：

1. 站点数 ≥ 2（起点 + 终点，直达线即 2 站）；
2. sequence 必须为 0..N-1 连续、无空洞；
3. 同一线路中同一网点不得出现两次；
4. sequence=0 的站点必须是线路起点；sequence=N-1 必须是终点。

**TransportPlanSpec.Validate(originBranchId, destinationBranchId, routeStopBranchIds, segments)**（区段集合）：

1. 区段数 ≥ 1（1 区段=直达，多区段=中转）；
2. sequence 必须为 1..N 连续；
3. 每区段 from ≠ to（空区段无意义）；
4. 首区段 from = 线路起点；尾区段 to = 线路终点；
5. 相邻区段首尾相连：seg[i].to == seg[i+1].from；
6. 每个区段端点必须属于该线路的站点序列（区段只能在线路站点之间行驶）。

返回违规列表（结构化错误，供后续 CRUD/API 返回），不抛异常——校验器纯函数可组合复用。

## 5. 数据库层约束与模块边界

- 复合外键复用既定模式：EF 模型只配列与唯一索引，迁移中 `migrationBuilder.AddForeignKey` 显式添加，可在空库执行。
- 跨模块引用：`branches(tenant_id, id)`（ORG-001 已建唯一索引）；Routing 模块**不引用** Organization 领域实体（AGENTS §4.2）。
- 租户范围唯一约束含 `tenant_id`；跨行完整性（顺序连续、首尾相连）无法用单表约束表达 → 由 Spec 校验器在变更时强制执行（写入顺序属 CRUD 任务）。

## 6. 测试计划

### 单元测试（Spec 纯函数）

- RouteSpec：站点连续、起点/终点匹配、重复网点、站点不足、合法直达（2 站）与经停。
- TransportPlanSpec：区段连续、首尾相连、端点必须属于站点序列、from=to、直达 1 区段、多段中转、起终点不匹配。

### 集成测试（Testcontainers）

1. 迁移可在空库执行。
2. 跨租户同编码允许、同租户重复被拒。
3. **跨租户复合外键拒绝**：站点/区段引用其他租户的网点、区段引用其他租户的方案 → `23503`。
4. 全局过滤器隔离 + fail-closed。
5. 同线路同序号唯一、同线路同网点唯一。
6. 起点=终点被领域校验拒绝。
7. xmin 乐观并发。

## 7. 验收标准

- [ ] 一条运输方案可包含多个运输区段（直达=1 区段、中转=多区段）。
- [ ] 线路站点引用网点且顺序可校验；区段必须首尾相连并落在线路站点之内。
- [ ] 数据按租户隔离；越权引用被数据库层拒绝。
- [ ] 上述测试全部通过。
