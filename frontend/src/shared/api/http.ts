import axios from 'axios';

/**
 * 统一 API Client（AGENTS.md §9.2）。
 * BASE-002 将在此扩展：统一认证、错误格式（ProblemDetails）处理与请求取消。
 */
export const http = axios.create({
  baseURL: '/api',
  timeout: 15000,
});
