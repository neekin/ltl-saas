# ADR（Architecture Decision Records）

架构决策记录。影响架构、数据或安全的决策必须记录为 ADR。

## 规则

- 已接受的 ADR 不可原地修改决策内容；如需变更，新增 ADR 并将旧 ADR 标记为“被 ADR-XXXX 取代”。
- 状态：`提议中` / `已接受` / `已废弃` / `被 ADR-XXXX 取代`。

## 索引

| 编号 | 标题 | 状态 |
| --- | --- | --- |
| [ADR-0001](ADR-0001-multi-tenancy.md) | 多租户隔离策略 | 已接受 |
| [ADR-0002](ADR-0002-modular-monolith.md) | 模块化单体与模块边界 | 已接受 |
| [ADR-0003](ADR-0003-transaction-and-outbox.md) | 事务、Outbox 与幂等策略 | 已接受 |
| [ADR-0004](ADR-0004-declarative-pricing-rules.md) | 声明式计价规则格式 | 已接受 |
| [ADR-0005](ADR-0005-authentication-and-tenant-resolution.md) | 认证机制（JWT）与租户上下文解析 | 已接受 |
