import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { App, Button, Form, Input, InputNumber, Popconfirm, Table, Tag, Typography } from 'antd';
import { createVehicle, disableVehicle, enableVehicle, listVehicles, type Vehicle } from '../api/fleet';

/** 车辆档案页（P5 前端）。 */
export function VehicleListPage() {
  const { message } = App.useApp();
  const queryClient = useQueryClient();
  const [form] = Form.useForm();

  const listQuery = useQuery({ queryKey: ['vehicles'], queryFn: () => listVehicles() });

  const createMutation = useMutation({
    mutationFn: (values: { plateNo: string; vehicleType: string; maxLoadKg: number; maxVolumeM3: number }) =>
      createVehicle(values),
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

  return (
    <div>
      <Typography.Title level={4}>车辆档案</Typography.Title>
      <Form form={form} layout="inline" onFinish={(values) => createMutation.mutate(values)} style={{ flexWrap: 'wrap', gap: 8, marginBottom: 16 }}>
        <Form.Item name="plateNo" rules={[{ required: true, message: '车牌必填' }]}>
          <Input placeholder="车牌号" style={{ width: 120 }} />
        </Form.Item>
        <Form.Item name="vehicleType" rules={[{ required: true, message: '车型必填' }]} initialValue="truck">
          <Input placeholder="车型(truck/van)" style={{ width: 130 }} />
        </Form.Item>
        <Form.Item name="maxLoadKg" rules={[{ required: true, message: '载重必填' }]}>
          <InputNumber placeholder="最大载重kg" min={1} style={{ width: 150 }} />
        </Form.Item>
        <Form.Item name="maxVolumeM3" rules={[{ required: true, message: '容积必填' }]}>
          <InputNumber placeholder="最大容积m³" min={0.1} style={{ width: 150 }} />
        </Form.Item>
        <Button htmlType="submit" loading={createMutation.isPending}>
          添加
        </Button>
      </Form>

      <Table
        rowKey="id"
        loading={listQuery.isLoading}
        dataSource={listQuery.data?.items}
        columns={[
          { title: '车牌号', dataIndex: 'plateNo' },
          { title: '车型', dataIndex: 'vehicleType' },
          { title: '最大载重(kg)', dataIndex: 'maxLoadKg' },
          { title: '最大容积(m³)', dataIndex: 'maxVolumeM3' },
          {
            title: '状态',
            dataIndex: 'status',
            render: (status: string) => <Tag color={status === 'Active' ? 'green' : 'default'}>{status === 'Active' ? '启用' : '停用'}</Tag>,
          },
          {
            title: '操作',
            key: 'actions',
            render: (_: unknown, record: Vehicle) => (
              <Popconfirm
                title={record.status === 'Active' ? '确认停用？' : '确认启用？'}
                onConfirm={() => toggleMutation.mutate({ vehicle: record, disable: record.status === 'Active' })}
              >
                <Button type="link" size="small" danger={record.status === 'Active'}>
                  {record.status === 'Active' ? '停用' : '启用'}
                </Button>
              </Popconfirm>
            ),
          },
        ]}
      />
    </div>
  );
}
