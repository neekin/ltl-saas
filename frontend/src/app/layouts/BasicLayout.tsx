import {
  CarOutlined,
  DashboardOutlined,
  FileTextOutlined,
  LogoutOutlined,
  DollarOutlined,
  ShopOutlined,
  SwapOutlined,
  TagsOutlined,
  ToolOutlined,
  UserOutlined,
} from '@ant-design/icons';
import { Avatar, Dropdown, Layout, Menu, Typography } from 'antd';
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
    items: [{ key: '/', label: '工作台', permission: '' }],
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
      { key: '/waybills', label: '运单列表', permission: PERMISSIONS.waybillRead },
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
      { key: '/deliveries', label: '交付确认', permission: 'delivery.manage' },
    ],
  },
  {
    key: 'finance',
    label: '财务/COD',
    icon: <DollarOutlined />,
    items: [
      { key: '/finance', label: '财务管理', permission: 'finance.read' },
      { key: '/finance/cod', label: 'COD 工作台', permission: 'cod.read' },
    ],
  },
  {
    key: 'system',
    label: '系统',
    icon: <ToolOutlined />,
    items: [{ key: '/system/status', label: '系统状态', permission: '' }],
  },
];

export function BasicLayout() {
  const navigate = useNavigate();
  const location = useLocation();
  const user = useAuthStore((s) => s.user);
  const refreshToken = useAuthStore((s) => s.refreshToken);
  const clear = useAuthStore((s) => s.clear);

  const visibleSections = menuSections
    .map((section) => ({
      ...section,
      items: section.items.filter((item) => !item.permission || user?.permissions.includes(item.permission)),
    }))
    .filter((section) => section.items.length > 0);

  const menuItems: MenuProps['items'] = visibleSections.map((section) => ({
    key: section.key,
    icon: section.icon,
    label: section.label,
    children: section.items.map((item) => ({ key: item.key, label: item.label })),
  }));

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
      <Sider width={220}>
        <div style={{ height: 48, display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
          <Typography.Title level={5} style={{ color: '#fff', margin: 0 }}>
            LTL 物流系统
          </Typography.Title>
        </div>
        <Menu
          theme="dark"
          mode="inline"
          selectedKeys={[location.pathname]}
          defaultOpenKeys={visibleSections.map((s) => s.key)}
          items={menuItems}
          onClick={({ key }) => navigate(key)}
        />
      </Sider>
      <Layout>
        <Header
          style={{
            background: '#fff',
            borderBottom: '1px solid #f0f0f0',
            display: 'flex',
            justifyContent: 'flex-end',
            alignItems: 'center',
          }}
        >
          <Dropdown
            menu={{
              items: [{ key: 'logout', icon: <LogoutOutlined />, label: '退出登录', onClick: onLogout }],
            }}
          >
            <span style={{ cursor: 'pointer', display: 'flex', alignItems: 'center', gap: 8 }}>
              <Avatar size="small" icon={<UserOutlined />} />
              <Typography.Text>{user?.displayName ?? user?.username}</Typography.Text>
            </span>
          </Dropdown>
        </Header>
        <Content style={{ margin: 16 }}>
          <Outlet />
        </Content>
      </Layout>
    </Layout>
  );
}
