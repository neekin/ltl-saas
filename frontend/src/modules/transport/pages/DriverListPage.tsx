import { PlusOutlined } from '@ant-design/icons';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { App, Button, Card, Flex, Form, Input, Popconfirm, Table } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { EmptyState, PageHeader, StatusTag } from '../../../shared/components';
import { createDriver, disableDriver, enableDriver, listDrivers, type Driver } from '../api/fleet';

/** 司机档案页（P5 前端）。 */
export function DriverListPage() {
  const { message } = App.useApp();
  const queryClient = useQueryClient();
  const [form] = Form.useForm();

  const listQuery = useQuery({ queryKey: ['drivers'], queryFn: () => listDrivers() });

  const createMutation = useMutation({
    mutationFn: (values: { name: string; phone: string; licenseNo: string }) =>
      createDriver(values),
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

  const columns: ColumnsType<Driver> = [
    { title: '姓名', dataIndex: 'name', width: 130 },
    { title: '电话', dataIndex: 'phone', width: 150 },
    {
      title: '驾驶证号',
      dataIndex: 'licenseNo',
      render: (v: string) => <span className="ltl-code">{v}</span>,
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
      render: (_: unknown, record: Driver) => (
        <Popconfirm
          title={record.status === 'Active' ? '确认停用？' : '确认启用？'}
          onConfirm={() =>
            toggleMutation.mutate({ driver: record, disable: record.status === 'Active' })
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
      <PageHeader title="司机档案" description="维护司机信息与启停状态。" />

      <Card title="添加司机" style={{ marginBottom: 16 }}>
        <Form form={form} layout="vertical" onFinish={(values) => createMutation.mutate(values)}>
          <Flex gap={12} align="flex-end" wrap>
            <Form.Item
              name="name"
              label="姓名"
              rules={[{ required: true, message: '姓名必填' }]}
              style={{ marginBottom: 0 }}
            >
              <Input placeholder="姓名" style={{ width: 130 }} />
            </Form.Item>
            <Form.Item
              name="phone"
              label="电话"
              rules={[{ required: true, message: '电话必填' }]}
              style={{ marginBottom: 0 }}
            >
              <Input placeholder="电话" style={{ width: 150 }} />
            </Form.Item>
            <Form.Item
              name="licenseNo"
              label="驾驶证号"
              rules={[{ required: true, message: '证号必填' }]}
              style={{ marginBottom: 0 }}
            >
              <Input placeholder="驾驶证号" style={{ width: 170 }} />
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
        <Table<Driver>
          rowKey="id"
          loading={listQuery.isLoading}
          dataSource={listQuery.data?.items}
          columns={columns}
          size="middle"
          scroll={{ x: 700 }}
          locale={{ emptyText: <EmptyState description="暂无司机，可在上方添加" /> }}
        />
      </Card>
    </div>
  );
}
