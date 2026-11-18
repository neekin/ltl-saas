import { useState } from 'react';
import {
  CarOutlined,
  DashboardOutlined,
  DeploymentUnitOutlined,
  DownOutlined,
  DollarOutlined,
  FileTextOutlined,
  LogoutOutlined,
  ShopOutlined,
  SwapOutlined,
  TagsOutlined,
  ToolOutlined,
  UserOutlined,
} from '@ant-design/icons';
import { Avatar, Breadcrumb, Dropdown, Layout, Menu, Typography } from 'antd';
import type { MenuProps } from 'antd';
import { Outlet, useLocation, useNavigate } from 'react-router-dom';
import { logout } from '../../modules/auth/api/auth';
import { useAuthStore } from '../../modules/auth/store/authStore';
import { PERMISSIONS } from '../../shared/permissions';

const { Sider, Header, Content } = Layout;

interface MenuSection {
  key: string;
  label: string;
  icon: React.ReactNode;
  items: { key: string; label: string; permission: string }[];
}

/** 菜单按权限点过滤（体验层；数据操作由后端实时校验，AGENTS.md §9.4）。 */
const menuSections: MenuSection[] = [
  {
    key: 'dashboard',
    label: '概览',
    icon: <DashboardOutlined />,
    items: [
      { key: '/', label: '工作台', permission: '' },
      { key: '/reports/dimension', label: '运营报表', permission: 'finance.read' },
      { key: '/reports/usage', label: '租户用量', permission: 'finance.read' },
    ],
  },
  {
    key: 'master',
    label: '基础资料',
    icon: <ShopOutlined />,
    items: [
      { key: '/customers', label: '客户管理', permission: PERMISSIONS.customerRead },
      { key: '/routes', label: '线路管理', permission: PERMISSIONS.routeRead },
    ],
  },
  {
    key: 'pricing',
    label: '计费引擎',
    icon: <TagsOutlined />,
    items: [
      { key: '/pricing/rule-sets', label: '计价规则集', permission: PERMISSIONS.pricingRead },
      { key: '/pricing/trial', label: '计价试算', permission: PERMISSIONS.pricingRead },
    ],
  },
  {
    key: 'waybill',
    label: '运单',
    icon: <FileTextOutlined />,
    items: [
      { key: '/waybills', label: '运单查询', permission: PERMISSIONS.waybillRead },
      { key: '/waybills/create', label: '开单', permission: PERMISSIONS.waybillManage },
      { key: '/warehouse/sorting', label: '分拣作业', permission: PERMISSIONS.cargoSort },
    ],
  },
  {
    key: 'transport',
    label: '运输',
    icon: <CarOutlined />,
    items: [
      { key: '/transport/vehicles', label: '车辆档案', permission: PERMISSIONS.vehicleRead },
      { key: '/transport/drivers', label: '司机档案', permission: PERMISSIONS.vehicleRead },
      { key: '/transport/trips', label: '运输批次', permission: PERMISSIONS.tripRead },
    ],
  },
  {
    key: 'transfer',
    label: '网点调拨',
    icon: <SwapOutlined />,
    items: [{ key: '/transfers', label: '调拨单', permission: PERMISSIONS.transferRead }],
  },
  {
    key: 'arrival',
    label: '到货/交付',
    icon: <ShopOutlined />,
    items: [
      { key: '/arrivals', label: '到货登记', permission: 'arrival.record' },
      { key: '/arrivals/differences', label: '到货差异', permission: 'arrival.read' },
      { key: '/arrivals/pickup', label: '待提货列表', permission: 'delivery.read' },
      { key: '/arrivals/exceptions', label: '异常件', permission: 'arrival.read' },
      { key: '/deliveries', label: '交付确认', permission: 'delivery.manage' },
    ],
  },
  {
    key: 'finance',
    label: '财务/COD',
    icon: <DollarOutlined />,
    items: [
      { key: '/finance', label: '财务管理', permission: 'finance.read' },
      { key: '/finance/allocations', label: '收款核销', permission: 'finance.read' },
      { key: '/finance/statements', label: '月结账单', permission: 'finance.read' },
      { key: '/finance/cod', label: 'COD 工作台', permission: 'cod.read' },
      { key: '/finance/cod/manage', label: 'COD 管理', permission: 'cod.manage' },
    ],
  },
  {
    key: 'system',
    label: '系统',
    icon: <ToolOutlined />,
    items: [{ key: '/system/status', label: '系统状态', permission: '' }],
  },
];

/** 详情类路由（不在菜单中）的面包屑末级名称，按前缀匹配。 */
const DETAIL_LABELS: { prefix: string; label: string }[] = [
  { prefix: '/waybills/', label: '运单详情' },
  { prefix: '/transport/trips/', label: '配载工作台' },
  { prefix: '/transfers/', label: '调拨详情' },
  { prefix: '/customers/', label: '客户详情' },
  { prefix: '/routes/', label: '线路编辑' },
  { prefix: '/pricing/rule-sets/', label: '规则版本' },
];

interface NavResolution {
  activeKey: string;
  sectionKey: string;
  sectionLabel: string;
  pageLabel: string;
}

