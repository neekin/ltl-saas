import axios from 'axios';
import { useAuthStore } from '../../modules/auth/store/authStore';

/**
 * 统一 API Client（AGENTS.md §9.2）：
 - 自动附带 JWT；401 时尝试刷新一次，失败则清除会话交由路由守卫跳转登录
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

http.interceptors.request.use((config) => {
  const token = useAuthStore.getState().accessToken;
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

let refreshing: Promise<boolean> | null = null;

async function tryRefresh(): Promise<boolean> {
  const { refreshToken, setAccessToken, setSession, user } = useAuthStore.getState();
  if (!refreshToken) {
    return false;
  }

  refreshing ??= axios
    .post<{ access_token: string; refresh_token: string }>('/api/auth/refresh', {
      refresh_token: refreshToken,
    })
    .then((response) => {
      setAccessToken(response.data.access_token);
      useAuthStore.setState({ refreshToken: response.data.refresh_token });
      if (user) {
        setSession(response.data.access_token, response.data.refresh_token, user);
      }
      return true;
    })
    .catch(() => false)
    .finally(() => {
      refreshing = null;
    });

  return refreshing;
}

http.interceptors.response.use(
  (response) => response,
  async (error: unknown) => {
    if (axios.isAxiosError(error)) {
      const problem = error.response?.data as ProblemDetails | undefined;
      const status = error.response?.status;

      // 401：尝试静默刷新一次；失败清除会话（路由守卫跳转登录）
      const config = error.config;
      if (status === 401 && config && !config._retried && useAuthStore.getState().refreshToken) {
        config._retried = true;
        if (await tryRefresh()) {
          const token = useAuthStore.getState().accessToken;
          if (token) {
            config.headers.Authorization = `Bearer ${token}`;
            return http.request(config);
          }
        }
        useAuthStore.getState().clear();
        window.location.href = '/login';
        return Promise.reject(new Error('登录已过期，请重新登录'));
      }

      // 内部异常仅返回安全标题，不暴露堆栈（AGENTS.md §8）
      const message = problem?.title || problem?.detail || error.message || '请求失败';
      return Promise.reject(new Error(message));
    }
    return Promise.reject(error instanceof Error ? error : new Error('请求失败'));
  },
);

// 扩展 AxiosRequestConfig（重试标记）
declare module 'axios' {
  interface AxiosRequestConfig {
    _retried?: boolean;
  }
}
