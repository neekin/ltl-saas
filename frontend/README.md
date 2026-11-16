# LTL 前端（React + TypeScript + Ant Design）

PC 管理后台，遵循 AGENTS.md §9 前端规范。

## 技术栈

- Vite + React + TypeScript（严格模式）
- Ant Design 6：通用交互与视觉基础
- React Router 7：路由
- TanStack Query 5：服务端数据状态（获取、缓存、失效）
- Zustand：仅用于确有必要的客户端全局状态（如工作上下文）
- Zod：前端数据结构校验（不替代后端校验）
- Axios：统一 API Client（`src/shared/api/http.ts`）
- Vitest + React Testing Library：组件测试
- Prettier / oxlint：格式化与静态检查

## 目录结构

```text
src/
  app/
    router/      路由定义
    providers/   全局 Provider 组合（Query、AntD locale、Router）
    layouts/     应用布局
  modules/       按业务功能组织的模块（customers、pricing、waybills…随阶段接入）
    system/      平台级页面（系统状态）
    dashboard/   概览
  shared/
    api/         统一 API Client
    components/  业务组件封装
    hooks/       通用 hooks
    permissions/ 权限工具（仅控制展示，真实校验在后端）
    utils/       工具函数
    types/       通用类型
  test/          测试环境配置
```

## 常用命令

```bash
npm install
npm run dev       # http://localhost:5173，代理 /api 与 /health 到 localhost:5080
npm run build     # tsc -b && vite build
npm run lint
npm test          # vitest run
npm run format:check
```
