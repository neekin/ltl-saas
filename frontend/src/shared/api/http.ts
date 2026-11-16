import axios from 'axios';

/**
 * 统一 API Client（AGENTS.md §9.2）：
 * - baseURL 指向后端业务 API（开发环境经 Vite 代理）
 * - 响应错误统一解析 RFC 9457 ProblemDetails，向上层抛出仅含用户可读消息的 Error
 */
export const http = axios.create({
  baseURL: '/api',
  timeout: 15000,
});

interface ProblemDetails {
  title?: string;
  detail?: string;
  status?: number;
}

http.interceptors.response.use(
  (response) => response,
  (error: unknown) => {
    if (axios.isAxiosError(error)) {
      const problem = error.response?.data as ProblemDetails | undefined;
      // 内部异常仅返回 "服务器内部错误" 等安全标题，不暴露堆栈（AGENTS.md §8）
      const message = problem?.title || problem?.detail || error.message || '请求失败';
      return Promise.reject(new Error(message));
    }
    return Promise.reject(error instanceof Error ? error : new Error('请求失败'));
  },
);
