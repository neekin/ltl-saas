import { PlusOutlined } from '@ant-design/icons';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { App, Button, Card, Flex, Form, Input, InputNumber, Popconfirm, Table } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { EmptyState, PageHeader, StatusTag } from '../../../shared/components';
import {
  createVehicle,
  disableVehicle,
  enableVehicle,
  listVehicles,
  type Vehicle,
} from '../api/fleet';

/** 车辆档案页（P5 前端）。 */
export function VehicleListPage() {
  const { message } = App.useApp();
  const queryClient = useQueryClient();
  const [form] = Form.useForm();

  const listQuery = useQuery({ queryKey: ['vehicles'], queryFn: () => listVehicles() });

  const createMutation = useMutation({
    mutationFn: (values: {
      plateNo: string;
      vehicleType: string;
      maxLoadKg: number;
      maxVolumeM3: number;
    }) => createVehicle(values),
    onSuccess: () => {
      message.success('车辆已创建');
      form.resetFields();
      queryClient.invalidateQueries({ queryKey: ['vehicles'] });
    },
    onError: (e: Error) => message.error(e.message),
  });

  const toggleMutation = useMutation({
    mutationFn: async ({ vehicle, disable }: { vehicle: Vehicle; disable: boolean }) =>
      disable ? disableVehicle(vehicle.id) : enableVehicle(vehicle.id),
    onSuccess: () => {
      message.success('状态已更新');
      queryClient.invalidateQueries({ queryKey: ['vehicles'] });
    },
    onError: (e: Error) => message.error(e.message),
  });

  const columns: ColumnsType<Vehicle> = [
    {
      title: '车牌号',
      dataIndex: 'plateNo',
      width: 140,
      render: (v: string) => <span className="ltl-code">{v}</span>,
    },
    { title: '车型', dataIndex: 'vehicleType', width: 120 },
    {
      title: '最大载重 (kg)',
      dataIndex: 'maxLoadKg',
      width: 140,
      align: 'right',
      render: (v: number) => <span className="ltl-amount">{v}</span>,
    },
    {
      title: '最大容积 (m³)',
      dataIndex: 'maxVolumeM3',
      width: 140,
      align: 'right',
      render: (v: number) => <span className="ltl-amount">{v}</span>,
    },
    {
      title: '状态',
      dataIndex: 'status',
      width: 100,
      render: (status: string) => <StatusTag status={status} />,
    },
    {
      title: '操作',
      key: 'actions',
      width: 90,
      render: (_: unknown, record: Vehicle) => (
        <Popconfirm
          title={record.status === 'Active' ? '确认停用？' : '确认启用？'}
          onConfirm={() =>
            toggleMutation.mutate({ vehicle: record, disable: record.status === 'Active' })
          }
        >
          <Button type="link" size="small" danger={record.status === 'Active'}>
            {record.status === 'Active' ? '停用' : '启用'}
          </Button>
        </Popconfirm>
      ),
    },
  ];

  return (
    <div>
      <PageHeader title="车辆档案" description="维护车队车辆信息与启停状态。" />

      <Card title="添加车辆" style={{ marginBottom: 16 }}>
        <Form form={form} layout="vertical" onFinish={(values) => createMutation.mutate(values)}>
          <Flex gap={12} align="flex-end" wrap>
            <Form.Item
              name="plateNo"
              label="车牌号"
              rules={[{ required: true, message: '车牌必填' }]}
              style={{ marginBottom: 0 }}
            >
              <Input placeholder="如 粤A·12345" style={{ width: 150 }} />
            </Form.Item>
            <Form.Item
              name="vehicleType"
              label="车型"
              rules={[{ required: true, message: '车型必填' }]}
              initialValue="truck"
              style={{ marginBottom: 0 }}
            >
              <Input placeholder="truck / van" style={{ width: 140 }} />
            </Form.Item>
            <Form.Item
              name="maxLoadKg"
              label="最大载重 (kg)"
              rules={[{ required: true, message: '载重必填' }]}
              style={{ marginBottom: 0 }}
            >
              <InputNumber placeholder="载重" min={1} style={{ width: 140 }} />
            </Form.Item>
            <Form.Item
              name="maxVolumeM3"
              label="最大容积 (m³)"
              rules={[{ required: true, message: '容积必填' }]}
              style={{ marginBottom: 0 }}
            >
              <InputNumber placeholder="容积" min={0.1} style={{ width: 140 }} />
            </Form.Item>
            <Button
              type="primary"
              htmlType="submit"
              icon={<PlusOutlined />}
              loading={createMutation.isPending}
            >
              添加
            </Button>
          </Flex>
        </Form>
      </Card>

      <Card styles={{ body: { padding: 0 } }}>
        <Table<Vehicle>
          rowKey="id"
          loading={listQuery.isLoading}
          dataSource={listQuery.data?.items}
          columns={columns}
          size="middle"
          scroll={{ x: 800 }}
          locale={{ emptyText: <EmptyState description="暂无车辆，可在上方添加" /> }}
        />
      </Card>
    </div>
  );
}
