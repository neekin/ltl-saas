import { create } from 'zustand';
import { persist } from 'zustand/middleware';
import type { AuthUser } from '../api/auth';

interface AuthState {
  accessToken: string | null;
  refreshToken: string | null;
  user: AuthUser | null;
  setSession: (accessToken: string, refreshToken: string, user: AuthUser) => void;
  setAccessToken: (accessToken: string | undefined) => void;
  clear: () => void;
}

/**
 * 认证会话（客户端状态，Zustand persist）。
 * 仅用于体验：所有数据操作仍由后端实时校验权限（AGENTS.md §9.4）。
 */
export const useAuthStore = create<AuthState>()(
  persist(
    (set) => ({
      accessToken: null,
      refreshToken: null,
      user: null,
      setSession: (accessToken, refreshToken, user) =>
        set({ accessToken, refreshToken, user }),
      setAccessToken: (accessToken) => set({ accessToken }),
      clear: () => set({ accessToken: null, refreshToken: null, user: null }),
    }),
    { name: 'ltl-auth' },
  ),
);

export function hasPermission(permission: string): boolean {
  const user = useAuthStore.getState().user;
  return user?.permissions.includes(permission) ?? false;
}
