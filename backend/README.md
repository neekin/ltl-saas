# LTL 后端（.NET 11）

模块化单体解决方案，遵循 AGENTS.md §4 架构原则。

## 结构

```text
src/
  Ltl.Api/              组合根与 API Host：启动、组合模块、中间件
  Ltl.SharedKernel/     仅限跨模块通用原语；禁止存放实体和业务逻辑
  Ltl.Modules.Tenancy/       租户生命周期、租户上下文
  Ltl.Modules.Organization/  网点、员工、角色、数据范围
  Ltl.Modules.Customers/     客户、联系人、地址、网点授权
  Ltl.Modules.Routing/       线路、站点、运输方案与区段
  Ltl.Modules.Pricing/       计费规则集、版本、匹配、计算、快照
  Ltl.Modules.Waybills/      运单、货物件、费用、状态与事件
  Ltl.Modules.Warehouse/     收货、入库、分拣、调拨
  Ltl.Modules.Transport/     车辆、司机、配载、运输批次与区段
  Ltl.Modules.Arrival/       到货登记、验货、差异处理
  Ltl.Modules.Delivery/      自提、配送、签收凭证
  Ltl.Modules.Finance/       应收、收款、核销、退款、月结账单
  Ltl.Modules.Cod/           代收货款：收款、结算、付款、异常
  Ltl.Modules.Reporting/     经营看板与报表（查询模型）
  Ltl.Modules.Platform/      文件、审计、序号、幂等、Outbox
tests/
  Ltl.UnitTests/             领域规则、计价、权限单元测试
  Ltl.IntegrationTests/      EF 映射、租户隔离、幂等集成测试（Testcontainers）
```

每个模块内部分层：`Domain/`、`Application/`、`Infrastructure/`（必要时加 Presentation，随任务演进）。

## 依赖规则

- Domain 不依赖 EF Core、ASP.NET Core、数据库或其他模块 Infrastructure。
- 模块之间只通过公开 Application 接口或集成事件通信；禁止直接引用其他模块的 DbContext 或私有实体。
- API Host（Ltl.Api）负责组合和启动；按任务逐步把模块接入组合根。

## 常用命令

```bash
dotnet build
dotnet test
dotnet run --project src/Ltl.Api    # http://localhost:5000/health
```
