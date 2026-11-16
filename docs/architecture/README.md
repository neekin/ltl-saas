# 架构：模块化单体

> 完整规则见 AGENTS.md §4。本文件维护当前模块清单与依赖方向约定。

## 原则

- 模块化单体，不提前拆微服务（业务约束确认：先单数据库起步）。
- 每个模块内部按 `Domain / Application / Infrastructure`（必要时 Presentation）分层。
- **依赖方向**：
  - Domain 不依赖 EF Core、ASP.NET Core、数据库或其他模块的 Infrastructure。
  - Application 依赖 Domain 和抽象接口。
  - Infrastructure 实现 Application/Domain 所需接口。
  - API Host（`Ltl.Api`）负责组合和启动模块。
  - 模块之间通过公开 Application 接口或集成事件通信；禁止直接引用其他模块的 DbContext、Infrastructure 实现或私有实体。
  - 禁止把所有实体和业务逻辑放进 SharedKernel。

## 模块清单

| 项目 | 职责 | 状态 |
| --- | --- | --- |
| Ltl.Api | 组合根与 API Host | BASE-001 已建骨架 |
| Ltl.SharedKernel | 跨模块通用原语（不含实体/业务逻辑） | BASE-001 已建骨架 |
| Ltl.Modules.Tenancy | 租户生命周期、租户上下文 | 骨架；实现于 TEN-001 |
| Ltl.Modules.Organization | 网点、员工、角色、数据范围 | 骨架；实现于 ORG-001/002 |
| Ltl.Modules.Customers | 客户、联系人、地址、网点授权 | 骨架；实现于 CUS-001 |
| Ltl.Modules.Routing | 线路、站点、运输方案与区段 | 骨架；实现于 ROUTE-001 |
| Ltl.Modules.Pricing | 计费规则集、版本、匹配、计算、快照 | 骨架；实现于 PRICE-001/002 |
| Ltl.Modules.Waybills | 运单、货物件、费用、状态与事件 | 骨架；实现于 WB-001/002 |
| Ltl.Modules.Warehouse | 收货、入库、分拣、调拨 | 骨架；P4/P5 |
| Ltl.Modules.Transport | 车辆、司机、配载、运输批次与区段 | 骨架；P5 |
| Ltl.Modules.Arrival | 到货登记、验货、差异处理 | 骨架；P6 |
| Ltl.Modules.Delivery | 自提、配送、签收凭证 | 骨架；P6 |
| Ltl.Modules.Finance | 应收、收款、核销、退款、月结账单 | 骨架；P7 |
| Ltl.Modules.Cod | 代收货款：收款、结算、付款、异常 | 骨架；P8 |
| Ltl.Modules.Reporting | 经营看板与报表（查询模型） | 骨架；P9 |
| Ltl.Modules.Platform | 文件、审计、序号、幂等、Outbox | 骨架；按需随任务实现 |

## 事务与事件

- 同一数据库内强一致的操作使用明确的数据库事务；不得把必须原子完成的步骤拆成异步事件。
- 跨模块可靠事件使用 Outbox 模式；消费端必须幂等。
- 后台任务（Hangfire）显式建立租户上下文。

## Git 工作流

- 主分支 `main`，通过功能分支提交，提交信息遵循 AGENTS.md §11 格式。
- **主分支保护策略**（需要远程仓库就绪后在托管平台配置）：禁止直推 `main`、合并前必须通过 CI（构建 + 测试）、要求至少一人审查。
- CI：`.github/workflows/ci.yml` 覆盖后端构建/测试与前端 lint/格式/测试/构建。
