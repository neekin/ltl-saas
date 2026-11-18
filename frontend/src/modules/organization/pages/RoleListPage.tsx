import { PlusOutlined } from '@ant-design/icons';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { App, Button, Checkbox, Form, Input, Modal, Popconfirm, Space, Spin, Table, Tag, Typography } from 'antd';
import { useState } from 'react';
import {
  createRole, disableRole, enableRole, getRole, listPermissions, listRoles,
  setRolePermissions, type Permission, type Role, type RoleDetail,
} from '../api/organization';

/** 角色管理页（P1 前端）：列表/创建/权限配置/启停。权限点目录按模块分组多选。 */
export function RoleListPage() {
  const { message } = App.useApp();
  const queryClient = useQueryClient();
  const [createOpen, setCreateOpen] = useState(false);
  const [editingRole, setEditingRole] = useState<RoleDetail | null>(null);
  const [selectedCodes, setSelectedCodes] = useState<string[]>([]);
  const [createForm] = Form.useForm<{ code: string; name: string }>();

  const listQuery = useQuery({ queryKey: ['roles'], queryFn: () => listRoles(1, 100) });
  const permissionsQuery = useQuery({
    queryKey: ['permissions'],
    queryFn: () => listPermissions(),
    enabled: editingRole !== null,
  });

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ['roles'] });

  const createMutation = useMutation({
    mutationFn: (values: { code: string; name: string }) => createRole(values),
    onSuccess: () => { message.success('角色已创建'); setCreateOpen(false); createForm.resetFields(); invalidate(); },
    onError: (e: Error) => message.error(e.message),
  });

  const toggleMutation = useMutation({
    mutationFn: async ({ role, disable }: { role: Role; disable: boolean }) =>
      disable ? disableRole(role.id) : enableRole(role.id),
    onSuccess: () => { message.success('状态已更新'); invalidate(); },
    onError: (e: Error) => message.error(e.message),
  });

  const openPermissionEditor = async (role: Role) => {
    try {
      const detail = await getRole(role.id);
      setSelectedCodes(detail.permissionCodes);
      setEditingRole(detail);
    } catch (e) {
      message.error(e instanceof Error ? e.message : '加载角色失败');
    }
  };

  const savePermissions = useMutation({
    mutationFn: () => setRolePermissions(editingRole!.id, selectedCodes),
    onSuccess: () => {
      message.success('权限已更新（已登录用户实时生效）');
      setEditingRole(null);
      invalidate();
    },
    onError: (e: Error) => message.error(e.message),
  });

  // 按模块分组渲染权限复选框；勾选合并时只替换当前模块的选中项
  const grouped = groupByModule(permissionsQuery.data ?? []);
  const onModuleChange = (moduleCodes: string[], checked: string[]) => {
    setSelectedCodes((prev) => [...prev.filter((c) => !moduleCodes.includes(c)), ...checked]);
  };

  return (
    <div>
      <Space style={{ marginBottom: 16 }}>
        <Button type="primary" icon={<PlusOutlined />} onClick={() => setCreateOpen(true)}>新建角色</Button>
      </Space>
      <Table
        rowKey="id"
        loading={listQuery.isLoading}
        dataSource={listQuery.data?.items}
        columns={[
          { title: '编码', dataIndex: 'code', width: 120 },
          { title: '名称', dataIndex: 'name' },
          {
            title: '状态', dataIndex: 'status', width: 100,
            render: (s: string) => <Tag color={s === 'Active' ? 'green' : 'default'}>{s === 'Active' ? '启用' : '停用'}</Tag>,
          },
          {
            title: '操作', key: 'actions',
            render: (_: unknown, record: Role) => (
              <Space>
                <Button type="link" size="small" onClick={() => void openPermissionEditor(record)}>
                  配置权限
                </Button>
                <Popconfirm
                  title={record.status === 'Active' ? '停用后角色权限即时失效' : '确认启用？'}
                  onConfirm={() => toggleMutation.mutate({ role: record, disable: record.status === 'Active' })}
                >
                  <Button type="link" size="small" danger={record.status === 'Active'}>
                    {record.status === 'Active' ? '停用' : '启用'}
                  </Button>
                </Popconfirm>
              </Space>
            ),
          },
        ]}
      />

      <Modal title="新建角色" open={createOpen} onCancel={() => setCreateOpen(false)} onOk={() => createForm.submit()}>
        <Form form={createForm} layout="vertical" onFinish={(values) => createMutation.mutate(values)}>
          <Form.Item name="code" label="角色编码" rules={[{ required: true, message: '编码必填' }]}>
            <Input placeholder="如 ops" />
          </Form.Item>
          <Form.Item name="name" label="角色名称" rules={[{ required: true, message: '名称必填' }]}>
            <Input placeholder="如 运营管理员" />
          </Form.Item>
        </Form>
      </Modal>

      <Modal
        title={editingRole ? `配置权限：${editingRole.name}（${editingRole.code}）` : ''}
        open={editingRole !== null}
        onCancel={() => setEditingRole(null)}
        onOk={() => savePermissions.mutate()}
        confirmLoading={savePermissions.isPending}
        width={720}
        destroyOnClose
      >
        <Typography.Paragraph type="secondary" style={{ marginTop: 0 }}>
          已选 {selectedCodes.length} 项；保存后对已登录用户实时生效。
        </Typography.Paragraph>
        {permissionsQuery.isLoading ? (
          <Spin />
        ) : (
          Object.entries(grouped).map(([module, perms]) => {
            const moduleCodes = perms.map((p) => p.code);
            return (
              <div key={module} style={{ marginBottom: 12 }}>
                <Typography.Text strong>{module}</Typography.Text>
                <Checkbox.Group
                  style={{ display: 'flex', flexWrap: 'wrap', gap: 4, marginTop: 4 }}
                  value={selectedCodes.filter((c) => moduleCodes.includes(c))}
                  onChange={(checked) => onModuleChange(moduleCodes, checked as string[])}
                  options={perms.map((p) => ({
                    value: p.code,
                    label: (
                      <span>
                        {p.name}{' '}
                        <Typography.Text type="secondary" code style={{ fontSize: 12 }}>{p.code}</Typography.Text>
                      </span>
                    ),
                  }))}
                />
              </div>
            );
          })
        )}
      </Modal>
    </div>
  );
}

function groupByModule(permissions: Permission[]): Record<string, Permission[]> {
  const grouped: Record<string, Permission[]> = {};
  for (const p of permissions) {
    (grouped[p.module] ??= []).push(p);
  }
  return grouped;
}
