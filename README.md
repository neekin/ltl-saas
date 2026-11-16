# LTL 专线零担物流 SaaS

面向中国专线零担物流企业的多租户 SaaS：单公司、多直营网点、多线路，覆盖报价、开单、收货、调拨、运输、到货、配送、签收、收款与 COD 结算。

## 文档

| 文档 | 说明 |
| --- | --- |
| [AGENTS.md](AGENTS.md) | 最高优先级项目约定：架构、安全、领域规则、禁止事项 |
| [ROADMAP.md](ROADMAP.md) | 分阶段实施计划与验收标准 |
| [docs/](docs/README.md) | 需求、领域、数据库、架构、API、ADR 文档 |

## 技术栈

- 后端：.NET 11 / ASP.NET Core / EF Core + Npgsql / PostgreSQL / Redis / Hangfire
- 前端：React + TypeScript + Vite + Ant Design + TanStack Query
- 基础设施：Docker / Dokku，模块化单体架构

## 目录结构

```text
backend/    .NET solution（Ltl.Api 组合根 + 各业务模块 + 测试）
frontend/   React 管理后台（Vite）
docs/       领域与架构文档、ADR
compose.yaml  本地 PostgreSQL + Redis
```

## 本地开发

### 前置要求

- .NET SDK 11（见 `backend/global.json`）
- Node.js 20+ 与 npm
- Docker

### 1. 启动数据库

```bash
cp .env.example .env
docker compose up -d
```

PostgreSQL 默认映射到本机 `5433`，Redis 映射到 `6380`（避免与本机已有实例冲突），可在 `.env` 调整。

### 2. 启动后端 API

```bash
cd backend
dotnet run --project src/Ltl.Api
```

- 健康检查：<http://localhost:5080/health>（端口避开 macOS AirPlay 占用的 5000；OpenAPI 文档随 BASE-002 接入）

### 3. 启动前端

```bash
cd frontend
npm install
npm run dev
```

前端开发服务器代理 `/api` 与 `/health` 到后端 `5080` 端口；打开"系统状态"页可验证前后端联通。

### 4. 运行测试

```bash
# 后端
cd backend && dotnet test

# 前端
cd frontend && npm test
```

## Git 工作流

- 主分支 `main`；远程仓库建立后需配置主分支保护（禁止直推、要求 CI 通过）。
- 提交信息格式见 AGENTS.md 第 11 节，例如 `feat(waybill): add multi-package waybill creation`。
