# 数据库设计规范

> 完整规范见 AGENTS.md §7。本文件是开发时的速查摘要；如有冲突以 AGENTS.md 为准。

## 标识与命名

- 主键默认 UUID；数据库标识符一律 `snake_case`；C# 类型与属性 `PascalCase`。
- 所有租户范围唯一约束必须包含 `tenant_id`，例如 `(tenant_id, waybill_no)`。
- 外键、唯一约束、检查约束和索引必须在数据库层设计，不得只依赖应用代码。

## 数据类型

| 数据 | PostgreSQL 类型 | 说明 |
| --- | --- | --- |
| 金额 | `NUMERIC(18,2)` | 禁止 `float`/`double`；应用层一律 `decimal` |
| 重量 | `NUMERIC(12,3)` | |
| 体积 | `NUMERIC(12,4)` | |
| 时间点 | `TIMESTAMPTZ` | 应用层 UTC；界面按 `Asia/Shanghai` 展示 |
| 主键 | UUID | |

## 通用审计字段

业务表按需增加 `created_at`、`created_by`、`updated_at`、`updated_by`、`row_version`。

## 约束性规则

- 业务单号禁止 `SELECT MAX(number) + 1`；使用事务安全的序号分配方案（Platform 模块 `branch_sequences`）。
- 财务流水与 COD 资金流水为追加式表；错误通过反向流水、退款或冲正处理，不更新/删除原记录。
- 跨租户关联必须通过复合外键或等效数据库约束保证一致性。
- 迁移必须可在空数据库执行；生产迁移考虑向前兼容和回滚策略。

## 多租户防线

- EF Core 全局查询过滤器只是纵深防御，不是唯一安全边界；原生 SQL 必须显式加租户条件。
- 数据访问功能的测试必须覆盖：跨租户读/写、越权网点访问、ID 猜测、导出/附件/后台任务越权、连接池租户上下文泄漏（AGENTS.md §5.3）。

## 索引原则

不得在没有评估查询模式时盲目为每个字段加索引。
