# PRICE-002：计价执行器和试算 — 设计方案

- 状态：已确认（匹配与计算语义为第一版契约，PRICE-001 定义模型不变）
- 日期：2026-11-17
- 关联：ROADMAP P3/PRICE-002；PRICE-001（规则模型与版本管理）；AGENTS.md §6.4；ADR-0004/0007

## 1. 业务目标

在 PRICE-001 的规则版本之上实现**匹配引擎与计算器**：同输入同版本结果确定可复现；无匹配规则明确报错（不静默计零）；逐项费用可解释（ROADMAP P3 验收"计价结果能解释每项费用的来源"）。

## 2. 边界与非目标

- **交付**：Domain 纯函数执行器（匹配 + 计算 + 解释）、Application 试算服务（按已发布版本试算）。
- **不交付**：HTTP 端点（与 ORG/CUS/ROUTE/PRICE-001 边界一致：认证/登录未交付前不暴露业务 API，"试算 API"以 Application 服务为对外契约，HTTP 随登录任务交付）；输入快照与费用明细的**持久化**随 WB-002（运单固化费用）落地，本任务的结果结构已携带快照与版本引用所需全部数据。

## 3. 匹配语义（固化 PRICE-001 契约）

1. 候选规则 = 版本定义内 `Enabled = true` 的规则，按 `Priority` 升序遍历；
2. 单条规则命中 = **全部条件 AND 成立**（空条件 = 默认规则，恒命中）；
3. **取第一个命中的规则**（优先级唯一由 PRICE-001 校验保证，结果无歧义）；
4. 无命中 → `PricingMatchFailedException` 明确报错（携带规则集与版本信息，AGENTS §6.4 禁止静默计零）；
5. 条件引用的 Id 字段在输入缺失（null）时条件不成立（客户专属规则要求输入携带客户）。
6. 条件引用的资源（客户/线路/网点）属本租户由**调用方输入**保证——试算服务按租户过滤器加载版本（跨租户规则集 Id → `EntityNotFoundException`）；资源存在性校验随 WB-001 开单（届时输入来自已验证的运单数据）。

## 4. 计算语义

### 4.1 计费数量（BillingWeightStrategy）

| 策略 | 计费数量 |
|------|---------|
| WeightOnly | 重量 kg |
| VolumeOnly | 体积 m³ |
| PieceCount | 件数 |
| WeightVolumeMax | max(重量, 体积 × 体积重系数) |
| WeightVolumeMin | min(重量, 体积 × 体积重系数) |

计费数量保留 **2 位小数 HalfUp**（件数为整数）后再参与单价/分段计算。

### 4.2 运费主项

- Fixed：`Amount`；
- ByWeight/ByVolume/ByPieces：`UnitPrice × 计费数量`；
- Tiered：逐段 `[上一档上界, 本档上界) × 本档单价` 求和（末档上界 null = 超出部分按末档单价）；
- 上述结果与 `MinCharge`（如配置）取大（最低收费/起步价）。

### 4.3 附加费与折扣

- 附加费：PerWaybill → `Amount`；PerPiece → `Amount × 件数`；
- 折扣（至多 1 个，作用于 运费+附加费 小计）：Rate → `小计 × Rate/100`；Amount → 固定减免；
- 总额 = `max(小计 − 折扣, 0)`。

### 4.4 精度与舍入（RoundingPolicy = HalfUpTwoDecimals）

全程 `decimal`；运费、每条附加费、折扣各自按公式计算后**保留 2 位 HalfUp**；小计与总额为已舍入项的代数和（不再二次舍入引入误差）。结果可复现：执行器为纯函数，仅依赖（定义快照, 输入）。

## 5. 结果结构（可解释性）

```text
PricingQuoteResult
  MatchedRulePriority / MatchedRuleName     -- 命中规则（费用来源）
  BillingQuantity / BillingBasis            -- 计费数量与口径（如 "计费重量 12.35 kg"）
  Items: [ { Kind, Name, Amount, Detail } ] -- 逐项：运费/附加费，Detail 为人类可读计算过程
  Discount { Name, Amount, Detail }?        -- 折扣单列
  Subtotal / Total                          -- 小计与总额
PricingTrialResult（Application）
  RuleSetId/Code, VersionNumber, VersionId  -- 规则版本引用（WB-002 快照落库用）
  Input（输入快照回显） + PricingQuoteResult
```

## 6. 测试计划

### 单元测试（执行器纯函数）

- 匹配：优先级顺序、首个命中、停用规则跳过、默认规则、条件各操作符（Equals/In/数值比较/Between）、Id 缺失不命中；
- 计费数量：四种策略 + 体积重系数（取大/取小）；
- 运费：Fixed、按重/体积/件单价、**多档分段跨越**（如 3.35kg 两档）、最低收费触发/未触发；
- 附加费：整票/每件；
- 折扣：比例/固定减免、减免后 clamp ≥0；
- 舍入：HalfUp 边界（x.005）、累计一致性；
- 无匹配 → `PricingMatchFailedException`；
- **确定性**：同输入同定义重复计算结果一致。

### 集成测试（Testcontainers）

- 经服务对**已发布版本**试算成功且结果正确；
- 规则集停用 → 明确报错；无已发布版本 → 明确报错；
- 跨租户规则集 → `EntityNotFoundException`；
- 版本被新版本取代后：按新版本试算得新结果，旧版本快照仍在（历史可追溯）。

## 7. 验收标准

- [ ] 同输入同版本确定可复现（实测）。
- [ ] 无匹配规则/无生效版本/停用规则集 → 明确报错，绝不出静默零价。
- [ ] 结果含命中规则、计费口径、逐项明细与折扣来源。
- [ ] 金额 decimal、HalfUp 2 位；边界/分段/组合规则单元测试通过。
