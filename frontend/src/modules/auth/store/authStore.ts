import { create } from 'zustand';
import { persist } from 'zustand/middleware';
import type { AuthUser } from '../api/auth';

/** 当前工作网点（客户端工作上下文，仅用于展示与业务场景识别，不替代服务端网点授权）。 */
export interface CurrentBranch {
  id: string;
  code: string;
  name: string;
}

interface AuthState {
  accessToken: string | null;
  refreshToken: string | null;
  user: AuthUser | null;
  currentBranch: CurrentBranch | null;
  setSession: (accessToken: string, refreshToken: string, user: AuthUser) => void;
  setAccessToken: (accessToken: string | undefined) => void;
  setCurrentBranch: (branch: CurrentBranch | null) => void;
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
      currentBranch: null,
      setSession: (accessToken, refreshToken, user) => set({ accessToken, refreshToken, user }),
      setAccessToken: (accessToken) => set({ accessToken }),
      setCurrentBranch: (branch) => set({ currentBranch: branch }),
      clear: () => set({ accessToken: null, refreshToken: null, user: null, currentBranch: null }),
    }),
    { name: 'ltl-auth' },
  ),
);

export function hasPermission(permission: string): boolean {
  const user = useAuthStore.getState().user;
  return user?.permissions.includes(permission) ?? false;
}
