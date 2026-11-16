# PRICE-001：计价规则模型和版本管理 — 设计方案

- 状态：已确认（规则 schema 与字段白名单为**第一版建议**，校验器与模型集中封装，审定后可扩展；分配/版本机制不受影响）
- 日期：2026-11-17
- 关联：ROADMAP P3/PRICE-001；AGENTS.md §6.4（声明式计费）；ADR-0004（声明式计价）；ADR-0007（版本粒度与 JSONB 快照）

## 1. 业务目标

为运单计价提供**安全（白名单、零代码执行）、可测试、可解释、可版本化**的计价规则模型与生命周期管理。本任务只交付**模型 + 版本管理 + 校验**；匹配与计算执行器、试算 API 属 PRICE-002。

## 2. 关键设计决策

1. **版本粒度 = 规则集**（ADR-0007）：一次"发布"固化整套规则（含全部规则、优先级、条件、费用项）为不可变快照。逐条规则版本化会导致"规则 A v3 × 规则 B v1"这类从未整体校验过的组合，整套发布是唯一能保证跨规则一致性（优先级唯一、互斥、边界覆盖）的粒度。
2. **规则定义存储 = 版本行内 JSONB 快照**（ADR-0007）：定义的权威结构是强类型 C# 模型（白名单枚举 + 分类型费用项），经**结构校验 + 语义校验**后以 JSONB 写入版本行。已发布版本不可变 = JSON 快照不可变；执行器按快照计算，结果可复现。
   - 说明：不采用全关系表拆分（条件表/费用项表/阶梯表 + 优先级/联动约束），原因是版本快照的原子不可变性与解释性（快照即文档）是硬需求，JSONB + C# 强类型 + 校验器已满足 AGENTS §6.4 的"声明式、可校验、不执行代码"。
3. **不执行任何代码**：条件字段、操作符、费用项类型、计费策略全部为**枚举白名单**；数值解析在 C# 内完成；无表达式树、无脚本。
4. **匹配语义（PRICE-002 执行器契约，本任务固化）**：按优先级升序取**第一个命中的启用规则**；每条规则**恰好一个运费项**（固定/按重/按体积/按件/分段）+ 0..n 附加费 + 至多 1 个折扣；无命中规则 → 明确报错（不静默计零，AGENTS §6.4）。
5. **金额精度**：C# 全程 `decimal`；单价列 `NUMERIC(18,4)`，金额 `NUMERIC(18,2)`；舍入策略第一版统一"保留 2 位小数、四舍五入（AwayFromZero）"，定义内以枚举白名单固化（预留扩展）。

## 3. 版本生命周期

```text
Draft ──发布(校验通过)──▶ Published ──新版本发布──▶ Superseded（保留历史）
                │
                └─ 修改草稿：仅 Draft 可编辑定义；Published/Superseded 拒绝（领域守卫）
```

- 同一规则集**同时最多一个 Draft**（部分唯一索引）、**最多一个 Published**（部分唯一索引，即"当前生效版本"）。
- 发布前置条件：规则集为启用状态、校验零错误；发布在同一事务内完成 Draft→Published 与 旧 Published→Superseded。
- 停用匹配 = 停用规则集（规则集状态 Disabled）；已发布版本历史永不删除。
- `version_number` 在创建草稿时分配（集合内递增）。说明：版本号是**内部元数据**而非对外业务单号，AGENTS §7 禁止的 `MAX+1` 针对业务单号（运单号等）；版本号分配有 `(tenant_id, rule_set_id, version_number)` 唯一索引兜底，并发冲突显式报错可重试，不产生重复。

## 4. 表结构

```sql
CREATE TABLE pricing_rule_sets (
    id uuid PRIMARY KEY,
    tenant_id uuid NOT NULL REFERENCES tenants(id),
    code varchar(64) NOT NULL,          -- 租户内唯一 ux_pricing_rule_sets_tenant_code
    name varchar(200) NOT NULL,
    status smallint NOT NULL,           -- 1=Active 2=Disabled  ck_pricing_rule_sets_status
    created_at timestamptz NOT NULL, created_by uuid NULL,
    updated_at timestamptz NULL, updated_by uuid NULL,
    xmin 并发令牌
);

CREATE TABLE pricing_rule_set_versions (
    id uuid PRIMARY KEY,
    tenant_id uuid NOT NULL,            -- 复合外键 → pricing_rule_sets(tenant_id, id)
    rule_set_id uuid NOT NULL,
    version_number int NOT NULL CHECK (>= 1),
    status smallint NOT NULL,           -- 0=Draft 1=Published 2=Superseded  ck_pricing_rule_set_versions_status
    definition jsonb NOT NULL,          -- 强类型 C# 模型快照（第 5 节）
    published_at timestamptz NULL, published_by uuid NULL,
    created_at timestamptz NOT NULL, created_by uuid NULL,
    updated_at timestamptz NULL, updated_by uuid NULL,
    xmin 并发令牌,
    UNIQUE (tenant_id, rule_set_id, version_number),
    -- 部分唯一索引：每集合最多一个 Published / 一个 Draft
    CREATE UNIQUE INDEX ... WHERE status = 1;
    CREATE UNIQUE INDEX ... WHERE status = 0
);
```

