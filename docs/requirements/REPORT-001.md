# REPORT-001：经营看板 — 设计方案

- 状态：已确认
- 日期：2026-11-18
- 关联：ROADMAP P9（经营看板：运单量、货量、运费收入、应收、收款）；AGENTS.md §5.1

## 1. 业务目标

提供租户级经营指标聚合查询，支持管理层快速掌握运营概况。只读端点，无写操作。

## 2. 指标定义

| 指标 | 数据来源 | 聚合口径 |
|------|---------|---------|
| 运单总量 | waybills | COUNT（排除 Cancelled） |
| 运单总量（本月） | waybills | COUNT + created_at 在本月 |
| 货物总重量 | waybill_packages | SUM(weight_kg × quantity) |
| 货物总体积 | waybill_packages | SUM(volume_m³ × quantity) |
| 运费收入（已固化） | waybills | SUM(total_amount)（排除 Cancelled） |
| 应收总额 | receivables | SUM(amount) |
| 已收总额 | receivables | SUM(settled_amount) |
| 未收余额 | receivables | SUM(amount - settled_amount) |
| COD 已收 | cod_orders | SUM(collected_amount) |
| COD 已付 | cod_orders | SUM(paid_amount) |
| 网点数 | branches | COUNT（Active） |
| 客户数 | customers | COUNT（Active） |

## 3. 实现

- 聚合查询走原生 SQL（跨模块 JOIN + 聚合，AGENTS §5.1）。
- 按月份筛选参数（可选，不传则全量）。
- 响应结构化 DTO（前端仪表盘直接渲染）。

## 4. API

| 端点 | 权限 |
|------|------|
| `GET /api/reports/dashboard?month=` | finance.read（需登录） |
