# FIN-003：月结信用政策与开单额度检查 — 设计方案

- 状态：已确认
- 日期：2026-11-19
- 关联：ROADMAP P7（月结客户账期和信用额度、逾期和信用额度检查）；AGENTS §6.5

## 1. 业务目标

月结客户的信用额度是开单前置校验依据：未收余额超过额度时拒绝月结开单。
本设计补齐额度配置闭环（此前 `Customer.SetCreditPolicy` 与开单校验均已存在，
但无 API/界面可设置额度，校验形同虚设）。

## 2. 规则

| 规则 | 说明 |
|------|------|
| 额度与账期成对设置 | `credit_limit NUMERIC(18,2)`、`payment_terms_days INT`，均可为 NULL（未设置） |
| 负数拒绝 | 领域层 `SetCreditPolicy` 校验（ArgumentException → 400） |
| NULL = 不检查 | `WaybillService.CheckCreditLimitAsync`：额度为 NULL 的客户不做额度校验 |
| 检查口径 | 未收余额 = Σ(receivable.amount − settled_amount)，status != Settled |
| 现付/到付不受限 | 额度检查仅月结客户业务含义上有意义，实现按客户读取额度（无额度 → 通过） |
| 审计 | 变更记录 `customer.credit-policy` 审计事件（含前后额度/账期） |

## 3. API

| 端点 | 权限 | 说明 |
|------|------|------|
| `PUT /api/customers/{id}/credit-policy` | customer.manage | 请求 `{ creditLimit, paymentTermsDays }`（可 null） |
| `GET /api/customers/{id}` | customer.read | 响应含 `creditLimit`、`paymentTermsDays` |

## 4. 界面

- 客户列表新增「月结额度」列（额度 + 账期）。
- 客户详情抽屉「月结信用政策」区：额度/账期内联编辑，留空即清除。

## 5. 幂等与并发

- 信用政策更新为覆盖式单行更新，行级 `xmin` 乐观并发兜底。
- 开单额度检查与收款之间存在天然窗口（读后写），严格防超限需在收款侧
  再校验——当前在开单侧前置检查（挡住新增敞口），收款侧按未收余额校验超额。

## 6. 测试

- 集成：`CreditPolicy_SetPersistAndClear_UpdatesCustomer`
  （设置/持久化/负数 400/清除/无权限 403）。
- 开单额度拒绝路径由 WaybillService 既有测试覆盖。
