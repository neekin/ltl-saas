import { PlusOutlined } from '@ant-design/icons';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { App, Button, Form, Input, Modal, Popconfirm, Space, Table, Tag } from 'antd';
import { useState } from 'react';
import {
  createRole, disableRole, enableRole, getRole, listRoles,
  type Role, type RoleDetail,
} from '../api/organization';

/** 角色管理页（P1 前端）：列表/创建/权限配置/启停。 */
export function RoleListPage() {
  const { message } = App.useApp();
  const queryClient = useQueryClient();
  const [createOpen, setCreateOpen] = useState(false);
  const [detailRole, setDetailRole] = useState<RoleDetail | null>(null);
  const [createForm] = Form.useForm<{ code: string; name: string }>();

  const listQuery = useQuery({ queryKey: ['roles'], queryFn: () => listRoles(1, 100) });

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
                <Button type="link" size="small" onClick={async () => {
                  setDetailRole(await getRole(record.id));
                }}>
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

      {detailRole && (
        <Modal
          title={`角色权限：${detailRole.name}`}
          open={!!detailRole}
          footer={null}
          onCancel={() => setDetailRole(null)}
        >
          <Space direction="vertical" style={{ width: '100%' }}>
            <div>编码：{detailRole.code}</div>
            <div>已分配权限：</div>
            <Space wrap>
              {detailRole.permissionCodes.map((code) => <Tag key={code}>{code}</Tag>)}
              {detailRole.permissionCodes.length === 0 && <Tag color="orange">未分配权限</Tag>}
            </Space>
            <Popconfirm
              title={detailRole.status === 'Active' ? '停用后该角色权限即时失效' : '确认启用？'}
              onConfirm={() => {
                if (detailRole.status === 'Active') disableRole(detailRole.id);
                else enableRole(detailRole.id);
                message.success('状态已更新');
                setDetailRole(null);
                invalidate();
              }}
            >
              <Button danger={detailRole.status === 'Active'}>
                {detailRole.status === 'Active' ? '停用角色' : '启用角色'}
              </Button>
            </Popconfirm>
          </Space>
        </Modal>
      )}
    </div>
  );
}