/** 从当前路径解析：高亮菜单项、所属分组、面包屑文案。菜单配置为唯一数据源。 */
function resolveNav(pathname: string): NavResolution {
  const home: NavResolution = {
    activeKey: '/',
    sectionKey: 'dashboard',
    sectionLabel: '概览',
    pageLabel: '工作台',
  };
  if (pathname === '/') {
    return home;
  }

  let matched: { section: MenuSection; item: MenuSection['items'][number] } | null = null;
  for (const section of menuSections) {
    for (const item of section.items) {
      if (pathname === item.key) {
        // 精确命中菜单项时直接用其标签，避免被详情路由的前缀规则误判
        // （如 /waybills/create 会被 /waybills/ 前缀匹配成"运单详情"）。
        return {
          activeKey: item.key,
          sectionKey: section.key,
          sectionLabel: section.label,
          pageLabel: item.label,
        };
      }
      if (
        pathname.startsWith(`${item.key}/`) &&
        (!matched || item.key.length > matched.item.key.length)
      ) {
        matched = { section, item };
      }
    }
  }

  if (!matched) {
    return home;
  }
  const detail = DETAIL_LABELS.find((d) => pathname.startsWith(d.prefix));
  return {
    activeKey: matched.item.key,
    sectionKey: matched.section.key,
    sectionLabel: matched.section.label,
    pageLabel: detail?.label ?? matched.item.label,
  };
}

export function BasicLayout() {
  const navigate = useNavigate();
  const location = useLocation();
  const user = useAuthStore((s) => s.user);
  const refreshToken = useAuthStore((s) => s.refreshToken);
  const clear = useAuthStore((s) => s.clear);

  const [collapsed, setCollapsed] = useState(false);
  const nav = resolveNav(location.pathname);
  const [openKeys, setOpenKeys] = useState<string[]>([nav.sectionKey]);
  const [lastSectionKey, setLastSectionKey] = useState(nav.sectionKey);

  // 路由切换时仅展开当前路径所属分组（渲染期同步派生状态，避免 effect 级联渲染）。
  if (nav.sectionKey !== lastSectionKey) {
    setLastSectionKey(nav.sectionKey);
    setOpenKeys([nav.sectionKey]);
  }

  const visibleSections = menuSections
    .map((section) => ({
      ...section,
      items: section.items.filter(
        (item) => !item.permission || user?.permissions.includes(item.permission),
      ),
    }))
    .filter((section) => section.items.length > 0);

  const menuItems: MenuProps['items'] = visibleSections.map((section) => ({
    key: section.key,
    icon: section.icon,
    label: section.label,
    children: section.items.map((item) => ({ key: item.key, label: item.label })),
  }));

  const breadcrumbItems =
    location.pathname === '/'
      ? [{ title: '工作台' }]
      : [{ title: nav.sectionLabel }, { title: nav.pageLabel }];

  const onLogout = async () => {
    if (refreshToken) {
      try {
        await logout(refreshToken);
      } catch {
        // 退出失败不阻断本地清理
      }
    }
    clear();
    navigate('/login', { replace: true });
  };

  return (
    <Layout style={{ minHeight: '100vh' }}>
      <Sider
        width={224}
        collapsedWidth={80}
        collapsible
        collapsed={collapsed}
        onCollapse={setCollapsed}
        breakpoint="lg"
        onBreakpoint={(broken) => setCollapsed(broken)}
      >
        <div
          style={{
            height: 64,
            display: 'flex',
            alignItems: 'center',
            justifyContent: collapsed ? 'center' : 'flex-start',
            gap: 12,
            padding: collapsed ? 0 : '0 20px',
            borderBottom: '1px solid rgba(255, 255, 255, 0.1)',
            overflow: 'hidden',
          }}
        >
          <div
            style={{
              width: 34,
              height: 34,
              borderRadius: 8,
              background: '#1D4FA0',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              flexShrink: 0,
            }}
          >
            <DeploymentUnitOutlined style={{ color: '#fff', fontSize: 18 }} />
          </div>
          {!collapsed && (
            <div style={{ minWidth: 0 }}>
              <div style={{ color: '#fff', fontSize: 15, fontWeight: 600, whiteSpace: 'nowrap' }}>
                LTL 零担物流
              </div>
              <div
                style={{ color: 'rgba(255, 255, 255, 0.45)', fontSize: 11, whiteSpace: 'nowrap' }}
              >
                专线运营管理平台
              </div>
            </div>
          )}
        </div>
        <Menu
          theme="dark"
          mode="inline"
          selectedKeys={[nav.activeKey]}
          openKeys={openKeys}
          onOpenChange={(keys) => setOpenKeys(keys as string[])}
          items={menuItems}
          onClick={({ key }) => navigate(key)}
        />
      </Sider>
      <Layout>
        <Header
          style={{
            background: '#fff',
            borderBottom: '1px solid #eef1f5',
            display: 'flex',
            justifyContent: 'space-between',
            alignItems: 'center',
            padding: '0 24px',
          }}
        >
          <Breadcrumb items={breadcrumbItems} />
          <Dropdown
            menu={{
              items: [
                { key: 'logout', icon: <LogoutOutlined />, label: '退出登录', onClick: onLogout },
              ],
            }}
          >
            <span style={{ cursor: 'pointer', display: 'flex', alignItems: 'center', gap: 8 }}>
              <Avatar size="small" icon={<UserOutlined />} />
              <Typography.Text>{user?.displayName ?? user?.username}</Typography.Text>
              <DownOutlined style={{ fontSize: 10, color: '#8c96a5' }} />
            </span>
          </Dropdown>
        </Header>
        <Content style={{ margin: 0, padding: '20px 24px', overflow: 'auto' }}>
          <div className="ltl-page">
            <Outlet />
          </div>
        </Content>
      </Layout>
    </Layout>
  );
}
