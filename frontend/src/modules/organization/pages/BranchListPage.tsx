import { PlusOutlined } from '@ant-design/icons';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { App, Button, Form, Input, Modal, Popconfirm, Space, Table, Tag } from 'antd';
import { useState } from 'react';
import {
  createBranch,
  disableBranch,
  enableBranch,
  listBranches,
  renameBranch,
  type Branch,
} from '../api/organization';

/** 网点管理页（P1 前端）：列表/创建/重命名/启停用。 */
export function BranchListPage() {
  const { message } = App.useApp();
  const queryClient = useQueryClient();
  const [createOpen, setCreateOpen] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [editName, setEditName] = useState('');
  const [form] = Form.useForm<{ code: string; name: string }>();

  const listQuery = useQuery({ queryKey: ['branches'], queryFn: () => listBranches(1, 100) });

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ['branches'] });

  const createMutation = useMutation({
    mutationFn: (values: { code: string; name: string }) => createBranch(values),
    onSuccess: () => {
      message.success('网点已创建');
      setCreateOpen(false);
      form.resetFields();
      invalidate();
    },
    onError: (e: Error) => message.error(e.message),
  });

  const renameMutation = useMutation({
    mutationFn: ({ id, name }: { id: string; name: string }) => renameBranch(id, name),
    onSuccess: () => {
      message.success('已重命名');
      setEditingId(null);
      invalidate();
    },
    onError: (e: Error) => message.error(e.message),
  });

  const toggleMutation = useMutation({
    mutationFn: async ({ branch, disable }: { branch: Branch; disable: boolean }) =>
      disable ? disableBranch(branch.id) : enableBranch(branch.id),
    onSuccess: () => {
      message.success('状态已更新');
      invalidate();
    },
    onError: (e: Error) => message.error(e.message),
  });

  return (
    <div>
      <Space style={{ marginBottom: 16 }}>
        <Button type="primary" icon={<PlusOutlined />} onClick={() => setCreateOpen(true)}>
          新建网点
        </Button>
      </Space>
      <Table
        rowKey="id"
        loading={listQuery.isLoading}
        dataSource={listQuery.data?.items}
        columns={[
          { title: '编码', dataIndex: 'code', width: 120 },
          { title: '名称', dataIndex: 'name' },
          {
            title: '状态',
            dataIndex: 'status',
            width: 100,
            render: (s: string) => (
              <Tag color={s === 'Active' ? 'green' : 'default'}>
                {s === 'Active' ? '启用' : '停用'}
              </Tag>
            ),
          },
          {
            title: '操作',
            key: 'actions',
            width: 180,
            render: (_: unknown, record: Branch) => (
              <Space>
                <Button
                  type="link"
                  size="small"
                  onClick={() => {
                    setEditingId(record.id);
                    setEditName(record.name);
                  }}
                >
                  重命名
                </Button>
                <Popconfirm
                  title={record.status === 'Active' ? '确认停用？' : '确认启用？'}
                  onConfirm={() =>
                    toggleMutation.mutate({ branch: record, disable: record.status === 'Active' })
                  }
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

      <Modal
        title="新建网点"
        open={createOpen}
        onCancel={() => setCreateOpen(false)}
        onOk={() => form.submit()}
      >
        <Form form={form} layout="vertical" onFinish={(values) => createMutation.mutate(values)}>
          <Form.Item name="code" label="网点编码" rules={[{ required: true, message: '编码必填' }]}>
            <Input placeholder="如 GZ01" />
          </Form.Item>
          <Form.Item name="name" label="网点名称" rules={[{ required: true, message: '名称必填' }]}>
            <Input placeholder="如 广州" />
          </Form.Item>
        </Form>
      </Modal>

      <Modal
        title="重命名网点"
        open={editingId !== null}
        onCancel={() => setEditingId(null)}
        onOk={() => editingId && renameMutation.mutate({ id: editingId, name: editName })}
      >
        <Input value={editName} onChange={(e) => setEditName(e.target.value)} />
      </Modal>
    </div>
  );
}
