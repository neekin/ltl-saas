import { LockOutlined, ShopOutlined, UserOutlined } from '@ant-design/icons';
import { Button, Card, Form, Input, Typography } from 'antd';
import { useState } from 'react';
import { Link, useLocation, useNavigate } from 'react-router-dom';
import { login } from '../api/auth';
import { useAuthStore } from '../store/authStore';

interface LoginForm {
  tenantCode: string;
  username: string;
  password: string;
}

const highlights = [
  '全程运单追踪，状态一目了然',
  '灵活计费规则，费用精准可控',
  '多网点协同，财务与 COD 清晰分离',
];

/**
 * 登录页（P1 前端）：租户编码 + 用户名 + 密码。
 * 后端失败统一"登录名或密码不正确"（防枚举），本页不区分具体原因。
 */
export function LoginPage() {
  const navigate = useNavigate();
  const location = useLocation();
  const setSession = useAuthStore((s) => s.setSession);
  const redirectTo = (location.state as { from?: string } | null)?.from;
  const [form] = Form.useForm<LoginForm>();
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const onFinish = async (values: LoginForm) => {
    setSubmitting(true);
    setError(null);
    try {
      const result = await login(values.tenantCode.trim(), values.username.trim(), values.password);
      setSession(result.access_token, result.refresh_token, {
        ...result.user,
        tenantCode: values.tenantCode.trim(),
      });
      navigate(redirectTo?.startsWith('/') ? redirectTo : '/', { replace: true });
    } catch (e) {
      setError(e instanceof Error ? e.message : '登录失败');
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div style={{ minHeight: '100vh', display: 'flex', background: '#f5f7fa' }}>
      {/* 品牌展示区：窄屏隐藏，保留纯登录表单 */}
      <div
        className="ltl-login-brand"
        style={{
          flex: '1 1 55%',
          background: '#16233A',
          color: '#fff',
          display: 'flex',
          flexDirection: 'column',
          justifyContent: 'center',
          padding: '0 8%',
        }}
      >
        <div style={{ maxWidth: 480 }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 14, marginBottom: 40 }}>
            <div
              style={{
                width: 44,
                height: 44,
                borderRadius: 10,
                background: '#1D4FA0',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                fontSize: 20,
              }}
            >
              ◆
            </div>
            <div>
              <div style={{ fontSize: 20, fontWeight: 600 }}>LTL 零担物流</div>
              <div style={{ fontSize: 12, color: 'rgba(255,255,255,0.5)' }}>专线运营管理平台</div>
            </div>
          </div>
          <Typography.Title
            level={2}
            style={{ color: '#fff', margin: '0 0 16px', fontWeight: 600 }}
          >
            让每一票货，都清晰可控
          </Typography.Title>
          <Typography.Paragraph
            style={{ color: 'rgba(255,255,255,0.6)', fontSize: 15, marginBottom: 32 }}
          >
            面向专线零担物流企业的一体化 SaaS，覆盖开单、运输、交付与结算全流程。
          </Typography.Paragraph>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
            {highlights.map((text) => (
              <div
                key={text}
                style={{
                  display: 'flex',
                  alignItems: 'center',
                  gap: 10,
                  color: 'rgba(255,255,255,0.85)',
                }}
              >
                <span
                  style={{
                    width: 6,
                    height: 6,
                    borderRadius: '50%',
                    background: '#5B9BF0',
                    flexShrink: 0,
                  }}
                />
                <span style={{ fontSize: 14 }}>{text}</span>
              </div>
            ))}
          </div>
        </div>
      </div>

      {/* 表单区 */}
      <div
        style={{
          flex: '1 1 45%',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          padding: 24,
        }}
      >
        <Card style={{ width: '100%', maxWidth: 400 }} styles={{ body: { padding: 32 } }}>
          <Typography.Title level={3} style={{ textAlign: 'center', marginBottom: 4 }}>
            登录
          </Typography.Title>
          <Typography.Paragraph type="secondary" style={{ textAlign: 'center', marginBottom: 28 }}>
            请输入租户编码与账号信息
          </Typography.Paragraph>
          <Form form={form} layout="vertical" onFinish={onFinish} requiredMark={false}>
            <Form.Item
              name="tenantCode"
              label="租户编码"
              rules={[{ required: true, message: '请输入租户编码' }]}
            >
              <Input
                prefix={<ShopOutlined />}
                placeholder="租户编码"
                autoComplete="off"
                size="large"
              />
            </Form.Item>
            <Form.Item
              name="username"
              label="用户名"
              rules={[{ required: true, message: '请输入用户名' }]}
            >
              <Input
                prefix={<UserOutlined />}
                placeholder="用户名"
                autoComplete="username"
                size="large"
              />
            </Form.Item>
            <Form.Item
              name="password"
              label="密码"
              rules={[{ required: true, message: '请输入密码' }]}
            >
              <Input.Password
                prefix={<LockOutlined />}
                placeholder="密码"
                autoComplete="current-password"
                size="large"
              />
            </Form.Item>
            {error && (
              <Typography.Text type="danger" style={{ display: 'block', marginBottom: 12 }}>
                {error}
              </Typography.Text>
            )}
            <Button type="primary" htmlType="submit" block size="large" loading={submitting}>
              登录
            </Button>
            <div style={{ textAlign: 'center', marginTop: 16 }}>
              <Link to="/track">我是客户，查询运单 ›</Link>
            </div>
          </Form>
        </Card>
      </div>
    </div>
  );
}
