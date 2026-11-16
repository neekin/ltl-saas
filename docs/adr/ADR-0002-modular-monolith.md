# ADR-0002：模块化单体与模块边界

- 状态：已接受
- 日期：2026-11-17
- 关联：AGENTS.md §4；ROADMAP §2.3、§3

## 背景

系统按领域划分为 14 个业务模块（Tenancy、Organization、Customers、Routing、Pricing、Waybills、Warehouse、Transport、Arrival、Delivery、Finance、COD、Reporting、Platform）。需要在不引入微服务复杂度的前提下，保证模块边界清晰、可逐步演进。

## 决策

1. 采用模块化单体，单数据库起步，不提前拆分微服务。
2. 每个模块为独立项目（`Ltl.Modules.<Name>`），内部按 `Domain / Application / Infrastructure`（必要时 Presentation）分层。
3. 依赖方向：Domain 不依赖 EF Core、ASP.NET Core、数据库或其他模块 Infrastructure；Application 依赖 Domain 与抽象接口；Infrastructure 实现抽象；API Host（`Ltl.Api`）负责组合与启动。
4. 模块之间只通过公开 Application 接口或集成事件通信；禁止直接引用其他模块的 DbContext、Infrastructure 实现或私有实体；禁止把所有实体和业务逻辑放进 SharedKernel（`Ltl.SharedKernel` 仅存跨模块通用原语）。
5. 跨模块可靠事件使用 Outbox 模式（见 ADR-0003）。
6. 后端 API 不绑定桌面端，为后续移动端预留。

## 后果

- 模块边界靠代码审查与架构约定维持；后续可在需要时补充架构守卫测试（Architecture Tests）。
- 首批任务必须按 ROADMAP §8 顺序推进（BASE → TEN → ORG → CUS → ROUTE → SEQ → PRICE → WB），不从运单页面开始。

## 依据

AGENTS.md §1（架构约束）、§4（架构原则）；ROADMAP §2.3（架构原则）、§8（首批任务顺序）。
