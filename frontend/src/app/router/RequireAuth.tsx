import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { useAuthStore } from '../../modules/auth/store/authStore';

/** 路由守卫：无会话 → 登录页（数据权限仍由后端实时校验，AGENTS.md §9.4）。 */
export function RequireAuth() {
  const accessToken = useAuthStore((s) => s.accessToken);
  const location = useLocation();

  if (!accessToken) {
    return <Navigate to="/login" state={{ from: location.pathname }} replace />;
  }

  return <Outlet />;
}
