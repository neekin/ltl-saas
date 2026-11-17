import { LockOutlined, ShopOutlined, UserOutlined } from '@ant-design/icons';
import { useState } from 'react';
import { Button, Card, Form, Input, Typography } from 'antd';
import { useNavigate } from 'react-router-dom';
import { login } from '../api/auth';
import { useAuthStore } from '../store/authStore';

interface LoginForm {
  tenantCode: string;
  username: string;
  password: string;
}

/**
 * 登录页（P1 前端）：租户编码 + 用户名 + 密码。
 * 后端失败统一"登录名或密码不正确"（防枚举），本页不区分具体原因。
 */
export function LoginPage() {
  const navigate = useNavigate();
  const setSession = useAuthStore((s) => s.setSession);
  const [form] = Form.useForm<LoginForm>();
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const onFinish = async (values: LoginForm) => {
    setSubmitting(true);
    setError(null);
    try {
      const result = await login(values.tenantCode.trim(), values.username.trim(), values.password);
      setSession(result.access_token, result.refresh_token, result.user);
      navigate('/', { replace: true });
    } catch (e) {
      setError(e instanceof Error ? e.message : '登录失败');
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div
      style={{
        minHeight: '100vh',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        background: '#f0f2f5',
      }}
    >
      <Card style={{ width: 400 }}>
        <Typography.Title level={3} style={{ textAlign: 'center' }}>
          LTL 物流系统
        </Typography.Title>
        <Typography.Paragraph type="secondary" style={{ textAlign: 'center' }}>
          专线零担物流管理平台
        </Typography.Paragraph>
        <Form form={form} layout="vertical" onFinish={onFinish} requiredMark={false}>
          <Form.Item
            name="tenantCode"
            label="租户编码"
            rules={[{ required: true, message: '请输入租户编码' }]}
          >
            <Input prefix={<ShopOutlined />} placeholder="租户编码" autoComplete="off" />
          </Form.Item>
          <Form.Item name="username" label="用户名" rules={[{ required: true, message: '请输入用户名' }]}>
            <Input prefix={<UserOutlined />} placeholder="用户名" autoComplete="username" />
          </Form.Item>
          <Form.Item name="password" label="密码" rules={[{ required: true, message: '请输入密码' }]}>
            <Input.Password prefix={<LockOutlined />} placeholder="密码" autoComplete="current-password" />
          </Form.Item>
          {error && <Typography.Text type="danger">{error}</Typography.Text>}
          <Button type="primary" htmlType="submit" block loading={submitting} style={{ marginTop: 8 }}>
            登录
          </Button>
        </Form>
      </Card>
    </div>
  );
}
