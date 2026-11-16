# Ltl.SharedKernel

跨模块共享的通用原语（如 Result、Error、时钟抽象、通用值对象）。

约束（AGENTS.md §4.2）：

- 仅允许存放与业务领域无关的通用代码。
- **禁止**把各模块的实体、业务逻辑或基础设施代码放进本项目。
- 不依赖 EF Core、ASP.NET Core 或任何数据库。
