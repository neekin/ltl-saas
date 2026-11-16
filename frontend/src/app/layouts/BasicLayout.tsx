import { AppstoreOutlined, SafetyCertificateOutlined } from '@ant-design/icons';
import { Layout, Menu, Typography } from 'antd';
import type { MenuProps } from 'antd';
import { Outlet, useLocation, useNavigate } from 'react-router-dom';

const { Sider, Header, Content } = Layout;

/**
 * 基础应用布局。菜单项随各阶段任务逐步接入（P1 起补充登录与网点切换器）。
 */
const menuItems: MenuProps['items'] = [
  { key: '/', icon: <AppstoreOutlined />, label: '概览' },
  { key: '/system/status', icon: <SafetyCertificateOutlined />, label: '系统状态' },
];

export function BasicLayout() {
  const navigate = useNavigate();
  const location = useLocation();

  return (
    <Layout style={{ minHeight: '100vh' }}>
      <Sider>
        <div
          style={{ height: 48, display: 'flex', alignItems: 'center', justifyContent: 'center' }}
        >
          <Typography.Title level={5} style={{ color: '#fff', margin: 0 }}>
            LTL 物流系统
          </Typography.Title>
        </div>
        <Menu
          theme="dark"
          mode="inline"
          selectedKeys={[location.pathname]}
          items={menuItems}
          onClick={(item) => navigate(item.key)}
        />
      </Sider>
      <Layout>
        <Header style={{ background: '#fff', borderBottom: '1px solid #f0f0f0' }} />
        <Content style={{ margin: 16 }}>
          <Outlet />
        </Content>
      </Layout>
    </Layout>
  );
}
