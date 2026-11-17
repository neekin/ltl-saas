import { PlusOutlined } from '@ant-design/icons';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { App, Button, Card, Form, Input, InputNumber, Select, Space, Table } from 'antd';
import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { EmptyState, PageHeader, StatusTag } from '../../../shared/components';
import { http } from '../../../shared/api/http';
import { createTransfer, listTransfers, type BranchTransfer } from '../api/transfers';
import type { ColumnsType } from 'antd/es/table';

interface BranchOption {
  id: string;
  code: string;
  name: string;
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
    { title: '调拨单号', dataIndex: 'transferNo', width: 180, render: (v: string) => <span className="ltl-code">{v}</span> },
    {
      title: '状态',
      dataIndex: 'status',
      width: 110,
      render: (status) => <StatusTag status={status} category="transfer" />,
    },
    {
      title: '清单',
      key: 'items',
      render: (_, record) => record.items.map((i) => `${i.cargoName}×${i.declaredQuantity}`).join('、'),
    },
    {
      title: '操作',
      key: 'actions',
      width: 120,
      fixed: 'right',
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
      <PageHeader
        title="网点调拨"
        description="同一租户内直营网点间的货物交接，记录调出、发出与验收差异。"
        actions={
          <Button type="primary" icon={<PlusOutlined />} onClick={() => setCreateOpen(!createOpen)}>
            创建调拨单
          </Button>
        }
      />

      {createOpen && (
        <Card title="新建调拨单" style={{ marginBottom: 16 }}>
          <Form form={form} layout="vertical" onFinish={(values) => createMutation.mutate(values)}>
            <Space align="end" size={12} wrap style={{ width: '100%' }}>
              <Form.Item name="fromBranchId" label="调出网点" rules={[{ required: true }]} style={{ marginBottom: 0 }}>
                <Select
                  showSearch
                  optionFilterProp="label"
                  style={{ width: 180 }}
                  placeholder="选择调出网点"
                  options={branchOptions.map((b) => ({ value: b.id, label: `${b.code} ${b.name}` }))}
                />
              </Form.Item>
              <Form.Item name="toBranchId" label="调入网点" rules={[{ required: true }]} style={{ marginBottom: 0 }}>
                <Select
                  showSearch
                  optionFilterProp="label"
                  style={{ width: 180 }}
                  placeholder="选择调入网点"
                  options={branchOptions.map((b) => ({ value: b.id, label: `${b.code} ${b.name}` }))}
                />
              </Form.Item>
              <Form.Item name="cargoName" label="货物" rules={[{ required: true }]} style={{ marginBottom: 0 }}>
                <Input placeholder="货物名称" style={{ width: 160 }} />
              </Form.Item>
              <Form.Item name="quantity" label="数量" rules={[{ required: true }]} initialValue={1} style={{ marginBottom: 0 }}>
                <InputNumber min={1} precision={0} style={{ width: 110 }} />
              </Form.Item>
              <Button type="primary" htmlType="submit" loading={createMutation.isPending}>
                创建
              </Button>
            </Space>
          </Form>
        </Card>
      )}

      <Card styles={{ body: { padding: 0 } }}>
        <Table<BranchTransfer>
          rowKey="id"
          loading={listQuery.isLoading}
          dataSource={listQuery.data?.items}
          columns={columns}
          size="middle"
          scroll={{ x: 750 }}
          locale={{ emptyText: <EmptyState description="暂无调拨单" /> }}
          pagination={{
            current: page,
            pageSize: 10,
            total: listQuery.data?.total ?? 0,
            onChange: setPage,
            showTotal: (total) => `共 ${total} 条`,
            showSizeChanger: false,
          }}
        />
      </Card>
    </div>
  );
}
