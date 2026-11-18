import { PlusOutlined } from '@ant-design/icons';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { App, Button, Form, Input, Modal, Popconfirm, Space, Table, Tag } from 'antd';
import { useState } from 'react';
import {
  createUser, disableUser, enableUser, getUser, listUsers,
  resetPassword, type User, type UserDetail,
} from '../api/organization';

/** 用户管理页（P1 前端）：列表/创建/启停/重置密码。 */
export function UserListPage() {
  const { message } = App.useApp();
  const queryClient = useQueryClient();
  const [createOpen, setCreateOpen] = useState(false);
  const [detailUser, setDetailUser] = useState<UserDetail | null>(null);
  const [createForm] = Form.useForm<{ username: string; displayName: string; password: string }>();

  const listQuery = useQuery({ queryKey: ['users'], queryFn: () => listUsers(1, 100) });

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ['users'] });

  const createMutation = useMutation({
    mutationFn: (values: { username: string; displayName: string; password: string }) =>
      createUser(values),
    onSuccess: () => { message.success('用户已创建'); setCreateOpen(false); createForm.resetFields(); invalidate(); },
    onError: (e: Error) => message.error(e.message),
  });

  const toggleMutation = useMutation({
    mutationFn: async ({ user, disable }: { user: User; disable: boolean }) =>
      disable ? disableUser(user.id) : enableUser(user.id),
    onSuccess: () => { message.success('状态已更新'); invalidate(); },
    onError: (e: Error) => message.error(e.message),
  });

  const resetMutation = useMutation({
    mutationFn: (params: { id: string; password: string }) => resetPassword(params.id, params.password),
    onSuccess: () => { message.success('密码已重置'); invalidate(); },
    onError: (e: Error) => message.error(e.message),
  });

  return (
    <div>
      <Space style={{ marginBottom: 16 }}>
        <Button type="primary" icon={<PlusOutlined />} onClick={() => setCreateOpen(true)}>新建用户</Button>
      </Space>
      <Table
        rowKey="id"
        loading={listQuery.isLoading}
        dataSource={listQuery.data?.items}
        columns={[
          { title: '登录名', dataIndex: 'username' },
          { title: '姓名', dataIndex: 'displayName' },
          {
            title: '状态', dataIndex: 'status', width: 100,
            render: (s: string) => <Tag color={s === 'Active' ? 'green' : 'default'}>{s === 'Active' ? '启用' : '停用'}</Tag>,
          },
          {
            title: '租户级范围', dataIndex: 'tenantWideScope', width: 120,
            render: (v: boolean) => v ? <Tag color="blue">全部网点</Tag> : <Tag>按网点</Tag>,
          },
          {
            title: '操作', key: 'actions',
            render: (_: unknown, record: User) => (
              <Space>
                <Button type="link" size="small" onClick={async () => {
                  setDetailUser(await getUser(record.id));
                }}>
                  详情
                </Button>
                <Popconfirm
                  title={record.status === 'Active' ? '确认停用？' : '确认启用？'}
                  onConfirm={() => toggleMutation.mutate({ user: record, disable: record.status === 'Active' })}
                >
                  <Button type="link" size="small" danger={record.status === 'Active'}>
                    {record.status === 'Active' ? '停用' : '启用'}
                  </Button>
                </Popconfirm>
                <Button
                  type="link" size="small"
                  onClick={() => {
                    const newPassword = `Reset@${Date.now()}`;
                    resetMutation.mutate({ id: record.id, password: newPassword });
                    message.info(`新密码：${newPassword}`);
                  }}
                >
                  重置密码
                </Button>
              </Space>
            ),
          },
        ]}
      />

      <Modal title="新建用户" open={createOpen} onCancel={() => setCreateOpen(false)} onOk={() => createForm.submit()}>
        <Form form={createForm} layout="vertical" onFinish={(values) => createMutation.mutate(values)}>
          <Form.Item name="username" label="登录名" rules={[{ required: true, message: '登录名必填' }]}>
            <Input autoComplete="off" />
          </Form.Item>
          <Form.Item name="displayName" label="姓名" rules={[{ required: true, message: '姓名必填' }]}>
            <Input />
          </Form.Item>
          <Form.Item name="password" label="密码" rules={[
            { required: true, message: '密码必填' },
            { min: 8, message: '密码至少 8 位' },
          ]}>
            <Input.Password autoComplete="new-password" />
          </Form.Item>
        </Form>
      </Modal>

      {detailUser && (
        <Modal title={`用户详情：${detailUser.displayName}`} open={!!detailUser} footer={null} onCancel={() => setDetailUser(null)}>
          <Space direction="vertical" style={{ width: '100%' }}>
            <div>登录名：{detailUser.username}</div>
            <div>角色：{detailUser.roleIds.length > 0 ? detailUser.roleIds.join(', ') : '无'}</div>
            <div>网点范围：{detailUser.branchScopeIds.length > 0 ? detailUser.branchScopeIds.join(', ') : '无'}</div>
            <div>租户级范围：{detailUser.tenantWideScope ? '是' : '否'}</div>
          </Space>
        </Modal>
      )}
    </div>
  );
}
