import { PlusOutlined } from '@ant-design/icons';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { App, Button, Form, Input, InputNumber, Select, Space, Table, Tag } from 'antd';
import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { http } from '../../../shared/api/http';
import { createTransfer, listTransfers, type BranchTransfer } from '../api/transfers';
import type { ColumnsType } from 'antd/es/table';

interface BranchOption {
  id: string;
  code: string;
  name: string;
}

function statusTag(status: string) {
  const map: Record<string, { color: string; text: string }> = {
    Draft: { color: 'orange', text: '草稿' },
    Submitted: { color: 'blue', text: '已提交' },
    Dispatched: { color: 'cyan', text: '已发出' },
    Received: { color: 'green', text: '已验收' },
    Cancelled: { color: 'default', text: '已取消' },
  };
  const item = map[status];
  return item ? <Tag color={item.color}>{item.text}</Tag> : <Tag>{status}</Tag>;
}

/** 调拨单列表页（P5 前端）。 */
export function TransferListPage() {
  const navigate = useNavigate();
  const { message } = App.useApp();
  const queryClient = useQueryClient();
  const [page, setPage] = useState(1);
  const [createOpen, setCreateOpen] = useState(false);
  const [form] = Form.useForm<{
    fromBranchId: string;
    toBranchId: string;
    cargoName: string;
    quantity: number;
  }>();

  const branchQuery = useQuery({
    queryKey: ['branch-options'],
    queryFn: async () => {
      const response = await http.get<{ items: BranchOption[] }>('/organization/branches', {
        params: { page: 1, pageSize: 100 },
      });
      return response.data.items;
    },
  });

  const listQuery = useQuery({ queryKey: ['transfers', page], queryFn: () => listTransfers(page, 10) });

  const columns: ColumnsType<BranchTransfer> = [
    { title: '调拨单号', dataIndex: 'transferNo' },
    {
      title: '状态',
      dataIndex: 'status',
      render: (status) => statusTag(status),
    },
    {
      title: '清单',
      key: 'items',
      render: (_, record) => record.items.map((i) => `${i.cargoName}×${i.declaredQuantity}`).join('、'),
    },
    {
      title: '操作',
      key: 'actions',
      render: (_, record) => (
        <Button type="link" size="small" onClick={() => navigate(`/transfers/${record.id}`)}>
          详情/验收
        </Button>
      ),
    },
  ];

  const createMutation = useMutation({
    mutationFn: (values: { fromBranchId: string; toBranchId: string; cargoName: string; quantity: number }) =>
      createTransfer({
        fromBranchId: values.fromBranchId,
        toBranchId: values.toBranchId,
        items: [{ cargoName: values.cargoName, quantity: values.quantity }],
      }),
    onSuccess: (transfer) => {
      message.success('调拨单已创建');
      setCreateOpen(false);
      queryClient.invalidateQueries({ queryKey: ['transfers'] });
      navigate(`/transfers/${transfer.id}`);
    },
    onError: (e: Error) => message.error(e.message),
  });

  const branchOptions = branchQuery.data ?? [];

  return (
    <div>
      <Space style={{ marginBottom: 16 }}>
        <Button type="primary" icon={<PlusOutlined />} onClick={() => setCreateOpen(!createOpen)}>
          创建调拨单
        </Button>
      </Space>

      {createOpen && (
        <Form form={form} layout="inline" onFinish={(values) => createMutation.mutate(values)} style={{ flexWrap: 'wrap', gap: 8, marginBottom: 16 }}>
          <Form.Item name="fromBranchId" label="调出网点" rules={[{ required: true }]}>
            <Select
              showSearch
              optionFilterProp="label"
              style={{ width: 180 }}
              options={branchOptions.map((b) => ({ value: b.id, label: `${b.code} ${b.name}` }))}
            />
          </Form.Item>
          <Form.Item name="toBranchId" label="调入网点" rules={[{ required: true }]}>
            <Select
              showSearch
              optionFilterProp="label"
              style={{ width: 180 }}
              options={branchOptions.map((b) => ({ value: b.id, label: `${b.code} ${b.name}` }))}
            />
          </Form.Item>
          <Form.Item name="cargoName" label="货物" rules={[{ required: true }]}>
            <Input placeholder="货物名称" style={{ width: 160 }} />
          </Form.Item>
          <Form.Item name="quantity" label="数量" rules={[{ required: true }]} initialValue={1}>
            <InputNumber min={1} precision={0} style={{ width: 110 }} />
          </Form.Item>
          <Button htmlType="submit" loading={createMutation.isPending}>
            创建
          </Button>
        </Form>
      )}

      <Table
        rowKey="id"
        loading={listQuery.isLoading}
        dataSource={listQuery.data?.items}
        pagination={{ current: page, pageSize: 10, total: listQuery.data?.total, onChange: setPage }}
        columns={columns}
      />
    </div>
  );
}
