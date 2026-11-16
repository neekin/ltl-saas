# 项目文档索引

> 阅读顺序：`AGENTS.md`（最高优先级约定）→ `ROADMAP.md`（阶段计划）→ 本目录文档。

| 目录 | 内容 |
| --- | --- |
| [requirements/](requirements/README.md) | 需求与业务约束（入口指向 ROADMAP） |
| [domain/](domain/glossary.md) | 领域术语表、业务流程图 |
| [database/](database/README.md) | PostgreSQL 与数据规范 |
| [architecture/](architecture/README.md) | 模块边界、依赖方向、Git 工作流 |
| [api/](api/README.md) | API 契约与规范 |
| [adr/](adr/README.md) | 架构决策记录（ADR） |

规则：

- 修改业务规则时同步更新领域文档和测试（AGENTS.md §11）。
- 修改 API 时同步更新 API 文档或契约。
- 影响架构的决策必须新增 ADR，不可原地推翻已接受的 ADR；如需变更，写新 ADR 标记旧 ADR 被取代。
