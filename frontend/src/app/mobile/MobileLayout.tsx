import {
  AppOutline,
  GlobalOutline,
  LocationOutline,
  ReceiptOutline,
  ScanCodeOutline,
  SetOutline,
  TruckOutline,
  UnorderedListOutline,
  UserCircleOutline,
} from 'antd-mobile-icons';
import { Dialog, TabBar } from 'antd-mobile';
import { Outlet, useLocation, useNavigate } from 'react-router-dom';
import { logout } from '../../modules/auth/api/auth';
import { useAuthStore } from '../../modules/auth/store/authStore';

const tabs = [
  { key: '/m', title: '作业台', icon: <AppOutline /> },
  { key: '/m/scan', title: '扫码查询', icon: <ScanCodeOutline /> },
  { key: '/m/receive', title: '收货', icon: <ReceiptOutline /> },
  { key: '/m/load', title: '装车', icon: <TruckOutline /> },
  { key: '/m/arrival', title: '到货', icon: <UnorderedListOutline /> },
  { key: '/m/dispatch', title: '派单', icon: <SetOutline /> },
  { key: '/m/gps', title: '位置', icon: <LocationOutline /> },
];

/** 移动作业应用布局（P10）：独立 /m 路由组，固定标题栏与底部 TabBar。 */
export function MobileLayout() {
  const navigate = useNavigate();
  const location = useLocation();
  const user = useAuthStore((s) => s.user);
  const refreshToken = useAuthStore((s) => s.refreshToken);
  const clear = useAuthStore((s) => s.clear);

  const activeKey = tabs.reduce(
    (best, tab) =>
      location.pathname.startsWith(tab.key) && tab.key.length > best.length ? tab.key : best,
    '/m',
  );
  const currentTitle = tabs.find((tab) => tab.key === activeKey)?.title ?? '移动作业';

  const onLogout = async () => {
    const confirmed = await Dialog.confirm({
      title: '退出登录',
      content: `当前账号：${user?.displayName ?? user?.username ?? '未登录'}`,
      confirmText: '退出登录',
      cancelText: '返回',
    });
    if (!confirmed) return;
    if (refreshToken) {
      try {
        await logout(refreshToken);
      } catch {
        // 退出失败不阻断本地会话清理
      }
    }
    clear();
    navigate('/login', { replace: true });
  };

  return (
    <div className="mobile-shell">
      <header className="mobile-header">
        <div className="mobile-header__title">
          <span className="mobile-header__eyebrow">LTL 移动作业</span>
          <strong>{currentTitle}</strong>
        </div>
        <div className="mobile-header__actions">
          <button type="button" onClick={() => navigate('/')} aria-label="打开 PC 后台">
            <GlobalOutline />
            <span>PC 后台</span>
          </button>
          <button type="button" onClick={() => void onLogout()} aria-label="账号与退出登录">
            <UserCircleOutline />
          </button>
        </div>
      </header>
      <main className="mobile-main">
        <Outlet />
      </main>
      <TabBar
        className="mobile-tabbar"
        activeKey={activeKey}
        onChange={(key) => navigate(key)}
        safeArea
      >
        {tabs.map((tab) => (
          <TabBar.Item key={tab.key} title={tab.title} icon={tab.icon} />
        ))}
      </TabBar>
    </div>
  );
}