跨模块引用（线路/网点/客户）通过**条件值**（Guid）表达，不在本模块建外键——条件字段引用的资源在 PRICE-002 执行器按租户范围实时校验可达性，本任务语义校验只验格式（Guid 可解析）。理由：规则版本是历史快照，引用外键会把"线路停用/删除"与历史版本耦合，破坏不可变语义；AGENTS §5.1 的跨租户一致性由执行器查询时的租户过滤器保证（匹配到的资源必然属于本租户）。

## 5. 规则定义模型（白名单）

```text
PricingRuleDefinition
  Rounding: HalfUpTwoDecimals           -- v1 唯一允许值（枚举预留扩展）
  Rules: [                              -- ≥1 条
    PricingRule
      Priority: int ≥1                  -- 集合内唯一；升序 = 匹配顺序
      Name: 非空 ≤200
      Enabled: bool                     -- 未启用规则参与校验但不参与匹配（PRICE-002）
      BillingWeightStrategy:            -- 计费重量策略（白名单）
        WeightVolumeMax | WeightVolumeMin | WeightOnly | VolumeOnly
      VolumeToWeightFactor: decimal?    -- Max/Min 时必填 >0（体积重 = m³ × 系数）
      Conditions: [                     -- 全部 AND；可为空（无条件 = 默认规则）
        PricingCondition
          Field: WeightKg | VolumeM3 | PieceCount        -- 数值字段
                | CustomerId | RouteId | OriginBranchId | DestBranchId   -- Id 字段
          Operator: 数值字段 → GreaterThan | GreaterOrEqual | LessThan | LessOrEqual | Between
                    Id 字段   → Equals | In
          Value: string（按字段类型解析）；Between 时 Value < Value2
      ]
      Freight: 恰好 1 个（运费主项，金额必须 >0，禁止零元运费）
        Type: Fixed   → Amount > 0
             ByWeight | ByVolume | ByPieces → UnitPrice > 0；MinCharge ≥0 可选
             Tiered → Tiers ≥1：UpTo 严格递增且 >0（末档 UpTo=null=无上限，"超出部分计费"）；
                      每档 UnitPrice >0；MinCharge ≥0 可选（起步价/最低收费）
      Surcharges: 0..n                  -- 提货/送货/装卸/包装等附加费
        Basis: PerWaybill | PerPiece    -- v1 白名单（预留扩展）
        Amount ≥0
      Discount: 0..1                    -- 折扣
        Mode: Rate → 0 < Rate < 100     -- 按比例（%，不含全免，全免用 Amount）
              Amount → ≥0              -- 固定减免
  ]
```

费用项分类型建模（非单一"万能费用项"）：`Freight`/`Surcharges`/`Discount` 是结构化属性，"恰好一个运费项""至多一个折扣"由**结构**保证，校验器只补语义（金额、区间、阶梯连续性）。

## 6. 校验（发布前置，零错误方可发布）

- **结构校验**：JSON 反序列化强类型模型（未知成员拒绝、未知枚举拒绝、未知多态类型拒绝）——畸形/越权定义在入口即失败。
- **语义校验**（`PricingDefinitionValidator` 纯函数，返回结构化错误列表）：第 5 节全部约束（优先级唯一、字段-操作符兼容、值可解析、Between 区间合法、阶梯递增且末档无界、金额/单价/系数边界、折扣区间）。
- 校验失败 → 发布拒绝并返回全部错误（不是第一条）。

## 7. Application 服务（Pricing 模块）

- 规则集：创建 / 重命名 / 启用 / 停用。
- 版本：创建草稿（校验定义，分配版本号）、更新草稿定义（仅 Draft）、试校验（返回错误列表，不改状态）、发布（事务内校验 + Draft→Published + 旧 Published→Superseded）。
- 查询：规则集列表、版本列表、当前生效版本定义（PRICE-002 执行器入口）。
- 无 HTTP 端点（与 ORG-001/CUS-001 边界一致：认证/登录未交付前不暴露业务 API）。

## 8. 测试计划

### 单元测试

- `PricingDefinitionValidatorTests`：合法定义零错误；空规则；优先级重复；字段-操作符不兼容；数值解析失败；Between 区间非法；阶梯不递增/末档有界/单价≤0；Max/Min 策略缺系数；附加费负数；折扣 Rate 越界/Amount 与 Mode 不匹配；运费零元；名称超长；未知舍入策略。
- 版本生命周期领域守卫：Published/Superseded 拒绝改定义；重复发布拒绝。

### 集成测试（Testcontainers）

1. 迁移空库执行。
2. `(tenant_id, code)` 租户范围唯一；跨租户允许同编码。
3. 部分唯一索引：第二个 Published / 第二个 Draft 被拒绝。
4. 跨租户复合外键拒绝（23503）。
5. 过滤器隔离 + fail-closed。
6. 发布流：Draft→Published，旧 Published→Superseded，且仅一个 Published。
7. 已发布版本定义不可修改（服务层守卫 + 持久层验证快照未变）。
8. 草稿更新持久化 + JSON 精度往返（如 0.35 单价、阶梯边界 3.5kg）。
9. xmin 乐观并发。

## 9. 验收标准

- [ ] 定义仅含白名单枚举字段/操作符/策略，无任何可执行代码载体。
- [ ] 已发布版本不可原地修改；修改走新版本（测试守护）。
- [ ] 校验零错误才可发布；错误全量返回。
- [ ] 同输入同版本结果确定可复现（PRICE-002 执行器按快照计算）。
- [ ] 金额 decimal / NUMERIC；租户隔离与复合外键测试通过。
