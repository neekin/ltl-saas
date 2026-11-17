import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { App, Button, Form, Input, Popconfirm, Table, Tag, Typography } from 'antd';
import { createDriver, disableDriver, enableDriver, listDrivers, type Driver } from '../api/fleet';

/** 司机档案页（P5 前端）。 */
export function DriverListPage() {
  const { message } = App.useApp();
  const queryClient = useQueryClient();
  const [form] = Form.useForm();

  const listQuery = useQuery({ queryKey: ['drivers'], queryFn: () => listDrivers() });

  const createMutation = useMutation({
    mutationFn: (values: { name: string; phone: string; licenseNo: string }) => createDriver(values),
    onSuccess: () => {
      message.success('司机已创建');
      form.resetFields();
      queryClient.invalidateQueries({ queryKey: ['drivers'] });
    },
    onError: (e: Error) => message.error(e.message),
  });

  const toggleMutation = useMutation({
    mutationFn: async ({ driver, disable }: { driver: Driver; disable: boolean }) =>
      disable ? disableDriver(driver.id) : enableDriver(driver.id),
    onSuccess: () => {
      message.success('状态已更新');
      queryClient.invalidateQueries({ queryKey: ['drivers'] });
    },
    onError: (e: Error) => message.error(e.message),
  });

  return (
    <div>
      <Typography.Title level={4}>司机档案</Typography.Title>
      <Form form={form} layout="inline" onFinish={(values) => createMutation.mutate(values)} style={{ flexWrap: 'wrap', gap: 8, marginBottom: 16 }}>
        <Form.Item name="name" rules={[{ required: true, message: '姓名必填' }]}>
          <Input placeholder="姓名" style={{ width: 120 }} />
        </Form.Item>
        <Form.Item name="phone" rules={[{ required: true, message: '电话必填' }]}>
          <Input placeholder="电话" style={{ width: 150 }} />
        </Form.Item>
        <Form.Item name="licenseNo" rules={[{ required: true, message: '证号必填' }]}>
          <Input placeholder="驾驶证号" style={{ width: 160 }} />
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
          { title: '姓名', dataIndex: 'name' },
          { title: '电话', dataIndex: 'phone' },
          { title: '驾驶证号', dataIndex: 'licenseNo' },
          {
            title: '状态',
            dataIndex: 'status',
            render: (status: string) => <Tag color={status === 'Active' ? 'green' : 'default'}>{status === 'Active' ? '启用' : '停用'}</Tag>,
          },
          {
            title: '操作',
            key: 'actions',
            render: (_: unknown, record: Driver) => (
              <Popconfirm
                title={record.status === 'Active' ? '确认停用？' : '确认启用？'}
                onConfirm={() => toggleMutation.mutate({ driver: record, disable: record.status === 'Active' })}
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
