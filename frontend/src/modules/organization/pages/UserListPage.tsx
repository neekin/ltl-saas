import { PlusOutlined } from '@ant-design/icons';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { App, Button, Form, Input, Modal, Popconfirm, Select, Space, Table, Tag } from 'antd';
import { useState } from 'react';
import {
  createUser, disableUser, enableUser, getUser, listBranches, listRoles, listUsers,
  renameUser, resetPassword, setBranchScopes, setUserRoles,
  type Branch, type Role, type User, type UserDetail,
} from '../api/organization';

/** 用户管理页（P1 前端）：列表/创建/启停/重置密码/编辑（改名、分配角色、网点授权）。 */
export function UserListPage() {
  const { message } = App.useApp();
  const queryClient = useQueryClient();
  const [createOpen, setCreateOpen] = useState(false);
  const [detailUser, setDetailUser] = useState<UserDetail | null>(null);
  const [editingUser, setEditingUser] = useState<UserDetail | null>(null);
  const [createForm] = Form.useForm<{ username: string; displayName: string; password: string }>();
  const [editForm] = Form.useForm<{
    displayName: string;
    roleIds: string[];
    branchScopeIds: string[];
  }>();

  const listQuery = useQuery({ queryKey: ['users'], queryFn: () => listUsers(1, 100) });
  const rolesQuery = useQuery({
    queryKey: ['roles'], queryFn: () => listRoles(1, 100),
    enabled: editingUser !== null || detailUser !== null,
  });
  const branchesQuery = useQuery({
    queryKey: ['branches'], queryFn: () => listBranches(1, 100),
    enabled: editingUser !== null || detailUser !== null,
  });

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

  const openEditor = async (user: User) => {
    try {
      const detail = await getUser(user.id);
      editForm.setFieldsValue({
        displayName: detail.displayName,
        roleIds: detail.roleIds,
        branchScopeIds: detail.branchScopeIds,
      });
      setEditingUser(detail);
    } catch (e) {
      message.error(e instanceof Error ? e.message : '加载用户失败');
    }
  };

  const renameMutation = useMutation({
    mutationFn: (params: { id: string; displayName: string }) => renameUser(params.id, params.displayName),
    onSuccess: () => { message.success('姓名已更新'); invalidate(); },
    onError: (e: Error) => message.error(e.message),
  });

  const rolesMutation = useMutation({
    mutationFn: (params: { id: string; roleIds: string[] }) => setUserRoles(params.id, params.roleIds),
    onSuccess: () => { message.success('角色已更新（实时生效）'); invalidate(); },
    onError: (e: Error) => message.error(e.message),
  });

  const scopesMutation = useMutation({
    mutationFn: (params: { id: string; branchIds: string[] }) => setBranchScopes(params.id, params.branchIds),
    onSuccess: () => { message.success('网点范围已更新（实时生效）'); invalidate(); },
    onError: (e: Error) => message.error(e.message),
  });

  const roleOptions: { value: string; label: string }[] = (rolesQuery.data?.items ?? [])
    .filter((r: Role) => r.status === 'Active')
    .map((r) => ({ value: r.id, label: `${r.code} ${r.name}` }));
  const branchOptions: { value: string; label: string }[] = (branchesQuery.data?.items ?? [])
    .filter((b: Branch) => b.status === 'Active')
    .map((b) => ({ value: b.id, label: `${b.code} ${b.name}` }));
  const roleNames = new Map(roleOptions.map((o) => [o.value, o.label]));
  const branchNames = new Map(branchOptions.map((o) => [o.value, o.label]));

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
                <Button type="link" size="small" onClick={() => void openEditor(record)}>
                  编辑
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

      <Modal
        title={editingUser ? `编辑用户：${editingUser.username}` : ''}
        open={editingUser !== null}
        onCancel={() => setEditingUser(null)}
        footer={null}
        destroyOnClose
      >
        <Form form={editForm} layout="vertical">
          <Form.Item name="displayName" label="姓名" rules={[{ required: true, message: '姓名必填' }]}>
            <Input />
          </Form.Item>
          <Button
            onClick={() => {
              const displayName = editForm.getFieldValue('displayName') as string | undefined;
              if (!displayName?.trim()) {
                message.warning('姓名必填');
                return;
              }
              renameMutation.mutate({ id: editingUser!.id, displayName });
            }}
            loading={renameMutation.isPending}
          >
            保存姓名
          </Button>

          <Form.Item name="roleIds" label="角色" style={{ marginTop: 16 }}>
            <Select mode="multiple" placeholder="选择角色" options={roleOptions} />
          </Form.Item>
          <Button
            onClick={() => rolesMutation.mutate({ id: editingUser!.id, roleIds: editForm.getFieldValue('roleIds') ?? [] })}
            loading={rolesMutation.isPending}
          >
            保存角色
          </Button>

          <Form.Item name="branchScopeIds" label="网点数据范围" style={{ marginTop: 16 }}>
            <Select mode="multiple" placeholder="选择可访问网点" options={branchOptions} />
          </Form.Item>
          <Button
            onClick={() => scopesMutation.mutate({ id: editingUser!.id, branchIds: editForm.getFieldValue('branchScopeIds') ?? [] })}
            loading={scopesMutation.isPending}
            disabled={editingUser?.tenantWideScope ?? false}
          >
            保存网点范围
          </Button>
          {editingUser?.tenantWideScope && (
            <p style={{ marginTop: 8, marginBottom: 0, color: '#8c96a5' }}>该用户为租户级范围（全部网点），无需逐网点授权。</p>
          )}
        </Form>
      </Modal>

      {detailUser && (
        <Modal title={`用户详情：${detailUser.displayName}`} open={!!detailUser} footer={null} onCancel={() => setDetailUser(null)}>
          <Space direction="vertical" style={{ width: '100%' }}>
            <div>登录名：{detailUser.username}</div>
            <div>状态：{detailUser.status === 'Active' ? '启用' : '停用'}</div>
            <div>
              角色：
              {detailUser.roleIds.length > 0
                ? detailUser.roleIds.map((id) => roleNames.get(id) ?? id).join('、')
                : '无'}
            </div>
            <div>
              网点范围：
              {detailUser.tenantWideScope
                ? '全部网点（租户级）'
                : detailUser.branchScopeIds.length > 0
                  ? detailUser.branchScopeIds.map((id) => branchNames.get(id) ?? id).join('、')
                  : '无'}
            </div>
          </Space>
        </Modal>
      )}
    </div>
  );
}
